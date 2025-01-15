using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EstateKit.Core.Entities;
using EstateKit.Core.Enums;
using EstateKit.Encryption;
using System;

namespace EstateKit.Infrastructure.Data.Configurations
{
    /// <summary>
    /// Entity Framework Core configuration class for the Contact entity.
    /// Defines database schema, relationships, constraints, and security settings.
    /// </summary>
    public class ContactConfiguration : IEntityTypeConfiguration<Contact>
    {
        private readonly IEncryptionService _encryptionService;

        public ContactConfiguration(IEncryptionService encryptionService)
        {
            _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
        }

        public void Configure(EntityTypeBuilder<Contact> builder)
        {
            // Table configuration
            builder.ToTable("Contacts", schema: "personal");

            // Primary key
            builder.HasKey(c => c.Id);
            builder.Property(c => c.Id)
                .ValueGeneratedNever()
                .IsRequired();

            // Required fields with encryption
            builder.Property(c => c.FirstName)
                .HasMaxLength(100)
                .IsRequired()
                .UseEncryption(_encryptionService)
                .UseCollation("SQL_Latin1_General_CP1_CI_AS");

            builder.Property(c => c.LastName)
                .HasMaxLength(100)
                .IsRequired()
                .UseEncryption(_encryptionService)
                .UseCollation("SQL_Latin1_General_CP1_CI_AS");

            // Optional fields with encryption
            builder.Property(c => c.MiddleName)
                .HasMaxLength(100)
                .UseEncryption(_encryptionService)
                .UseCollation("SQL_Latin1_General_CP1_CI_AS");

            builder.Property(c => c.MaidenName)
                .HasMaxLength(100)
                .UseEncryption(_encryptionService)
                .UseCollation("SQL_Latin1_General_CP1_CI_AS");

            // Relationships configuration
            builder.HasMany(c => c.Addresses)
                .WithOne()
                .HasForeignKey("ContactId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            builder.HasMany(c => c.ContactMethods)
                .WithOne()
                .HasForeignKey("ContactId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            builder.HasMany(c => c.Relationships)
                .WithOne()
                .HasForeignKey("ContactId")
                .OnDelete(DeleteBehavior.Restrict);

            // Audit fields
            builder.Property(c => c.CreatedAt)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");

            builder.Property(c => c.UpdatedAt)
                .IsRequired(false);

            // Optimistic concurrency
            builder.Property(c => c.ConcurrencyToken)
                .IsConcurrencyToken()
                .HasDefaultValueSql("NEWID()");

            // Indexes for performance
            builder.HasIndex(c => new { c.LastName, c.FirstName })
                .HasDatabaseName("IX_Contacts_Names");

            builder.HasIndex(c => c.CreatedAt)
                .HasDatabaseName("IX_Contacts_CreatedAt");

            // Soft delete configuration
            builder.Property<bool>("IsDeleted")
                .HasDefaultValue(false);
            builder.HasQueryFilter(c => !EF.Property<bool>(c, "IsDeleted"));

            // Check constraints for enum values
            builder.HasCheckConstraint(
                "CK_Contact_RelationshipType",
                $"RelationshipType IN ({string.Join(",", Enum.GetValues<RelationshipType>().Cast<int>())})");

            // Value converters for complex types
            builder.Property<ContactMethodType>("PreferredContactMethod")
                .HasConversion(
                    v => v.ToString(),
                    v => (ContactMethodType)Enum.Parse(typeof(ContactMethodType), v));
        }
    }
}