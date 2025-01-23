using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using System.Text;
using System.Security.Cryptography.X509Certificates;
using System.Collections.Generic;

namespace EstateKit.Infrastructure.Security
{
    /// <summary>
    /// Interface defining the contract for field-level encryption operations
    /// </summary>
    public interface IEncryptionProvider
    {
        Task<EncryptedValue> EncryptField(string value, string fieldName, EncryptionContext context);
        Task<string> DecryptField(EncryptedValue encryptedValue, string fieldName, EncryptionContext context);
    }

    /// <summary>
    /// Provides field-level encryption using AWS KMS and AES-256-GCM for sensitive data protection
    /// </summary>
    public sealed class EncryptionProvider : IEncryptionProvider
    {
        private readonly IAmazonKeyManagementService _kmsClient;
        private readonly ILogger<EncryptionProvider> _logger;
        private readonly string _keyId;
        private readonly IMemoryCache _keyCache;
        private readonly EncryptionMetrics _metrics;
        private const int GCM_TAG_SIZE = 16;
        private const int IV_SIZE = 12;
        private const string KEY_CACHE_PREFIX = "DataKey_";
        private static readonly TimeSpan KEY_CACHE_DURATION = TimeSpan.FromHours(4);

        public EncryptionProvider(
            IAmazonKeyManagementService kmsClient,
            ILogger<EncryptionProvider> logger,
            IMemoryCache keyCache,
            EncryptionMetrics metrics,
            EncryptionConfiguration config)
        {
            _kmsClient = kmsClient ?? throw new ArgumentNullException(nameof(kmsClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _keyCache = keyCache ?? throw new ArgumentNullException(nameof(keyCache));
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            _keyId = config?.KeyId ?? throw new ArgumentNullException(nameof(config.KeyId));

            ValidateKmsKeyAccessibility().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Encrypts a field value using AES-256-GCM with AWS KMS
        /// </summary>
        public async Task<EncryptedValue> EncryptField(string value, string fieldName, EncryptionContext context)
        {
            try
            {
                if (string.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(value));
                if (string.IsNullOrEmpty(fieldName)) throw new ArgumentNullException(nameof(fieldName));
                if (context == null) throw new ArgumentNullException(nameof(context));

                var startTime = DateTime.UtcNow;
                _logger.LogDebug("Starting encryption for field {FieldName}", fieldName);

                // Generate IV
                var iv = new byte[IV_SIZE];
                using (var rng = new RNGCryptoServiceProvider())
                {
                    rng.GetBytes(iv);
                }

                // Get data key
                var dataKey = await GenerateDataKey(context);
                
                // Perform encryption
                byte[] encryptedData;
                byte[] tag = new byte[GCM_TAG_SIZE];
                
                using (var aes = new AesGcm(dataKey.PlaintextKey))
                {
                    var plaintext = Encoding.UTF8.GetBytes(value);
                    encryptedData = new byte[plaintext.Length];
                    
                    aes.Encrypt(
                        iv,
                        plaintext,
                        encryptedData,
                        tag,
                        Encoding.UTF8.GetBytes(context.ToString())
                    );
                }

                var encryptedValue = new EncryptedValue
                {
                    EncryptedData = Convert.ToBase64String(encryptedData),
                    Iv = Convert.ToBase64String(iv),
                    AuthTag = Convert.ToBase64String(tag),
                    KeyId = _keyId,
                    EncryptedKeyBlob = Convert.ToBase64String(dataKey.EncryptedKey),
                    Algorithm = "AES-256-GCM",
                    Context = context
                };

                var duration = DateTime.UtcNow - startTime;
                _metrics.RecordEncryption(fieldName, duration);
                
                _logger.LogInformation(
                    "Successfully encrypted field {FieldName} in {Duration}ms",
                    fieldName,
                    duration.TotalMilliseconds);

                return encryptedValue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error encrypting field {FieldName}", fieldName);
                _metrics.RecordEncryptionError(fieldName);
                throw new EncryptionException("Field encryption failed", ex);
            }
        }

        /// <summary>
        /// Decrypts a field value using AES-256-GCM with AWS KMS
        /// </summary>
        public async Task<string> DecryptField(EncryptedValue encryptedValue, string fieldName, EncryptionContext context)
        {
            try
            {
                if (encryptedValue == null) throw new ArgumentNullException(nameof(encryptedValue));
                if (string.IsNullOrEmpty(fieldName)) throw new ArgumentNullException(nameof(fieldName));
                if (context == null) throw new ArgumentNullException(nameof(context));

                var startTime = DateTime.UtcNow;
                _logger.LogDebug("Starting decryption for field {FieldName}", fieldName);

                // Decode components
                var encryptedData = Convert.FromBase64String(encryptedValue.EncryptedData);
                var iv = Convert.FromBase64String(encryptedValue.Iv);
                var tag = Convert.FromBase64String(encryptedValue.AuthTag);
                var encryptedKey = Convert.FromBase64String(encryptedValue.EncryptedKeyBlob);

                // Decrypt the data key
                var decryptRequest = new DecryptRequest
                {
                    CiphertextBlob = new MemoryStream(encryptedKey),
                    EncryptionContext = context.ToDictionary(),
                    KeyId = _keyId
                };

                var decryptResponse = await _kmsClient.DecryptAsync(decryptRequest);
                var plaintextKey = decryptResponse.Plaintext.ToArray();

                // Perform decryption
                var plaintext = new byte[encryptedData.Length];
                
                using (var aes = new AesGcm(plaintextKey))
                {
                    aes.Decrypt(
                        iv,
                        encryptedData,
                        tag,
                        plaintext,
                        Encoding.UTF8.GetBytes(context.ToString())
                    );
                }

                var decryptedValue = Encoding.UTF8.GetString(plaintext);

                var duration = DateTime.UtcNow - startTime;
                _metrics.RecordDecryption(fieldName, duration);

                _logger.LogInformation(
                    "Successfully decrypted field {FieldName} in {Duration}ms",
                    fieldName,
                    duration.TotalMilliseconds);

                return decryptedValue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error decrypting field {FieldName}", fieldName);
                _metrics.RecordDecryptionError(fieldName);
                throw new EncryptionException("Field decryption failed", ex);
            }
        }

        /// <summary>
        /// Generates a new data key using AWS KMS with caching
        /// </summary>
        private async Task<DataKey> GenerateDataKey(EncryptionContext context)
        {
            var cacheKey = $"{KEY_CACHE_PREFIX}{context.GetHashCode()}";

            if (_keyCache.TryGetValue(cacheKey, out DataKey cachedKey))
            {
                return cachedKey;
            }

            try
            {
                var request = new GenerateDataKeyRequest
                {
                    KeyId = _keyId,
                    KeySpec = DataKeySpec.AES_256,
                    EncryptionContext = context.ToDictionary()
                };

                var response = await _kmsClient.GenerateDataKeyAsync(request);

                var dataKey = new DataKey
                {
                    PlaintextKey = response.Plaintext.ToArray(),
                    EncryptedKey = response.CiphertextBlob.ToArray(),
                    KeyId = _keyId
                };

                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetSlidingExpiration(KEY_CACHE_DURATION)
                    .RegisterPostEvictionCallback((key, value, reason, state) =>
                    {
                        _logger.LogDebug("Data key evicted from cache. Reason: {Reason}", reason);
                    });

                _keyCache.Set(cacheKey, dataKey, cacheOptions);

                _logger.LogDebug("Generated new data key with ID {KeyId}", _keyId);

                return dataKey;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating data key");
                throw new EncryptionException("Failed to generate data key", ex);
            }
        }

        private async Task ValidateKmsKeyAccessibility()
        {
            try
            {
                var request = new DescribeKeyRequest { KeyId = _keyId };
                await _kmsClient.DescribeKeyAsync(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate KMS key accessibility");
                throw new EncryptionException("KMS key validation failed", ex);
            }
        }
    }

    public class EncryptedValue
    {
        public string EncryptedData { get; set; }
        public string Iv { get; set; }
        public string AuthTag { get; set; }
        public string KeyId { get; set; }
        public string EncryptedKeyBlob { get; set; }
        public string Algorithm { get; set; }
        public EncryptionContext Context { get; set; }
    }

    public class DataKey
    {
        public byte[] PlaintextKey { get; set; }
        public byte[] EncryptedKey { get; set; }
        public string KeyId { get; set; }
    }

    public class EncryptionException : Exception
    {
        public EncryptionException(string message) : base(message) { }
        public EncryptionException(string message, Exception innerException) : base(message, innerException) { }
    }
}
