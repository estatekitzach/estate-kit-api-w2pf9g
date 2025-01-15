using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using EstateKit.Business.API.Configuration;
using Business.API.Configuration;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
using System;
using System.IO.Compression;
using System.Linq;

namespace EstateKit.Business.API
{
    /// <summary>
    /// Configures the Business Logic API services and request pipeline with enterprise-grade
    /// security, monitoring, and performance features.
    /// </summary>
    public class Startup
    {
        private readonly IConfiguration Configuration;
        private readonly IWebHostEnvironment Environment;
        private readonly MonitoringConfig _monitoringConfig;
        private readonly AwsConfig _awsConfig;

        public Startup(IConfiguration configuration, IWebHostEnvironment env)
        {
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            Environment = env ?? throw new ArgumentNullException(nameof(env));
            _monitoringConfig = new MonitoringConfig(configuration);
            _awsConfig = new AwsConfig(configuration);
        }

        /// <summary>
        /// Configures application services with comprehensive security and monitoring
        /// </summary>
        public void ConfigureServices(IServiceCollection services)
        {
            // Configure authentication with enhanced security
            services.ConfigureAuthentication(Configuration);

            // Configure GraphQL services with schema validation and security
            services.AddGraphQLServices();

            // Configure monitoring services with metrics and tracing
            _monitoringConfig.AddMonitoringServices(services);

            // Configure AWS services for S3 and Textract
            services.AddSingleton(_awsConfig);

            // Configure CORS with strict policy
            services.AddCors(options =>
            {
                options.AddPolicy("ApiCorsPolicy", builder =>
                {
                    builder
                        .WithOrigins(Configuration.GetSection("AllowedOrigins").Get<string[]>())
                        .WithMethods("GET", "POST")
                        .WithHeaders("Authorization", "Content-Type")
                        .AllowCredentials();
                });
            });

            // Configure response compression
            services.AddResponseCompression(options =>
            {
                options.Providers.Add<BrotliCompressionProvider>();
                options.Providers.Add<GzipCompressionProvider>();
                options.EnableForHttps = true;
                options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
                    new[] { "application/json", "application/graphql" });
            });

            services.Configure<BrotliCompressionProviderOptions>(options =>
            {
                options.Level = CompressionLevel.Optimal;
            });

            services.Configure<GzipCompressionProviderOptions>(options =>
            {
                options.Level = CompressionLevel.Optimal;
            });

            // Configure forwarded headers for proper IP handling
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | 
                                         ForwardedHeaders.XForwardedProto;
                options.RequireHeaderSymmetry = false;
                options.KnownNetworks.Clear();
                options.KnownProxies.Clear();
            });

            // Configure health checks
            services.AddHealthChecks()
                .AddCheck<GraphQLHealthCheck>("GraphQL")
                .AddCheck<S3HealthCheck>("S3")
                .AddCheck<TextractHealthCheck>("Textract");

            // Configure validation services
            services.AddScoped<ValidationService>();

            // Configure distributed caching with Redis
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = Configuration.GetConnectionString("Redis");
                options.InstanceName = "EstateKit_";
            });
        }

        /// <summary>
        /// Configures the HTTP request pipeline with security middleware and monitoring
        /// </summary>
        public void Configure(IApplicationBuilder app)
        {
            // Configure forwarded headers
            app.UseForwardedHeaders();

            // Enable response compression
            app.UseResponseCompression();

            // Configure security headers
            app.UseSecurityHeaders(options =>
            {
                options.AddDefaultSecurePolicy();
                options.AddStrictTransportSecurityMaxAgeIncludeSubDomains();
                options.AddXssProtectionBlock();
                options.AddContentTypeOptionsNoSniff();
                options.AddReferrerPolicyStrictOriginWhenCrossOrigin();
            });

            // Enable HTTPS redirection and HSTS
            app.UseHttpsRedirection();
            if (!Environment.IsDevelopment())
            {
                app.UseHsts();
            }

            // Configure CORS
            app.UseCors("ApiCorsPolicy");

            // Configure routing and endpoints
            app.UseRouting();

            // Configure authentication and authorization
            app.UseAuthentication();
            app.UseAuthorization();

            // Configure request logging and correlation
            app.UseRequestLogging();
            app.UseCorrelationId();

            // Configure monitoring middleware
            app.UseAWSXRayRecorder();
            app.UseMetricServer();
            app.UseHttpMetrics();

            // Configure endpoints
            app.UseEndpoints(endpoints =>
            {
                // Configure GraphQL endpoint
                endpoints.MapGraphQL()
                    .RequireAuthorization()
                    .WithMetadata(new RateLimitMetadata(1000, TimeSpan.FromMinutes(1)));

                // Configure health check endpoint
                endpoints.MapHealthChecks("/health")
                    .RequireAuthorization("HealthCheckPolicy");

                // Configure metrics endpoint
                endpoints.MapMetrics()
                    .RequireAuthorization("MetricsPolicy");
            });
        }
    }
}