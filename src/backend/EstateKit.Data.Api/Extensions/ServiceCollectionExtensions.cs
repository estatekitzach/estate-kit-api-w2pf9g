
using Microsoft.Extensions.Options;
using Polly;
//using Polly.Extensions.Http;
//using StackExchange.Redis;
using System.Net.Security;
using System.Security.Authentication;
//using HealthChecks.Redis;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using EstateKit.Data.DBContexts;
using EstateKit.Core.Interfaces;
using EstateKit.Infrastructure.Data.Repositories;

namespace EstateKit.Data.Api.Extensions
{
    /// <summary>
    /// Extension methods for configuring EstateKit API services with enhanced security,
    /// monitoring, and resilience features.
    /// </summary>
    internal static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Configures all required services for the EstateKit API with FIPS 140-2 compliance
        /// and enterprise-grade security features.
        /// </summary>
        /// <param name="services">The service collection to configure.</param>
        /// <param name="configuration">The application configuration.</param>
        /// <returns>The configured service collection.</returns>
        public static IServiceCollection AddEstateKitDataApiServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);

            // Set up the dependency injection
            services.AddScoped<IUserAssetRepository, UserAssetRepository>();
            services.AddScoped<IContactRepository, ContactRepository>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IUserDocumentRepository, UserDocumentRepository>();
            services.AddScoped<IUserIdentifierRepository, UserIdentifierRepository>();

            //Todo: debug 
            services.AddSwaggerGen();

            return services;
        }

   
        /// <summary>
        /// Configures encryption services with FIPS 140-2 compliance and monitoring.
        /// </summary>
        public static IServiceCollection AddDbContexts(
            this IServiceCollection services)
        {
            services.AddDbContext<DbContext, EstateKitContext>(options =>
            {
                options.UseNpgsql("EKVault");
            });

            // Register encryption service with proper security controls
            /*services.AddScoped<IUserKeyRepository, UserKeyRepository>();
            services.AddScoped<IKeyManagementService, KeyManagementService>();
            services.AddScoped<IEncryptionService, EncryptionService>(); */

            // Configure encryption service monitoring
            /* todo: why isn't this working?
             * services.AddHealthChecks()
                .AddCheck<EncryptionService>("EncryptionService")
                .AddRedis("Redis")
                .AddAWSService<IAmazonKeyManagementService>("KMS");*/

            return services;
        }
    }
}