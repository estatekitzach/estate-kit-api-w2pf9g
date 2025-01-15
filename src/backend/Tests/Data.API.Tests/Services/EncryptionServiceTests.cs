using System;
using System.Threading.Tasks;
using Xunit; // v2.5.0
using Moq; // v4.18.0
using Microsoft.Extensions.Options; // v9.0.0
using Microsoft.Extensions.Logging; // v9.0.0
using EstateKit.Data.API.Services;
using EstateKit.Infrastructure.Security;
using EstateKit.Data.API.Configuration;

namespace EstateKit.Data.API.Tests.Services
{
    [TestCategory("Security")]
    public class EncryptionServiceTests
    {
        private readonly Mock<IEncryptionProvider> _mockEncryptionProvider;
        private readonly Mock<IOptions<EncryptionOptions>> _mockOptions;
        private readonly Mock<AuditService> _mockAuditService;
        private readonly Mock<ILogger<EncryptionService>> _mockLogger;
        private readonly EncryptionService _encryptionService;
        private readonly TestData _testData;

        public EncryptionServiceTests()
        {
            // Initialize mocks with strict behavior
            _mockEncryptionProvider = new Mock<IEncryptionProvider>(MockBehavior.Strict);
            _mockOptions = new Mock<IOptions<EncryptionOptions>>(MockBehavior.Strict);
            _mockAuditService = new Mock<AuditService>(MockBehavior.Strict);
            _mockLogger = new Mock<ILogger<EncryptionService>>();

            // Configure encryption options
            var options = new EncryptionOptions
            {
                KmsKeyId = "arn:aws:kms:us-east-1:123456789012:key/abcd1234-a123-456a-a12b-a123b4cd5678",
                KmsKeyRegion = "us-east-1",
                EnableKeyRotation = true,
                KeyRotationIntervalDays = 90,
                EncryptionAlgorithm = "AES_256_GCM"
            };
            options.SensitiveFields.Add("dateOfBirth");
            options.SensitiveFields.Add("governmentId");
            options.FieldEncryptionKeys.Add("dateOfBirth", "key1");
            options.FieldEncryptionKeys.Add("governmentId", "key2");

            _mockOptions.Setup(x => x.Value).Returns(options);

            // Initialize test data
            _testData = new TestData();

            // Create service instance
            _encryptionService = new EncryptionService(
                _mockEncryptionProvider.Object,
                _mockOptions.Object,
                _mockAuditService.Object,
                _mockLogger.Object
            );
        }

        [Fact]
        public async Task EncryptSensitiveField_ValidInput_SuccessfullyEncrypts()
        {
            // Arrange
            var value = "1990-01-01";
            var fieldName = "dateOfBirth";
            var userId = Guid.NewGuid().ToString();
            var context = new EncryptionContext
            {
                EntityType = "User",
                EntityId = Guid.NewGuid().ToString()
            };

            var encryptedValue = new EncryptedValue
            {
                EncryptedData = "encrypted123",
                Iv = "iv123",
                AuthTag = "tag123",
                KeyId = "key1",
                Algorithm = "AES-256-GCM",
                Context = context
            };

            _mockEncryptionProvider
                .Setup(x => x.EncryptField(value, fieldName, context))
                .ReturnsAsync(encryptedValue);

            _mockAuditService
                .Setup(x => x.LogSensitiveDataAccess(
                    userId,
                    fieldName,
                    context.EntityType,
                    context.EntityId,
                    ComplianceRequirement.FieldLevelEncryption,
                    EncryptionStatus.Encrypted))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _encryptionService.EncryptSensitiveField(value, fieldName, userId, context);

            // Assert
            Assert.Equal(encryptedValue.EncryptedData, result);
            _mockEncryptionProvider.Verify(
                x => x.EncryptField(value, fieldName, context),
                Times.Once);
            _mockAuditService.Verify(
                x => x.LogSensitiveDataAccess(
                    userId,
                    fieldName,
                    context.EntityType,
                    context.EntityId,
                    ComplianceRequirement.FieldLevelEncryption,
                    EncryptionStatus.Encrypted),
                Times.Once);
        }

        [Fact]
        public async Task DecryptSensitiveField_ValidInput_SuccessfullyDecrypts()
        {
            // Arrange
            var encryptedValue = "encrypted123";
            var fieldName = "governmentId";
            var userId = Guid.NewGuid().ToString();
            var context = new EncryptionContext
            {
                EntityType = "User",
                EntityId = Guid.NewGuid().ToString()
            };

            var decryptedValue = "123-45-6789";

            _mockEncryptionProvider
                .Setup(x => x.DecryptField(
                    It.Is<EncryptedValue>(v => v.EncryptedData == encryptedValue),
                    fieldName,
                    context))
                .ReturnsAsync(decryptedValue);

            _mockAuditService
                .Setup(x => x.LogSensitiveDataAccess(
                    userId,
                    fieldName,
                    context.EntityType,
                    context.EntityId,
                    ComplianceRequirement.FieldLevelEncryption,
                    EncryptionStatus.Decrypted))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _encryptionService.DecryptSensitiveField(encryptedValue, fieldName, userId, context);

            // Assert
            Assert.Equal(decryptedValue, result);
            _mockEncryptionProvider.Verify(
                x => x.DecryptField(
                    It.Is<EncryptedValue>(v => v.EncryptedData == encryptedValue),
                    fieldName,
                    context),
                Times.Once);
            _mockAuditService.Verify(
                x => x.LogSensitiveDataAccess(
                    userId,
                    fieldName,
                    context.EntityType,
                    context.EntityId,
                    ComplianceRequirement.FieldLevelEncryption,
                    EncryptionStatus.Decrypted),
                Times.Once);
        }

        [Fact]
        public void IsSensitiveField_ValidField_ReturnsExpectedResult()
        {
            // Arrange
            var sensitiveField = "dateOfBirth";
            var nonSensitiveField = "name";

            // Act & Assert
            Assert.True(_encryptionService.IsSensitiveField(sensitiveField));
            Assert.False(_encryptionService.IsSensitiveField(nonSensitiveField));
        }

        [Fact]
        public async Task RotateEncryptionKey_ValidKey_SuccessfullyRotates()
        {
            // Arrange
            var fieldName = "dateOfBirth";
            var context = new KeyRotationContext
            {
                UserId = Guid.NewGuid().ToString(),
                Reason = "Scheduled rotation"
            };

            _mockAuditService
                .Setup(x => x.LogSecurityEvent(
                    context.UserId,
                    SecurityEventType.KeyRotation,
                    It.IsAny<string>(),
                    It.Is<SecurityContext>(c => c.FieldName == fieldName),
                    SecuritySeverity.High))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _encryptionService.RotateEncryptionKey(fieldName, context);

            // Assert
            Assert.True(result);
            _mockAuditService.Verify(
                x => x.LogSecurityEvent(
                    context.UserId,
                    SecurityEventType.KeyRotation,
                    It.IsAny<string>(),
                    It.Is<SecurityContext>(c => c.FieldName == fieldName),
                    SecuritySeverity.High),
                Times.Once);
        }

        private class TestData
        {
            public readonly string CriticalValue = "123-45-6789";
            public readonly string SensitiveValue = "1990-01-01";
            public readonly string InternalValue = "John Doe";
            public readonly string EncryptedCritical = "encrypted_critical_123";
            public readonly string EncryptedSensitive = "encrypted_sensitive_456";
            public readonly string EncryptedInternal = "encrypted_internal_789";
        }
    }
}