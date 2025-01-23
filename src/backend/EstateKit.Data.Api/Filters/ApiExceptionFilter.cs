using System;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using EstateKit.Core.Constants;
using EstateKit.Infrastructure.Logger.Extensions;
using EstateKit.Core.Models.UserModels;
using System.Globalization;

namespace EstateKit.Data.Api.Filters
{
    /// <summary>
    /// Global exception filter that provides standardized error handling, secure logging,
    /// and environment-aware error responses for the EstateKit Personal Information API.
    /// </summary>
    internal sealed class ApiExceptionFilter : IExceptionFilter
    {
        private readonly ILogger<ApiExceptionFilter> _logger;
        private readonly IWebHostEnvironment _environment;

        /// <summary>
        /// Initializes a new instance of the ApiExceptionFilter with required dependencies.
        /// </summary>
        /// <param name="logger">Logger for structured exception logging.</param>
        /// <param name="environment">Web host environment for determining error detail exposure.</param>
        public ApiExceptionFilter(ILogger<ApiExceptionFilter> logger, IWebHostEnvironment environment)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        }

        /// <summary>
        /// Handles exceptions by converting them to standardized API responses with appropriate
        /// security measures and logging.
        /// </summary>
        /// <param name="context">The exception context containing the current request and exception details.</param>
        public void OnException(ExceptionContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            var correlationId = Guid.NewGuid().ToString();
            var exception = context.Exception;

            _logger.LogGenericException(string.Format(CultureInfo.InvariantCulture,
                "Error processing request. CorrelationId: {0}, Path: {1}, Method: {2}",
                correlationId,
                context.HttpContext.Request.Path,
                context.HttpContext.Request.Method), null);
            // Log exception with correlation ID and request context

            var (statusCode, errorCode) = DetermineStatusCodeAndErrorCode(exception);
            var errorResponse = CreateErrorResponse(exception, errorCode, correlationId);

            var result = new ObjectResult(errorResponse)
            {
                StatusCode = (int)statusCode
            };

            // Add correlation ID to response headers for request tracking
            context.HttpContext.Response.Headers["X-Correlation-ID"] = correlationId;
            context.Result = result;
            context.ExceptionHandled = true;
        }

        /// <summary>
        /// Creates a standardized error response object with security considerations.
        /// </summary>
        private object CreateErrorResponse(Exception exception, string errorCode, string correlationId)
        {
            var response = new
            {
                ErrorCode = errorCode,
                Message = GetSanitizedMessage(exception),
                CorrelationId = correlationId,
                Timestamp = DateTime.UtcNow,
                RecoveryAction = GetRecoveryAction(exception, errorCode),
                // Only include stack trace in development environment
                StackTrace = _environment.IsDevelopment() ? exception.StackTrace : null
            };

            return response;
        }

        /// <summary>
        /// Determines appropriate HTTP status code and error code based on exception type.
        /// </summary>
        private static (HttpStatusCode statusCode, string errorCode) DetermineStatusCodeAndErrorCode(Exception exception)
        {
            return exception switch
            {

                ArgumentException _ =>
                    (HttpStatusCode.BadRequest, "Argument Exception"),
                _ =>
                (HttpStatusCode.BadRequest, "Argument Exception"),

            };
        }

        /// <summary>
        /// Returns a sanitized error message safe for client consumption.
        /// </summary>
        private static string GetSanitizedMessage(Exception exception)
        {
            // For security, only return specific exception messages for known exception types
            return exception switch
            {
                ArgumentException argEx => "Invalid input format. Please verify request parameters.",
                _ => "An unexpected error occurred. Please try again later."
            };
        }

        /// <summary>
        /// Provides appropriate recovery action guidance based on exception type.
        /// </summary>
        private static string GetRecoveryAction(Exception exception, string errorCode)
        {
            return errorCode switch
            {

                ErrorCodes.INVALIDINPUTFORMAT =>
                    "Validate input format against API specifications",


                _ => "Please try again later or contact support if the issue persists"
            };
        }
    }
}