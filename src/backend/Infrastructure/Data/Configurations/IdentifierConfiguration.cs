// External package versions:
// Microsoft.EntityFrameworkCore v10.0.0
// Microsoft.EntityFrameworkCore.Metadata.Builders v10.0.0

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EstateKit.Core.Entities;

namespace EstateKit.Infrastructure.Data.Configurations
{
    /// <summary>
    /// Configures the database schema, security settings, and performance optimizations 
    /// for the Identifier entity with comprehensive field-level encryption and audit capabilities.
    /// </summary>
    public class IdentifierConfiguration : IEntityTypeConfiguration<Identifier>
    {
        public void Configure(EntityTypeBuilder<Identifier> builder)
        {
            // Table configuration
            builder.ToTable("Identifiers", schema: "identity")
                .HasComment("Stores government-issued identification documents with field-level encryption");

            // Primary key configuration
            builder.HasKey(i => i.Id)
                .IsClustered(false)
                .HasName("PK_Identifiers");

            // Required fields configuration
            builder.Property(i => i.UserId)
                .IsRequired()
                .HasComment("Foreign key to the User entity");

            builder.Property(i => i.Type)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(50)
                .HasComment("Type of government-issued identification");

            // Field-level encryption configuration for sensitive data
            builder.Property(i => i.Value)
                .IsRequired()
                .HasMaxLength(256)
                .IsEncrypted(options => {
                    options.Algorithm = "AES-256-GCM";
                    options.KeyRotationDays = 90;
                    options.ContextBinding = "UserId";
                    options.StorageLocation = "HSM";
                })
                .HasComment("Encrypted identification number");

            builder.Property(i => i.IssuingAuthority)
                .IsRequired()
                .HasMaxLength(100)
                .HasComment("Authority that issued the identification");

            // Date fields configuration
            builder.Property(i => i.IssueDate)
                .IsRequired()
                .HasColumnType("datetime2(0)")
                .HasComment("Date when the identification was issued");

            builder.Property(i => i.ExpiryDate)
                .IsRequired()
                .HasColumnType("datetime2(0)")
                .HasComment("Date when the identification expires");

            // Audit fields configuration
            builder.Property(i => i.CreatedAt)
                .IsRequired()
                .HasColumnType("datetime2(0)")
                .HasDefaultValueSql("SYSUTCDATETIME()")
                .ValueGeneratedOnAdd()
                .HasComment("UTC timestamp of record creation");

            builder.Property(i => i.UpdatedAt)
                .HasColumnType("datetime2(0)")
                .ValueGeneratedOnUpdate()
                .HasComment("UTC timestamp of last update");

            builder.Property(i => i.AuditTrail)
                .IsRequired()
                .HasMaxLength(10000)
                .HasComment("JSON-formatted audit trail of changes");

            // Status field configuration
            builder.Property(i => i.IsActive)
                .IsRequired()
                .HasDefaultValue(true)
                .HasComment("Indicates if the identification is currently active");

            // Index configuration for performance optimization
            builder.HasIndex(i => i.UserId)
                .HasDatabaseName("IX_Identifiers_UserId")
                .IncludeProperties(i => new { i.Type, i.IsActive })
                .HasFilter("[IsActive] = 1");

            builder.HasIndex(i => new { i.Type, i.Value })
                .HasDatabaseName("IX_Identifiers_Type_Value")
                .IsUnique()
                .HasFilter("[IsActive] = 1")
                .IsClustered(false);

            // Foreign key relationship configuration
            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(i => i.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_Identifiers_Users");

            // Concurrency token for optimistic locking
            builder.UseXminAsConcurrencyToken();

            // Query filters for security and performance
            builder.HasQueryFilter(i => i.IsActive);

            // Type-specific value validation
            builder.HasCheckConstraint(
                "CK_Identifier_Value_Format",
                @"CASE [Type]
                    WHEN 'DRIVERS_LICENSE_NUMBER' THEN LEN([Value]) <= 20 AND [Value] LIKE '[A-Z0-9]%'
                    WHEN 'PASSPORT_ID' THEN LEN([Value]) = 9 AND [Value] LIKE '[A-Z0-9]%'
                    WHEN 'SOCIAL_SECURITY_NUMBER' THEN LEN([Value]) = 11 AND [Value] LIKE '[0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9][0-9][0-9]'
                    ELSE LEN([Value]) <= 50 AND [Value] LIKE '[A-Z0-9-]%'
                  END = 1"
            );

            // Date validation constraints
            builder.HasCheckConstraint(
                "CK_Identifier_Dates",
                "[IssueDate] <= SYSUTCDATETIME() AND [IssueDate] < [ExpiryDate]"
            );
        }
    }
}