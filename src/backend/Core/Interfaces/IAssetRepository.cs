using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EstateKit.Core.Entities;
using EstateKit.Core.Enums;

namespace EstateKit.Core.Interfaces
{
    /// <summary>
    /// Repository interface for managing Asset entities with support for encrypted data access,
    /// optimized query patterns, and comprehensive audit logging.
    /// </summary>
    public interface IAssetRepository
    {
        /// <summary>
        /// Retrieves an encrypted asset by its unique identifier with proper decryption handling.
        /// </summary>
        /// <param name="id">The unique identifier of the asset to retrieve</param>
        /// <returns>The decrypted asset if found, null otherwise</returns>
        Task<Asset?> GetByIdAsync(Guid id);

        /// <summary>
        /// Retrieves all encrypted assets belonging to a specific user with batch decryption.
        /// </summary>
        /// <param name="userId">The unique identifier of the user whose assets to retrieve</param>
        /// <returns>Collection of decrypted user's assets</returns>
        Task<IEnumerable<Asset>> GetByUserIdAsync(Guid userId);

        /// <summary>
        /// Retrieves encrypted assets of a specific type for a user with optimized query.
        /// </summary>
        /// <param name="userId">The unique identifier of the user whose assets to retrieve</param>
        /// <param name="type">The type of assets to filter by</param>
        /// <returns>Collection of decrypted assets matching the type</returns>
        Task<IEnumerable<Asset>> GetByTypeAsync(Guid userId, AssetType type);

        /// <summary>
        /// Adds a new asset with proper field encryption and audit logging.
        /// </summary>
        /// <param name="asset">The asset to be added</param>
        /// <returns>The newly created asset with encrypted fields</returns>
        Task<Asset> AddAsync(Asset asset);

        /// <summary>
        /// Updates an existing asset with encryption and version tracking.
        /// </summary>
        /// <param name="asset">The asset with updated information</param>
        /// <returns>The updated asset with encrypted fields</returns>
        Task<Asset> UpdateAsync(Asset asset);

        /// <summary>
        /// Performs a soft delete of an asset with audit logging.
        /// </summary>
        /// <param name="id">The unique identifier of the asset to delete</param>
        /// <returns>True if deleted successfully, false otherwise</returns>
        Task<bool> DeleteAsync(Guid id);

        /// <summary>
        /// Retrieves all active assets for a user with optimized caching.
        /// </summary>
        /// <param name="userId">The unique identifier of the user whose active assets to retrieve</param>
        /// <returns>Collection of active, decrypted assets</returns>
        Task<IEnumerable<Asset>> GetActiveAssetsAsync(Guid userId);
    }
}