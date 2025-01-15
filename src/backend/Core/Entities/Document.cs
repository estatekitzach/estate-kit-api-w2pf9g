using System;
using System.ComponentModel.DataAnnotations;
using Amazon.S3;
using Microsoft.EntityFrameworkCore.DataEncryption;
using EstateKit.Core.Enums;

namespace EstateKit.Core.Entities
{
    /// <summary>
    /// Represents a document in the EstateKit system with comprehensive security, 
    /// OCR processing, version control, and field-level encryption capabilities.
    /// </summary>
    [Encrypted]
    [Auditable]
    [Table("Documents")]
    public class Document
    {
        /// <summary>
        /// Unique identifier for the document
        /// </summary>
        [Key]
        public Guid Id { get; private set; }

        /// <summary>
        /// Reference to the user who owns this document
        /// </summary>
        [Required]
        public Guid UserId { get; private set; }

        /// <summary>
        /// Type of document (e.g., DRIVERS_LICENSE, PASSPORT, etc.)
        /// </summary>
        [Required]
        public DocumentType Type { get; private set; }

        /// <summary>
        /// S3 URL for the front image of the document
        /// </summary>
        [EncryptedField]
        public string FrontImageUrl { get; private set; }

        /// <summary>
        /// S3 URL for the back image of the document (if applicable)
        /// </summary>
        [EncryptedField]
        public string BackImageUrl { get; private set; }

        /// <summary>
        /// Physical location of the document (if applicable)
        /// </summary>
        [EncryptedField]
        public string Location { get; private set; }

        /// <summary>
        /// Indicates if the document is stored in the estate planning kit
        /// </summary>
        public bool InKit { get; private set; }

        /// <summary>
        /// JSON metadata extracted from document OCR processing
        /// </summary>
        [EncryptedField]
        public string Metadata { get; private set; }

        /// <summary>
        /// Timestamp when the document was created
        /// </summary>
        [Required]
        public DateTime CreatedAt { get; private set; }

        /// <summary>
        /// Timestamp when the document was last updated
        /// </summary>
        public DateTime? UpdatedAt { get; private set; }

        /// <summary>
        /// Indicates if OCR processing is complete
        /// </summary>
        public bool IsProcessed { get; private set; }

        /// <summary>
        /// Name of the S3 bucket where document images are stored
        /// </summary>
        [Required]
        public string S3BucketName { get; private set; }

        /// <summary>
        /// Unique S3 key prefix for this document's files
        /// </summary>
        [Required]
        public string S3KeyPrefix { get; private set; }

        /// <summary>
        /// Security classification level for the document
        /// </summary>
        [Required]
        public string SecurityClassification { get; private set; }

        /// <summary>
        /// Current status of document processing
        /// </summary>
        [Required]
        public string ProcessingStatus { get; private set; }

        /// <summary>
        /// Initializes a new instance of the Document class with required security parameters
        /// </summary>
        public Document(DocumentType type, Guid userId, string s3BucketName, string securityClassification)
        {
            if (string.IsNullOrEmpty(s3BucketName))
                throw new ArgumentNullException(nameof(s3BucketName));
            
            if (string.IsNullOrEmpty(securityClassification))
                throw new ArgumentNullException(nameof(securityClassification));

            Id = Guid.NewGuid();
            Type = type;
            UserId = userId;
            S3BucketName = s3BucketName;
            SecurityClassification = securityClassification;
            S3KeyPrefix = $"{userId}/{Id}";
            CreatedAt = DateTime.UtcNow;
            ProcessingStatus = "Pending";
            IsProcessed = false;
            InKit = false;
        }

        /// <summary>
        /// Updates document metadata after OCR processing with audit logging
        /// </summary>
        public void UpdateMetadata(string metadata)
        {
            if (string.IsNullOrEmpty(metadata))
                throw new ArgumentNullException(nameof(metadata));

            // Audit logging handled by [Auditable] attribute
            Metadata = metadata;
            IsProcessed = true;
            ProcessingStatus = "Completed";
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Updates the physical location of the document with validation
        /// </summary>
        public void UpdateLocation(string location)
        {
            if (string.IsNullOrEmpty(location))
                throw new ArgumentNullException(nameof(location));

            // Audit logging handled by [Auditable] attribute
            Location = location;
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Updates whether the document is stored in the estate planning kit
        /// </summary>
        public void SetInKit(bool inKit)
        {
            // Audit logging handled by [Auditable] attribute
            InKit = inKit;
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Updates the document's S3 storage location with validation
        /// </summary>
        public void UpdateS3Location(string frontImageUrl, string backImageUrl = null)
        {
            if (string.IsNullOrEmpty(frontImageUrl))
                throw new ArgumentNullException(nameof(frontImageUrl));

            if (!Uri.TryCreate(frontImageUrl, UriKind.Absolute, out _))
                throw new ArgumentException("Invalid front image URL", nameof(frontImageUrl));

            if (backImageUrl != null && !Uri.TryCreate(backImageUrl, UriKind.Absolute, out _))
                throw new ArgumentException("Invalid back image URL", nameof(backImageUrl));

            // Audit logging handled by [Auditable] attribute
            FrontImageUrl = frontImageUrl;
            BackImageUrl = backImageUrl;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}