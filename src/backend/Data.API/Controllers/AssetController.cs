using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc; // v9.0.0
using Microsoft.Extensions.Logging; // v9.0.0
using Microsoft.ApplicationInsights; // v9.0.0
using Microsoft.AspNetCore.Authentication; // v9.0.0
using EstateKit.Core.Entities;
using EstateKit.Core.Interfaces;
using EstateKit.Data.API.Services;

namespace EstateKit.Data.API.Controllers
{
    /// <summary>
    /// Controller handling secure asset management operations with comprehensive
    /// validation, encryption, and audit logging capabilities.
    /// </summary>
    [ApiController]
    [Route("api/v1/[controller]")]
    [ApiVersion("1.0")]
    [Authorize]
    public class AssetController : ControllerBase
    {
        private readonly IAssetRepository _assetRepository;
        private readonly DataService _dataService;
        private readonly EncryptionService _encryptionService;
        private readonly ILogger<AssetController> _logger;
        private readonly TelemetryClient _telemetryClient;
        private readonly AuditService _auditService;

        public AssetController(
            IAssetRepository assetRepository,
            DataService dataService,
            EncryptionService encryptionService,
            ILogger<AssetController> logger,
            TelemetryClient telemetryClient,
            AuditService auditService)
        {
            _assetRepository = assetRepository ?? throw new ArgumentNullException(nameof(assetRepository));
            _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
            _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        }

        /// <summary>
        /// Retrieves an asset by ID with decryption and security validation
        /// </summary>
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<Asset>> GetAssetAsync(Guid id)
        {
            using var operation = _telemetryClient.StartOperation<RequestTelemetry>("GetAsset");
            
            try
            {
                var userId = User.FindFirst("sub")?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("Unauthorized access attempt to asset {AssetId}", id);
                    return Unauthorized();
                }

                var asset = await _assetRepository.GetByIdAsync(id);
                if (asset == null)
                {
                    _logger.LogInformation("Asset {AssetId} not found", id);
                    return NotFound();
                }

                if (asset.UserId.ToString() != userId)
                {
                    _logger.LogWarning("Forbidden access attempt to asset {AssetId} by user {UserId}", id, userId);
                    return Forbid();
                }

                // Create encryption context for sensitive fields
                var context = new EncryptionContext
                {
                    EntityType = "Asset",
                    EntityId = id.ToString(),
                    UserId = userId
                };

                // Decrypt sensitive fields
                await _encryptionService.DecryptEntityFieldsAsync(asset, context);

                // Log access
                await _auditService.LogDataAccess(
                    userId,
                    "Read",
                    "Asset",
                    id.ToString(),
                    null,
                    SecurityClassification.Internal);

                return Ok(asset);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving asset {AssetId}", id);
                operation.Telemetry.Success = false;
                return StatusCode(500, "An error occurred while retrieving the asset");
            }
        }

        /// <summary>
        /// Retrieves all assets for a user with decryption and pagination
        /// </summary>
        [HttpGet("user/{userId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<IEnumerable<Asset>>> GetUserAssetsAsync(
            Guid userId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            using var operation = _telemetryClient.StartOperation<RequestTelemetry>("GetUserAssets");

            try
            {
                var currentUserId = User.FindFirst("sub")?.Value;
                if (string.IsNullOrEmpty(currentUserId))
                {
                    _logger.LogWarning("Unauthorized access attempt to user assets {UserId}", userId);
                    return Unauthorized();
                }

                if (userId.ToString() != currentUserId)
                {
                    _logger.LogWarning("Forbidden access attempt to assets of user {UserId} by {CurrentUserId}", 
                        userId, currentUserId);
                    return Forbid();
                }

                var assets = await _assetRepository.GetByUserIdAsync(userId);

                // Create encryption context for batch decryption
                var context = new EncryptionContext
                {
                    EntityType = "Asset",
                    UserId = currentUserId
                };

                // Decrypt sensitive fields for all assets
                foreach (var asset in assets)
                {
                    context.EntityId = asset.Id.ToString();
                    await _encryptionService.DecryptEntityFieldsAsync(asset, context);
                }

                // Log access
                await _auditService.LogDataAccess(
                    currentUserId,
                    "ReadAll",
                    "Asset",
                    userId.ToString(),
                    null,
                    SecurityClassification.Internal);

                return Ok(assets);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving assets for user {UserId}", userId);
                operation.Telemetry.Success = false;
                return StatusCode(500, "An error occurred while retrieving assets");
            }
        }

        /// <summary>
        /// Creates a new asset with encryption and validation
        /// </summary>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<Asset>> CreateAssetAsync([FromBody] Asset asset)
        {
            using var operation = _telemetryClient.StartOperation<RequestTelemetry>("CreateAsset");

            try
            {
                var userId = User.FindFirst("sub")?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("Unauthorized attempt to create asset");
                    return Unauthorized();
                }

                if (asset == null)
                {
                    return BadRequest("Asset data is required");
                }

                // Create encryption context
                var context = new EncryptionContext
                {
                    EntityType = "Asset",
                    EntityId = asset.Id.ToString(),
                    UserId = userId
                };

                // Encrypt sensitive fields
                await _encryptionService.EncryptEntityFieldsAsync(asset, context);

                var createdAsset = await _dataService.CreateEntityAsync(asset, userId);

                // Log creation
                await _auditService.LogDataAccess(
                    userId,
                    "Create",
                    "Asset",
                    createdAsset.Id.ToString(),
                    createdAsset,
                    SecurityClassification.Internal);

                return CreatedAtAction(
                    nameof(GetAssetAsync),
                    new { id = createdAsset.Id },
                    createdAsset);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating asset");
                operation.Telemetry.Success = false;
                return StatusCode(500, "An error occurred while creating the asset");
            }
        }

        /// <summary>
        /// Updates an existing asset with encryption and validation
        /// </summary>
        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<Asset>> UpdateAssetAsync(Guid id, [FromBody] Asset asset)
        {
            using var operation = _telemetryClient.StartOperation<RequestTelemetry>("UpdateAsset");

            try
            {
                var userId = User.FindFirst("sub")?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("Unauthorized attempt to update asset {AssetId}", id);
                    return Unauthorized();
                }

                if (asset == null || asset.Id != id)
                {
                    return BadRequest("Invalid asset data");
                }

                var existingAsset = await _assetRepository.GetByIdAsync(id);
                if (existingAsset == null)
                {
                    return NotFound();
                }

                if (existingAsset.UserId.ToString() != userId)
                {
                    _logger.LogWarning("Forbidden update attempt to asset {AssetId} by user {UserId}", id, userId);
                    return Forbid();
                }

                // Create encryption context
                var context = new EncryptionContext
                {
                    EntityType = "Asset",
                    EntityId = id.ToString(),
                    UserId = userId
                };

                // Encrypt sensitive fields
                await _encryptionService.EncryptEntityFieldsAsync(asset, context);

                var updatedAsset = await _dataService.UpdateEntityAsync(asset, userId);

                // Log update
                await _auditService.LogDataAccess(
                    userId,
                    "Update",
                    "Asset",
                    id.ToString(),
                    asset,
                    SecurityClassification.Internal);

                return Ok(updatedAsset);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating asset {AssetId}", id);
                operation.Telemetry.Success = false;
                return StatusCode(500, "An error occurred while updating the asset");
            }
        }

        /// <summary>
        /// Deletes an asset with security validation and audit logging
        /// </summary>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteAssetAsync(Guid id)
        {
            using var operation = _telemetryClient.StartOperation<RequestTelemetry>("DeleteAsset");

            try
            {
                var userId = User.FindFirst("sub")?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("Unauthorized attempt to delete asset {AssetId}", id);
                    return Unauthorized();
                }

                var asset = await _assetRepository.GetByIdAsync(id);
                if (asset == null)
                {
                    return NotFound();
                }

                if (asset.UserId.ToString() != userId)
                {
                    _logger.LogWarning("Forbidden delete attempt to asset {AssetId} by user {UserId}", id, userId);
                    return Forbid();
                }

                await _dataService.DeleteEntityAsync<Asset>(id, userId);

                // Log deletion
                await _auditService.LogDataAccess(
                    userId,
                    "Delete",
                    "Asset",
                    id.ToString(),
                    null,
                    SecurityClassification.Internal);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting asset {AssetId}", id);
                operation.Telemetry.Success = false;
                return StatusCode(500, "An error occurred while deleting the asset");
            }
        }
    }
}