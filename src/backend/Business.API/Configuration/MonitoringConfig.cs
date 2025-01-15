using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Amazon.CloudWatch;
using Amazon.XRay.Recorder.Core;
using Amazon.XRay.Recorder.Core.Sampling;
using Amazon.XRay.Recorder.Core.Internal.Entities;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using System;
using System.Collections.Generic;

namespace Business.API.Configuration
{
    /// <summary>
    /// Configures comprehensive monitoring, logging, tracing, and observability services
    /// for the Business Logic API using AWS CloudWatch, X-Ray, and OpenTelemetry.
    /// </summary>
    public class MonitoringConfig
    {
        private readonly IConfiguration _configuration;
        private const string MONITORING_SECTION = "Monitoring";

        /// <summary>
        /// Initializes a new instance of the MonitoringConfig class.
        /// </summary>
        /// <param name="configuration">Application configuration instance</param>
        /// <exception cref="ArgumentNullException">Thrown when configuration is null</exception>
        public MonitoringConfig(IConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        /// <summary>
        /// Configures and adds all monitoring services to the dependency injection container.
        /// </summary>
        /// <param name="services">The service collection to configure</param>
        /// <returns>The configured service collection</returns>
        /// <exception cref="ArgumentNullException">Thrown when services is null</exception>
        public IServiceCollection AddMonitoringServices(IServiceCollection services)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            var monitoringConfig = _configuration.GetSection(MONITORING_SECTION);

            if (monitoringConfig.GetValue<bool>("CloudWatch:Enabled"))
            {
                ConfigureCloudWatch(services);
            }

            if (monitoringConfig.GetValue<bool>("XRay:Enabled"))
            {
                ConfigureXRay(services);
            }

            if (monitoringConfig.GetValue<bool>("OpenTelemetry:Enabled"))
            {
                ConfigureOpenTelemetry(services);
            }

            ConfigureHealthChecks(services);

            return services;
        }

        /// <summary>
        /// Configures AWS CloudWatch monitoring with metrics and logging.
        /// </summary>
        private void ConfigureCloudWatch(IServiceCollection services)
        {
            var cloudWatchConfig = _configuration.GetSection($"{MONITORING_SECTION}:CloudWatch");
            
            services.AddAWSService<IAmazonCloudWatch>();
            
            services.Configure<AmazonCloudWatchConfig>(options =>
            {
                options.MetricNamespace = cloudWatchConfig.GetValue<string>("MetricNamespace");
                options.FlushInterval = TimeSpan.FromSeconds(
                    cloudWatchConfig.GetValue<int>("FlushIntervalSeconds"));
            });

            // Configure CloudWatch Logs
            services.AddLogging(builder =>
            {
                builder.AddAWSProvider(config =>
                {
                    config.LogGroup = $"/estatekit/businessapi/{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}";
                    config.RetentionDays = cloudWatchConfig.GetValue<int>("LogRetentionDays");
                });
            });
        }

        /// <summary>
        /// Configures AWS X-Ray for distributed tracing.
        /// </summary>
        private void ConfigureXRay(IServiceCollection services)
        {
            var xrayConfig = _configuration.GetSection($"{MONITORING_SECTION}:XRay");

            services.AddAWSXRay(options =>
            {
                options.SamplingRuleManifest = new SamplingRuleManifest
                {
                    Rules = new List<SamplingRule>
                    {
                        new SamplingRule
                        {
                            RuleName = "Default",
                            Priority = 1000,
                            FixedRate = xrayConfig.GetValue<double>("SamplingRate"),
                            ReservoirSize = 60,
                            ServiceName = xrayConfig.GetValue<string>("SegmentName")
                        }
                    }
                };

                options.CollectSqlQueries = xrayConfig.GetValue<bool>("CollectSqlQueries");
            });
        }

        /// <summary>
        /// Configures OpenTelemetry for vendor-neutral monitoring.
        /// </summary>
        private void ConfigureOpenTelemetry(IServiceCollection services)
        {
            var otelConfig = _configuration.GetSection($"{MONITORING_SECTION}:OpenTelemetry");

            services.AddOpenTelemetry()
                .WithTracing(builder =>
                {
                    builder
                        .AddSource(otelConfig.GetValue<string>("ServiceName"))
                        .SetResourceBuilder(
                            ResourceBuilder.CreateDefault()
                                .AddService(
                                    serviceName: otelConfig.GetValue<string>("ServiceName"),
                                    serviceVersion: otelConfig.GetValue<string>("ServiceVersion"))
                                .AddAttributes(new Dictionary<string, object>
                                {
                                    ["deployment.environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
                                    ["service.instance.id"] = Environment.MachineName
                                }))
                        .AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation()
                        .AddEntityFrameworkCoreInstrumentation()
                        .AddOtlpExporter(opts =>
                        {
                            opts.BatchExportProcessorOptions = new BatchExportProcessorOptions<Activity>
                            {
                                MaxQueueSize = otelConfig.GetValue<int>("BatchSize"),
                                ExporterTimeoutMilliseconds = otelConfig.GetValue<int>("ExportIntervalMs")
                            };
                        });
                })
                .WithMetrics(builder =>
                {
                    builder
                        .AddMeter(otelConfig.GetValue<string>("ServiceName"))
                        .AddRuntimeInstrumentation()
                        .AddHttpClientInstrumentation()
                        .AddAspNetCoreInstrumentation()
                        .AddOtlpExporter(opts =>
                        {
                            opts.BatchExportProcessorOptions = new BatchExportProcessorOptions<Metric>
                            {
                                MaxQueueSize = otelConfig.GetValue<int>("BatchSize"),
                                ExporterTimeoutMilliseconds = otelConfig.GetValue<int>("ExportIntervalMs")
                            };
                        });
                });
        }

        /// <summary>
        /// Configures health checks for system status monitoring.
        /// </summary>
        private void ConfigureHealthChecks(IServiceCollection services)
        {
            services.AddHealthChecks()
                .AddCheck("self", () => HealthCheckResult.Healthy())
                .AddAWSService<IAmazonCloudWatch>()
                .AddDbContextCheck<ApplicationDbContext>();
        }
    }
}