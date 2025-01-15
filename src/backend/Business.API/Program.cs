using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Sinks.CloudWatch;
using OpenTelemetry.Extensions.Hosting;
using System;
using System.Threading.Tasks;
using System.Security.Authentication;
using EstateKit.Business.API;

namespace Business.API
{
    /// <summary>
    /// Entry point for the Business Logic API application with enhanced security,
    /// monitoring, and observability features.
    /// </summary>
    public class Program
    {
        private const string CORRELATION_ID_HEADER = "X-Correlation-Id";

        /// <summary>
        /// Application entry point with comprehensive error handling and environment-specific configuration
        /// </summary>
        public static async Task Main(string[] args)
        {
            try
            {
                // Configure Serilog early for startup logging
                ConfigureBootstrapLogger();

                Log.Information("Starting EstateKit Business Logic API");

                var host = CreateHostBuilder(args).Build();

                // Initialize services that require startup configuration
                using (var scope = host.Services.CreateScope())
                {
                    var services = scope.ServiceProvider;
                    try
                    {
                        // Additional startup initialization can be added here
                        Log.Information("Initializing application services");
                    }
                    catch (Exception ex)
                    {
                        Log.Fatal(ex, "An error occurred while initializing application services");
                        throw;
                    }
                }

                await host.RunAsync();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Application terminated unexpectedly");
                throw;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        /// <summary>
        /// Creates and configures the web host builder with enhanced security and monitoring features
        /// </summary>
        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    var env = hostingContext.HostingEnvironment;
                    
                    config.SetBasePath(env.ContentRootPath)
                          .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                          .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true)
                          .AddEnvironmentVariables()
                          .AddCommandLine(args);

                    if (env.IsDevelopment())
                    {
                        config.AddUserSecrets<Program>();
                    }
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder
                        .UseStartup<Startup>()
                        .ConfigureKestrel(options =>
                        {
                            // Enforce TLS 1.3
                            options.ConfigureHttpsDefaults(https =>
                            {
                                https.SslProtocols = SslProtocols.Tls13;
                                https.ClientCertificateMode = Microsoft.AspNetCore.Server.Kestrel.Https.ClientCertificateMode.RequireCertificate;
                            });

                            // Configure HTTP/2 support
                            options.ConfigureEndpointDefaults(endpoints =>
                            {
                                endpoints.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
                            });

                            // Configure request limits
                            options.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10MB
                            options.Limits.MaxRequestHeaderCount = 50;
                            options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(30);
                        });
                })
                .UseSerilog((context, services, configuration) => configuration
                    .ReadFrom.Configuration(context.Configuration)
                    .ReadFrom.Services(services)
                    .Enrich.FromLogContext()
                    .Enrich.WithCorrelationId(CORRELATION_ID_HEADER)
                    .Enrich.WithEnvironmentName()
                    .WriteTo.CloudWatch(
                        logGroup: "/estatekit/businessapi/logs",
                        restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Information,
                        configuration: context.Configuration))
                .ConfigureServices((context, services) =>
                {
                    // Add OpenTelemetry instrumentation
                    services.AddOpenTelemetryTracing(builder =>
                    {
                        builder.AddAspNetCoreInstrumentation()
                               .AddHttpClientInstrumentation()
                               .AddEntityFrameworkCoreInstrumentation()
                               .AddOtlpExporter();
                    });

                    services.AddOpenTelemetryMetrics(builder =>
                    {
                        builder.AddAspNetCoreInstrumentation()
                               .AddHttpClientInstrumentation()
                               .AddRuntimeInstrumentation()
                               .AddOtlpExporter();
                    });
                });

        /// <summary>
        /// Configures bootstrap logger for application startup
        /// </summary>
        private static void ConfigureBootstrapLogger()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
                .MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithEnvironmentName()
                .WriteTo.Console()
                .CreateBootstrapLogger();
        }
    }
}