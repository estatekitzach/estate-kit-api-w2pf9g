using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.EntityFrameworkCore; // Version 10.0.0
using Microsoft.Extensions.Logging; // Version 10.0.0
using EstateKit.Core.Interfaces;
using EstateKit.Core.Entities;
using EstateKit.Core.Enums;

namespace EstateKit.Infrastructure.Data.Repositories
{
    /// <summary>
    /// High-performance repository implementation for managing Asset entities with support for
    /// encryption, auditing, and optimized query patterns.
    /// </summary>
    public class AssetRepository : IAssetRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AssetRepository> _logger;

        public AssetRepository(
            ApplicationDbContext context,
            ILogger<AssetRepository> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<Asset?> GetByIdAsync(Guid id)
        {
            try
            {
                _logger.LogInformation("Retrieving asset with ID: {AssetId}", id);

                return await _context.Assets
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.Id == id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving asset with ID: {AssetId}", id);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<Asset>> GetByUserIdAsync(Guid userId)
        {
            try
            {
                _logger.LogInformation("Retrieving assets for user: {UserId}", userId);

                return await _context.Assets
                    .AsNoTracking()
                    .Where(a => a.UserId == userId)
                    .OrderByDescending(a => a.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving assets for user: {UserId}", userId);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<Asset>> GetByTypeAsync(Guid userId, AssetType type)
        {
            try
            {
                _logger.LogInformation("Retrieving assets of type {AssetType} for user: {UserId}", type, userId);

                return await _context.Assets
                    .AsNoTracking()
                    .Where(a => a.UserId == userId && a.Type == type)
                    .OrderByDescending(a => a.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving assets of type {AssetType} for user: {UserId}", type, userId);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<Asset> AddAsync(Asset asset)
        {
            if (asset == null)
                throw new ArgumentNullException(nameof(asset));

            try
            {
                _logger.LogInformation("Adding new asset for user: {UserId}", asset.UserId);

                await _context.Assets.AddAsync(asset);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully added asset with ID: {AssetId}", asset.Id);
                return asset;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding asset for user: {UserId}", asset.UserId);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<Asset> UpdateAsync(Asset asset)
        {
            if (asset == null)
                throw new ArgumentNullException(nameof(asset));

            try
            {
                _logger.LogInformation("Updating asset: {AssetId}", asset.Id);

                var existingAsset = await _context.Assets
                    .FirstOrDefaultAsync(a => a.Id == asset.Id);

                if (existingAsset == null)
                {
                    throw new InvalidOperationException($"Asset with ID {asset.Id} not found");
                }

                _context.Entry(existingAsset).CurrentValues.SetValues(asset);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully updated asset: {AssetId}", asset.Id);
                return existingAsset;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating asset: {AssetId}", asset.Id);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteAsync(Guid id)
        {
            try
            {
                _logger.LogInformation("Soft deleting asset: {AssetId}", id);

                var asset = await _context.Assets
                    .FirstOrDefaultAsync(a => a.Id == id);

                if (asset == null)
                {
                    _logger.LogWarning("Asset not found for deletion: {AssetId}", id);
                    return false;
                }

                asset.Deactivate();
                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully soft deleted asset: {AssetId}", id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting asset: {AssetId}", id);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<Asset>> GetActiveAssetsAsync(Guid userId)
        {
            try
            {
                _logger.LogInformation("Retrieving active assets for user: {UserId}", userId);

                return await _context.Assets
                    .AsNoTracking()
                    .Where(a => a.UserId == userId && a.IsActive)
                    .OrderByDescending(a => a.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving active assets for user: {UserId}", userId);
                throw;
            }
        }
    }
}