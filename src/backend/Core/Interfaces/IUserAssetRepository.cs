using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EstateKit.Core.Models.UserModels;
using EstateKit.Core.Enums;

namespace EstateKit.Core.Interfaces
{
    /// <summary>
    /// Repository interface for managing Asset entities with support for encrypted data access,
    /// optimized query patterns, and comprehensive audit logging.
    /// </summary>
    public interface IUserAssetRepository
    {
        /// <summary>
        /// Retrieves an encrypted asset by its unique identifier with proper decryption handling.
        /// </summary>
        /// <param name="id">The unique identifier of the asset to retrieve</param>
        /// <returns>The decrypted asset if found, null otherwise</returns>
        Task<UserAsset?> GetByIdAsync(int id);

        /// <summary>
        /// Retrieves all encrypted assets belonging to a specific user with batch decryption.
        /// </summary>
        /// <param name="userId">The unique identifier of the user whose assets to retrieve</param>
        /// <returns>Collection of decrypted user's assets</returns>
        Task<IEnumerable<UserAsset>> GetByUserIdAsync(int userId);

        /// <summary>
        /// Retrieves encrypted assets of a specific type for a user with optimized query.
        /// </summary>
        /// <param name="userId">The unique identifier of the user whose assets to retrieve</param>
        /// <param name="type">The type of assets to filter by</param>
        /// <returns>Collection of decrypted assets matching the type</returns>
        Task<IEnumerable<UserAsset>> GetByTypeAsync(int userId, AssetType type);

        /// <summary>
        /// Adds a new asset with proper field encryption and audit logging.
        /// </summary>
        /// <param name="userAsset">The asset to be added</param>
        /// <returns>The newly created asset with encrypted fields</returns>
        Task<UserAsset> AddAsync(UserAsset userAsset);

        /// <summary>
        /// Updates an existing asset with encryption and version tracking.
        /// </summary>
        /// <param name="userAsset">The asset with updated information</param>
        /// <returns>The updated asset with encrypted fields</returns>
        Task<UserAsset> UpdateAsync(UserAsset userAsset);

        /// <summary>
        /// Performs a soft delete of an asset with audit logging.
        /// </summary>
        /// <param name="id">The unique identifier of the asset to delete</param>
        /// <returns>True if deleted successfully, false otherwise</returns>
        Task<bool> DeleteAsync(int id);

        /// <summary>
        /// Retrieves all active assets for a user with optimized caching.
        /// </summary>
        /// <param name="userId">The unique identifier of the user whose active assets to retrieve</param>
        /// <returns>Collection of active, decrypted assets</returns>
        Task<IEnumerable<UserAsset>> GetActiveAssetsAsync(int userId);
    }
}
