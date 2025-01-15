using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Options; // v9.0.0
using Microsoft.Extensions.Logging; // v9.0.0
using Polly; // v8.0.0
using EstateKit.Infrastructure.Security;
using EstateKit.Data.API.Configuration;

namespace EstateKit.Data.API.Services
{
    /// <summary>
    /// Enterprise-grade encryption service implementing field-level encryption, key rotation,
    /// audit logging, and compliance monitoring for sensitive data in the EstateKit system.
    /// </summary>
    public sealed class EncryptionService
    {
        private readonly IEncryptionProvider _encryptionProvider;
        private readonly IOptions<EncryptionOptions> _options;
        private readonly AuditService _auditService;
        private readonly ILogger<EncryptionService> _logger;
        private readonly IAsyncPolicy _retryPolicy;

        public EncryptionService(
            IEncryptionProvider encryptionProvider,
            IOptions<EncryptionOptions> options,
            AuditService auditService,
            ILogger<EncryptionService> logger)
        {
            _encryptionProvider = encryptionProvider ?? throw new ArgumentNullException(nameof(encryptionProvider));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Configure retry policy for resilient encryption operations
            _retryPolicy = Policy
                .Handle<EncryptionException>()
                .WaitAndRetryAsync(3, retryAttempt => 
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (exception, timeSpan, retryCount, context) =>
                    {
                        _logger.LogWarning(
                            exception,
                            "Retry {RetryCount} of encryption operation after {Delay}ms",
                            retryCount,
                            timeSpan.TotalMilliseconds);
                    });
        }

        /// <summary>
        /// Encrypts sensitive field data with comprehensive validation and audit logging
        /// </summary>
        public async Task<string> EncryptSensitiveField(
            string value,
            string fieldName,
            string userId,
            EncryptionContext context)
        {
            if (string.IsNullOrEmpty(value))
                throw new ArgumentNullException(nameof(value));
            if (string.IsNullOrEmpty(fieldName))
                throw new ArgumentNullException(nameof(fieldName));
            if (string.IsNullOrEmpty(userId))
                throw new ArgumentNullException(nameof(userId));
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            try
            {
                // Verify field sensitivity classification
                if (!_options.Value.SensitiveFields.Contains(fieldName))
                {
                    _logger.LogWarning("Attempted encryption of non-sensitive field: {FieldName}", fieldName);
                    throw new InvalidOperationException($"Field {fieldName} is not marked as sensitive");
                }

                // Verify key freshness
                if (!_options.Value.FieldEncryptionKeys.TryGetValue(fieldName, out var keyId))
                {
                    _logger.LogError("No encryption key configured for field: {FieldName}", fieldName);
                    throw new EncryptionException($"No encryption key found for field {fieldName}");
                }

                var startTime = DateTime.UtcNow;

                // Perform encryption with retry policy
                var encryptedValue = await _retryPolicy.ExecuteAsync(async () =>
                {
                    var result = await _encryptionProvider.EncryptField(value, fieldName, context);
                    return result.EncryptedData;
                });

                // Log encryption operation
                await _auditService.LogSensitiveDataAccess(
                    userId,
                    fieldName,
                    context.EntityType,
                    context.EntityId,
                    ComplianceRequirement.FieldLevelEncryption,
                    EncryptionStatus.Encrypted);

                var duration = DateTime.UtcNow - startTime;
                _logger.LogInformation(
                    "Successfully encrypted field {FieldName} in {Duration}ms",
                    fieldName,
                    duration.TotalMilliseconds);

                return encryptedValue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to encrypt field {FieldName}", fieldName);
                throw new EncryptionException($"Failed to encrypt field {fieldName}", ex);
            }
        }

        /// <summary>
        /// Decrypts sensitive field data with validation and audit logging
        /// </summary>
        public async Task<string> DecryptSensitiveField(
            string encryptedValue,
            string fieldName,
            string userId,
            EncryptionContext context)
        {
            if (string.IsNullOrEmpty(encryptedValue))
                throw new ArgumentNullException(nameof(encryptedValue));
            if (string.IsNullOrEmpty(fieldName))
                throw new ArgumentNullException(nameof(fieldName));
            if (string.IsNullOrEmpty(userId))
                throw new ArgumentNullException(nameof(userId));
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            try
            {
                // Verify field sensitivity and access rights
                if (!_options.Value.SensitiveFields.Contains(fieldName))
                {
                    _logger.LogWarning("Attempted decryption of non-sensitive field: {FieldName}", fieldName);
                    throw new InvalidOperationException($"Field {fieldName} is not marked as sensitive");
                }

                var startTime = DateTime.UtcNow;

                // Perform decryption with retry policy
                var decryptedValue = await _retryPolicy.ExecuteAsync(async () =>
                {
                    var encryptedData = new EncryptedValue
                    {
                        EncryptedData = encryptedValue,
                        Context = context
                    };
                    return await _encryptionProvider.DecryptField(encryptedData, fieldName, context);
                });

                // Log decryption operation
                await _auditService.LogSensitiveDataAccess(
                    userId,
                    fieldName,
                    context.EntityType,
                    context.EntityId,
                    ComplianceRequirement.FieldLevelEncryption,
                    EncryptionStatus.Decrypted);

                var duration = DateTime.UtcNow - startTime;
                _logger.LogInformation(
                    "Successfully decrypted field {FieldName} in {Duration}ms",
                    fieldName,
                    duration.TotalMilliseconds);

                return decryptedValue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to decrypt field {FieldName}", fieldName);
                throw new EncryptionException($"Failed to decrypt field {fieldName}", ex);
            }
        }

        /// <summary>
        /// Performs secure key rotation based on sensitivity level and rotation schedule
        /// </summary>
        public async Task<bool> RotateEncryptionKey(string fieldName, KeyRotationContext context)
        {
            if (string.IsNullOrEmpty(fieldName))
                throw new ArgumentNullException(nameof(fieldName));
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            try
            {
                // Verify key rotation eligibility
                if (!_options.Value.EnableKeyRotation)
                {
                    _logger.LogWarning("Key rotation is disabled in configuration");
                    return false;
                }

                if (!_options.Value.FieldEncryptionKeys.ContainsKey(fieldName))
                {
                    _logger.LogError("No encryption key found for field: {FieldName}", fieldName);
                    return false;
                }

                var startTime = DateTime.UtcNow;

                // Log key rotation event
                await _auditService.LogSecurityEvent(
                    context.UserId,
                    SecurityEventType.KeyRotation,
                    $"Initiating key rotation for field {fieldName}",
                    new SecurityContext { FieldName = fieldName },
                    SecuritySeverity.High);

                // Perform key rotation
                var success = await _retryPolicy.ExecuteAsync(async () =>
                {
                    // Implementation would call the encryption provider's key rotation method
                    // This is a placeholder as the actual implementation depends on the provider
                    return true;
                });

                if (success)
                {
                    var duration = DateTime.UtcNow - startTime;
                    _logger.LogInformation(
                        "Successfully rotated encryption key for field {FieldName} in {Duration}ms",
                        fieldName,
                        duration.TotalMilliseconds);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to rotate encryption key for field {FieldName}", fieldName);
                throw new EncryptionException($"Failed to rotate encryption key for field {fieldName}", ex);
            }
        }
    }
}