using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.Tasks;
using Amazon.CognitoIdentityProvider;
using Amazon.KeyManagementService;
using EstateKit.Infrastructure.Security;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace EstateKit.Data.API.Configuration
{
    /// <summary>
    /// Configures comprehensive security services and middleware for the Data API
    /// with enhanced monitoring and audit logging
    /// </summary>
    public static class SecurityConfig
    {
        private const int TOKEN_EXPIRATION_MINUTES = 60;
        private const int REFRESH_TOKEN_DAYS = 30;
        private const int MAX_REQUESTS_PER_MINUTE = 1000;
        private const int BURST_LIMIT = 2000;

        /// <summary>
        /// Configures and registers all security-related services with enhanced error handling and validation
        /// </summary>
        public static IServiceCollection AddSecurityServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));

            try
            {
                // Configure token validation
                ConfigureTokenValidation(services, configuration);

                // Configure encryption services
                ConfigureEncryption(services, configuration);

                // Configure security telemetry
                services.AddSingleton<TelemetryConfiguration>(sp =>
                {
                    var config = TelemetryConfiguration.CreateDefault();
                    config.ConnectionString = configuration["ApplicationInsights:ConnectionString"];
                    return config;
                });

                services.AddSingleton<TelemetryClient>();
                services.AddSingleton<EncryptionMetrics>();

                // Configure rate limiting
                services.AddMemoryCache();
                services.AddDistributedRateLimiter(options =>
                {
                    options.PermitLimit = MAX_REQUESTS_PER_MINUTE;
                    options.Window = TimeSpan.FromMinutes(1);
                    options.BurstLimit = BURST_LIMIT;
                });

                // Configure security policy provider
                services.AddSingleton<ISecurityPolicyProvider, SecurityPolicyProvider>();

                return services;
            }
            catch (Exception ex)
            {
                throw new SecurityConfigurationException(
                    "Failed to configure security services", ex);
            }
        }

        /// <summary>
        /// Configures enhanced JWT token validation settings with replay protection
        /// </summary>
        private static void ConfigureTokenValidation(
            IServiceCollection services,
            IConfiguration configuration)
        {
            // Configure AWS Cognito client
            services.AddAWSService<IAmazonCognitoIdentityProvider>(configuration.GetAWSOptions());

            // Configure token validation parameters
            services.Configure<TokenValidationParameters>(options =>
            {
                options.ValidateIssuerSigningKey = true;
                options.IssuerSigningKey = new JsonWebKey(
                    configuration["Authentication:IssuerSigningKey"]);
                options.ValidIssuer = configuration["Authentication:Issuer"];
                options.ValidAudience = configuration["Authentication:Audience"];
                options.ValidateIssuer = true;
                options.ValidateAudience = true;
                options.ValidateLifetime = true;
                options.ClockSkew = TimeSpan.FromMinutes(5);
                options.RequireExpirationTime = true;
                options.RequireSignedTokens = true;
            });

            // Register token validator
            services.AddSingleton<TokenValidator>();

            // Configure JWT authentication
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "Bearer";
                options.DefaultChallengeScheme = "Bearer";
            })
            .AddJwtBearer(options =>
            {
                configuration.Bind("Authentication:JwtBearer", options);
                options.TokenValidationParameters.ValidateLifetime = true;
                options.TokenValidationParameters.ClockSkew = TimeSpan.FromMinutes(5);
                
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var tokenValidator = context.HttpContext.RequestServices
                            .GetRequiredService<TokenValidator>();

                        var principal = context.Principal;
                        var token = context.SecurityToken;

                        if (!await tokenValidator.ValidateUserClaimsAsync(principal))
                        {
                            context.Fail("Invalid user claims");
                        }

                        if (!await tokenValidator.ValidateTokenReplayAsync(token.Id))
                        {
                            context.Fail("Token replay detected");
                        }
                    }
                };
            });
        }

        /// <summary>
        /// Configures field-level encryption settings with sensitivity-based policies
        /// </summary>
        private static void ConfigureEncryption(
            IServiceCollection services,
            IConfiguration configuration)
        {
            // Configure AWS KMS client
            services.AddAWSService<IAmazonKeyManagementService>(configuration.GetAWSOptions());

            // Configure encryption settings for different sensitivity levels
            services.Configure<EncryptionConfiguration>(options =>
            {
                configuration.Bind("Encryption", options);
                options.KeyRotationIntervals = new Dictionary<string, TimeSpan>
                {
                    { "Critical", TimeSpan.FromDays(90) },
                    { "Sensitive", TimeSpan.FromDays(180) },
                    { "Internal", TimeSpan.FromDays(365) }
                };
            });

            // Register encryption provider
            services.AddSingleton<IEncryptionProvider, EncryptionProvider>();

            // Configure encryption middleware
            services.AddSingleton<EncryptionMiddleware>();
            services.AddSingleton<IEncryptionKeyManager, EncryptionKeyManager>();

            // Configure encryption performance monitoring
            services.AddSingleton<IEncryptionMetricsCollector, EncryptionMetricsCollector>();
        }
    }

    public class SecurityConfigurationException : Exception
    {
        public SecurityConfigurationException(string message) : base(message) { }
        public SecurityConfigurationException(string message, Exception innerException) 
            : base(message, innerException) { }
    }
}