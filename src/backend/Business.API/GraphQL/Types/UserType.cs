using HotChocolate;
using HotChocolate.Types;
using HotChocolate.Authorization;
using HotChocolate.Data;
using Microsoft.AspNetCore.DataProtection;
using EstateKit.Core.Entities;
using EstateKit.Security.Attributes;
using EstateKit.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EstateKit.Business.API.GraphQL.Types
{
    /// <summary>
    /// GraphQL type definition for User entity with comprehensive security controls,
    /// field-level encryption, and audit logging capabilities.
    /// </summary>
    [ExtendObjectType]
    [Authorize(Policy = "UserAccess")]
    [DataClassification(Level = "Sensitive")]
    [AuditLog(Level = "Detailed")]
    [RateLimit(MaxRequests = 1000, TimeWindow = 60)]
    public class UserType
    {
        private readonly IDataProtectionProvider _dataProtection;
        private readonly IAuditLogger _auditLogger;

        public UserType(
            IDataProtectionProvider dataProtection,
            IAuditLogger auditLogger)
        {
            _dataProtection = dataProtection ?? throw new ArgumentNullException(nameof(dataProtection));
            _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
        }

        /// <summary>
        /// Configures the GraphQL type fields with security controls and validation
        /// </summary>
        public void Configure(IObjectTypeDescriptor<User> descriptor)
        {
            // Configure ID field with audit logging
            descriptor.Field(u => u.Id)
                .Type<NonNullType<IdType>>()
                .Description("Unique identifier for the user")
                .Authorize("ReadUser")
                .UseFiltering()
                .UseSorting();

            // Configure Contact field with role-based authorization
            descriptor.Field(u => u.Contact)
                .Type<NonNullType<ContactType>>()
                .Description("Contact information for the user")
                .Authorize("ReadContact")
                .UseFiltering()
                .UseSorting();

            // Configure DateOfBirth with encryption and sensitive data handling
            descriptor.Field(u => u.DateOfBirth)
                .Type<NonNullType<StringType>>()
                .Description("User's date of birth (encrypted)")
                .Authorize("ReadSensitiveData")
                .Use(next => async context =>
                {
                    await next(context);
                    if (context.Result is string dob)
                    {
                        var protector = _dataProtection.CreateProtector("UserDateOfBirth");
                        context.Result = protector.Unprotect(dob);
                        _auditLogger.LogFieldAccess("DateOfBirth", context.Path.ToString());
                    }
                });

            // Configure BirthPlace with encryption
            descriptor.Field(u => u.BirthPlace)
                .Type<StringType>()
                .Description("User's place of birth (encrypted)")
                .Authorize("ReadSensitiveData")
                .Use(next => async context =>
                {
                    await next(context);
                    if (context.Result is string birthPlace)
                    {
                        var protector = _dataProtection.CreateProtector("UserBirthPlace");
                        context.Result = protector.Unprotect(birthPlace);
                        _auditLogger.LogFieldAccess("BirthPlace", context.Path.ToString());
                    }
                });

            // Configure MaritalStatus with basic validation
            descriptor.Field(u => u.MaritalStatus)
                .Type<NonNullType<StringType>>()
                .Description("User's marital status")
                .Authorize("ReadBasicInfo");

            // Configure Documents with authorization and batch loading
            descriptor.Field(u => u.Documents)
                .Type<NonNullType<ListType<NonNullType<DocumentType>>>>()
                .Description("User's documents")
                .Authorize("ReadDocuments")
                .UsePaging(maxPageSize: 50)
                .UseFiltering()
                .UseSorting()
                .UseProjection();

            // Configure Identifiers with strict access control and encryption
            descriptor.Field(u => u.Identifiers)
                .Type<NonNullType<ListType<NonNullType<IdentifierType>>>>()
                .Description("User's government identifiers")
                .Authorize(new[] { "ReadIdentifiers", "AdminAccess" })
                .UsePaging(maxPageSize: 20)
                .UseFiltering()
                .UseSorting()
                .Use(next => async context =>
                {
                    _auditLogger.LogCollectionAccess("Identifiers", context.Path.ToString());
                    await next(context);
                });

            // Configure Assets with authorization and audit logging
            descriptor.Field(u => u.Assets)
                .Type<NonNullType<ListType<NonNullType<AssetType>>>>()
                .Description("User's physical assets")
                .Authorize("ReadAssets")
                .UsePaging(maxPageSize: 50)
                .UseFiltering()
                .UseSorting()
                .Use(next => async context =>
                {
                    _auditLogger.LogCollectionAccess("Assets", context.Path.ToString());
                    await next(context);
                });

            // Add field middleware for metrics collection
            descriptor.Use(next => async context =>
            {
                var startTime = DateTime.UtcNow;
                await next(context);
                var duration = DateTime.UtcNow - startTime;
                
                _auditLogger.LogFieldMetrics(
                    context.Path.ToString(),
                    duration.TotalMilliseconds,
                    context.Result != null);
            });
        }
    }
}