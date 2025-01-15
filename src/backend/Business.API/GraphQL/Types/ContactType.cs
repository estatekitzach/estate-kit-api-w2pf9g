// HotChocolate v13.0.0
// HotChocolate.Types v13.0.0
// EstateKit.Security v1.0.0
// EstateKit.Monitoring v1.0.0
using HotChocolate;
using HotChocolate.Types;
using EstateKit.Core.Entities;
using EstateKit.Core.Enums;
using EstateKit.Security.Attributes;
using EstateKit.Security.Encryption;
using EstateKit.Monitoring.Audit;
using EstateKit.Monitoring.Performance;
using System;
using System.Threading.Tasks;

namespace EstateKit.Business.API.GraphQL.Types
{
    /// <summary>
    /// GraphQL type definition for Contact entity with comprehensive security controls 
    /// and field-level encryption for sensitive personal information.
    /// </summary>
    [ObjectType]
    [ExtendObjectType]
    [DataClassification(DataSensitivity.Sensitive)]
    [AuditLog]
    [RateLimit(1000, TimeSpan.FromMinutes(1))]
    public class ContactType : ObjectType<Contact>
    {
        private readonly IFieldEncryptionProvider _encryptionProvider;
        private readonly IAuditLogger _auditLogger;
        private readonly IPerformanceMonitor _performanceMonitor;

        public ContactType(
            IFieldEncryptionProvider encryptionProvider,
            IAuditLogger auditLogger,
            IPerformanceMonitor performanceMonitor)
        {
            _encryptionProvider = encryptionProvider ?? throw new ArgumentNullException(nameof(encryptionProvider));
            _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
            _performanceMonitor = performanceMonitor ?? throw new ArgumentNullException(nameof(performanceMonitor));
        }

        protected override void Configure(IObjectTypeDescriptor<Contact> descriptor)
        {
            // Configure Id field with GUID type and authorization
            descriptor.Field(c => c.Id)
                .Type<NonNullType<IdType>>()
                .Authorize(Policies.ContactRead);

            // Configure FirstName field with encryption and validation
            descriptor.Field(c => c.FirstName)
                .Type<NonNullType<StringType>>()
                .Authorize(Policies.ContactRead)
                .UseEncryption(_encryptionProvider)
                .UseAuditLog(_auditLogger)
                .Validate(validation =>
                {
                    validation.MaxLength(100);
                    validation.NotEmpty();
                });

            // Configure LastName field with encryption and validation
            descriptor.Field(c => c.LastName)
                .Type<NonNullType<StringType>>()
                .Authorize(Policies.ContactRead)
                .UseEncryption(_encryptionProvider)
                .UseAuditLog(_auditLogger)
                .Validate(validation =>
                {
                    validation.MaxLength(100);
                    validation.NotEmpty();
                });

            // Configure MiddleName field with optional encryption
            descriptor.Field(c => c.MiddleName)
                .Type<StringType>()
                .Authorize(Policies.ContactRead)
                .UseEncryption(_encryptionProvider)
                .UseAuditLog(_auditLogger)
                .Validate(validation =>
                {
                    validation.MaxLength(100);
                });

            // Configure MaidenName field with optional encryption
            descriptor.Field(c => c.MaidenName)
                .Type<StringType>()
                .Authorize(Policies.ContactRead)
                .UseEncryption(_encryptionProvider)
                .UseAuditLog(_auditLogger)
                .Validate(validation =>
                {
                    validation.MaxLength(100);
                });

            // Configure Addresses field with authorization and validation
            descriptor.Field(c => c.Addresses)
                .Type<NonNullType<ListType<NonNullType<AddressType>>>>()
                .Authorize(Policies.ContactRead)
                .UseAuditLog(_auditLogger)
                .UseFiltering()
                .UseSorting();

            // Configure ContactMethods field with authorization and rate limiting
            descriptor.Field(c => c.ContactMethods)
                .Type<NonNullType<ListType<NonNullType<ContactMethodType>>>>()
                .Authorize(Policies.ContactRead)
                .UseAuditLog(_auditLogger)
                .UseFiltering()
                .UseSorting()
                .RateLimit(100, TimeSpan.FromMinutes(1));

            // Configure Relationships field with authorization and dataloader
            descriptor.Field(c => c.Relationships)
                .Type<NonNullType<ListType<NonNullType<RelationshipType>>>>()
                .Authorize(Policies.ContactRead)
                .UseAuditLog(_auditLogger)
                .UseDataLoader<RelationshipByContactDataLoader>()
                .UseFiltering()
                .UseSorting();

            // Add field resolvers with error handling
            descriptor.Field("fullName")
                .Type<NonNullType<StringType>>()
                .Resolve(async context =>
                {
                    using var _ = _performanceMonitor.TrackOperation("ResolveFullName");
                    try
                    {
                        var contact = context.Parent<Contact>();
                        var middleName = !string.IsNullOrEmpty(contact.MiddleName) 
                            ? $" {contact.MiddleName}" 
                            : string.Empty;
                        return await Task.FromResult($"{contact.FirstName}{middleName} {contact.LastName}");
                    }
                    catch (Exception ex)
                    {
                        _auditLogger.LogError("Error resolving full name", ex);
                        throw new GraphQLException("Unable to resolve full name");
                    }
                });

            // Configure caching strategy
            descriptor.CacheControl(maxAge: 300); // 5 minutes cache

            // Add deprecation notices for any deprecated fields
            descriptor.Field(c => c.MaidenName)
                .Deprecated("Use 'previousNames' field instead. Will be removed in v2.0");

            // Setup metrics collection
            descriptor.Use(next => async context =>
            {
                using var _ = _performanceMonitor.TrackOperation(context.Selection.Field.Name);
                await next(context);
            });
        }
    }
}