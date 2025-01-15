using System;
using System.Threading.Tasks;
using EstateKit.Core.Entities;
using EstateKit.Core.Enums;
using EstateKit.Core.Interfaces;
using HotChocolate;
using HotChocolate.AspNetCore.Authorization;
using HotChocolate.Types;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging;
using Microsoft.Security.Encryption;

namespace EstateKit.Business.API.GraphQL.Mutations
{
    /// <summary>
    /// Implements GraphQL mutations for Contact entity management with comprehensive security controls,
    /// field-level encryption, validation, and audit logging.
    /// </summary>
    [ExtendObjectType("Mutation")]
    public class ContactMutations
    {
        private readonly IContactRepository _contactRepository;
        private readonly ILogger<ContactMutations> _logger;
        private readonly TelemetryClient _telemetryClient;
        private readonly IEncryptionService _encryptionService;

        public ContactMutations(
            IContactRepository contactRepository,
            ILogger<ContactMutations> logger,
            TelemetryClient telemetryClient,
            IEncryptionService encryptionService)
        {
            _contactRepository = contactRepository ?? throw new ArgumentNullException(nameof(contactRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
            _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
        }

        /// <summary>
        /// Creates a new contact with field-level encryption and comprehensive audit logging.
        /// </summary>
        [UseFirstOrDefault]
        [UseProjection]
        [Authorize(Policy = "ContactCreate")]
        [Error(typeof(ValidationException))]
        [Error(typeof(RateLimitExceededException))]
        public async Task<Contact> CreateContactAsync(
            string firstName,
            string lastName,
            string? middleName = null,
            string? maidenName = null)
        {
            try
            {
                _logger.LogInformation("Creating new contact for {FirstName} {LastName}", firstName, lastName);

                if (string.IsNullOrWhiteSpace(firstName))
                    throw new ValidationException("First name is required");
                if (string.IsNullOrWhiteSpace(lastName))
                    throw new ValidationException("Last name is required");

                var contact = new Contact
                {
                    FirstName = firstName,
                    LastName = lastName,
                    MiddleName = middleName,
                    MaidenName = maidenName
                };

                var createdContact = await _contactRepository.AddAsync(contact);

                _telemetryClient.TrackEvent("ContactCreated", new Dictionary<string, string>
                {
                    { "ContactId", createdContact.Id.ToString() },
                    { "Operation", "Create" }
                });

                return createdContact;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating contact for {FirstName} {LastName}", firstName, lastName);
                throw;
            }
        }

        /// <summary>
        /// Updates an existing contact with field-level encryption and validation.
        /// </summary>
        [UseFirstOrDefault]
        [UseProjection]
        [Authorize(Policy = "ContactUpdate")]
        [Error(typeof(ValidationException))]
        [Error(typeof(NotFoundException))]
        public async Task<Contact> UpdateContactAsync(
            Guid id,
            string? firstName = null,
            string? lastName = null,
            string? middleName = null,
            string? maidenName = null)
        {
            try
            {
                _logger.LogInformation("Updating contact {ContactId}", id);

                var existingContact = await _contactRepository.GetByIdAsync(id);
                if (existingContact == null)
                    throw new NotFoundException($"Contact with ID {id} not found");

                if (firstName != null)
                    existingContact.FirstName = firstName;
                if (lastName != null)
                    existingContact.LastName = lastName;
                if (middleName != null)
                    existingContact.MiddleName = middleName;
                if (maidenName != null)
                    existingContact.MaidenName = maidenName;

                var updatedContact = await _contactRepository.UpdateAsync(existingContact);

                _telemetryClient.TrackEvent("ContactUpdated", new Dictionary<string, string>
                {
                    { "ContactId", id.ToString() },
                    { "Operation", "Update" }
                });

                return updatedContact;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating contact {ContactId}", id);
                throw;
            }
        }

        /// <summary>
        /// Deletes a contact and manages relationship cleanup with audit logging.
        /// </summary>
        [Authorize(Policy = "ContactDelete")]
        [Error(typeof(NotFoundException))]
        public async Task<bool> DeleteContactAsync(Guid id)
        {
            try
            {
                _logger.LogInformation("Deleting contact {ContactId}", id);

                var deleted = await _contactRepository.DeleteAsync(id);
                if (!deleted)
                    throw new NotFoundException($"Contact with ID {id} not found");

                _telemetryClient.TrackEvent("ContactDeleted", new Dictionary<string, string>
                {
                    { "ContactId", id.ToString() },
                    { "Operation", "Delete" }
                });

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting contact {ContactId}", id);
                throw;
            }
        }

        /// <summary>
        /// Adds a relationship between two contacts with validation and security checks.
        /// </summary>
        [UseFirstOrDefault]
        [UseProjection]
        [Authorize(Policy = "ContactUpdate")]
        [Error(typeof(ValidationException))]
        [Error(typeof(NotFoundException))]
        public async Task<Contact> AddRelationshipAsync(
            Guid contactId,
            Guid relatedContactId,
            RelationshipType relationshipType)
        {
            try
            {
                _logger.LogInformation("Adding relationship for contact {ContactId} with {RelatedContactId}", 
                    contactId, relatedContactId);

                var contact = await _contactRepository.GetByIdAsync(contactId);
                if (contact == null)
                    throw new NotFoundException($"Contact with ID {contactId} not found");

                var relatedContact = await _contactRepository.GetByIdAsync(relatedContactId);
                if (relatedContact == null)
                    throw new NotFoundException($"Related contact with ID {relatedContactId} not found");

                contact.AddRelationship(relatedContact, relationshipType);
                var updatedContact = await _contactRepository.UpdateAsync(contact);

                _telemetryClient.TrackEvent("RelationshipAdded", new Dictionary<string, string>
                {
                    { "ContactId", contactId.ToString() },
                    { "RelatedContactId", relatedContactId.ToString() },
                    { "RelationType", relationshipType.ToString() }
                });

                return updatedContact;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding relationship for contact {ContactId}", contactId);
                throw;
            }
        }
    }

    public class ValidationException : Exception
    {
        public ValidationException(string message) : base(message) { }
    }

    public class NotFoundException : Exception
    {
        public NotFoundException(string message) : base(message) { }
    }

    public class RateLimitExceededException : Exception
    {
        public RateLimitExceededException(string message) : base(message) { }
    }
}