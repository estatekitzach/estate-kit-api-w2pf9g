using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using EstateKit.Core.Constants;
using EstateKit.Infrastructure.Logger.Extensions;
using System.Globalization;

namespace EstateKit.Data.Api.Middleware
{
    /// <summary>
    /// Middleware component that provides comprehensive request logging, security monitoring,
    /// and performance tracking for the EstateKit Personal Information API.
    /// </summary>
    internal sealed class RequestLoggingMiddleware : IDisposable
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestLoggingMiddleware> _logger;
        private readonly ActivitySource _activitySource;
        private const string ActivitySourceName = "EstateKit.Api.RequestLogging";
        private static readonly HashSet<string> SensitiveHeaders = new(StringComparer.OrdinalIgnoreCase)
        {
            "Authorization",
            "X-API-Key",
            "Cookie"
        };

        public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _activitySource = new ActivitySource(ActivitySourceName, "1.0.0");
        }

        /// <summary>
        /// Processes the HTTP request with comprehensive logging, performance tracking, and error handling.
        /// </summary>
        public async Task InvokeAsync(HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            var sw = Stopwatch.StartNew();
            var correlationId = CreateCorrelationId(context);
            
            using var activity = _activitySource.StartActivity("ProcessHttpRequest");
            activity?.SetTag("correlation.id", correlationId);
            activity?.SetTag("http.method", context.Request.Method);
            activity?.SetTag("http.url", context.Request.Path);

            try
            {
                // Log request details
                await LogRequestDetails(context).ConfigureAwait(false);

                // Process the request through the pipeline
                await _next(context).ConfigureAwait(false);

                // Log response details
                await LogResponseDetails(context, sw.ElapsedMilliseconds).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Log error details
                LogErrorDetails(context, ex, sw.ElapsedMilliseconds);
                
                // Set appropriate error response
                await HandleException(context, ex).ConfigureAwait(false);
                
                throw; // Re-throw to maintain middleware pipeline behavior
            }
            finally
            {
                activity?.Stop();
            }
        }

        private async Task LogRequestDetails(HttpContext context)
        {
            var logData = new Dictionary<string, object>
            {
                ["Timestamp"] = DateTimeOffset.UtcNow,
                ["TraceId"] = Activity.Current?.TraceId.ToString() ?? string.Empty,
                ["Method"] = context.Request.Method,
                ["Path"] = context.Request.Path,
                ["QueryString"] = context.Request.QueryString.ToString(),
                ["Headers"] = RedactSensitiveHeaders(context.Request.Headers),
                ["RemoteIpAddress"] = context.Connection.RemoteIpAddress?.ToString() ??  "unknown IP"
            };

            if (context.Request.ContentLength > 0)
            {
                context.Request.EnableBuffering();
                using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
                var body = await reader.ReadToEndAsync().ConfigureAwait(false);
                context.Request.Body.Position = 0;

                logData["RequestBody"] = RedactSensitiveData(body);
            }

            // _logger.LogInformation("Incoming request details: {@RequestData}", logData);
        }

        private async Task LogResponseDetails(HttpContext context, long elapsedMs)
        {
            var logData = new Dictionary<string, object>
            {
                ["Timestamp"] = DateTimeOffset.UtcNow,
                ["TraceId"] = Activity.Current?.TraceId.ToString() ?? string.Empty,
                ["StatusCode"] = context.Response.StatusCode,
                ["ElapsedMilliseconds"] = elapsedMs
            };

            if (context.Response.Body.CanRead)
            {
                var originalBodyStream = context.Response.Body;
                using var memoryStream = new MemoryStream();
                context.Response.Body = memoryStream;

                memoryStream.Position = 0;
                using (var reader = new StreamReader(memoryStream))
                {
                    var responseBody = await reader.ReadToEndAsync().ConfigureAwait(false);
                    logData["ResponseBody"] = RedactSensitiveData(responseBody);
                }
                memoryStream.Position = 0;
                await memoryStream.CopyToAsync(originalBodyStream).ConfigureAwait(false);
            }

            // _logger.LogInformation("Outgoing response details: {@ResponseData}", logData);
        }

        private void LogErrorDetails(HttpContext context, Exception ex, long elapsedMs)
        {
            var logData = new Dictionary<string, object>
            {
                ["Timestamp"] = DateTimeOffset.UtcNow,
                ["TraceId"] = Activity.Current?.TraceId.ToString() ?? string.Empty,
                ["ExceptionType"] = ex.GetType().Name,
                ["ExceptionMessage"] = ex.Message,
                ["StackTrace"] = ex.StackTrace ?? string.Empty,
                ["ElapsedMilliseconds"] = elapsedMs
            };

            _logger.LogGenericException(string.Format(CultureInfo.InvariantCulture,
               "Request processing error: {0}", logData), null);
        }

        public static async Task HandleException(HttpContext context, Exception ex)
        {
            context.Response.ContentType = "application/json";
            var error = new
            {
                Code = DetermineErrorCode(ex),
                Message = "An error occurred processing your request",
                TraceId = Activity.Current?.TraceId.ToString()
            };

            context.Response.StatusCode = DetermineStatusCode(ex);
            await context.Response.WriteAsJsonAsync(error).ConfigureAwait(false);
        }

        private static string DetermineErrorCode(Exception ex)
        {
            return ex switch
            {
                UnauthorizedAccessException _ => ErrorCodes.INVALIDOAUTHTOKEN,
                InvalidOperationException _ => ErrorCodes.KMSSERVICEERROR,
                _ => "SYS999" // Generic system error
            };
        }

        private static int DetermineStatusCode(Exception ex)
        {
            return ex switch
            {
                UnauthorizedAccessException _ => StatusCodes.Status401Unauthorized,
                InvalidOperationException _ => StatusCodes.Status500InternalServerError,
                _ => StatusCodes.Status500InternalServerError
            };
        }

        private static string CreateCorrelationId(HttpContext context)
        {
            const string correlationHeader = "X-Correlation-ID";

            string correlationId = context.Request.Headers[correlationHeader].FirstOrDefault() ??
                                 Activity.Current?.Id ??
                                 Guid.NewGuid().ToString();

            context.Response.Headers[correlationHeader] = correlationId;
            return correlationId;
        }

        private static Dictionary<string, string?[]> RedactSensitiveHeaders(IHeaderDictionary headers)
        {
            var sanitizedHeaders = new Dictionary<string, string?[]>();

            foreach (var header in headers)
            {
                if(header.Value.Count == 0) continue;   
                sanitizedHeaders[header.Key] = SensitiveHeaders.Contains(header.Key)
                    ? new[] { "[REDACTED]" }
                    : header.Value.ToArray();
            }

            return sanitizedHeaders;
        }

        private object RedactSensitiveData(object data)
        {
            if (data == null) return "[NULL]";

            try
            {
                if (data is string strData)
                {
                    if (string.IsNullOrWhiteSpace(strData)) return strData;

                    // Attempt to parse as JSON for structured data
                    try
                    {
                        var jsonObj = JsonSerializer.Deserialize<JsonElement>(strData);
                        return RedactSensitiveJsonElement(jsonObj);
                    }
                    catch (JsonException)
                    {
                        // Not valid JSON, return masked string if it contains sensitive patterns
                        return RedactSensitiveString(strData);
                    }
                }

                // For other object types, serialize to JSON and redact
                var jsonString = JsonSerializer.Serialize(data);
                var jsonElement = JsonSerializer.Deserialize<JsonElement>(jsonString);
                return RedactSensitiveJsonElement(jsonElement);
            }
            catch
            {
                _logger.LogGenericException("Error redacting sensitive data", null);
                throw;
            }
        }

        private static object RedactSensitiveJsonElement(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    var obj = new Dictionary<string, object>();
                    foreach (var property in element.EnumerateObject())
                    {
                        var propertyName = property.Name.ToUpperInvariant();
                        obj[property.Name] = IsSensitiveProperty(propertyName)
                            ? "[REDACTED]"
                            : RedactSensitiveJsonElement(property.Value);
                    }
                    return obj;

                case JsonValueKind.Array:
                    return element.EnumerateArray()
                        .Select(item => RedactSensitiveJsonElement(item))
                        .ToArray();

                case JsonValueKind.String:
                    var strValue = element.GetString() ?? string.Empty;
                    return IsSensitiveValue(strValue) ? "[REDACTED]" : strValue;

                default:
                    return element.GetRawText();
            }
        }

        private static string RedactSensitiveString(string input)
        {
            // Redact common sensitive patterns
            var patterns = new[]
            {
                @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b", // Email
                @"\b\d{4}[- ]?\d{4}[- ]?\d{4}[- ]?\d{4}\b",           // Credit card
                @"\b\d{3}[-.]?\d{3}[-.]?\d{4}\b"                       // SSN
            };

            return patterns.Aggregate(input, (current, pattern) =>
                System.Text.RegularExpressions.Regex.Replace(current, pattern, "[REDACTED]"));
        }

        private static bool IsSensitiveProperty(string propertyName)
        {
            var sensitiveProperties = new[]
            {
                "password",
                "secret",
                "token",
                "key",
                "authorization",
                "ssn",
                "creditcard",
                "email"
            };

            return sensitiveProperties.Any(sp => propertyName.Contains(sp, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsSensitiveValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;

            // Check for common sensitive patterns
            return System.Text.RegularExpressions.Regex.IsMatch(value, @"^(?:Bearer\s+|Basic\s+).+$") || // Auth tokens
                   System.Text.RegularExpressions.Regex.IsMatch(value, @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b") || // Email
                   System.Text.RegularExpressions.Regex.IsMatch(value, @"\b\d{4}[- ]?\d{4}[- ]?\d{4}[- ]?\d{4}\b"); // Credit card
        }

        public void Dispose()
        {
            _activitySource.Dispose();
        }
    }
}