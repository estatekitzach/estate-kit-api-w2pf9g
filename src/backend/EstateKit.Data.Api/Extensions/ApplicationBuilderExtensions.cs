
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using System;
using System.Threading.RateLimiting;
using EstateKit.Data.Api.Middleware;

namespace EstateKit.Data.Api.Extensions
{
    /// <summary>
    /// Extension methods for IApplicationBuilder to configure the EstateKit Personal Information API
    /// middleware pipeline with enhanced security, monitoring, and documentation features.
    /// </summary>
    internal static class ApplicationBuilderExtensions
    {
        /// <summary>
        /// Configures all required middleware components for the EstateKit API with enhanced security and monitoring.
        /// </summary>
        /// <param name="app">The application builder instance</param>
        /// <param name="env">The web host environment</param>
        /// <returns>The configured application builder</returns>
        public static IApplicationBuilder UseEstateKitMiddleware(
    this IApplicationBuilder app,
    IWebHostEnvironment env)
        {
            ArgumentNullException.ThrowIfNull(app);
            ArgumentNullException.ThrowIfNull(env);

            // Configure security headers
            //TODO: Enable this:  app.UseSecurityHeaders();

            // Enable request logging and monitoring
            app.UseRequestLogging();

            // Configure correlation ID tracking
            //TODO: Enable this: app.UseCorrelationId();

            if (!env.IsDevelopment())
            {
                // Configure HSTS in production
                app.UseHsts();
            }

            // Enable HTTPS redirection
            app.UseHttpsRedirection();

            // Configure forwarded headers for proxy servers
            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            });

            // Enable rate limiting
            //TODO: Enable this: app.UseRateLimiter();

            // Enable authentication and authorization
            // app.UseAuthentication();
            //Todo: Enable this: app.UseAuthorization();

            // Configure Swagger documentation in development
            if (env.IsDevelopment())
            {
                app.UseSwaggerConfiguration();
            }

            // Configure routing and endpoints
            app.UseRouting();
            /* todo: enable this: app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapHealthChecks("/health");
            });*/

            return app;
        }

        /// <summary>
        /// Configures enhanced request logging middleware with performance monitoring.
        /// </summary>
       private static IApplicationBuilder UseRequestLogging(this IApplicationBuilder app)
        {
            return app.UseMiddleware<RequestLoggingMiddleware>();
        }

        /// <summary>
        /// Configures security headers for enhanced protection.
        /// </summary>
        private static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
        {
            return app.Use(async (context, next) =>
            {
                context.Response.Headers["X-Frame-Options"] = "DENY";
                context.Response.Headers["X-Content-Type-Options"] = "nosniff";
                context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
                context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                context.Response.Headers["Permissions-Policy"] =
                    "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()";
                context.Response.Headers["Content-Security-Policy"] =
                    "default-src 'self'; script-src 'self' 'unsafe-inline' 'unsafe-eval'; style-src 'self' 'unsafe-inline';";

                await next().ConfigureAwait(false);
            });
        }

        /// <summary>
        /// Configures correlation ID middleware for request tracking.
        /// </summary>
        private static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
        {
            return app.Use(async (context, next) =>
            {
                const string correlationHeader = "X-Correlation-ID";

                if (!context.Request.Headers.TryGetValue(correlationHeader, out var correlationId))
                {
                    correlationId = Guid.NewGuid().ToString();
                    context.Request.Headers[correlationHeader] = correlationId.ToString();
                }

                context.Response.Headers[correlationHeader] = correlationId.ToString();

                await next().ConfigureAwait(false);
            });
        }

        /// <summary>
        /// Configures rate limiting middleware with token bucket algorithm.
        /// </summary>
        private static IApplicationBuilder UseRateLimiter(this IApplicationBuilder app)
        {
            return app.Use(async (context, next) =>
            {
                var limiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
                {
                    TokenLimit = 1000,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 2,
                    ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                    TokensPerPeriod = 1000,
                    AutoReplenishment = true
                });

                using var lease = await limiter.AcquireAsync().ConfigureAwait(false);
                
                if (lease.IsAcquired)
                {
                    context.Response.Headers["X-RateLimit-Limit"] = "1000";
                    // context.Response.Headers.Add("X-RateLimit-Remaining", 
                        // lease.GetRemainingPermits().ToString());
                    
                    await next().ConfigureAwait(false); ;
                }
                else
                {
                    context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    await context.Response.WriteAsJsonAsync(new 
                    { 
                        error = "Too many requests", 
                        // retryAfter = lease.GetRetryAfter()?.TotalSeconds ?? 60 
                        retryAfter = 60 //Todo: imlement this
                    }).ConfigureAwait(false); ;
                }
            });
        }

        /// <summary>
        /// Configures enhanced Swagger documentation UI with security features.
        /// </summary>
        private static IApplicationBuilder UseSwaggerConfiguration(this IApplicationBuilder app)
        {
            //api/v1/
            app.UseSwagger()
            .UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
                c.RoutePrefix = string.Empty;
            });
            return app;
                /* .UseSwagger(options =>
                {
                    options.RouteTemplate = "api-docs/{documentName}/swagger.json";
                    options.PreSerializeFilters.Add((swaggerDoc, httpReq) =>
                    {
                        swaggerDoc.Servers = new List<Microsoft.OpenApi.Models.OpenApiServer>
                        {
                            new OpenApiServer 
                            { 
                                Url = $"{httpReq.Scheme}://{httpReq.Host.Value}" 
                            }
                        };
                    });
                })*/
               
            /* .UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/api-docs/v1/swagger.json", "EstateKit Personal Information API v1");
                options.RoutePrefix = "api-docs";
                options.DocumentTitle = "EstateKit Personal Information API Documentation";
                options.EnableDeepLinking();
                options.DisplayRequestDuration();
                options.EnableFilter();
                options.EnableTryItOutByDefault();

                // Configure OAuth2
                options.OAuthClientId("swagger-ui");
                options.OAuthAppName("EstateKit API - Swagger");
                options.OAuthUsePkce();
            });*/
        }
    }
}