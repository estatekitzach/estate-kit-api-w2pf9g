using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc; // v9.0.0
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging; // v9.0.0
using Microsoft.ApplicationInsights; // v9.0.0
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNetCore.Authentication; // v9.0.0
using EstateKit.Core.Models.UserModels;
using EstateKit.Core.Interfaces;
using Asp.Versioning;
using EstateKit.Infrastructure.Logger.Extensions;
using System.Globalization;
using static Microsoft.ApplicationInsights.MetricDimensionNames.TelemetryContext;

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
        private readonly IUserAssetRepository _userAssetRepository;
        // private readonly DataService _dataService;
        // private readonly EncryptionService _encryptionService;
        private readonly ILogger<AssetController> _logger;
        //private readonly TelemetryClient _telemetryClient;
        // private readonly AuditService _auditService;

        public AssetController(
            IUserAssetRepository assetRepository,
            //DataService dataService,
            //EncryptionService encryptionService,
            ILogger<AssetController> logger
            //,TelemetryClient telemetryClient
            //,AuditService auditService
            )
        {
            _userAssetRepository = assetRepository ?? throw new ArgumentNullException(nameof(assetRepository));
            //_dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
            //_encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            //_telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
            //_auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        }

        /// <summary>
        /// Retrieves an asset by ID with decryption and security validation
        /// </summary>
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<UserAsset>> GetAssetAsync(int id)
        {
            //using var operation = _telemetryClient.StartOperation<RequestTelemetry>("GetAsset");
            
            try
            {
                /* todo: check if the user is allowed access
                 * if (id <= 0)
                {
                    _logger.LogGenericWarning(string.Format(CultureInfo.InvariantCulture, "Unauthorized access attempt to asset {0}", id));
                    return Unauthorized();
                } */

                var userAsset = await _userAssetRepository.GetByIdAsync(id).ConfigureAwait(false);
                if (userAsset == null)
                {
                    _logger.LogGenericInformation(string.Format(CultureInfo.InvariantCulture, "UserAsset {0} not found", id));
                    return NotFound();
                }

                // Create encryption context for sensitive fields
                /*var context = new EncryptionContext
                {
                    EntityType = "Asset",
                    EntityId = id.ToString(),
                    UserId = userId
                };*/

                // Decrypt sensitive fields
                // await _encryptionService.DecryptEntityFieldsAsync(asset, context);

                // Log access
                /* await _auditService.LogDataAccess(
                    userId,
                    "Read",
                    "Asset",
                    id.ToString(),
                    null,
                    SecurityClassification.Internal); */

                return Ok(userAsset);
            }
            catch (Exception ex)
            {
                _logger.LogGenericException(string.Format(CultureInfo.InvariantCulture, "Error retrieving asset {0}", id), ex);
                //operation.Telemetry.Success = false;
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
        public async Task<ActionResult<IEnumerable<UserAsset>>> GetUserAssetsAsync(
            int userId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
           // using var operation = _telemetryClient.StartOperation<RequestTelemetry>("GetUserAssets");

            try
            {
                /* todo: need a way to check if the user is allowed to access the data
                 * if (userId <= 0)
                {
                    _logger.LogGenericWarning(string.Format(CultureInfo.InvariantCulture, "Unauthorized access attempt to user assets {0}", userId));
                    return Unauthorized();
                }*/

                var assets = await _userAssetRepository.GetByUserIdAsync(userId).ConfigureAwait(false);

                // Create encryption context for batch decryption
                /*var context = new EncryptionContext
                {
                    EntityType = "Asset",
                    UserId = currentUserId
                }; */

                // Decrypt sensitive fields for all assets
                /*foreach (var asset in assets)
                {
                    context.EntityId = asset.Id.ToString();
                    await _encryptionService.DecryptEntityFieldsAsync(asset, context);
                }*/

                // Log access
                /*await _auditService.LogDataAccess(
                    currentUserId,
                    "ReadAll",
                    "Asset",
                    userId.ToString(),
                    null,
                    SecurityClassification.Internal); */

                return Ok(assets);
            }
            catch (Exception ex)
            {
                _logger.LogGenericException(string.Format(CultureInfo.InvariantCulture, "Error retrieving assets for user {0}", userId), ex);
                //operation.Telemetry.Success = false;
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
        public async Task<ActionResult<UserAsset>> CreateAssetAsync([FromBody] UserAsset asset)
        {
            //using var operation = _telemetryClient.StartOperation<RequestTelemetry>("CreateUserAsset");

            try
            {
                /* w--Todo: find a way to check if the user has access.  
                 * var userId = User.FindFirst("sub")?.Value;
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
                /*var context = new EncryptionContext
                {
                    EntityType = "Asset",
                    EntityId = asset.Id.ToString(),
                    UserId = userId
                };

                // Encrypt sensitive fields
                await _encryptionService.EncryptEntityFieldsAsync(asset, context);*/
                var createdAsset = _userAssetRepository.AddAsync(asset);

                // Log creation
                /*await _auditService.LogDataAccess(
                    userId,
                    "Create",
                    "Asset",
                    createdAsset.Id.ToString(),
                    createdAsset,
return CreatedAtAction(
    nameof(GetAssetAsync),
    new { id = createdAsset.Id },
    await createdAsset);
                    SecurityClassification.Internal);*/
                return await Task.Run(async () => {
                    return CreatedAtAction(
                            nameof(GetAssetAsync),
                            new { id = createdAsset.Id },
                            await createdAsset.ConfigureAwait(false));
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogGenericException(string.Format(CultureInfo.InvariantCulture, "Error creating asset"), ex);
                //operation.Telemetry.Success = false;
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
        public async Task<ActionResult<UserAsset>> UpdateAssetAsync(int id, [FromBody] UserAsset asset)
        {
            //using var operation = _telemetryClient.StartOperation<RequestTelemetry>("UpdateAsset");

            try
            {
                /* Todo: Check if the user has access
                 * if (id <= 0)
                {
                    _logger.LogWarning("Unauthorized attempt to update asset {AssetId}", id);
                    return Unauthorized();
                } */

                if (asset == null || asset.Id != id)
                {
                    return BadRequest("Invalid asset data");
                }

                var existingAsset = await _userAssetRepository.GetByIdAsync(id).ConfigureAwait(false);
                if (existingAsset == null)
                {
                    return NotFound();
                }

                // Create encryption context
                /*var context = new EncryptionContext
                {
                    EntityType = "Asset",
                    EntityId = id.ToString(),
                    UserId = userId
                };

                // Encrypt sensitive fields
                await _encryptionService.EncryptEntityFieldsAsync(asset, context);*/

                var updatedAsset = await _userAssetRepository.UpdateAsync(asset).ConfigureAwait(false);

                // Log update
                /*await _auditService.LogDataAccess(
                    userId,
                    "Update",
                    "Asset",
                    id.ToString(),
                    asset,
                    SecurityClassification.Internal);*/

                return Ok(updatedAsset);
            }
            catch (Exception ex)
            {
                _logger.LogGenericException(string.Format(CultureInfo.InvariantCulture, "Error updating asset {0}", id), ex);
                //operation.Telemetry.Success = false;
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
        public async Task<IActionResult> DeleteAssetAsync(int id)
        {
            //using var operation = _telemetryClient.StartOperation<RequestTelemetry>("DeleteAsset");

            try
            {
                /* Todo check if user has access
                 * var userId = User.FindFirst("sub")?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("Unauthorized attempt to delete asset {AssetId}", id);
                    return Unauthorized();
                }*/

                var asset = await _userAssetRepository.GetByIdAsync(id).ConfigureAwait(false);
                if (asset == null)
                {
                    return NotFound();
                }

                await _userAssetRepository.DeleteAsync(id).ConfigureAwait(false);

                // Log deletion
                /* await _auditService.LogDataAccess(
                    userId,
                    "Delete",
                    "Asset",
                    id.ToString(),
                    null,
                    SecurityClassification.Internal); */

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogGenericException(string.Format(CultureInfo.InvariantCulture, "Error deleting asset {0}", id), ex);
                //operation.Telemetry.Success = false;
                return StatusCode(500, "An error occurred while deleting the asset");
            }
        }
    }
}