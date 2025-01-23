
using EstateKit.Core.Models.UserModels;
using EstateKit.Core.Enums;

namespace EstateKit.Core.Interfaces
{
    /// <summary>
    /// Repository interface for managing government-issued identification documents with 
    /// field-level encryption and comprehensive audit logging. Implements Critical security
    /// classification requirements for handling sensitive identifier data.
    /// </summary>
    public interface IUserIdentifierRepository
    {
        /// <summary>
        /// Retrieves a single identifier by its unique ID with decrypted sensitive fields
        /// and comprehensive audit logging of the access attempt.
        /// </summary>
        /// <param name="id">The unique identifier of the document to retrieve</param>
        /// <returns>The decrypted identifier entity if found, null otherwise</returns>
        Task<UserIdentifier?> GetByIdAsync(int id);

        /// <summary>
        /// Retrieves all active identifiers for a specific user with decrypted sensitive
        /// fields and audit logging of the bulk access operation.
        /// </summary>
        /// <param name="userId">The unique identifier of the user</param>
        /// <returns>Collection of decrypted identifiers belonging to the user</returns>
        Task<IEnumerable<UserIdentifier>> GetByUserIdAsync(int userId);

        /// <summary>
        /// Retrieves all active identifiers of a specific type for a user with proper
        /// encryption handling and access logging.
        /// </summary>
        /// <param name="userId">The unique identifier of the user</param>
        /// <param name="type">The type of identification document to retrieve</param>
        /// <returns>Collection of decrypted identifiers of the specified type</returns>
        Task<IEnumerable<UserIdentifier>> GetByTypeAsync(int userId, IdentifierType type);

        /// <summary>
        /// Adds a new identifier with field-level encryption of sensitive data and
        /// comprehensive audit logging of the creation operation.
        /// </summary>
        /// <param name="userIdentifier">The identifier entity to be added</param>
        /// <returns>The newly created identifier with encrypted sensitive fields</returns>
        Task<UserIdentifier> AddAsync(UserIdentifier userIdentifier);

        /// <summary>
        /// Updates an existing identifier while maintaining field-level encryption and
        /// creating a detailed audit trail of the changes.
        /// </summary>
        /// <param name="userIdentifier">The identifier entity with updated information</param>
        /// <returns>The updated identifier with encrypted sensitive fields</returns>
        Task<UserIdentifier> UpdateAsync(UserIdentifier userIdentifier);

        /// <summary>
        /// Performs a soft delete of an identifier by setting IsActive to false and
        /// logging the deletion operation in the audit trail.
        /// </summary>
        /// <param name="id">The unique identifier of the document to delete</param>
        /// <returns>True if the deletion was successful, false otherwise</returns>
        Task<bool> DeleteAsync(int id);
    }
}
