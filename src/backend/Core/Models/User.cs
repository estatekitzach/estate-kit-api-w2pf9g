using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EstateKit.Security.Attributes;
using EstateKit.Security.Validation;
using EstateKit.Logging;
using Microsoft.EntityFrameworkCore.DataEncryption;

namespace EstateKit.Core.Entities
{
    /// <summary>
    /// Represents a user in the EstateKit system with comprehensive security features,
    /// field-level encryption, and audit logging capabilities.
    /// </summary>
    [Table("Users")]
    [Encrypted]
    [AuditLogged]
    [SecurityValidated]
    public class User
    {
        private readonly IAuditLogger _auditLogger;
        private readonly ISecurityValidator _securityValidator;

        /// <summary>
        /// Unique identifier for the user
        /// </summary>
        [Key]
        public Guid Id { get; private set; }

        /// <summary>
        /// Contact information for the user with field-level encryption
        /// </summary>
        [Required]
        public Contact Contact { get; private set; }

        /// <summary>
        /// Date of birth with field-level encryption
        /// </summary>
        [Required]
        [EncryptedField(Algorithm = "AES-256-GCM", KeyRotationDays = 90)]
        public string DateOfBirth { get; private set; }

        /// <summary>
        /// Place of birth with field-level encryption
        /// </summary>
        [EncryptedField(Algorithm = "AES-256-GCM", KeyRotationDays = 90)]
        public string BirthPlace { get; private set; }

        /// <summary>
        /// Current marital status
        /// </summary>
        [Required]
        public string MaritalStatus { get; private set; }

        /// <summary>
        /// Collection of user documents with secure storage
        /// </summary>
        [Required]
        public ICollection<Document> Documents { get; private set; }

        /// <summary>
        /// Collection of government identifiers with critical protection
        /// </summary>
        [Required]
        public ICollection<Identifier> Identifiers { get; private set; }

        /// <summary>
        /// Collection of physical assets
        /// </summary>
        [Required]
        public ICollection<Asset> Assets { get; private set; }

        /// <summary>
        /// Indicates if the user account is active
        /// </summary>
        public bool IsActive { get; private set; }

        /// <summary>
        /// Timestamp of user creation
        /// </summary>
        [Required]
        public DateTime CreatedAt { get; private set; }

        /// <summary>
        /// Timestamp of last update
        /// </summary>
        public DateTime? UpdatedAt { get; private set; }

        /// <summary>
        /// User who last modified the record
        /// </summary>
        [Required]
        public string LastModifiedBy { get; private set; }

        /// <summary>
        /// Security stamp for concurrency and validation
        /// </summary>
        [Required]
        public string SecurityStamp { get; private set; }

        /// <summary>
        /// Timestamp of last security audit
        /// </summary>
        [Required]
        public DateTime LastAuditDate { get; private set; }

        /// <summary>
        /// Initializes a new instance of the User class with security and audit initialization
        /// </summary>
        public User(IAuditLogger auditLogger, ISecurityValidator securityValidator)
        {
            _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
            _securityValidator = securityValidator ?? throw new ArgumentNullException(nameof(securityValidator));

            Id = Guid.NewGuid();
            CreatedAt = DateTime.UtcNow;
            LastAuditDate = DateTime.UtcNow;
            Documents = new List<Document>();
            Identifiers = new List<Identifier>();
            Assets = new List<Asset>();
            IsActive = true;
            SecurityStamp = GenerateSecurityStamp();
        }

        /// <summary>
        /// Adds a new document to the user's collection with security validation and audit logging
        /// </summary>
        public void AddDocument(Document document)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            _securityValidator.ValidateDocument(document);
            
            Documents.Add(document);
            UpdatedAt = DateTime.UtcNow;
            LastModifiedBy = _securityValidator.GetCurrentUser();
            SecurityStamp = GenerateSecurityStamp();

            _auditLogger.LogDocumentAdded(Id, document.Id);
        }

        /// <summary>
        /// Adds a new government identifier with enhanced validation and encryption
        /// </summary>
        public void AddIdentifier(Identifier identifier)
        {
            if (identifier == null)
                throw new ArgumentNullException(nameof(identifier));

            _securityValidator.ValidateIdentifier(identifier);
            
            if (!identifier.Validate())
                throw new InvalidOperationException("Identifier validation failed");

            Identifiers.Add(identifier);
            UpdatedAt = DateTime.UtcNow;
            LastModifiedBy = _securityValidator.GetCurrentUser();
            SecurityStamp = GenerateSecurityStamp();

            _auditLogger.LogIdentifierAdded(Id, identifier.Id);
        }

        /// <summary>
        /// Adds a new asset to the user's collection with security checks
        /// </summary>
        public void AddAsset(Asset asset)
        {
            if (asset == null)
                throw new ArgumentNullException(nameof(asset));

            _securityValidator.ValidateAsset(asset);
            
            Assets.Add(asset);
            UpdatedAt = DateTime.UtcNow;
            LastModifiedBy = _securityValidator.GetCurrentUser();
            SecurityStamp = GenerateSecurityStamp();

            _auditLogger.LogAssetAdded(Id, asset.Id);
        }

        /// <summary>
        /// Deactivates the user account with audit logging
        /// </summary>
        public void Deactivate()
        {
            _securityValidator.ValidateDeactivation(Id);
            
            IsActive = false;
            UpdatedAt = DateTime.UtcNow;
            LastModifiedBy = _securityValidator.GetCurrentUser();
            SecurityStamp = GenerateSecurityStamp();

            _auditLogger.LogUserDeactivated(Id);
        }

        /// <summary>
        /// Generates a new security stamp for concurrency and validation
        /// </summary>
        private string GenerateSecurityStamp()
        {
            return Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        }
    }
}