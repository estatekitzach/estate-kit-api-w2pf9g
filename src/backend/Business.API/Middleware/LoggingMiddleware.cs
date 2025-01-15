using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Amazon.CloudWatch;
using Amazon.CloudWatch.Model;

namespace EstateKit.Business.API.Middleware
{
    /// <summary>
    /// Middleware component that provides comprehensive request/response logging,
    /// performance tracking, and telemetry for the Business Logic API.
    /// </summary>
    public class LoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<LoggingMiddleware> _logger;
        private readonly IConfiguration _configuration;
        private readonly AmazonCloudWatchClient _cloudWatchClient;
        private readonly ActivitySource _activitySource;
        private readonly double _samplingRate;
        private readonly HashSet<string> _sensitiveHeaders;
        private readonly Regex _piiPattern;

        private const string ACTIVITY_SOURCE_NAME = "EstateKit.BusinessAPI.Http";
        private const double DEFAULT_SAMPLING_RATE = 0.1;

        /// <summary>
        /// Initializes a new instance of the LoggingMiddleware class.
        /// </summary>
        public LoggingMiddleware(
            RequestDelegate next,
            ILogger<LoggingMiddleware> logger,
            IConfiguration configuration,
            AmazonCloudWatchClient cloudWatchClient)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _cloudWatchClient = cloudWatchClient ?? throw new ArgumentNullException(nameof(cloudWatchClient));

            _samplingRate = configuration.GetValue<double>("Telemetry:SamplingRate", DEFAULT_SAMPLING_RATE);
            _activitySource = new ActivitySource(ACTIVITY_SOURCE_NAME, "1.0.0");

            // Initialize sensitive data patterns
            _sensitiveHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Authorization",
                "Cookie",
                "X-API-Key"
            };

            _piiPattern = new Regex(
                @"(\b\d{3}-\d{2}-\d{4}\b)|(\b\d{9}\b)|(\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }

        /// <summary>
        /// Processes the HTTP request pipeline with comprehensive logging and monitoring.
        /// </summary>
        public async Task InvokeAsync(HttpContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            var startTimestamp = Stopwatch.GetTimestamp();
            var requestId = context.TraceIdentifier;
            
            using var activity = _activitySource.StartActivity(
                $"HTTP {context.Request.Method}",
                ActivityKind.Server,
                requestId,
                _samplingRate);

            try
            {
                // Add standard tags to the activity
                activity?.SetTag("service.name", "EstateKit.BusinessAPI");
                activity?.SetTag("http.method", context.Request.Method);
                activity?.SetTag("http.path", SanitizePath(context.Request.Path));

                // Log request details
                await LogRequest(context);

                // Capture original body stream
                var originalBodyStream = context.Response.Body;
                using var responseBody = new MemoryStream();
                context.Response.Body = responseBody;

                var sw = Stopwatch.StartNew();
                try
                {
                    await _next(context);
                }
                catch (Exception ex)
                {
                    activity?.SetTag("error", true);
                    activity?.SetTag("error.type", ex.GetType().Name);
                    _logger.LogError(ex, "Request processing failed for {RequestPath}", context.Request.Path);
                    throw;
                }
                finally
                {
                    sw.Stop();
                    
                    // Log response details
                    LogResponse(context, sw.Elapsed.TotalMilliseconds);

                    // Record metrics
                    await RecordMetrics(context, sw.Elapsed.TotalMilliseconds);

                    // Copy the response to the original stream
                    if (responseBody.Position > 0)
                        responseBody.Position = 0;
                    await responseBody.CopyToAsync(originalBodyStream);
                }
            }
            finally
            {
                activity?.Dispose();
            }
        }

        /// <summary>
        /// Logs sanitized information about the incoming HTTP request.
        /// </summary>
        private void LogRequest(HttpContext context)
        {
            var request = context.Request;
            
            // Create structured log with sanitized data
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["RequestId"] = context.TraceIdentifier,
                ["Method"] = request.Method,
                ["Path"] = SanitizePath(request.Path),
                ["QueryString"] = SanitizeQueryString(request.QueryString.Value),
                ["ClientIP"] = AnonymizeIp(context.Connection.RemoteIpAddress?.ToString()),
                ["UserAgent"] = request.Headers["User-Agent"].ToString()
            }))
            {
                _logger.LogInformation(
                    "Request started {Method} {Path}",
                    request.Method,
                    SanitizePath(request.Path));
            }
        }

        /// <summary>
        /// Logs information about the HTTP response with performance metrics.
        /// </summary>
        private void LogResponse(HttpContext context, double durationMs)
        {
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["RequestId"] = context.TraceIdentifier,
                ["StatusCode"] = context.Response.StatusCode,
                ["Duration"] = durationMs
            }))
            {
                _logger.LogInformation(
                    "Request completed with status {StatusCode} in {Duration:F2}ms",
                    context.Response.StatusCode,
                    durationMs);
            }
        }

        /// <summary>
        /// Records request metrics to CloudWatch.
        /// </summary>
        private async Task RecordMetrics(HttpContext context, double durationMs)
        {
            try
            {
                var metrics = new List<MetricDatum>
                {
                    new MetricDatum
                    {
                        MetricName = "RequestDuration",
                        Value = durationMs,
                        Unit = StandardUnit.Milliseconds,
                        Dimensions = new List<Dimension>
                        {
                            new Dimension { Name = "Path", Value = SanitizePath(context.Request.Path) },
                            new Dimension { Name = "Method", Value = context.Request.Method },
                            new Dimension { Name = "StatusCode", Value = context.Response.StatusCode.ToString() }
                        }
                    }
                };

                await _cloudWatchClient.PutMetricDataAsync(new PutMetricDataRequest
                {
                    Namespace = "EstateKit/BusinessAPI",
                    MetricData = metrics
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to record metrics to CloudWatch");
            }
        }

        /// <summary>
        /// Sanitizes the request path by removing potential sensitive information.
        /// </summary>
        private string SanitizePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            return _piiPattern.Replace(path, "[REDACTED]");
        }

        /// <summary>
        /// Sanitizes query string parameters by removing sensitive information.
        /// </summary>
        private string SanitizeQueryString(string queryString)
        {
            if (string.IsNullOrEmpty(queryString)) return queryString;
            return _piiPattern.Replace(queryString, "[REDACTED]");
        }

        /// <summary>
        /// Anonymizes IP addresses for privacy compliance.
        /// </summary>
        private string AnonymizeIp(string ip)
        {
            if (string.IsNullOrEmpty(ip)) return ip;
            var parts = ip.Split('.');
            if (parts.Length == 4)
            {
                return $"{parts[0]}.{parts[1]}.{parts[2]}.xxx";
            }
            return ip;
        }
    }
}