using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace EstateKit.Business.API.Middleware
{
    /// <summary>
    /// ASP.NET Core middleware that implements comprehensive security headers to protect against 
    /// common web vulnerabilities and enforce secure communication policies.
    /// </summary>
    public class SecurityHeadersMiddleware
    {
        private readonly RequestDelegate _next;

        /// <summary>
        /// Initializes a new instance of the SecurityHeadersMiddleware class.
        /// </summary>
        /// <param name="next">The next middleware delegate in the pipeline.</param>
        /// <exception cref="ArgumentNullException">Thrown when next is null.</exception>
        public SecurityHeadersMiddleware(RequestDelegate next)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
        }

        /// <summary>
        /// Processes the HTTP request by adding comprehensive security headers to the response
        /// before continuing the middleware pipeline.
        /// </summary>
        /// <param name="context">The HTTP context for the current request.</param>
        /// <returns>A Task representing the asynchronous middleware operation.</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var headers = context.Response.Headers;

            // Enforce HTTPS and HSTS
            headers["Strict-Transport-Security"] = 
                "max-age=31536000; includeSubDomains; preload";

            // Prevent clickjacking attacks
            headers["X-Frame-Options"] = "DENY";

            // Prevent MIME type sniffing
            headers["X-Content-Type-Options"] = "nosniff";

            // Enable browser XSS protection
            headers["X-XSS-Protection"] = "1; mode=block";

            // Define strict Content Security Policy
            headers["Content-Security-Policy"] = 
                "default-src 'self'; " +
                "frame-ancestors 'none'; " +
                "form-action 'self'; " +
                "upgrade-insecure-requests; " +
                "block-all-mixed-content";

            // Control referrer information
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

            // Restrict browser features
            headers["Permissions-Policy"] = 
                "accelerometer=(), " +
                "camera=(), " +
                "geolocation=(), " +
                "gyroscope=(), " +
                "magnetometer=(), " +
                "microphone=(), " +
                "payment=(), " +
                "usb=()";

            // Cross-Origin isolation policies
            headers["Cross-Origin-Embedder-Policy"] = "require-corp";
            headers["Cross-Origin-Opener-Policy"] = "same-origin";
            headers["Cross-Origin-Resource-Policy"] = "same-origin";

            // Continue the middleware pipeline
            await _next(context);
        }
    }
}