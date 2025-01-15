// External package versions:
// HotChocolate v13.0.0
// HotChocolate.Types v13.0.0

using HotChocolate;
using HotChocolate.Types;
using HotChocolate.Authorization;
using HotChocolate.Data;
using EstateKit.Core.Entities;
using EstateKit.Core.Enums;
using System;

namespace EstateKit.Business.API.GraphQL.Types
{
    /// <summary>
    /// GraphQL type definition for government-issued identification documents with field-level
    /// encryption and comprehensive security controls. Implements Critical security classification
    /// handling for sensitive identifier data exposure through the GraphQL API.
    /// </summary>
    [ExtendObjectType]
    [Authorize(Policy = "IdentifierAccess")]
    public class IdentifierType
    {
        /// <summary>
        /// Configures the GraphQL type schema for Identifier with security measures and
        /// field-level encryption handling for sensitive data exposure.
        /// </summary>
        /// <param name="descriptor">The GraphQL type descriptor for configuration</param>
        [UseSecurity]
        [UseAuditLog]
        public void Configure(IObjectTypeDescriptor<Identifier> descriptor)
        {
            // Configure non-sensitive identifier fields
            descriptor
                .Field(f => f.Id)
                .Type<NonNullType<UuidType>>()
                .Description("Unique identifier for the government ID document");

            descriptor
                .Field(f => f.UserId)
                .Type<NonNullType<UuidType>>()
                .Description("Reference to the user who owns this identification")
                .Authorize(new[] { "Owner", "Admin" });

            descriptor
                .Field(f => f.Type)
                .Type<NonNullType<EnumType<IdentifierType>>>()
                .Description("Type of government-issued identification")
                .UseFiltering()
                .UseSorting();

            // Configure sensitive fields with encryption and access controls
            descriptor
                .Field(f => f.Value)
                .Type<NonNullType<StringType>>()
                .Description("Encrypted identification number or value")
                .Authorize(new[] { "Admin" })
                .UseEncryption()
                .UseSensitiveData()
                .UseRateLimiting(maxRequests: 10, timeWindow: 60)
                .Error(error => 
                {
                    error.Message = "Access denied to sensitive identifier value";
                    error.Code = "SENSITIVE_DATA_ACCESS";
                });

            descriptor
                .Field(f => f.IssuingAuthority)
                .Type<NonNullType<StringType>>()
                .Description("Authority that issued the identification")
                .UseFiltering()
                .UseSorting();

            descriptor
                .Field(f => f.IssueDate)
                .Type<NonNullType<DateTimeType>>()
                .Description("Date when the identification was issued")
                .UseFiltering()
                .UseSorting();

            descriptor
                .Field(f => f.ExpiryDate)
                .Type<NonNullType<DateTimeType>>()
                .Description("Date when the identification expires")
                .UseFiltering()
                .UseSorting();

            descriptor
                .Field(f => f.IsActive)
                .Type<NonNullType<BooleanType>>()
                .Description("Indicates if the identification is currently active")
                .UseFiltering()
                .UseSorting();

            // Configure field resolvers with security context
            descriptor.UseField(f => 
            {
                f.UseDbContext<SecurityContext>();
                f.UsePaging(maxPageSize: 50);
                f.UseProjection();
                f.UseAuditLog(level: AuditLogLevel.Detailed);
            });

            // Configure error masking for sensitive data
            descriptor.UseError(error =>
            {
                error.UseMasking(pattern: "***");
                error.UseAuditLog();
                error.UseNotification(NotificationLevel.Security);
            });

            // Configure caching strategy
            descriptor.UseCache(cache =>
            {
                cache.Policy = CachePolicy.NeverCache;
                cache.Scope = CacheScope.PerUser;
            });
        }
    }
}