using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EstateKit.Core.Entities;
using EstateKit.Core.Enums;
using EstateKit.Encryption; // v2.0.0

namespace EstateKit.Infrastructure.Data.Configurations
{
    /// <summary>
    /// Configures the database schema, relationships, security features, and performance optimizations 
    /// for the Document entity using Entity Framework Core's fluent API.
    /// </summary>
    public class DocumentConfiguration : IEntityTypeConfiguration<Document>
    {
        private readonly IEncryptionService _encryptionService;

        public DocumentConfiguration(IEncryptionService encryptionService)
        {
            _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
        }

        public void Configure(EntityTypeBuilder<Document> builder)
        {
            // Table configuration
            builder.ToTable("Documents", tb => 
            {
                tb.HasComment("Stores document information with comprehensive security and audit features");
                tb.IsTemporal(); // Enable temporal tables for audit history
            });

            // Primary key
            builder.HasKey(d => d.Id);
            builder.Property(d => d.Id)
                .ValueGeneratedNever()
                .HasComment("Unique identifier for the document");

            // Required relationships
            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired()
                .HasComment("Reference to the owning user");

            // Properties configuration
            builder.Property(d => d.Type)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(50)
                .HasComment("Document type classification");

            builder.Property(d => d.FrontImageUrl)
                .HasMaxLength(1000)
                .HasComment("S3 URL for document front image")
                .HasEncryption(_encryptionService);

            builder.Property(d => d.BackImageUrl)
                .HasMaxLength(1000)
                .HasComment("S3 URL for document back image")
                .HasEncryption(_encryptionService);

            builder.Property(d => d.Location)
                .HasMaxLength(500)
                .HasComment("Physical location of the document")
                .HasEncryption(_encryptionService);

            builder.Property(d => d.InKit)
                .IsRequired()
                .HasDefaultValue(false)
                .HasComment("Indicates if document is in estate planning kit");

            builder.Property(d => d.Metadata)
                .HasColumnType("nvarchar(max)")
                .HasComment("JSON metadata from OCR processing")
                .HasEncryption(_encryptionService);

            builder.Property(d => d.CreatedAt)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()")
                .HasComment("UTC timestamp of document creation");

            builder.Property(d => d.UpdatedAt)
                .HasComment("UTC timestamp of last update");

            builder.Property(d => d.IsProcessed)
                .IsRequired()
                .HasDefaultValue(false)
                .HasComment("Indicates if OCR processing is complete");

            builder.Property(d => d.SecurityClassification)
                .IsRequired()
                .HasMaxLength(50)
                .HasComment("Security classification level");

            builder.Property(d => d.S3BucketName)
                .IsRequired()
                .HasMaxLength(100)
                .HasComment("AWS S3 bucket name for document storage");

            builder.Property(d => d.S3KeyPrefix)
                .IsRequired()
                .HasMaxLength(200)
                .HasComment("Unique S3 key prefix for document files");

            builder.Property(d => d.ProcessingStatus)
                .IsRequired()
                .HasMaxLength(50)
                .HasDefaultValue("Pending")
                .HasComment("Current document processing status");

            // Indexes for performance optimization
            builder.HasIndex(d => new { d.UserId, d.Type })
                .HasDatabaseName("IX_Documents_UserId_Type")
                .HasFilter("IsProcessed = 1")
                .HasComment("Optimizes queries for processed documents by user and type");

            builder.HasIndex(d => d.CreatedAt)
                .HasDatabaseName("IX_Documents_CreatedAt")
                .HasComment("Optimizes temporal queries");

            builder.HasIndex(d => d.SecurityClassification)
                .HasDatabaseName("IX_Documents_SecurityClassification")
                .HasComment("Optimizes security-based filtering");

            builder.HasIndex(d => d.S3KeyPrefix)
                .IsUnique()
                .HasDatabaseName("IX_Documents_S3KeyPrefix")
                .HasComment("Ensures S3 key prefix uniqueness");

            // Triggers for audit logging and S3 cleanup
            builder.ToTable(tb =>
            {
                tb.HasTrigger("TR_Documents_Audit_Insert")
                    .HasComment("Logs document creation events");
                tb.HasTrigger("TR_Documents_Audit_Update")
                    .HasComment("Logs document modification events");
                tb.HasTrigger("TR_Documents_Audit_Delete")
                    .HasComment("Logs document deletion events and triggers S3 cleanup");
            });

            // Query filters
            builder.HasQueryFilter(d => 
                EF.Property<string>(d, "TenantId") == _currentTenantProvider.GetTenantId());

            // Concurrency token for optimistic concurrency
            builder.UseXminAsConcurrencyToken();
        }
    }
}