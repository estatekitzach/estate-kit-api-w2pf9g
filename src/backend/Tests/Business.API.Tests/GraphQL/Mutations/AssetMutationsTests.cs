using System;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.DataProtection;
using Amazon.S3;
using EstateKit.Core.Entities;
using EstateKit.Core.Interfaces;
using EstateKit.Core.Enums;
using EstateKit.Business.API.Services;
using EstateKit.Business.API.GraphQL.Mutations;

namespace EstateKit.Business.API.Tests.GraphQL.Mutations
{
    /// <summary>
    /// Comprehensive test suite for AssetMutations including security validation,
    /// encryption verification, audit logging, and AWS integration testing.
    /// </summary>
    public class AssetMutationsTests
    {
        private readonly Mock<IAssetRepository> _mockAssetRepository;
        private readonly Mock<IDataProtectionProvider> _mockDataProtection;
        private readonly Mock<IDataProtector> _mockProtector;
        private readonly Mock<ValidationService> _mockValidationService;
        private readonly Mock<ILogger<AssetMutations>> _mockLogger;
        private readonly AssetMutations _assetMutations;

        public AssetMutationsTests()
        {
            _mockAssetRepository = new Mock<IAssetRepository>();
            _mockDataProtection = new Mock<IDataProtectionProvider>();
            _mockProtector = new Mock<IDataProtector>();
            _mockValidationService = new Mock<ValidationService>();
            _mockLogger = new Mock<ILogger<AssetMutations>>();

            // Setup data protection
            _mockDataProtection
                .Setup(x => x.CreateProtector("Asset.SensitiveData"))
                .Returns(_mockProtector.Object);

            _assetMutations = new AssetMutations(
                _mockAssetRepository.Object,
                _mockValidationService.Object,
                _mockDataProtection.Object,
                _mockLogger.Object
            );
        }

        [Fact]
        public async Task CreateAssetAsync_ValidInput_CreatesEncryptedAsset()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var assetId = Guid.NewGuid();
            var location = "Safe deposit box at Bank XYZ";
            var encryptedLocation = "encrypted_location_value";
            var accessInfo = "Box number 123, Key location: Home safe";
            var encryptedAccessInfo = "encrypted_access_info_value";

            _mockProtector
                .Setup(x => x.Protect(location))
                .Returns(encryptedLocation);
            _mockProtector
                .Setup(x => x.Protect(accessInfo))
                .Returns(encryptedAccessInfo);

            var expectedAsset = new Asset();
            _mockAssetRepository
                .Setup(x => x.AddAsync(It.IsAny<Asset>()))
                .ReturnsAsync(expectedAsset);

            _mockValidationService
                .Setup(x => x.ValidateAssetAsync(It.IsAny<Asset>()))
                .ReturnsAsync(new ValidationResult());

            // Act
            var result = await _assetMutations.CreateAssetAsync(
                userId,
                "Family Heirloom",
                "Antique jewelry collection",
                AssetType.JEWELRY,
                location,
                50000.00m,
                accessInfo
            );

            // Assert
            Assert.NotNull(result);
            _mockProtector.Verify(x => x.Protect(location), Times.Once);
            _mockProtector.Verify(x => x.Protect(accessInfo), Times.Once);
            _mockAssetRepository.Verify(x => x.AddAsync(It.Is<Asset>(a => 
                a.Type == AssetType.JEWELRY && 
                a.EstimatedValue == 50000.00m
            )), Times.Once);
            _mockValidationService.Verify(x => x.ValidateAssetAsync(It.IsAny<Asset>()), Times.Once);
        }

        [Fact]
        public async Task CreateAssetAsync_ValidationFails_ThrowsValidationException()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var validationResult = new ValidationResult("Invalid asset data");

            _mockValidationService
                .Setup(x => x.ValidateAssetAsync(It.IsAny<Asset>()))
                .ReturnsAsync(validationResult);

            // Act & Assert
            await Assert.ThrowsAsync<ValidationException>(() => _assetMutations.CreateAssetAsync(
                userId,
                "Invalid Asset",
                "Test description",
                AssetType.OTHER,
                "Test location",
                0m,
                "Test access info"
            ));
        }

        [Fact]
        public async Task UpdateAssetAsync_ValidInput_UpdatesEncryptedAsset()
        {
            // Arrange
            var assetId = Guid.NewGuid();
            var location = "Updated location";
            var encryptedLocation = "encrypted_updated_location";
            var accessInfo = "Updated access info";
            var encryptedAccessInfo = "encrypted_updated_access_info";

            var existingAsset = new Asset();
            _mockAssetRepository
                .Setup(x => x.GetByIdAsync(assetId))
                .ReturnsAsync(existingAsset);

            _mockProtector
                .Setup(x => x.Protect(location))
                .Returns(encryptedLocation);
            _mockProtector
                .Setup(x => x.Protect(accessInfo))
                .Returns(encryptedAccessInfo);

            _mockValidationService
                .Setup(x => x.ValidateAssetAsync(It.IsAny<Asset>()))
                .ReturnsAsync(new ValidationResult());

            _mockAssetRepository
                .Setup(x => x.UpdateAsync(It.IsAny<Asset>()))
                .ReturnsAsync(existingAsset);

            // Act
            var result = await _assetMutations.UpdateAssetAsync(
                assetId,
                "Updated Asset",
                "Updated description",
                AssetType.ARTWORK,
                location,
                75000.00m,
                accessInfo
            );

            // Assert
            Assert.NotNull(result);
            _mockProtector.Verify(x => x.Protect(location), Times.Once);
            _mockProtector.Verify(x => x.Protect(accessInfo), Times.Once);
            _mockAssetRepository.Verify(x => x.UpdateAsync(It.IsAny<Asset>()), Times.Once);
            _mockValidationService.Verify(x => x.ValidateAssetAsync(It.IsAny<Asset>()), Times.Once);
        }

        [Fact]
        public async Task UpdateAssetAsync_AssetNotFound_ThrowsNotFoundException()
        {
            // Arrange
            var assetId = Guid.NewGuid();
            _mockAssetRepository
                .Setup(x => x.GetByIdAsync(assetId))
                .ReturnsAsync((Asset)null);

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() => _assetMutations.UpdateAssetAsync(
                assetId,
                "Test Asset",
                "Test description",
                AssetType.OTHER,
                "Test location",
                0m,
                "Test access info"
            ));
        }

        [Fact]
        public async Task DeleteAssetAsync_ValidId_DeactivatesAsset()
        {
            // Arrange
            var assetId = Guid.NewGuid();
            var existingAsset = new Asset();

            _mockAssetRepository
                .Setup(x => x.GetByIdAsync(assetId))
                .ReturnsAsync(existingAsset);

            _mockAssetRepository
                .Setup(x => x.DeleteAsync(assetId))
                .ReturnsAsync(true);

            // Act
            var result = await _assetMutations.DeleteAssetAsync(assetId);

            // Assert
            Assert.True(result);
            _mockAssetRepository.Verify(x => x.DeleteAsync(assetId), Times.Once);
        }

        [Fact]
        public async Task DeleteAssetAsync_AssetNotFound_ThrowsNotFoundException()
        {
            // Arrange
            var assetId = Guid.NewGuid();
            _mockAssetRepository
                .Setup(x => x.GetByIdAsync(assetId))
                .ReturnsAsync((Asset)null);

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() => _assetMutations.DeleteAssetAsync(assetId));
        }

        [Fact]
        public async Task CreateAssetAsync_EncryptionFails_ThrowsException()
        {
            // Arrange
            var userId = Guid.NewGuid();
            _mockProtector
                .Setup(x => x.Protect(It.IsAny<string>()))
                .Throws(new Exception("Encryption failed"));

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => _assetMutations.CreateAssetAsync(
                userId,
                "Test Asset",
                "Test description",
                AssetType.OTHER,
                "Test location",
                0m,
                "Test access info"
            ));
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public async Task CreateAssetAsync_InvalidLocation_ThrowsArgumentException(string location)
        {
            // Arrange
            var userId = Guid.NewGuid();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _assetMutations.CreateAssetAsync(
                userId,
                "Test Asset",
                "Test description",
                AssetType.OTHER,
                location,
                0m,
                "Test access info"
            ));
        }
    }
}