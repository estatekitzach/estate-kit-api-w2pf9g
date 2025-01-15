using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Amazon.Extensions.CognitoAuthentication;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using EstateKit.Infrastructure.Security;
using System.Net;
using System.Security.Authentication;

namespace EstateKit.Business.API.Configuration
{
    /// <summary>
    /// Configures authentication services with enhanced security features for the Business Logic API
    /// </summary>
    public static class AuthenticationConfig
    {
        private const int TOKEN_EXPIRATION_MINUTES = 60;
        private const int REFRESH_TOKEN_DAYS = 30;
        private const int CLOCK_SKEW_MINUTES = 5;
        private const int MAX_FAILED_ATTEMPTS = 5;
        private const int RATE_LIMIT_REQUESTS = 1000;
        private const int RATE_LIMIT_WINDOW_MINUTES = 1;
        private const double TLS_VERSION = 1.3;
        private const int MIN_PASSWORD_LENGTH = 12;

        /// <summary>
        /// Configures authentication services with enhanced JWT bearer and AWS Cognito options
        /// </summary>
        public static IServiceCollection ConfigureAuthentication(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Enforce TLS 1.3
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls13;

            // Configure JWT Bearer Authentication
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.SaveToken = true;
                options.TokenValidationParameters = ConfigureTokenValidation(configuration);
                options.RequireHttpsMetadata = true;

                // Configure AWS Cognito options
                var cognitoOptions = ConfigureCognitoOptions(configuration);
                options.Authority = cognitoOptions.Authority;
                options.Audience = cognitoOptions.ClientId;

                // Enhanced security event handlers
                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                        context.Response.Headers.Add("WWW-Authenticate", "Bearer error=\"invalid_token\"");
                        context.Logger.LogWarning("Authentication failed: {Error}", context.Exception.Message);
                        return Task.CompletedTask;
                    },

                    OnTokenValidated = async context =>
                    {
                        var tokenValidator = context.HttpContext.RequestServices
                            .GetRequiredService<TokenValidator>();

                        var (isValid, principal) = await tokenValidator.ValidateTokenAsync(
                            context.SecurityToken.RawData);

                        if (!isValid)
                        {
                            context.Fail("Token validation failed");
                            return;
                        }

                        if (!await tokenValidator.ValidateUserClaimsAsync(principal))
                        {
                            context.Fail("User claims validation failed");
                            return;
                        }
                    },

                    OnChallenge = context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                        context.Response.Headers.Add("WWW-Authenticate", "Bearer");
                        return Task.CompletedTask;
                    }
                };
            });

            // Register TokenValidator service
            services.AddScoped<TokenValidator>();

            // Configure rate limiting
            services.AddRateLimiter(options =>
            {
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: context.User.Identity?.Name ?? context.Request.Headers.Host.ToString(),
                        factory: partition => new FixedWindowRateLimiterOptions
                        {
                            AutoReplenishment = true,
                            PermitLimit = RATE_LIMIT_REQUESTS,
                            Window = TimeSpan.FromMinutes(RATE_LIMIT_WINDOW_MINUTES)
                        }));
            });

            // Configure security headers middleware
            services.AddSecurityHeaders(options =>
            {
                options.UseHsts = true;
                options.UseXFrameOptions = true;
                options.UseXContentTypeOptions = true;
                options.UseXXSSProtection = true;
                options.UseReferrerPolicy = true;
                options.UseCsp = true;
            });

            return services;
        }

        /// <summary>
        /// Configures enhanced JWT token validation parameters
        /// </summary>
        private static TokenValidationParameters ConfigureTokenValidation(IConfiguration configuration)
        {
            return new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new JsonWebKey(configuration["JWT:SigningKey"]),
                ValidateIssuer = true,
                ValidIssuer = configuration["JWT:Issuer"],
                ValidateAudience = true,
                ValidAudience = configuration["JWT:Audience"],
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(CLOCK_SKEW_MINUTES),
                RequireExpirationTime = true,
                RequireSignedTokens = true,
                ValidAlgorithms = new[] { "RS256" },
                TokenDecryptionKey = new JsonWebKey(configuration["JWT:EncryptionKey"])
            };
        }

        /// <summary>
        /// Configures AWS Cognito authentication options with enhanced security
        /// </summary>
        private static CognitoAuthenticationOptions ConfigureCognitoOptions(IConfiguration configuration)
        {
            return new CognitoAuthenticationOptions
            {
                UserPoolId = configuration["Cognito:UserPoolId"],
                ClientId = configuration["Cognito:ClientId"],
                ClientSecret = configuration["Cognito:ClientSecret"],
                Authority = configuration["Cognito:Authority"],
                MetadataAddress = configuration["Cognito:MetadataAddress"],
                TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(CLOCK_SKEW_MINUTES)
                },
                SaveToken = true,
                ResponseType = "code",
                UsePkce = true,
                Scope = new[] { "openid", "profile", "email" },
                RequireHttpsMetadata = true,
                BackchannelHttpHandler = new HttpClientHandler
                {
                    SslProtocols = SslProtocols.Tls13,
                    ServerCertificateCustomValidationCallback = null
                },
                RefreshTokenExpiration = TimeSpan.FromDays(REFRESH_TOKEN_DAYS),
                AccessTokenExpiration = TimeSpan.FromMinutes(TOKEN_EXPIRATION_MINUTES),
                PasswordPolicy = new CognitoPasswordPolicy
                {
                    MinimumLength = MIN_PASSWORD_LENGTH,
                    RequireLowercase = true,
                    RequireUppercase = true,
                    RequireNumbers = true,
                    RequireSymbols = true
                },
                MfaConfiguration = "ON",
                EnableAdvancedSecurityMode = true
            };
        }
    }
}