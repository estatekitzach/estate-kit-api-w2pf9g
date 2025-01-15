using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using EstateKit.Data.API.Services;

namespace EstateKit.Data.API.Middleware
{
    /// <summary>
    /// Middleware component that provides comprehensive audit logging for all data access operations
    /// with security event monitoring, performance tracking, and compliance logging capabilities.
    /// </summary>
    public class AuditLoggingMiddleware : IMiddleware
    {
        private readonly ILogger<AuditLoggingMiddleware> _logger;
        private readonly AuditService _auditService;
        private readonly IConfiguration _configuration;
        private readonly ISecurityContextProvider _securityContext;

        private static readonly TimeSpan PerformanceThreshold = TimeSpan.FromSeconds(3);
        private const int MaxRequestSize = 10 * 1024 * 1024; // 10MB

        public AuditLoggingMiddleware(
            ILogger<AuditLoggingMiddleware> logger,
            AuditService auditService,
            IConfiguration configuration,
            ISecurityContextProvider securityContext)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _securityContext = securityContext ?? throw new ArgumentNullException(nameof(securityContext));
        }

        /// <summary>
        /// Processes HTTP requests and performs comprehensive audit logging with security monitoring
        /// </summary>
        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (next == null) throw new ArgumentNullException(nameof(next));

            var correlationId = Guid.NewGuid().ToString();
            var stopwatch = Stopwatch.StartNew();
            var initialMemory = GC.GetTotalMemory(false);

            try
            {
                // Extract and validate user identity
                var userId = await ExtractUserIdentity(context.User);
                context.Items["CorrelationId"] = correlationId;
                context.Items["UserId"] = userId;

                // Log request details
                await LogRequestDetails(context, userId, correlationId);

                // Enable request body buffering for logging
                context.Request.EnableBuffering();

                // Process the request
                await next(context);

                // Log response and performance metrics
                await LogResponseAndMetrics(context, userId, correlationId, stopwatch, initialMemory);
            }
            catch (Exception ex)
            {
                // Log security event for exceptions
                await _auditService.LogSecurityEvent(
                    userId: context.Items["UserId"]?.ToString() ?? "anonymous",
                    eventType: SecurityEventType.ERROR,
                    description: ex.Message,
                    context: new SecurityContext
                    {
                        CorrelationId = correlationId,
                        Path = context.Request.Path,
                        Method = context.Request.Method
                    },
                    severity: SecuritySeverity.Critical
                );

                throw;
            }
        }

        private async Task<string> ExtractUserIdentity(ClaimsPrincipal user)
        {
            if (user?.Identity?.IsAuthenticated != true)
            {
                return "anonymous";
            }

            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                await _auditService.LogSecurityEvent(
                    userId: "anonymous",
                    eventType: SecurityEventType.AUTHENTICATION_ERROR,
                    description: "Missing user identifier claim",
                    context: new SecurityContext(),
                    severity: SecuritySeverity.High
                );
                return "anonymous";
            }

            return userId;
        }

        private async Task LogRequestDetails(HttpContext context, string userId, string correlationId)
        {
            var request = context.Request;
            
            if (request.ContentLength > MaxRequestSize)
            {
                await _auditService.LogSecurityEvent(
                    userId,
                    SecurityEventType.VALIDATION_ERROR,
                    "Request size exceeds maximum allowed",
                    new SecurityContext { CorrelationId = correlationId },
                    SecuritySeverity.High
                );
                throw new InvalidOperationException("Request size exceeds maximum allowed");
            }

            // Log data access operation
            await _auditService.LogDataAccess(
                userId: userId,
                operation: request.Method,
                entityType: ExtractEntityType(request.Path),
                entityId: ExtractEntityId(request.Path),
                changes: await GetRequestBody(request),
                classification: DetermineSecurityClassification(request.Path)
            );
        }

        private async Task LogResponseAndMetrics(
            HttpContext context,
            string userId,
            string correlationId,
            Stopwatch stopwatch,
            long initialMemory)
        {
            stopwatch.Stop();
            var finalMemory = GC.GetTotalMemory(false);
            var memoryDelta = finalMemory - initialMemory;

            // Log performance metrics if threshold exceeded
            if (stopwatch.Elapsed > PerformanceThreshold)
            {
                await _auditService.LogPerformanceMetric(
                    new PerformanceMetric
                    {
                        CorrelationId = correlationId,
                        Duration = stopwatch.Elapsed,
                        MemoryUsed = memoryDelta,
                        Path = context.Request.Path,
                        Method = context.Request.Method,
                        StatusCode = context.Response.StatusCode
                    }
                );
            }

            // Log security events for suspicious responses
            if (context.Response.StatusCode >= 400)
            {
                var severity = context.Response.StatusCode switch
                {
                    >= 500 => SecuritySeverity.Critical,
                    >= 400 => SecuritySeverity.High,
                    _ => SecuritySeverity.Medium
                };

                await _auditService.LogSecurityEvent(
                    userId,
                    SecurityEventType.API_ERROR,
                    $"HTTP {context.Response.StatusCode} response",
                    new SecurityContext { CorrelationId = correlationId },
                    severity
                );
            }
        }

        private static async Task<string> GetRequestBody(HttpRequest request)
        {
            request.Body.Position = 0;
            using var reader = new StreamReader(
                request.Body,
                encoding: Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 1024,
                leaveOpen: true);

            var body = await reader.ReadToEndAsync();
            request.Body.Position = 0;
            return body;
        }

        private static string ExtractEntityType(string path)
        {
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            return segments.Length > 1 ? segments[1].ToUpperInvariant() : "UNKNOWN";
        }

        private static string ExtractEntityId(string path)
        {
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            return segments.Length > 2 ? segments[2] : "UNKNOWN";
        }

        private static SecurityClassification DetermineSecurityClassification(string path)
        {
            // Determine classification based on path patterns
            return path.Contains("/identifiers/", StringComparison.OrdinalIgnoreCase) ||
                   path.Contains("/documents/", StringComparison.OrdinalIgnoreCase)
                ? SecurityClassification.Critical
                : SecurityClassification.Sensitive;
        }
    }
}