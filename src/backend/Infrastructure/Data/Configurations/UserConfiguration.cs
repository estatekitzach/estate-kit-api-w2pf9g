// Microsoft.EntityFrameworkCore v10.0.0
// Microsoft.EntityFrameworkCore.Metadata.Builders v10.0.0
// EstateKit.Security v2.0.0
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EstateKit.Core.Entities;
using EstateKit.Security;

namespace EstateKit.Infrastructure.Data.Configurations
{
    /// <summary>
    /// Entity Framework Core configuration class for the User entity.
    /// Implements comprehensive database schema, relationships, security features,
    /// and audit capabilities as per technical specifications.
    /// </summary>
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        private readonly IEncryptionService _encryptionService;

        public UserConfiguration(IEncryptionService encryptionService)
        {
            _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
        }

        public void Configure(EntityTypeBuilder<User> builder)
        {
            // Table configuration
            builder.ToTable("users", "estate");

            // Partitioning strategy
            builder.HasPartitioningStrategy("HASH", "Id");

            // Primary key
            builder.HasKey(u => u.Id);
            builder.Property(u => u.Id)
                .IsRequired()
                .ValueGeneratedNever();

            // Field-level encryption for sensitive data
            builder.Property(u => u.DateOfBirth)
                .IsRequired()
                .HasEncryption(_encryptionService)
                .HasMaxLength(256);

            builder.Property(u => u.BirthPlace)
                .HasEncryption(_encryptionService)
                .HasMaxLength(256);

            // Required fields
            builder.Property(u => u.MaritalStatus)
                .IsRequired()
                .HasMaxLength(50);

            // One-to-one relationship with Contact
            builder.HasOne(u => u.Contact)
                .WithOne()
                .HasForeignKey<User>("ContactId")
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);

            // One-to-many relationships
            builder.HasMany(u => u.Documents)
                .WithOne()
                .HasForeignKey("UserId")
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(u => u.Identifiers)
                .WithOne()
                .HasForeignKey("UserId")
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(u => u.Assets)
                .WithOne()
                .HasForeignKey("UserId")
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes
            builder.HasIndex(u => u.Id)
                .IsUnique()
                .IsClustered(false)
                .HasFilter(null);

            builder.HasIndex("ContactId")
                .HasFilter(null);

            builder.HasIndex(u => u.CreatedAt)
                .HasFilter(null);

            // Audit fields
            builder.Property(u => u.CreatedAt)
                .IsRequired()
                .ValueGeneratedOnAdd();

            builder.Property(u => u.UpdatedAt)
                .ValueGeneratedOnUpdate();

            builder.Property(u => u.LastModifiedBy)
                .IsRequired()
                .HasMaxLength(100);

            // Soft delete configuration
            builder.Property(u => u.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            builder.HasQueryFilter(u => u.IsActive);

            // Security and versioning
            builder.Property(u => u.SecurityStamp)
                .IsRequired()
                .HasMaxLength(36)
                .IsConcurrencyToken();

            builder.Property(u => u.LastAuditDate)
                .IsRequired();

            // GDPR compliance
            builder.HasDataProtection(dp =>
            {
                dp.HasRetentionPolicy(TimeSpan.FromDays(2555)); // 7 years
                dp.RequiresConsentFor("PersonalData");
                dp.AllowsDataPortability();
            });

            // Audit logging configuration
            builder.HasChangeTracking(ct =>
            {
                ct.TrackAllProperties();
                ct.TrackCreatedBy();
                ct.TrackModifiedBy();
                ct.TrackTimestamps();
            });
        }
    }
}