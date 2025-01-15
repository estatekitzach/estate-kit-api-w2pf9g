using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using EstateKit.Data.API.Configuration;
using EstateKit.Data.API.Services;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.HttpOverrides;
using System;
using System.IO.Compression;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Versioning;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Amazon.CloudWatch;
using Amazon.KeyManagementService;

namespace EstateKit.Data.API
{
    /// <summary>
    /// Configures services and request pipeline for the Data Access API with comprehensive
    /// security, encryption, and monitoring capabilities.
    /// </summary>
    public class Startup
    {
        private readonly IConfiguration Configuration;
        private readonly IWebHostEnvironment Environment;
        private readonly ILogger<Startup> _logger;

        public Startup(IConfiguration configuration, IWebHostEnvironment env, ILogger<Startup> logger)
        {
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            Environment = env ?? throw new ArgumentNullException(nameof(env));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            ValidateConfiguration();
        }

        /// <summary>
        /// Configures application services with comprehensive security and monitoring
        /// </summary>
        public void ConfigureServices(IServiceCollection services)
        {
            try
            {
                // Configure database with retry policies and encryption
                services.AddDatabaseConfiguration(Configuration);

                // Configure field-level encryption with AWS KMS
                services.ConfigureEncryption(Configuration);

                // Configure security services with OAuth and JWT validation
                services.AddSecurityServices(Configuration);

                // Configure AWS services
                services.AddAWSService<IAmazonKeyManagementService>();
                services.AddAWSService<IAmazonCloudWatch>();

                // Configure API versioning
                services.AddApiVersioning(options =>
                {
                    options.DefaultApiVersion = new ApiVersion(1, 0);
                    options.AssumeDefaultVersionWhenUnspecified = true;
                    options.ReportApiVersions = true;
                    options.ApiVersionReader = new HeaderApiVersionReader("api-version");
                });

                // Configure response compression
                services.AddResponseCompression(options =>
                {
                    options.EnableForHttps = true;
                    options.Providers.Add<GzipCompressionProvider>();
                    options.MimeTypes = ResponseCompressionDefaults.MimeTypes;
                });

                services.Configure<GzipCompressionProviderOptions>(options =>
                {
                    options.Level = CompressionLevel.Fastest;
                });

                // Configure rate limiting
                services.AddRateLimiter(options =>
                {
                    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                    {
                        return RateLimitPartition.GetFixedWindowLimiter(
                            partitionKey: context.User.Identity?.Name ?? context.Request.Headers.Host.ToString(),
                            factory: partition => new FixedWindowRateLimiterOptions
                            {
                                AutoReplenishment = true,
                                PermitLimit = 1000,
                                QueueLimit = 0,
                                Window = TimeSpan.FromMinutes(1)
                            });
                    });
                });

                // Configure OpenTelemetry tracing
                services.AddOpenTelemetryTracing(builder =>
                {
                    builder
                        .SetResourceBuilder(ResourceBuilder
                            .CreateDefault()
                            .AddService("EstateKit.Data.API"))
                        .AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation()
                        .AddAWSInstrumentation()
                        .AddOtlpExporter();
                });

                // Configure health checks
                services.AddHealthChecks()
                    .AddDbContextCheck<ApplicationDbContext>()
                    .AddAWSService<IAmazonKeyManagementService>()
                    .AddAWSService<IAmazonCloudWatch>();

                // Configure audit service
                services.AddScoped<AuditService>();

                // Configure controllers with security policies
                services.AddControllers(options =>
                {
                    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
                });

                _logger.LogInformation("Service configuration completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Failed to configure services");
                throw;
            }
        }

        /// <summary>
        /// Configures the HTTP request pipeline with security middleware and monitoring
        /// </summary>
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            try
            {
                // Configure global exception handling
                app.UseExceptionHandler(options =>
                {
                    options.Run(async context =>
                    {
                        context.Response.StatusCode = 500;
                        await context.Response.WriteAsJsonAsync(new
                        {
                            error = "An unexpected error occurred"
                        });
                    });
                });

                // Configure security headers
                app.UseSecurityHeaders(options =>
                {
                    options.AddDefaultSecurePolicy();
                    options.AddStrictTransportSecurityMaxAgeIncludeSubDomains();
                });

                // Enable HTTPS redirection
                app.UseHttpsRedirection();

                // Configure forwarded headers
                app.UseForwardedHeaders(new ForwardedHeadersOptions
                {
                    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
                });

                // Enable response compression
                app.UseResponseCompression();

                // Configure authentication and authorization
                app.UseAuthentication();
                app.UseAuthorization();

                // Configure rate limiting
                app.UseRateLimiter();

                // Configure routing
                app.UseRouting();

                // Configure endpoints
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapControllers();
                    endpoints.MapHealthChecks("/health");
                });

                _logger.LogInformation("Application pipeline configured successfully");
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Failed to configure application pipeline");
                throw;
            }
        }

        private void ValidateConfiguration()
        {
            var requiredSections = new[]
            {
                "Database",
                "Encryption",
                "Authentication",
                "AWS"
            };

            foreach (var section in requiredSections)
            {
                if (!Configuration.GetSection(section).Exists())
                {
                    throw new InvalidOperationException($"Required configuration section '{section}' is missing");
                }
            }
        }
    }
}