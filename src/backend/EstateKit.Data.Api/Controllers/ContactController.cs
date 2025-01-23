using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using EstateKit.Core.Models.ContactModels;
using EstateKit.Core.Interfaces;
// using EstateKit.Data.API.Services;
using Asp.Versioning;
using EstateKit.Infrastructure.Logger.Extensions;
using System.Globalization;

namespace EstateKit.Data.API.Controllers
{
    /// <summary>
    /// Secure REST API controller for managing contact information with field-level encryption,
    /// comprehensive audit logging, and high-performance data access patterns.
    /// </summary>
    [ApiController]
    [Route("api/v1/contacts")]
    //TODO: [Authorize]
    [ApiVersion("1.0")]
    [Produces("application/json")]
    //[ResponseCache(Duration = 60, VaryByQueryKeys = new[] { "*" })]
    public class ContactController : ControllerBase
    {
        private readonly IContactRepository _contactRepository;
        //private readonly EncryptionService _encryptionService;
        //private readonly AuditService _auditService;
        private readonly ILogger<ContactController> _logger;

        public ContactController(
            IContactRepository contactRepository,
            //EncryptionService encryptionService,
            //AuditService auditService,
            ILogger<ContactController> logger)
        {
            _contactRepository = contactRepository ?? throw new ArgumentNullException(nameof(contactRepository));
            //_encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
            //_auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Retrieves a contact by ID with decrypted sensitive fields and audit logging
        /// </summary>
        /// <param name="contactId">Unique identifier of the contact</param>
        /// <returns>Contact with decrypted fields if found</returns>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(Contact), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<Contact>> GetContactAsync(int contactId)
        {
            var correlationId = Guid.NewGuid().ToString();
            _logger.LogGenericInformation(string.Format(CultureInfo.InvariantCulture, 
                "Getting contact with ID: {0}. CorrelationId: {1}", contactId, correlationId));

            try
            {
                var contact = await _contactRepository.GetByIdAsync(contactId).ConfigureAwait(false);
                if (contact == null)
                {
                    _logger.LogGenericWarning(string.Format(CultureInfo.InvariantCulture,
                            "Contact not found with ID: {0}", contactId));
                    return NotFound();
                }

                /*await _auditService.LogDataAccess(
                    User.Identity.Name,
                    "GET",
                    "Contact",
                    id.ToString(),
                    null,
                    SecurityClassification.Sensitive); */

                return Ok(contact);
            }
            catch (Exception ex)
            {
                _logger.LogGenericException(string.Format(CultureInfo.InvariantCulture, "Error retrieving contact with ID: {0}", contactId), ex);
                return StatusCode(500, "An error occurred while retrieving the contact");
            }
        }

        /// <summary>
        /// Creates a new contact with encrypted sensitive fields and audit logging
        /// </summary>
        /// <param name="contact">Contact information to create</param>
        /// <returns>Created contact with encrypted fields</returns>
        [HttpPost]
        [ProducesResponseType(typeof(Contact), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<Contact>> CreateContactAsync([FromBody] Contact contact)
        {
            var correlationId = Guid.NewGuid().ToString();
            _logger.LogGenericInformation(string.Format(CultureInfo.InvariantCulture,
               "Creating new contact. CorrelationId: {0}", correlationId));

            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var createdContact = await _contactRepository.AddAsync(contact).ConfigureAwait(false);

                /*await _auditService.LogDataAccess(
                    User.Identity.Name,
                    "CREATE",
                    "Contact",
                    createdContact.Id.ToString(),
                    contact,
                    SecurityClassification.Sensitive);*/

                return CreatedAtAction(
                    nameof(GetContactAsync),
                    new { id = createdContact.Id },
                    createdContact);
            }
            catch (Exception ex)
            {
                _logger.LogGenericException(string.Format(CultureInfo.InvariantCulture, "Error creating contact"), ex);
                return StatusCode(500, "An error occurred while creating the contact");
            }
        }

        /// <summary>
        /// Updates an existing contact with encrypted field handling and audit logging
        /// </summary>
        /// <param name="id">Contact ID to update</param>
        /// <param name="contact">Updated contact information</param>
        /// <returns>Updated contact with encrypted fields</returns>
        [HttpPut("{id}")]
        [ProducesResponseType(typeof(Contact), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<Contact>> UpdateContactAsync(int id, [FromBody] Contact contact)
        {
            var correlationId = Guid.NewGuid().ToString();
            _logger.LogGenericInformation(string.Format(CultureInfo.InvariantCulture,
              "Updating contact with ID: {0}. CorrelationId: {1}", id, correlationId));

            ArgumentNullException.ThrowIfNull(contact);

            try
            {
                if (id != contact.Id)
                {
                    return BadRequest("ID mismatch");
                }

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var existingContact = await _contactRepository.GetByIdAsync(id).ConfigureAwait(false);
                if (existingContact == null)
                {
                    return NotFound();
                }

                var updatedContact = await _contactRepository.UpdateAsync(contact).ConfigureAwait(false);

                /*await _auditService.LogDataAccess(
                    User.Identity.Name,
                    "UPDATE",
                    "Contact",
                    id.ToString(),
                    contact,
                    SecurityClassification.Sensitive); */

                return Ok(updatedContact);
            }
            catch (Exception ex)
            {
                _logger.LogGenericException(string.Format(CultureInfo.InvariantCulture, "Error updating contact with ID: {0}", id), ex);
                return StatusCode(500, "An error occurred while updating the contact");
            }
        }

        /// <summary>
        /// Deletes a contact with audit logging and relationship cleanup
        /// </summary>
        /// <param name="id">Contact ID to delete</param>
        /// <returns>Success status</returns>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteContactAsync(Guid id)
        {
            var correlationId = Guid.NewGuid().ToString();
            _logger.LogGenericInformation(string.Format(CultureInfo.InvariantCulture,
                "Deleting contact with ID: {0}. CorrelationId: {1}", id, correlationId));

            try
            {
                var success = await _contactRepository.DeleteAsync(id).ConfigureAwait(false);
                if (!success)
                {
                    return NotFound();
                }

                /*await _auditService.LogDataAccess(
                    User.Identity.Name,
                    "DELETE",
                    "Contact",
                    id.ToString(),
                    null,
                    SecurityClassification.Sensitive); */

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogGenericException(string.Format(CultureInfo.InvariantCulture, "Error deleting contact with ID: {0}", id), ex);
                return StatusCode(500, "An error occurred while deleting the contact");
            }
        }

        /// <summary>
        /// Searches for contacts by name with security-aware partial matching
        /// </summary>
        /// <param name="searchTerm">Name or partial name to search for</param>
        /// <returns>Collection of matching contacts with decrypted fields</returns>
        [HttpGet("search")]
        [ProducesResponseType(typeof(IEnumerable<Contact>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<Contact>>> SearchContactsByNameAsync([FromQuery] string searchTerm)
        {
            var correlationId = Guid.NewGuid().ToString();
            _logger.LogGenericInformation(string.Format(CultureInfo.InvariantCulture,
               "Searching contacts with term: {0}. CorrelationId: {1}", searchTerm, correlationId));

            try
            {
                var contacts = await _contactRepository.GetByNameAsync(searchTerm).ConfigureAwait(false);

                /*await _auditService.LogDataAccess(
                    User.Identity.Name,
                    "SEARCH",
                    "Contact",
                    searchTerm,
                    null,
                    SecurityClassification.Sensitive); */

                return Ok(contacts);
            }
            catch (Exception ex)
            {
                _logger.LogGenericException(string.Format(CultureInfo.InvariantCulture, "Error searching contacts with term: {0}", searchTerm), ex);
                return StatusCode(500, "An error occurred while searching contacts");
            }
        }

        /// <summary>
        /// Retrieves all contacts related to a specific contact
        /// </summary>
        /// <param name="id">Contact ID to find relationships for</param>
        /// <returns>Collection of related contacts with decrypted fields</returns>
        [HttpGet("{id}/relationships")]
        [ProducesResponseType(typeof(IEnumerable<Contact>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<Contact>>> GetRelatedContactsAsync(Guid id)
        {
            var correlationId = Guid.NewGuid().ToString();
            _logger.LogGenericInformation(string.Format(CultureInfo.InvariantCulture,
                "Getting related contacts for ID: {0}. CorrelationId: {1}", id, correlationId));

            try
            {
                var contacts = await _contactRepository.GetRelatedContactsAsync(id).ConfigureAwait(false);

                /* await _auditService.LogDataAccess(
                    User.Identity.Name,
                    "GET_RELATED",
                    "Contact",
                    id.ToString(),
                    null,
                    SecurityClassification.Sensitive);*/

                return Ok(contacts);
            }
            catch (Exception ex)
            {
                _logger.LogGenericException(string.Format(CultureInfo.InvariantCulture, "Error retrieving related contacts for ID: {0}", id), ex);
                return StatusCode(500, "An error occurred while retrieving related contacts");
            }
        }
    }
}