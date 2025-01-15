using Amazon.S3;
using Amazon.S3.Model;
using EstateKit.Core.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace EstateKit.Business.API.Services.AWS
{
    /// <summary>
    /// Service that handles AWS S3 operations for secure document storage with enhanced security features and logging
    /// </summary>
    public sealed class S3Service
    {
        private readonly IAmazonS3 _s3Client;
        private readonly ILogger<S3Service> _logger;
        private readonly string _bucketName;
        private readonly string _kmsKeyId;
        private readonly int _keyRotationDays = 180; // As per security requirements
        private readonly string _region;
        private readonly bool _versioningEnabled = true;

        public S3Service(IConfiguration configuration, IAmazonS3 s3Client, ILogger<S3Service> logger)
        {
            _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _bucketName = configuration["AWS:S3:BucketName"] 
                ?? throw new ArgumentException("S3 bucket name not configured");
            _kmsKeyId = configuration["AWS:KMS:KeyId"] 
                ?? throw new ArgumentException("KMS key ID not configured");
            _region = configuration["AWS:Region"] 
                ?? throw new ArgumentException("AWS region not configured");

            ValidateEncryptionConfigAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Uploads a document to S3 with server-side encryption and enhanced security logging
        /// </summary>
        public async Task<string> UploadDocumentAsync(Stream fileStream, Document document, string contentType)
        {
            if (fileStream == null) throw new ArgumentNullException(nameof(fileStream));
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (string.IsNullOrEmpty(contentType)) throw new ArgumentNullException(nameof(contentType));

            var objectKey = $"{document.S3KeyPrefix}/{Guid.NewGuid()}";

            try
            {
                var putRequest = new PutObjectRequest
                {
                    BucketName = _bucketName,
                    Key = objectKey,
                    InputStream = fileStream,
                    ContentType = contentType,
                    ServerSideEncryptionMethod = ServerSideEncryptionMethod.AWSKMS,
                    ServerSideEncryptionKeyManagementServiceKeyId = _kmsKeyId,
                    Metadata =
                    {
                        ["DocumentId"] = document.Id.ToString(),
                        ["DocumentType"] = document.Type.ToString(),
                        ["Timestamp"] = DateTime.UtcNow.ToString("O"),
                        ["SecurityClassification"] = "High"
                    }
                };

                _logger.LogInformation(
                    "Initiating secure upload for document {DocumentId} of type {DocumentType}",
                    document.Id, document.Type);

                await _s3Client.PutObjectAsync(putRequest);

                _logger.LogInformation(
                    "Successfully uploaded document {DocumentId} to S3 with encryption",
                    document.Id);

                return objectKey;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to upload document {DocumentId} to S3",
                    document.Id);
                throw;
            }
        }

        /// <summary>
        /// Generates a presigned URL for secure document access with validation
        /// </summary>
        public async Task<string> GetPresignedUrlAsync(string objectKey, int expirationMinutes = 15)
        {
            if (string.IsNullOrEmpty(objectKey))
                throw new ArgumentNullException(nameof(objectKey));

            if (expirationMinutes <= 0 || expirationMinutes > 60)
                throw new ArgumentException("Expiration must be between 1 and 60 minutes", nameof(expirationMinutes));

            try
            {
                // Verify object exists and is encrypted
                var metadata = await _s3Client.GetObjectMetadataAsync(_bucketName, objectKey);
                if (metadata.ServerSideEncryptionMethod != ServerSideEncryptionMethod.AWSKMS)
                {
                    throw new InvalidOperationException("Object is not properly encrypted");
                }

                var request = new GetPreSignedUrlRequest
                {
                    BucketName = _bucketName,
                    Key = objectKey,
                    Expires = DateTime.UtcNow.AddMinutes(expirationMinutes),
                    Protocol = Protocol.HTTPS
                };

                _logger.LogInformation(
                    "Generating presigned URL for object {ObjectKey} with {ExpirationMinutes} minute expiration",
                    objectKey, expirationMinutes);

                return _s3Client.GetPreSignedURL(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to generate presigned URL for object {ObjectKey}",
                    objectKey);
                throw;
            }
        }

        /// <summary>
        /// Securely deletes a document from S3 storage with validation
        /// </summary>
        public async Task<bool> DeleteDocumentAsync(string objectKey)
        {
            if (string.IsNullOrEmpty(objectKey))
                throw new ArgumentNullException(nameof(objectKey));

            try
            {
                // Verify object exists before deletion
                await _s3Client.GetObjectMetadataAsync(_bucketName, objectKey);

                var deleteRequest = new DeleteObjectRequest
                {
                    BucketName = _bucketName,
                    Key = objectKey
                };

                _logger.LogInformation(
                    "Initiating secure deletion for object {ObjectKey}",
                    objectKey);

                await _s3Client.DeleteObjectAsync(deleteRequest);

                _logger.LogInformation(
                    "Successfully deleted object {ObjectKey}",
                    objectKey);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to delete object {ObjectKey}",
                    objectKey);
                throw;
            }
        }

        /// <summary>
        /// Validates S3 bucket encryption configuration and KMS key status
        /// </summary>
        private async Task<bool> ValidateEncryptionConfigAsync()
        {
            try
            {
                var encryptionConfig = await _s3Client.GetBucketEncryptionAsync(_bucketName);
                
                if (encryptionConfig.ServerSideEncryptionConfiguration.Rules.Count == 0)
                {
                    throw new InvalidOperationException("Bucket encryption not properly configured");
                }

                var versioningConfig = await _s3Client.GetBucketVersioningAsync(_bucketName);
                if (!_versioningEnabled || versioningConfig.Status != VersionStatus.Enabled)
                {
                    throw new InvalidOperationException("Bucket versioning not enabled");
                }

                _logger.LogInformation(
                    "Successfully validated encryption configuration for bucket {BucketName}",
                    _bucketName);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to validate encryption configuration for bucket {BucketName}",
                    _bucketName);
                throw;
            }
        }
    }
}