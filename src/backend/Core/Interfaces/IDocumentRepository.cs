using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EstateKit.Core.Entities;

namespace EstateKit.Core.Interfaces
{
    /// <summary>
    /// Repository interface for managing Document entities with secure storage,
    /// field-level encryption, and comprehensive audit logging capabilities.
    /// </summary>
    /// <remarks>
    /// Version: 1.0.0
    /// Security Classification: Critical
    /// Audit Requirements: All operations must be logged with detailed audit trails
    /// </remarks>
    public interface IDocumentRepository
    {
        /// <summary>
        /// Retrieves a document by its unique identifier with automatic decryption of sensitive fields.
        /// </summary>
        /// <param name="id">The unique identifier of the document to retrieve</param>
        /// <returns>The decrypted document if found, null otherwise</returns>
        /// <remarks>
        /// - Automatically decrypts sensitive fields using EstateKit encryption service
        /// - Logs access attempt in audit trail
        /// - Enforces security classification checks
        /// </remarks>
        Task<Document> GetByIdAsync(Guid id);

        /// <summary>
        /// Retrieves all documents belonging to a specific user with decrypted fields.
        /// </summary>
        /// <param name="userId">The unique identifier of the user</param>
        /// <returns>Collection of decrypted documents belonging to the user</returns>
        /// <remarks>
        /// - Automatically decrypts sensitive fields for each document
        /// - Logs bulk access in audit trail
        /// - Enforces user-level access controls
        /// </remarks>
        Task<IEnumerable<Document>> GetByUserIdAsync(Guid userId);

        /// <summary>
        /// Adds a new document to the repository with automatic encryption of sensitive fields.
        /// </summary>
        /// <param name="document">The document to add</param>
        /// <returns>The newly created document with assigned Id and encrypted fields</returns>
        /// <remarks>
        /// - Automatically encrypts sensitive fields using EstateKit encryption service
        /// - Generates and assigns new document Id
        /// - Logs creation in audit trail with document metadata
        /// </remarks>
        Task<Document> AddAsync(Document document);

        /// <summary>
        /// Updates an existing document in the repository while maintaining field-level encryption.
        /// </summary>
        /// <param name="document">The document with updated information</param>
        /// <returns>True if update successful, false otherwise</returns>
        /// <remarks>
        /// - Re-encrypts modified sensitive fields
        /// - Maintains encryption for unchanged sensitive fields
        /// - Logs modification details in audit trail
        /// </remarks>
        Task<bool> UpdateAsync(Document document);

        /// <summary>
        /// Removes a document from the repository with secure audit logging.
        /// </summary>
        /// <param name="id">The unique identifier of the document to delete</param>
        /// <returns>True if deletion successful, false otherwise</returns>
        /// <remarks>
        /// - Logs deletion in audit trail with document metadata
        /// - Maintains audit history after deletion
        /// - Enforces deletion permissions
        /// </remarks>
        Task<bool> DeleteAsync(Guid id);

        /// <summary>
        /// Updates the processing status and metadata of a document after OCR completion.
        /// </summary>
        /// <param name="id">The unique identifier of the document</param>
        /// <param name="metadata">The extracted and processed metadata JSON</param>
        /// <returns>True if update successful, false otherwise</returns>
        /// <remarks>
        /// - Encrypts sensitive metadata fields
        /// - Updates processing status
        /// - Logs processing completion in audit trail
        /// </remarks>
        Task<bool> UpdateProcessingStatusAsync(Guid id, string metadata);

        /// <summary>
        /// Updates the physical location of a document with security logging.
        /// </summary>
        /// <param name="id">The unique identifier of the document</param>
        /// <param name="location">The new physical location of the document</param>
        /// <returns>True if update successful, false otherwise</returns>
        /// <remarks>
        /// - Encrypts location if classified as sensitive
        /// - Logs location update in audit trail
        /// - Enforces location update permissions
        /// </remarks>
        Task<bool> UpdateLocationAsync(Guid id, string location);
    }
}