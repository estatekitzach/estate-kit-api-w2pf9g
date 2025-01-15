// External package versions:
// System v9.0.0
// System.ComponentModel.DataAnnotations v9.0.0

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.RegularExpressions;
using EstateKit.Core.Enums;

namespace EstateKit.Core.Entities
{
    /// <summary>
    /// Represents a government-issued identification document with field-level encryption
    /// for sensitive data and comprehensive validation capabilities. This entity handles
    /// Critical security classification data requiring AES-256-GCM encryption with 90-day
    /// key rotation.
    /// </summary>
    [Table("Identifiers")]
    [Encrypted]
    public class Identifier
    {
        /// <summary>
        /// Unique identifier for the identification document
        /// </summary>
        [Key]
        public Guid Id { get; private set; }

        /// <summary>
        /// Reference to the user who owns this identification
        /// </summary>
        [Required]
        public Guid UserId { get; set; }

        /// <summary>
        /// Type of government-issued identification
        /// </summary>
        [Required]
        public IdentifierType Type { get; set; }

        /// <summary>
        /// Encrypted identification number or value
        /// </summary>
        [Required]
        [EncryptedField(Algorithm = "AES-256-GCM", KeyRotationDays = 90)]
        [MaxLength(256)]
        public string Value { get; set; }

        /// <summary>
        /// Authority that issued the identification
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string IssuingAuthority { get; set; }

        /// <summary>
        /// Date when the identification was issued
        /// </summary>
        [Required]
        public DateTime IssueDate { get; set; }

        /// <summary>
        /// Date when the identification expires
        /// </summary>
        [Required]
        public DateTime ExpiryDate { get; set; }

        /// <summary>
        /// Indicates if the identification is currently active
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Timestamp of when the record was created
        /// </summary>
        [Required]
        public DateTime CreatedAt { get; private set; }

        /// <summary>
        /// Timestamp of the last update to the record
        /// </summary>
        public DateTime? UpdatedAt { get; private set; }

        /// <summary>
        /// JSON-formatted audit trail of changes to the record
        /// </summary>
        [Required]
        public string AuditTrail { get; private set; }

        /// <summary>
        /// Initializes a new instance of the Identifier class with secure defaults
        /// </summary>
        public Identifier()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTime.UtcNow;
            IsActive = true;
            AuditTrail = "[]";
        }

        /// <summary>
        /// Validates the identifier data based on its type with enhanced security checks
        /// </summary>
        /// <returns>True if validation passes, false otherwise</returns>
        public bool Validate()
        {
            if (!Enum.IsDefined(typeof(IdentifierType), Type))
                return false;

            if (string.IsNullOrWhiteSpace(Value) || string.IsNullOrWhiteSpace(IssuingAuthority))
                return false;

            if (IssueDate > DateTime.UtcNow || ExpiryDate <= IssueDate)
                return false;

            // Type-specific validation patterns
            var isValid = Type switch
            {
                IdentifierType.DRIVERS_LICENSE_NUMBER => ValidateDriversLicense(),
                IdentifierType.PASSPORT_ID => ValidatePassport(),
                IdentifierType.SOCIAL_SECURITY_NUMBER => ValidateSSN(),
                _ => ValidateGenericIdentifier()
            };

            LogValidationAttempt(isValid);
            return isValid;
        }

        /// <summary>
        /// Updates the identifier information with security checks and audit logging
        /// </summary>
        public void Update(string value, string issuingAuthority, DateTime issueDate, DateTime expiryDate)
        {
            var previousState = SerializeCurrentState();
            
            Value = value;
            IssuingAuthority = issuingAuthority;
            IssueDate = issueDate;
            ExpiryDate = expiryDate;
            UpdatedAt = DateTime.UtcNow;

            if (!Validate())
            {
                throw new InvalidOperationException("Updated identifier data failed validation");
            }

            UpdateAuditTrail(previousState);
        }

        private bool ValidateDriversLicense()
        {
            // Alphanumeric pattern with state-specific formats
            return Regex.IsMatch(Value, @"^[A-Z0-9]{1,20}$", RegexOptions.Compiled);
        }

        private bool ValidatePassport()
        {
            // Standard passport format: 9 characters, alphanumeric
            return Regex.IsMatch(Value, @"^[A-Z0-9]{9}$", RegexOptions.Compiled);
        }

        private bool ValidateSSN()
        {
            // SSN format: XXX-XX-XXXX
            return Regex.IsMatch(Value, @"^\d{3}-\d{2}-\d{4}$", RegexOptions.Compiled);
        }

        private bool ValidateGenericIdentifier()
        {
            // Generic alphanumeric validation with reasonable length limits
            return Regex.IsMatch(Value, @"^[A-Z0-9-]{1,50}$", RegexOptions.Compiled);
        }

        private void LogValidationAttempt(bool isValid)
        {
            var auditEntry = new
            {
                Timestamp = DateTime.UtcNow,
                Action = "Validation",
                Success = isValid,
                Type = Type.ToString()
            };

            UpdateAuditTrail(auditEntry);
        }

        private string SerializeCurrentState()
        {
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                Value,
                IssuingAuthority,
                IssueDate,
                ExpiryDate,
                IsActive
            });
        }

        private void UpdateAuditTrail(object entry)
        {
            var audit = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<object>>(AuditTrail);
            audit.Add(entry);
            AuditTrail = System.Text.Json.JsonSerializer.Serialize(audit);
        }
    }
}