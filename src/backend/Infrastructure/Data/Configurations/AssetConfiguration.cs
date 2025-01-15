using Microsoft.EntityFrameworkCore; // Version 10.0.0
using Microsoft.EntityFrameworkCore.Metadata.Builders; // Version 10.0.0
using EstateKit.Core.Entities;

namespace EstateKit.Infrastructure.Data.Configurations
{
    /// <summary>
    /// Entity Framework Core configuration class for the Asset entity.
    /// Defines database schema, relationships, encryption settings, and performance optimizations.
    /// </summary>
    public class AssetConfiguration : IEntityTypeConfiguration<Asset>
    {
        public void Configure(EntityTypeBuilder<Asset> builder)
        {
            // Table configuration
            builder.ToTable("Assets", "estate");

            // Primary key
            builder.HasKey(a => a.Id);
            builder.Property(a => a.Id)
                .ValueGeneratedNever()
                .IsRequired();

            // Required fields
            builder.Property(a => a.UserId)
                .IsRequired();

            builder.Property(a => a.Name)
                .IsRequired()
                .HasMaxLength(200)
                .IsUnicode(true)
                .HasColumnType("nvarchar(200)");

            builder.Property(a => a.Description)
                .HasMaxLength(1000)
                .IsUnicode(true)
                .HasColumnType("nvarchar(1000)");

            // Asset type enum configuration
            builder.Property(a => a.Type)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(50);

            // Encrypted fields configuration
            builder.Property(a => a.Location)
                .IsRequired()
                .HasMaxLength(500)
                .UseEstateKitEncryption();

            builder.Property(a => a.AccessInformation)
                .HasMaxLength(1000)
                .UseEstateKitEncryption();

            // Decimal configuration
            builder.Property(a => a.EstimatedValue)
                .HasPrecision(18, 2)
                .HasColumnType("decimal(18,2)");

            builder.HasCheckConstraint("CK_Asset_EstimatedValue", "[EstimatedValue] >= 0");

            // Soft delete configuration
            builder.Property(a => a.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            // Audit fields configuration
            builder.Property(a => a.CreatedAt)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");

            builder.Property(a => a.UpdatedAt)
                .IsRequired(false)
                .ValueGeneratedOnUpdate();

            // Relationships
            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            // Indexes for performance optimization
            builder.HasIndex(a => new { a.UserId, a.Type })
                .HasDatabaseName("IX_Asset_UserId_Type");

            builder.HasIndex(a => a.IsActive)
                .HasDatabaseName("IX_Asset_IsActive")
                .HasFilter("[IsActive] = 1");

            builder.HasIndex(a => a.EstimatedValue)
                .HasDatabaseName("IX_Asset_EstimatedValue");

            // Global query filter for soft delete
            builder.HasQueryFilter(a => a.IsActive);

            // Concurrency token for optimistic concurrency
            builder.UseXminAsConcurrencyToken();
        }
    }
}