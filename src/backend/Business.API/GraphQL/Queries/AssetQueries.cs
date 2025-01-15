using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using EstateKit.Core.Entities;
using EstateKit.Core.Interfaces;
using HotChocolate;
using HotChocolate.Types;
using HotChocolate.Authorization;
using Microsoft.Extensions.Logging;

namespace EstateKit.Business.API.GraphQL.Queries
{
    /// <summary>
    /// Implements GraphQL queries for Asset entities with comprehensive security,
    /// authorization, and performance optimization.
    /// </summary>
    [ExtendObjectType("Query")]
    [Authorize]
    public class AssetQueries
    {
        private readonly IAssetRepository _assetRepository;
        private readonly ILogger<AssetQueries> _logger;

        /// <summary>
        /// Initializes a new instance of the AssetQueries class with required dependencies.
        /// </summary>
        /// <param name="assetRepository">Repository for secure asset data access</param>
        /// <param name="logger">Logger for audit and error tracking</param>
        /// <exception cref="ArgumentNullException">Thrown when required dependencies are null</exception>
        public AssetQueries(
            IAssetRepository assetRepository,
            ILogger<AssetQueries> logger)
        {
            _assetRepository = assetRepository ?? throw new ArgumentNullException(nameof(assetRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Retrieves a single asset by ID with security validation and field-level authorization.
        /// </summary>
        /// <param name="id">Unique identifier of the requested asset</param>
        /// <param name="claimsPrincipal">Current user's claims for authorization</param>
        /// <returns>The requested asset if found and authorized, null otherwise</returns>
        [UseFirstOrDefault]
        [UseProjection]
        [Authorize(Policy = "AssetAccess")]
        public async Task<Asset?> GetAssetByIdAsync(
            [ID] Guid id,
            ClaimsPrincipal claimsPrincipal)
        {
            try
            {
                _logger.LogInformation("Attempting to retrieve asset with ID: {AssetId}", id);

                var userId = claimsPrincipal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("User ID not found in claims principal");
                    return null;
                }

                var asset = await _assetRepository.GetByIdAsync(id);
                
                if (asset == null)
                {
                    _logger.LogInformation("Asset not found with ID: {AssetId}", id);
                    return null;
                }

                // Verify user has access to this asset
                if (asset.UserId != Guid.Parse(userId))
                {
                    _logger.LogWarning("Unauthorized access attempt to asset {AssetId} by user {UserId}", id, userId);
                    return null;
                }

                _logger.LogInformation("Successfully retrieved asset {AssetId} for user {UserId}", id, userId);
                return asset;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving asset with ID: {AssetId}", id);
                throw;
            }
        }

        /// <summary>
        /// Retrieves all assets belonging to the current user with filtering and sorting support.
        /// </summary>
        /// <param name="claimsPrincipal">Current user's claims for authorization</param>
        /// <returns>Collection of authorized assets for the current user</returns>
        [UseProjection]
        [UseFiltering]
        [UseSorting]
        [Authorize(Policy = "AssetAccess")]
        public async Task<IEnumerable<Asset>> GetUserAssetsAsync(
            ClaimsPrincipal claimsPrincipal)
        {
            try
            {
                var userId = claimsPrincipal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("User ID not found in claims principal");
                    return Array.Empty<Asset>();
                }

                _logger.LogInformation("Retrieving assets for user {UserId}", userId);

                var userGuid = Guid.Parse(userId);
                var assets = await _assetRepository.GetActiveAssetsAsync(userGuid);

                _logger.LogInformation("Successfully retrieved {Count} assets for user {UserId}", 
                    assets?.Count() ?? 0, userId);

                return assets ?? Array.Empty<Asset>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving assets for user");
                throw;
            }
        }

        /// <summary>
        /// Retrieves assets of a specific type for the current user with filtering and sorting.
        /// </summary>
        /// <param name="type">Type of assets to retrieve</param>
        /// <param name="claimsPrincipal">Current user's claims for authorization</param>
        /// <returns>Collection of assets matching the specified type</returns>
        [UseProjection]
        [UseFiltering]
        [UseSorting]
        [Authorize(Policy = "AssetAccess")]
        public async Task<IEnumerable<Asset>> GetAssetsByTypeAsync(
            AssetType type,
            ClaimsPrincipal claimsPrincipal)
        {
            try
            {
                var userId = claimsPrincipal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("User ID not found in claims principal");
                    return Array.Empty<Asset>();
                }

                _logger.LogInformation("Retrieving assets of type {AssetType} for user {UserId}", type, userId);

                var userGuid = Guid.Parse(userId);
                var assets = await _assetRepository.GetByTypeAsync(userGuid, type);

                _logger.LogInformation("Successfully retrieved {Count} assets of type {AssetType} for user {UserId}", 
                    assets?.Count() ?? 0, type, userId);

                return assets ?? Array.Empty<Asset>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving assets by type {AssetType}", type);
                throw;
            }
        }
    }
}