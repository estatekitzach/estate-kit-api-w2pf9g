using System;
using System.Threading.Tasks;
using HotChocolate; // Version 13.0.0
using HotChocolate.Types; // Version 13.0.0
using Microsoft.Extensions.Logging; // Version 9.0.0
using Microsoft.AspNetCore.DataProtection; // Version 9.0.0
using EstateKit.Core.Entities;
using EstateKit.Core.Interfaces;
using EstateKit.Core.Enums;
using EstateKit.Business.API.Services;

namespace EstateKit.Business.API.GraphQL.Mutations
{
    /// <summary>
    /// Implements GraphQL mutations for secure asset management with field-level encryption
    /// and comprehensive audit logging.
    /// </summary>
    [ExtendObjectType("Mutation")]
    [UseFiltering]
    [UseSorting]
    public class AssetMutations
    {
        private readonly IAssetRepository _assetRepository;
        private readonly ValidationService _validationService;
        private readonly IDataProtectionProvider _encryptionService;
        private readonly ILogger<AssetMutations> _logger;

        public AssetMutations(
            IAssetRepository assetRepository,
            ValidationService validationService,
            IDataProtectionProvider encryptionService,
            ILogger<AssetMutations> logger)
        {
            _assetRepository = assetRepository ?? throw new ArgumentNullException(nameof(assetRepository));
            _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
            _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Creates a new asset with encrypted sensitive fields
        /// </summary>
        [UseFirstOrDefault]
        [UseProjection]
        [Authorize(Policy = "AssetCreate")]
        [Error(typeof(ValidationException))]
        public async Task<Asset> CreateAssetAsync(
            Guid userId,
            string name,
            string description,
            AssetType type,
            string location,
            decimal estimatedValue,
            string accessInformation)
        {
            var correlationId = Guid.NewGuid();
            _logger.LogInformation("Starting asset creation. CorrelationId: {CorrelationId}, UserId: {UserId}", 
                correlationId, userId);

            try
            {
                // Encrypt sensitive fields
                var protector = _encryptionService.CreateProtector("Asset.SensitiveData");
                var encryptedLocation = protector.Protect(location);
                var encryptedAccessInfo = !string.IsNullOrEmpty(accessInformation) 
                    ? protector.Protect(accessInformation) 
                    : null;

                var asset = new Asset();
                asset.Update(
                    name,
                    description,
                    type,
                    encryptedLocation,
                    estimatedValue,
                    encryptedAccessInfo);

                // Validate asset data and business rules
                var validationResult = await _validationService.ValidateAssetAsync(asset);
                if (!validationResult.IsValid)
                {
                    _logger.LogWarning("Asset validation failed. CorrelationId: {CorrelationId}, Errors: {Errors}",
                        correlationId, string.Join(", ", validationResult.Errors));
                    throw new ValidationException(validationResult.Errors);
                }

                var createdAsset = await _assetRepository.AddAsync(asset);
                
                _logger.LogInformation("Asset created successfully. CorrelationId: {CorrelationId}, AssetId: {AssetId}",
                    correlationId, createdAsset.Id);
                
                return createdAsset;
            }
            catch (Exception ex) when (ex is not ValidationException)
            {
                _logger.LogError(ex, "Error creating asset. CorrelationId: {CorrelationId}", correlationId);
                throw;
            }
        }

        /// <summary>
        /// Updates an existing asset with field-level encryption
        /// </summary>
        [UseFirstOrDefault]
        [UseProjection]
        [Authorize(Policy = "AssetUpdate")]
        [Error(typeof(ValidationException))]
        public async Task<Asset> UpdateAssetAsync(
            Guid id,
            string name,
            string description,
            AssetType type,
            string location,
            decimal estimatedValue,
            string accessInformation)
        {
            var correlationId = Guid.NewGuid();
            _logger.LogInformation("Starting asset update. CorrelationId: {CorrelationId}, AssetId: {AssetId}",
                correlationId, id);

            try
            {
                var existingAsset = await _assetRepository.GetByIdAsync(id);
                if (existingAsset == null)
                {
                    throw new NotFoundException($"Asset with ID {id} not found");
                }

                // Encrypt sensitive fields
                var protector = _encryptionService.CreateProtector("Asset.SensitiveData");
                var encryptedLocation = protector.Protect(location);
                var encryptedAccessInfo = !string.IsNullOrEmpty(accessInformation)
                    ? protector.Protect(accessInformation)
                    : null;

                existingAsset.Update(
                    name,
                    description,
                    type,
                    encryptedLocation,
                    estimatedValue,
                    encryptedAccessInfo);

                // Validate updated asset
                var validationResult = await _validationService.ValidateAssetAsync(existingAsset);
                if (!validationResult.IsValid)
                {
                    _logger.LogWarning("Asset update validation failed. CorrelationId: {CorrelationId}, Errors: {Errors}",
                        correlationId, string.Join(", ", validationResult.Errors));
                    throw new ValidationException(validationResult.Errors);
                }

                var updatedAsset = await _assetRepository.UpdateAsync(existingAsset);

                _logger.LogInformation("Asset updated successfully. CorrelationId: {CorrelationId}, AssetId: {AssetId}",
                    correlationId, updatedAsset.Id);

                return updatedAsset;
            }
            catch (Exception ex) when (ex is not ValidationException && ex is not NotFoundException)
            {
                _logger.LogError(ex, "Error updating asset. CorrelationId: {CorrelationId}", correlationId);
                throw;
            }
        }

        /// <summary>
        /// Performs a soft delete of an asset with audit logging
        /// </summary>
        [UseFirstOrDefault]
        [Authorize(Policy = "AssetDelete")]
        [Error(typeof(ValidationException))]
        public async Task<bool> DeleteAssetAsync(Guid id)
        {
            var correlationId = Guid.NewGuid();
            _logger.LogInformation("Starting asset deletion. CorrelationId: {CorrelationId}, AssetId: {AssetId}",
                correlationId, id);

            try
            {
                var asset = await _assetRepository.GetByIdAsync(id);
                if (asset == null)
                {
                    throw new NotFoundException($"Asset with ID {id} not found");
                }

                asset.Deactivate();
                var result = await _assetRepository.DeleteAsync(id);

                _logger.LogInformation("Asset deleted successfully. CorrelationId: {CorrelationId}, AssetId: {AssetId}",
                    correlationId, id);

                return result;
            }
            catch (Exception ex) when (ex is not NotFoundException)
            {
                _logger.LogError(ex, "Error deleting asset. CorrelationId: {CorrelationId}", correlationId);
                throw;
            }
        }
    }

    public class NotFoundException : Exception
    {
        public NotFoundException(string message) : base(message) { }
    }

    public class ValidationException : Exception
    {
        public ValidationException(string message) : base(message) { }
    }
}