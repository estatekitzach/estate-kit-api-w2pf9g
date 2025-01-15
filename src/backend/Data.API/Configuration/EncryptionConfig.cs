using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace EstateKit.Data.API.Configuration
{
    /// <summary>
    /// Configuration options for field-level encryption in the Data API with comprehensive validation and AWS KMS settings
    /// </summary>
    public class EncryptionOptions
    {
        [Required]
        [RegularExpression(@"^arn:aws:kms:[a-z0-9-]+:[0-9]{12}:key/[a-f0-9-]{36}$", 
            ErrorMessage = "Invalid AWS KMS key ARN format")]
        public string KmsKeyId { get; set; }

        [Required]
        [RegularExpression(@"^[a-z]{2}-[a-z]+-\d{1}$", 
            ErrorMessage = "Invalid AWS region format")]
        public string KmsKeyRegion { get; set; }

        [Range(1, 365, ErrorMessage = "Key rotation interval must be between 1 and 365 days")]
        public int KeyRotationIntervalDays { get; set; }

        [Required]
        public HashSet<string> SensitiveFields { get; }

        [Required]
        public Dictionary<string, string> FieldEncryptionKeys { get; }

        public bool EnableKeyRotation { get; set; }

        public string EncryptionAlgorithm { get; set; }

        public EncryptionOptions()
        {
            SensitiveFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            FieldEncryptionKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            KeyRotationIntervalDays = 90;
            EnableKeyRotation = true;
            EncryptionAlgorithm = "AES_256_GCM";
        }

        /// <summary>
        /// Validates the encryption configuration settings
        /// </summary>
        /// <returns>True if configuration is valid</returns>
        public bool Validate()
        {
            if (string.IsNullOrEmpty(KmsKeyId) || !System.Text.RegularExpressions.Regex.IsMatch(
                KmsKeyId, @"^arn:aws:kms:[a-z0-9-]+:[0-9]{12}:key/[a-f0-9-]{36}$"))
            {
                return false;
            }

            if (string.IsNullOrEmpty(KmsKeyRegion) || !System.Text.RegularExpressions.Regex.IsMatch(
                KmsKeyRegion, @"^[a-z]{2}-[a-z]+-\d{1}$"))
            {
                return false;
            }

            if (SensitiveFields == null || SensitiveFields.Count == 0)
            {
                return false;
            }

            if (FieldEncryptionKeys == null || FieldEncryptionKeys.Count == 0)
            {
                return false;
            }

            if (KeyRotationIntervalDays < 1 || KeyRotationIntervalDays > 365)
            {
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Defines a sensitive field and its encryption requirements with validation
    /// </summary>
    public class SensitiveFieldDefinition
    {
        [Required]
        [StringLength(200)]
        public string FieldName { get; set; }

        [Required]
        public string EntityType { get; set; }

        [Required]
        public EncryptionLevel Level { get; set; }

        public string EncryptionAlgorithm { get; set; } = "AES_256_GCM";

        public bool RequireAuditLogging { get; set; } = true;
    }

    /// <summary>
    /// Enumeration defining different levels of encryption requirements based on data sensitivity
    /// </summary>
    public enum EncryptionLevel
    {
        Critical = 1,
        Sensitive = 2,
        Internal = 3
    }

    /// <summary>
    /// Extension methods for configuring encryption services
    /// </summary>
    public static class EncryptionConfigurationExtensions
    {
        /// <summary>
        /// Configures encryption options with validation and error handling
        /// </summary>
        /// <param name="services">The service collection to configure</param>
        /// <param name="configuration">The configuration source</param>
        /// <returns>Configured service collection with encryption options</returns>
        public static IServiceCollection ConfigureEncryption(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var options = new EncryptionOptions();
            configuration.GetSection("Encryption").Bind(options);

            if (!options.Validate())
            {
                throw new ValidationException("Invalid encryption configuration");
            }

            // Configure default sensitive fields based on technical requirements
            options.SensitiveFields.Add("dateOfBirth");
            options.SensitiveFields.Add("governmentId");
            options.SensitiveFields.Add("passportNumber");
            options.SensitiveFields.Add("driversLicenseNumber");
            options.SensitiveFields.Add("socialSecurityNumber");
            options.SensitiveFields.Add("financialAccountNumber");

            // Register encryption options as singleton
            services.AddSingleton(options);

            // Configure audit logging for sensitive field access
            services.AddSingleton<IAuditLogger, EncryptionAuditLogger>();

            // Configure encryption key management
            services.AddSingleton<IKeyManagementService, AwsKmsKeyManagementService>();

            // Configure monitoring and alerts
            services.AddSingleton<IEncryptionMonitor, EncryptionMonitoringService>();

            return services;
        }
    }
}