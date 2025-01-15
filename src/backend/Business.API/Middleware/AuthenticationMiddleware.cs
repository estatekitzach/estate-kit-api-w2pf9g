using System;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.IdentityModel.Tokens.Jwt;
using Polly;
using Microsoft.ApplicationInsights;
using EstateKit.Infrastructure.Security;

namespace EstateKit.Business.API.Middleware
{
    /// <summary>
    /// Enterprise-grade middleware that handles authentication for incoming HTTP requests
    /// using JWT tokens with comprehensive security features.
    /// </summary>
    public class AuthenticationMiddleware
    {
        private const string AUTHORIZATION_HEADER = "Authorization";
        private const string BEARER_PREFIX = "Bearer ";
        private const int MAX_TOKEN_LENGTH = 8192;
        private const int RATE_LIMIT_CONCURRENT = 100;
        private const int RATE_LIMIT_INTERVAL_MS = 1000;
        private const int TOKEN_CACHE_DURATION = 3600;
        private const string REQUIRED_TLS_VERSION = "1.3";

        private readonly RequestDelegate _next;
        private readonly ILogger<AuthenticationMiddleware> _logger;
        private readonly TokenValidator _tokenValidator;
        private readonly IAsyncPolicy _retryPolicy;
        private readonly SemaphoreSlim _rateLimiter;
        private readonly TelemetryClient _telemetryClient;

        public AuthenticationMiddleware(
            RequestDelegate next,
            ILogger<AuthenticationMiddleware> logger,
            TokenValidator tokenValidator,
            TelemetryClient telemetryClient)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _tokenValidator = tokenValidator ?? throw new ArgumentNullException(nameof(tokenValidator));
            _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));

            // Configure retry policy with exponential backoff
            _retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(3, 
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (exception, timeSpan, retryCount, context) =>
                    {
                        _logger.LogWarning(exception,
                            "Retry {RetryCount} after {TimeSpan}ms delay due to: {Message}",
                            retryCount, timeSpan.TotalMilliseconds, exception.Message);
                    });

            // Initialize rate limiter
            _rateLimiter = new SemaphoreSlim(RATE_LIMIT_CONCURRENT, RATE_LIMIT_CONCURRENT);
        }

        /// <summary>
        /// Processes authentication for incoming HTTP requests with comprehensive security measures.
        /// </summary>
        public async Task InvokeAsync(HttpContext context)
        {
            using var operation = _telemetryClient.StartOperation<RequestTelemetry>("AuthenticationMiddleware");
            
            try
            {
                // Validate TLS version
                var tlsVersion = context.Connection.Protocol;
                if (!tlsVersion.EndsWith(REQUIRED_TLS_VERSION))
                {
                    _logger.LogWarning("Invalid TLS version: {Version}", tlsVersion);
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await context.Response.WriteAsync("Invalid TLS version");
                    return;
                }

                // Apply rate limiting
                if (!await _rateLimiter.WaitAsync(RATE_LIMIT_INTERVAL_MS))
                {
                    _logger.LogWarning("Rate limit exceeded for client IP: {IP}", 
                        context.Connection.RemoteIpAddress);
                    context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    await context.Response.WriteAsync("Rate limit exceeded");
                    return;
                }

                try
                {
                    // Validate security headers
                    if (!await ValidateSecurityHeadersAsync(context))
                    {
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        await context.Response.WriteAsync("Invalid security headers");
                        return;
                    }

                    // Extract JWT token
                    if (!context.Request.Headers.TryGetValue(AUTHORIZATION_HEADER, out var authHeader) ||
                        string.IsNullOrEmpty(authHeader))
                    {
                        _logger.LogWarning("Missing Authorization header");
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        await context.Response.WriteAsync("Missing Authorization header");
                        return;
                    }

                    var token = authHeader.ToString();
                    if (!token.StartsWith(BEARER_PREFIX) || token.Length > MAX_TOKEN_LENGTH)
                    {
                        _logger.LogWarning("Invalid token format or length");
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        await context.Response.WriteAsync("Invalid token format");
                        return;
                    }

                    token = token.Substring(BEARER_PREFIX.Length);

                    // Validate token using retry policy
                    var (isValid, principal) = await _retryPolicy.ExecuteAsync(async () => 
                        await _tokenValidator.ValidateTokenAsync(token));

                    if (!isValid || principal == null)
                    {
                        _logger.LogWarning("Token validation failed");
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        await context.Response.WriteAsync("Invalid token");
                        return;
                    }

                    // Validate user claims
                    if (!await _tokenValidator.ValidateUserClaimsAsync(principal))
                    {
                        _logger.LogWarning("User claims validation failed");
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        await context.Response.WriteAsync("Invalid claims");
                        return;
                    }

                    // Set authenticated user
                    context.User = principal;

                    // Add security context
                    context.Items["AuthTime"] = DateTime.UtcNow;
                    context.Items["TokenId"] = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;

                    _logger.LogInformation(
                        "Successfully authenticated user {UserId}",
                        principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value);

                    // Track successful authentication
                    _telemetryClient.TrackEvent("SuccessfulAuthentication", new Dictionary<string, string>
                    {
                        ["UserId"] = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value,
                        ["TokenId"] = context.Items["TokenId"]?.ToString()
                    });

                    await _next(context);
                }
                finally
                {
                    _rateLimiter.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Authentication error occurred");
                _telemetryClient.TrackException(ex);
                
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsync("Authentication error occurred");
            }
        }

        /// <summary>
        /// Validates required security headers in the request.
        /// </summary>
        private async Task<bool> ValidateSecurityHeadersAsync(HttpContext context)
        {
            var headers = context.Request.Headers;

            // Validate Content-Security-Policy
            if (!headers.ContainsKey("Content-Security-Policy"))
            {
                _logger.LogWarning("Missing Content-Security-Policy header");
                return false;
            }

            // Validate X-Frame-Options
            if (!headers.ContainsKey("X-Frame-Options") || 
                headers["X-Frame-Options"] != "DENY")
            {
                _logger.LogWarning("Invalid X-Frame-Options header");
                return false;
            }

            // Validate X-Content-Type-Options
            if (!headers.ContainsKey("X-Content-Type-Options") || 
                headers["X-Content-Type-Options"] != "nosniff")
            {
                _logger.LogWarning("Invalid X-Content-Type-Options header");
                return false;
            }

            // Validate Strict-Transport-Security
            if (!headers.ContainsKey("Strict-Transport-Security"))
            {
                _logger.LogWarning("Missing Strict-Transport-Security header");
                return false;
            }

            // Validate X-XSS-Protection
            if (!headers.ContainsKey("X-XSS-Protection") || 
                headers["X-XSS-Protection"] != "1; mode=block")
            {
                _logger.LogWarning("Invalid X-XSS-Protection header");
                return false;
            }

            return await Task.FromResult(true);
        }
    }
}