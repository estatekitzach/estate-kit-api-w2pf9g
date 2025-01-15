using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Extensions.Hosting;
using System;
using System.Threading.Tasks;

namespace EstateKit.Data.API
{
    /// <summary>
    /// Entry point for the Data Access API application with comprehensive security,
    /// monitoring, and configuration capabilities.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Application entry point with enhanced error handling and logging
        /// </summary>
        public static async Task Main(string[] args)
        {
            try
            {
                var host = CreateHostBuilder(args).Build();
                await host.RunAsync();
            }
            catch (Exception ex)
            {
                // Ensure critical startup errors are logged
                Log.Fatal(ex, "Host terminated unexpectedly during startup");
                throw;
            }
        }

        /// <summary>
        /// Configures the web host builder with comprehensive security, monitoring,
        /// and infrastructure services
        /// </summary>
        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                    webBuilder.ConfigureKestrel(options =>
                    {
                        options.AddServerHeader = false;
                        options.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10MB limit
                        options.Limits.MaxConcurrentConnections = 1000;
                        options.Limits.MaxConcurrentUpgradedConnections = 100;
                    });
                })
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    var env = hostingContext.HostingEnvironment;

                    config
                        .SetBasePath(env.ContentRootPath)
                        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                        .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true)
                        .AddEnvironmentVariables()
                        .AddCommandLine(args);

                    if (env.IsDevelopment())
                    {
                        config.AddUserSecrets<Program>();
                    }

                    // Validate required configuration sections
                    var configuration = config.Build();
                    ValidateConfiguration(configuration);
                })
                .ConfigureLogging((hostingContext, logging) =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                    logging.AddDebug();
                    logging.AddApplicationInsights();
                    
                    // Configure structured logging
                    logging.AddJsonConsole(options =>
                    {
                        options.IncludeScopes = true;
                        options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
                        options.JsonWriterOptions = new System.Text.Json.JsonWriterOptions
                        {
                            Indented = true
                        };
                    });

                    if (hostingContext.HostingEnvironment.IsDevelopment())
                    {
                        logging.SetMinimumLevel(LogLevel.Debug);
                    }
                    else
                    {
                        logging.SetMinimumLevel(LogLevel.Information);
                    }
                })
                .ConfigureServices((hostContext, services) =>
                {
                    // Configure OpenTelemetry for distributed tracing
                    services.AddOpenTelemetryTracing(builder =>
                    {
                        builder
                            .SetResourceBuilder(OpenTelemetry.Resources.ResourceBuilder
                                .CreateDefault()
                                .AddService("EstateKit.Data.API"))
                            .AddAspNetCoreInstrumentation()
                            .AddHttpClientInstrumentation()
                            .AddAWSInstrumentation()
                            .AddOtlpExporter(options =>
                            {
                                options.Endpoint = new Uri(hostContext.Configuration["Telemetry:OtlpEndpoint"]);
                            });
                    });

                    // Configure metrics collection
                    services.AddOpenTelemetryMetrics(builder =>
                    {
                        builder
                            .AddAspNetCoreInstrumentation()
                            .AddRuntimeInstrumentation()
                            .AddOtlpExporter();
                    });
                });

        /// <summary>
        /// Validates required configuration sections for application startup
        /// </summary>
        private static void ValidateConfiguration(IConfiguration configuration)
        {
            var requiredSections = new[]
            {
                "Database",
                "Encryption",
                "Authentication",
                "AWS",
                "Telemetry"
            };

            foreach (var section in requiredSections)
            {
                if (!configuration.GetSection(section).Exists())
                {
                    throw new InvalidOperationException(
                        $"Required configuration section '{section}' is missing");
                }
            }

            // Validate AWS configuration
            var awsSection = configuration.GetSection("AWS");
            if (string.IsNullOrEmpty(awsSection["Region"]) || 
                string.IsNullOrEmpty(awsSection["KmsKeyId"]))
            {
                throw new InvalidOperationException(
                    "Required AWS configuration values are missing");
            }

            // Validate database configuration
            var dbSection = configuration.GetSection("Database");
            if (string.IsNullOrEmpty(dbSection["ConnectionString"]))
            {
                throw new InvalidOperationException(
                    "Database connection string is required");
            }
        }
    }
}