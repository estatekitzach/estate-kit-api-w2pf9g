using System; // Version 9.0.0
using System.ComponentModel.DataAnnotations; // Version 9.0.0
using EstateKit.Core.Enums;
using EstateKit.Core.Security.Attributes;
using EstateKit.Core.Audit.Attributes;

namespace EstateKit.Core.Entities
{
    /// <summary>
    /// Represents a physical asset or property in the EstateKit system.
    /// Implements comprehensive validation, security measures, and audit logging.
    /// </summary>
    [Encrypted]
    [Auditable]
    [Table("Assets")]
    public class Asset
    {
        /// <summary>
        /// Unique identifier for the asset
        /// </summary>
        [Key]
        public Guid Id { get; private set; }

        /// <summary>
        /// Reference to the user who owns this asset
        /// </summary>
        [Required]
        public Guid UserId { get; private set; }

        /// <summary>
        /// Name or title of the asset
        /// </summary>
        [Required]
        [StringLength(200, MinimumLength = 1)]
        [RegularExpression(@"^[a-zA-Z0-9\s\-\.]+$")]
        public string Name { get; private set; }

        /// <summary>
        /// Detailed description of the asset
        /// </summary>
        [StringLength(2000)]
        public string Description { get; private set; }

        /// <summary>
        /// Categorization of the asset type
        /// </summary>
        [Required]
        public AssetType Type { get; private set; }

        /// <summary>
        /// Physical location or storage location of the asset
        /// </summary>
        [Required]
        [StringLength(500)]
        [EncryptedField]
        public string Location { get; private set; }

        /// <summary>
        /// Estimated monetary value of the asset
        /// </summary>
        [Range(0, 999999999999.99)]
        [DataType(DataType.Currency)]
        public decimal EstimatedValue { get; private set; }

        /// <summary>
        /// Encrypted access information for the asset
        /// </summary>
        [StringLength(1000)]
        [EncryptedField]
        [SensitiveData]
        public string AccessInformation { get; private set; }

        /// <summary>
        /// Indicates if the asset is currently active in the system
        /// </summary>
        public bool IsActive { get; private set; }

        /// <summary>
        /// Timestamp of asset creation
        /// </summary>
        [Required]
        public DateTime CreatedAt { get; private set; }

        /// <summary>
        /// Timestamp of last update
        /// </summary>
        public DateTime? UpdatedAt { get; private set; }

        /// <summary>
        /// Initializes a new instance of the Asset class
        /// </summary>
        public Asset()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTime.UtcNow;
            IsActive = true;
        }

        /// <summary>
        /// Updates the asset's information with validation and security checks
        /// </summary>
        /// <param name="name">New name for the asset</param>
        /// <param name="description">New description for the asset</param>
        /// <param name="type">New asset type categorization</param>
        /// <param name="location">New location information</param>
        /// <param name="estimatedValue">New estimated value</param>
        /// <param name="accessInformation">New access information</param>
        /// <exception cref="ArgumentNullException">Thrown when required parameters are null</exception>
        /// <exception cref="ArgumentException">Thrown when validation fails</exception>
        public void Update(
            string name,
            string description,
            AssetType type,
            string location,
            decimal estimatedValue,
            string accessInformation)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentNullException(nameof(name));
            
            if (string.IsNullOrWhiteSpace(location))
                throw new ArgumentNullException(nameof(location));

            if (estimatedValue < 0)
                throw new ArgumentException("Estimated value cannot be negative", nameof(estimatedValue));

            Name = name.Trim();
            Description = description?.Trim();
            Type = type;
            Location = location.Trim();
            EstimatedValue = estimatedValue;
            AccessInformation = accessInformation?.Trim();
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Marks the asset as inactive in the system
        /// </summary>
        public void Deactivate()
        {
            IsActive = false;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}