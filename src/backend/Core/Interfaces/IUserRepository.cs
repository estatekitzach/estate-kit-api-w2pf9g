using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EstateKit.Core.Models.UserModels;

namespace EstateKit.Core.Interfaces
{
    /// <summary>
    /// Repository interface for securely managing User entities with comprehensive support for
    /// CRUD operations, specialized queries, field-level encryption, and audit logging.
    /// </summary>
    public interface IUserRepository
    {
        /// <summary>
        /// Retrieves a user by their unique identifier with automatic decryption of sensitive fields.
        /// </summary>
        /// <param name="id">The unique identifier of the user</param>
        /// <returns>The decrypted user if found, null otherwise</returns>
        /// <remarks>
        /// - Handles field-level decryption for sensitive data
        /// - Includes related Contact, Documents, Identifiers, and Assets
        /// - Logs access attempt in audit trail
        /// </remarks>
        Task<User?> GetByIdAsync(int id);

        /// <summary>
        /// Retrieves all active users with proper decryption of sensitive fields.
        /// </summary>
        /// <returns>Collection of all active users with decrypted sensitive data</returns>
        /// <remarks>
        /// - Automatically decrypts sensitive fields
        /// - Includes essential related entities
        /// - Filters inactive users
        /// - Implements pagination for large datasets
        /// </remarks>
        Task<IEnumerable<User>> GetAllAsync();

        /// <summary>
        /// Adds a new user with proper encryption of sensitive fields and comprehensive audit logging.
        /// </summary>
        /// <param name="user">The user entity to create</param>
        /// <returns>The created user with generated Id and encrypted sensitive fields</returns>
        /// <remarks>
        /// - Encrypts sensitive fields before storage
        /// - Validates all required fields and relationships
        /// - Creates audit trail entry
        /// - Ensures security stamp generation
        /// </remarks>
        Task<User> AddAsync(User user);

        /// <summary>
        /// Updates user information with proper encryption and audit logging of changes.
        /// </summary>
        /// <param name="user">The user entity with updated information</param>
        /// <returns>The updated user with encrypted sensitive fields</returns>
        /// <remarks>
        /// - Re-encrypts modified sensitive fields
        /// - Validates all updates
        /// - Logs detailed change history
        /// - Updates security stamp
        /// </remarks>
        Task<User> UpdateAsync(User user);

        /// <summary>
        /// Soft deletes a user and logs the operation in the audit trail.
        /// </summary>
        /// <param name="id">The unique identifier of the user to delete</param>
        /// <returns>True if successful, false if user not found</returns>
        /// <remarks>
        /// - Implements soft delete pattern
        /// - Maintains referential integrity
        /// - Creates deletion audit entry
        /// - Updates security stamp
        /// </remarks>
        Task<bool> DeleteAsync(int id);

        /// <summary>
        /// Retrieves a user by their contact ID with proper decryption.
        /// </summary>
        /// <param name="contactId">The unique identifier of the user's contact</param>
        /// <returns>The decrypted user if found, null otherwise</returns>
        /// <remarks>
        /// - Decrypts sensitive contact information
        /// - Includes related entities
        /// - Logs access in audit trail
        /// </remarks>
        Task<User?> GetByContactIdAsync(int contactId);

        /// <summary>
        /// Retrieves users with specific document types, handling encrypted data appropriately.
        /// </summary>
        /// <param name="documentType">The type of document to filter by</param>
        /// <returns>Collection of users with specified document type</returns>
        /// <remarks>
        /// - Filters by document type
        /// - Decrypts relevant fields
        /// - Includes necessary related data
        /// - Logs query in audit trail
        /// </remarks>
        Task<IEnumerable<User>> GetUsersWithDocumentTypeAsync(string documentType);

        /// <summary>
        /// Retrieves users with specific identifier types, handling encrypted data appropriately.
        /// </summary>
        /// <param name="identifierType">The type of identifier to filter by</param>
        /// <returns>Collection of users with specified identifier type</returns>
        /// <remarks>
        /// - Filters by identifier type
        /// - Decrypts relevant fields
        /// - Includes necessary related data
        /// - Logs query in audit trail
        /// </remarks>
        Task<IEnumerable<User>> GetUsersWithIdentifierTypeAsync(string identifierType);
    }
}
