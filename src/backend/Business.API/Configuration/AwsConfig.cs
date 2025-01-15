using Microsoft.Extensions.Configuration; // v7.0.0
using Amazon.S3; // v3.7.0
using System;

namespace Business.API.Configuration
{
    /// <summary>
    /// Primary configuration class for AWS services used in the Business API.
    /// Manages both S3 and Textract configurations with comprehensive security validation.
    /// </summary>
    public class AwsConfig
    {
        /// <summary>
        /// S3 service configuration settings with security and versioning parameters
        /// </summary>
        public S3Config S3 { get; private set; }

        /// <summary>
        /// Textract OCR service configuration settings with processing parameters
        /// </summary>
        public TextractConfig Textract { get; private set; }

        /// <summary>
        /// Initializes AWS configuration from application settings with comprehensive validation
        /// </summary>
        /// <param name="configuration">Application configuration instance</param>
        /// <exception cref="ArgumentNullException">Thrown when configuration is null</exception>
        /// <exception cref="ArgumentException">Thrown when required configuration values are missing or invalid</exception>
        public AwsConfig(IConfiguration configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            // Initialize S3 configuration
            var s3Section = configuration.GetSection("AWS:S3");
            if (!s3Section.Exists())
                throw new ArgumentException("S3 configuration section is missing");

            S3 = new S3Config
            {
                Region = s3Section["Region"] ?? throw new ArgumentException("S3 Region is required"),
                BucketName = s3Section["BucketName"] ?? throw new ArgumentException("S3 BucketName is required"),
                KmsKeyId = s3Section["KmsKeyId"] ?? throw new ArgumentException("S3 KmsKeyId is required"),
                EncryptionMethod = ParseEncryptionMethod(s3Section["EncryptionMethod"]),
                PresignedUrlExpirationMinutes = ParseInt(s3Section["PresignedUrlExpirationMinutes"], 15),
                VersioningEnabled = ParseBool(s3Section["VersioningEnabled"], true)
            };

            // Initialize Textract configuration
            var textractSection = configuration.GetSection("AWS:Textract");
            if (!textractSection.Exists())
                throw new ArgumentException("Textract configuration section is missing");

            Textract = new TextractConfig
            {
                Region = textractSection["Region"] ?? throw new ArgumentException("Textract Region is required"),
                ConfidenceThreshold = ParseFloat(textractSection["ConfidenceThreshold"], 90.0f),
                JobTimeoutMinutes = ParseInt(textractSection["JobTimeoutMinutes"], 15),
                PollingIntervalSeconds = ParseInt(textractSection["PollingIntervalSeconds"], 5),
                CustomVocabularyEnabled = ParseBool(textractSection["CustomVocabularyEnabled"], false)
            };

            ValidateConfiguration();
        }

        private void ValidateConfiguration()
        {
            // Validate S3 configuration
            if (string.IsNullOrWhiteSpace(S3.Region))
                throw new ArgumentException("S3 Region cannot be empty");
            
            if (string.IsNullOrWhiteSpace(S3.BucketName))
                throw new ArgumentException("S3 BucketName cannot be empty");
            
            if (string.IsNullOrWhiteSpace(S3.KmsKeyId))
                throw new ArgumentException("S3 KmsKeyId cannot be empty");
            
            if (S3.PresignedUrlExpirationMinutes <= 0)
                throw new ArgumentException("PresignedUrlExpirationMinutes must be greater than 0");

            // Validate Textract configuration
            if (string.IsNullOrWhiteSpace(Textract.Region))
                throw new ArgumentException("Textract Region cannot be empty");
            
            if (Textract.ConfidenceThreshold < 0 || Textract.ConfidenceThreshold > 100)
                throw new ArgumentException("ConfidenceThreshold must be between 0 and 100");
            
            if (Textract.JobTimeoutMinutes <= 0)
                throw new ArgumentException("JobTimeoutMinutes must be greater than 0");
            
            if (Textract.PollingIntervalSeconds <= 0)
                throw new ArgumentException("PollingIntervalSeconds must be greater than 0");
        }

        private ServerSideEncryptionMethod ParseEncryptionMethod(string value)
        {
            if (Enum.TryParse<ServerSideEncryptionMethod>(value, true, out var result))
                return result;
            return ServerSideEncryptionMethod.AES256; // Default to AES256
        }

        private int ParseInt(string value, int defaultValue)
        {
            if (int.TryParse(value, out var result))
                return result;
            return defaultValue;
        }

        private float ParseFloat(string value, float defaultValue)
        {
            if (float.TryParse(value, out var result))
                return result;
            return defaultValue;
        }

        private bool ParseBool(string value, bool defaultValue)
        {
            if (bool.TryParse(value, out var result))
                return result;
            return defaultValue;
        }
    }

    /// <summary>
    /// Configuration settings for AWS S3 service with enhanced security and versioning support
    /// </summary>
    public class S3Config
    {
        /// <summary>
        /// AWS region for S3 service
        /// </summary>
        public string Region { get; set; }

        /// <summary>
        /// Name of the S3 bucket for document storage
        /// </summary>
        public string BucketName { get; set; }

        /// <summary>
        /// KMS key ID for server-side encryption
        /// </summary>
        public string KmsKeyId { get; set; }

        /// <summary>
        /// Server-side encryption method for document storage
        /// </summary>
        public ServerSideEncryptionMethod EncryptionMethod { get; set; }

        /// <summary>
        /// Expiration time in minutes for presigned URLs
        /// </summary>
        public int PresignedUrlExpirationMinutes { get; set; }

        /// <summary>
        /// Flag indicating if versioning is enabled for the bucket
        /// </summary>
        public bool VersioningEnabled { get; set; }
    }

    /// <summary>
    /// Configuration settings for AWS Textract service with OCR processing parameters
    /// </summary>
    public class TextractConfig
    {
        /// <summary>
        /// AWS region for Textract service
        /// </summary>
        public string Region { get; set; }

        /// <summary>
        /// Minimum confidence threshold for OCR results (0-100)
        /// </summary>
        public float ConfidenceThreshold { get; set; }

        /// <summary>
        /// Timeout in minutes for Textract jobs
        /// </summary>
        public int JobTimeoutMinutes { get; set; }

        /// <summary>
        /// Interval in seconds for polling Textract job status
        /// </summary>
        public int PollingIntervalSeconds { get; set; }

        /// <summary>
        /// Flag indicating if custom vocabulary is enabled for OCR
        /// </summary>
        public bool CustomVocabularyEnabled { get; set; }
    }
}