// System v9.0.0
// System.Collections.Generic v9.0.0
// System.Threading.Tasks v9.0.0
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EstateKit.Core.Entities;

namespace EstateKit.Core.Interfaces
{
    /// <summary>
    /// Repository interface for managing Contact entities with comprehensive security and encryption support.
    /// Implements field-level encryption and security classifications according to A.4 Security Classifications.
    /// </summary>
    public interface IContactRepository
    {
        /// <summary>
        /// Retrieves a contact by their unique identifier with automatic decryption of sensitive fields.
        /// </summary>
        /// <param name="id">The unique identifier of the contact to retrieve</param>
        /// <returns>The decrypted contact if found, null otherwise</returns>
        /// <remarks>
        /// Sensitive fields are automatically decrypted based on security classification levels:
        /// - Critical: Government IDs, Access Codes
        /// - Sensitive: Contact Info
        /// - Internal: Relationships
        /// </remarks>
        Task<Contact?> GetByIdAsync(Guid id);

        /// <summary>
        /// Retrieves all contacts with pagination support and automatic field decryption.
        /// </summary>
        /// <param name="skip">Number of records to skip for pagination</param>
        /// <param name="take">Number of records to take per page</param>
        /// <returns>Collection of decrypted contacts</returns>
        Task<IEnumerable<Contact>> GetAllAsync(int? skip = null, int? take = null);

        /// <summary>
        /// Adds a new contact with automatic encryption of sensitive fields based on security classification.
        /// </summary>
        /// <param name="contact">The contact to add with unencrypted sensitive fields</param>
        /// <returns>The newly created contact with encrypted sensitive fields</returns>
        /// <remarks>
        /// Applies field-level encryption according to security classification:
        /// - FirstName, LastName: Sensitive
        /// - MiddleName, MaidenName: Sensitive
        /// - Contact Methods: Critical
        /// </remarks>
        Task<Contact> AddAsync(Contact contact);

        /// <summary>
        /// Updates an existing contact with encryption handling and concurrency check.
        /// </summary>
        /// <param name="contact">The contact to update with modified fields</param>
        /// <returns>The updated contact with encrypted sensitive fields</returns>
        /// <remarks>
        /// Maintains encryption state of sensitive fields and handles partial updates
        /// while preserving existing encrypted values for unchanged fields.
        /// </remarks>
        Task<Contact> UpdateAsync(Contact contact);

        /// <summary>
        /// Deletes a contact and manages relationship cleanup.
        /// </summary>
        /// <param name="id">The unique identifier of the contact to delete</param>
        /// <returns>True if deleted successfully, false if contact not found</returns>
        Task<bool> DeleteAsync(Guid id);

        /// <summary>
        /// Searches for contacts by name with security-aware partial matching.
        /// </summary>
        /// <param name="searchTerm">The name or partial name to search for</param>
        /// <returns>Collection of matching contacts with decrypted fields</returns>
        /// <remarks>
        /// Searches across FirstName, LastName, and MaidenName fields
        /// with proper handling of encrypted field search operations.
        /// </remarks>
        Task<IEnumerable<Contact>> GetByNameAsync(string searchTerm);

        /// <summary>
        /// Retrieves all contacts related to a specific contact with relationship type filtering.
        /// </summary>
        /// <param name="contactId">The unique identifier of the contact to find relationships for</param>
        /// <returns>Collection of related contacts with decrypted fields</returns>
        /// <remarks>
        /// Returns contacts with established relationships through the Relationships collection,
        /// maintaining proper security classification and field-level encryption.
        /// </remarks>
        Task<IEnumerable<Contact>> GetRelatedContactsAsync(Guid contactId);
    }
}