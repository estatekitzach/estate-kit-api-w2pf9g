using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HotChocolate; // v13.0.0
using HotChocolate.Types; // v13.0.0
using HotChocolate.Authorization; // v13.0.0
using HotChocolate.Data; // v13.0.0
using HotChocolate.DataLoader; // v13.0.0
using Microsoft.Extensions.Logging.Abstractions; // v7.0.0
using OpenTelemetry; // v1.7.0
using OpenTelemetry.Trace;
using EstateKit.Core.Entities;
using EstateKit.Core.Interfaces;

namespace EstateKit.Business.API.GraphQL.Queries
{
    /// <summary>
    /// Implements GraphQL query resolvers for Contact-related operations with comprehensive security,
    /// audit logging, and performance optimization features.
    /// </summary>
    [ExtendObjectType("Query")]
    [Authorize]
    [QueryComplexity(maximumComplexity: 100)]
    [RateLimiting(maxRequestsPerMinute: 1000)]
    public class ContactQueries
    {
        private readonly IContactRepository _contactRepository;
        private readonly ILogger<ContactQueries> _logger;
        private readonly ActivitySource _activitySource;

        public ContactQueries(
            IContactRepository contactRepository,
            ILogger<ContactQueries> logger)
        {
            _contactRepository = contactRepository ?? throw new ArgumentNullException(nameof(contactRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _activitySource = new ActivitySource("EstateKit.Business.API.ContactQueries");
        }

        /// <summary>
        /// Retrieves a contact by their unique identifier with security checks and audit logging.
        /// </summary>
        [UseOffsetPaging(MaxPageSize = 1000)]
        [UseFiltering]
        [UseSorting]
        [Authorize(Policy = "ContactRead")]
        public async Task<Contact?> GetContactByIdAsync(
            [ID] Guid id,
            [Service] IUserContextAccessor userContext)
        {
            using var activity = _activitySource.StartActivity("GetContactById");
            activity?.SetTag("ContactId", id);

            _logger.LogInformation("Attempting to retrieve contact with ID: {ContactId}", id);

            try
            {
                var contact = await _contactRepository.GetByIdAsync(id);
                
                if (contact == null)
                {
                    _logger.LogWarning("Contact not found with ID: {ContactId}", id);
                    return null;
                }

                _logger.LogInformation("Successfully retrieved contact: {ContactId}", id);
                return contact;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving contact with ID: {ContactId}", id);
                throw;
            }
        }

        /// <summary>
        /// Retrieves a paginated list of contacts with security filtering and performance optimization.
        /// </summary>
        [UseOffsetPaging(MaxPageSize = 1000)]
        [UseFiltering]
        [UseSorting]
        [Authorize(Policy = "ContactRead")]
        public async Task<IEnumerable<Contact>> GetContactsAsync(
            int? skip = null,
            int? take = null,
            [Service] IUserContextAccessor userContext)
        {
            using var activity = _activitySource.StartActivity("GetContacts");
            activity?.SetTag("Skip", skip);
            activity?.SetTag("Take", take);

            _logger.LogInformation("Retrieving contacts list with skip: {Skip}, take: {Take}", skip, take);

            try
            {
                var contacts = await _contactRepository.GetAllAsync(skip, take);
                _logger.LogInformation("Successfully retrieved {Count} contacts", contacts.Count());
                return contacts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving contacts list");
                throw;
            }
        }

        /// <summary>
        /// Searches for contacts by name with security-aware partial matching and performance optimization.
        /// </summary>
        [UseOffsetPaging(MaxPageSize = 1000)]
        [UseFiltering]
        [UseSorting]
        [Authorize(Policy = "ContactRead")]
        public async Task<IEnumerable<Contact>> SearchContactsByNameAsync(
            string searchTerm,
            [Service] IUserContextAccessor userContext)
        {
            using var activity = _activitySource.StartActivity("SearchContactsByName");
            activity?.SetTag("SearchTerm", searchTerm);

            _logger.LogInformation("Searching contacts with term: {SearchTerm}", searchTerm);

            try
            {
                var contacts = await _contactRepository.GetByNameAsync(searchTerm);
                _logger.LogInformation("Found {Count} contacts matching search term", contacts.Count());
                return contacts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching contacts with term: {SearchTerm}", searchTerm);
                throw;
            }
        }

        /// <summary>
        /// Retrieves related contacts for a given contact ID with relationship type filtering.
        /// </summary>
        [UseOffsetPaging(MaxPageSize = 1000)]
        [UseFiltering]
        [UseSorting]
        [Authorize(Policy = "ContactRead")]
        public async Task<IEnumerable<Contact>> GetRelatedContactsAsync(
            [ID] Guid contactId,
            [Service] IUserContextAccessor userContext)
        {
            using var activity = _activitySource.StartActivity("GetRelatedContacts");
            activity?.SetTag("ContactId", contactId);

            _logger.LogInformation("Retrieving related contacts for ID: {ContactId}", contactId);

            try
            {
                var relatedContacts = await _contactRepository.GetRelatedContactsAsync(contactId);
                _logger.LogInformation("Found {Count} related contacts for ID: {ContactId}", 
                    relatedContacts.Count(), contactId);
                return relatedContacts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving related contacts for ID: {ContactId}", contactId);
                throw;
            }
        }
    }
}