using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics.Metrics;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using EstateKit.Data.API.Services;
using EstateKit.Data.API.Configuration;

namespace EstateKit.Data.API.Middleware
{
    /// <summary>
    /// Enterprise-grade middleware that provides secure, high-performance encryption/decryption 
    /// of sensitive data in the HTTP request/response pipeline with comprehensive monitoring
    /// </summary>
    public class EncryptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly EncryptionService _encryptionService;
        private readonly IOptions<EncryptionOptions> _options;
        private readonly ILogger<EncryptionMiddleware> _logger;
        private readonly IMemoryCache _cache;
        private readonly Meter _meter;
        private readonly Counter<long> _encryptionCounter;
        private readonly Counter<long> _decryptionCounter;
        private readonly Histogram<double> _processingDuration;

        public EncryptionMiddleware(
            RequestDelegate next,
            EncryptionService encryptionService,
            IOptions<EncryptionOptions> options,
            ILogger<EncryptionMiddleware> logger,
            IMemoryCache cache)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));

            // Initialize metrics
            _meter = new Meter("EstateKit.Data.API.Encryption");
            _encryptionCounter = _meter.CreateCounter<long>("encryption_operations");
            _decryptionCounter = _meter.CreateCounter<long>("decryption_operations");
            _processingDuration = _meter.CreateHistogram<double>("encryption_processing_duration_ms");
        }

        /// <summary>
        /// Processes HTTP request/response with encryption, validation and monitoring
        /// </summary>
        public async Task InvokeAsync(HttpContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // Process request body if POST/PUT
                if (HttpMethods.IsPost(context.Request.Method) || 
                    HttpMethods.IsPut(context.Request.Method))
                {
                    await DecryptRequestBodyAsync(context.Request);
                }

                // Enable response buffering for encryption
                var originalBody = context.Response.Body;
                using var memoryStream = new MemoryStream();
                context.Response.Body = memoryStream;

                // Process next middleware
                await _next(context);

                // Process response body
                memoryStream.Seek(0, SeekOrigin.Begin);
                await EncryptResponseBodyAsync(context.Response);
                memoryStream.Seek(0, SeekOrigin.Begin);
                await memoryStream.CopyToAsync(originalBody);

                stopwatch.Stop();
                _processingDuration.Record(stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in encryption middleware");
                throw;
            }
        }

        private async Task DecryptRequestBodyAsync(HttpRequest request)
        {
            // Check cache first
            var cacheKey = $"decrypt_{request.Path}_{request.QueryString}";
            if (_cache.TryGetValue(cacheKey, out string cachedBody))
            {
                request.Body = new MemoryStream(Encoding.UTF8.GetBytes(cachedBody));
                return;
            }

            request.EnableBuffering();
            using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            request.Body.Seek(0, SeekOrigin.Begin);

            if (string.IsNullOrEmpty(body))
                return;

            try
            {
                var jsonDoc = JsonDocument.Parse(body);
                var sensitiveFields = _options.Value.SensitiveFields;
                var decryptedBody = await ProcessJsonFields(jsonDoc, sensitiveFields, true);

                // Cache decrypted result
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetSlidingExpiration(TimeSpan.FromMinutes(5));
                _cache.Set(cacheKey, decryptedBody, cacheOptions);

                request.Body = new MemoryStream(Encoding.UTF8.GetBytes(decryptedBody));
                _decryptionCounter.Add(1);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Invalid JSON in request body");
                throw;
            }
        }

        private async Task EncryptResponseBodyAsync(HttpResponse response)
        {
            if (!response.Body.CanRead || !response.Body.CanSeek)
                return;

            // Check cache first
            var cacheKey = $"encrypt_{response.StatusCode}_{response.Headers}";
            if (_cache.TryGetValue(cacheKey, out string cachedBody))
            {
                var cachedBytes = Encoding.UTF8.GetBytes(cachedBody);
                response.Body = new MemoryStream(cachedBytes);
                return;
            }

            using var reader = new StreamReader(response.Body, Encoding.UTF8, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            response.Body.Seek(0, SeekOrigin.Begin);

            if (string.IsNullOrEmpty(body))
                return;

            try
            {
                var jsonDoc = JsonDocument.Parse(body);
                var sensitiveFields = _options.Value.SensitiveFields;
                var encryptedBody = await ProcessJsonFields(jsonDoc, sensitiveFields, false);

                // Cache encrypted result
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetSlidingExpiration(TimeSpan.FromMinutes(5));
                _cache.Set(cacheKey, encryptedBody, cacheOptions);

                var encryptedBytes = Encoding.UTF8.GetBytes(encryptedBody);
                await response.Body.WriteAsync(encryptedBytes, 0, encryptedBytes.Length);
                _encryptionCounter.Add(1);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Invalid JSON in response body");
                throw;
            }
        }

        private async Task<string> ProcessJsonFields(
            JsonDocument doc,
            HashSet<string> sensitiveFields,
            bool isDecryption)
        {
            using var jsonWriter = new MemoryStream();
            using var writer = new Utf8JsonWriter(jsonWriter, new JsonWriterOptions { Indented = true });

            await ProcessJsonElement(doc.RootElement, writer, sensitiveFields, isDecryption);
            writer.Flush();

            return Encoding.UTF8.GetString(jsonWriter.ToArray());
        }

        private async Task ProcessJsonElement(
            JsonElement element,
            Utf8JsonWriter writer,
            HashSet<string> sensitiveFields,
            bool isDecryption)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    writer.WriteStartObject();
                    foreach (var property in element.EnumerateObject())
                    {
                        writer.WritePropertyName(property.Name);
                        if (sensitiveFields.Contains(property.Name) && property.Value.ValueKind == JsonValueKind.String)
                        {
                            var value = property.Value.GetString();
                            if (isDecryption)
                            {
                                var decrypted = await _encryptionService.DecryptSensitiveField(
                                    value,
                                    property.Name,
                                    GetCurrentUserId(),
                                    new EncryptionContext { FieldName = property.Name });
                                writer.WriteStringValue(decrypted);
                            }
                            else
                            {
                                var encrypted = await _encryptionService.EncryptSensitiveField(
                                    value,
                                    property.Name,
                                    GetCurrentUserId(),
                                    new EncryptionContext { FieldName = property.Name });
                                writer.WriteStringValue(encrypted);
                            }
                        }
                        else
                        {
                            await ProcessJsonElement(property.Value, writer, sensitiveFields, isDecryption);
                        }
                    }
                    writer.WriteEndObject();
                    break;

                case JsonValueKind.Array:
                    writer.WriteStartArray();
                    foreach (var item in element.EnumerateArray())
                    {
                        await ProcessJsonElement(item, writer, sensitiveFields, isDecryption);
                    }
                    writer.WriteEndArray();
                    break;

                default:
                    element.WriteTo(writer);
                    break;
            }
        }

        private string GetCurrentUserId()
        {
            // Implementation would get the current user ID from the authentication context
            return "system";
        }
    }
}