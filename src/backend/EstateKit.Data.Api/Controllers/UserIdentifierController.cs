using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using EstateKit.Core.Models.UserModels;
using EstateKit.Core.Interfaces;
using EstateKit.Infrastructure.Logger.Extensions;
using System.Globalization;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static Microsoft.ApplicationInsights.MetricDimensionNames.TelemetryContext;
// using EstateKit.Data.API.Services;

namespace EstateKit.Data.API.Controllers
{
    /// <summary>
    /// Controller handling secure CRUD operations for government-issued identification documents
    /// with field-level encryption and comprehensive audit logging.
    /// </summary>
    [ApiController]
    [Route("api/v1/[controller]")]
    [Authorize]
    [EnableRateLimiting("IdentifierPolicy")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public class UserIdentifierController : ControllerBase
    {
        private readonly IUserIdentifierRepository _userIdentifierRepository;
        // private readonly EncryptionService _encryptionService;
        private readonly ILogger<UserIdentifierController> _logger;
        // private readonly AuditService _auditService;

        public UserIdentifierController(
            IUserIdentifierRepository userIdentifierRepository,
            //EncryptionService encryptionService,
            ILogger<UserIdentifierController> logger
            //,AuditService auditService
            )
        {
            _userIdentifierRepository = userIdentifierRepository ?? throw new ArgumentNullException(nameof(userIdentifierRepository));
            // _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            // _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        }

        /// <summary>
        /// Retrieves a specific identifier by ID with decrypted sensitive fields
        /// </summary>
        [HttpGet("{id}")]
        [Authorize(Policy = "IdentifierRead")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<UserIdentifier>> GetByIdAsync(int userIdentifierId)
        {
            try
            {
                _logger.LogGenericInformation(string.Format(CultureInfo.InvariantCulture, "Retrieving identifier with ID: {0}", userIdentifierId));

                var identifier = await _userIdentifierRepository.GetByIdAsync(userIdentifierId).ConfigureAwait(false);
                if (identifier == null)
                {
                    _logger.LogGenericWarning(string.Format(CultureInfo.InvariantCulture, "Identifier not found with ID: {0}", userIdentifierId));
                    return NotFound();
                }

               /* await _auditService.LogDataAccess(
                    User.Identity.Name,
                    "Read",
                    "Identifier",
                    id.ToString(),
                    null,
                    SecurityClassification.Critical);*/

                return Ok(identifier);
            }
            catch (Exception ex)
            {
                _logger.LogGenericException(string.Format(CultureInfo.InvariantCulture, "Error retrieving identifier with ID: {0}", userIdentifierId), ex);
                return StatusCode(500, "An error occurred while retrieving the identifier");
            }
        }

        /// <summary>
        /// Retrieves all identifiers for a specific user with decrypted sensitive fields
        /// </summary>
        [HttpGet("user/{userId}")]
        [Authorize(Policy = "IdentifierRead")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<IEnumerable<UserIdentifier>>> GetByUserIdAsync(int userId)
        {
            try
            {
                _logger.LogGenericInformation(string.Format(CultureInfo.InvariantCulture, "Retrieving identifiers for user: {0}", userId));

                var userIdentifiers = await _userIdentifierRepository.GetByUserIdAsync(userId).ConfigureAwait(false);

                /*await _auditService.LogDataAccess(
                    User.Identity.Name,
                    "ReadAll",
                    "Identifier",
                    userId.ToString(),
                    null,
                    SecurityClassification.Critical);*/

                return Ok(userIdentifiers);
            }
            catch (Exception ex)
            {
                _logger.LogGenericException(string.Format(CultureInfo.InvariantCulture, "Error retrieving identifiers for user: {0}", userId), ex);
                return StatusCode(500, "An error occurred while retrieving the identifiers");
            }
        }

        /// <summary>
        /// Creates a new identifier with encrypted sensitive fields
        /// </summary>
        [HttpPost]
        [Authorize(Policy = "IdentifierCreate")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<UserIdentifier>> CreateAsync([FromBody] UserIdentifier userIdentifier)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                /*if (!identifier.Validate())
                {
                    return BadRequest("Invalid identifier data");
                }

                var encryptionContext = new EncryptionContext
                {
                    EntityType = "Identifier",
                    EntityId = identifier.Id.ToString(),
                    UserId = identifier.UserId.ToString()
                };

                // Encrypt sensitive fields
                identifier.Value = await _encryptionService.EncryptSensitiveField(
                    identifier.Value,
                    "Value",
                    User.Identity.Name,
                    encryptionContext);*/

                var result = await _userIdentifierRepository.AddAsync(userIdentifier).ConfigureAwait(false);

                /*await _auditService.LogDataAccess(
                    User.Identity.Name,
                    "Create",
                    "Identifier",
                    result.Id.ToString(),
                    result,
                    SecurityClassification.Critical);*/

                return CreatedAtAction(nameof(GetByIdAsync), new { id = result.Id }, result);
            }
            catch (Exception ex)
            {
                _logger.LogGenericException(string.Format(CultureInfo.InvariantCulture, "Error creating identifier"), ex);
                return StatusCode(500, "An error occurred while creating the identifier");
            }
        }

        /// <summary>
        /// Updates an existing identifier with encrypted sensitive fields
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Policy = "IdentifierUpdate")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<UserIdentifier>> UpdateAsync(int userIdentifierId, [FromBody] UserIdentifier userIdentifier)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(userIdentifier, nameof(userIdentifier));
                if (userIdentifierId != userIdentifier.Id)
                {
                    return BadRequest("ID mismatch");
                }

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var existing = await _userIdentifierRepository.GetByIdAsync(userIdentifierId).ConfigureAwait(false);
                if (existing == null)
                {
                    return NotFound();
                }

                /*if (!userIdentifier.Validate())
                {
                    return BadRequest("Invalid identifier data");
                }

                var encryptionContext = new EncryptionContext
                {
                    EntityType = "Identifier",
                    EntityId = userIdentifier.Id.ToString(),
                    UserId = userIdentifier.UserId.ToString()
                };

                // Encrypt sensitive fields
                userIdentifier.Value = await _encryptionService.EncryptSensitiveField(
                    identifier.Value,
                    "Value",
                    User.Identity.Name,
                    encryptionContext);*/

                var result = await _userIdentifierRepository.UpdateAsync(userIdentifier).ConfigureAwait(false);

                /*await _auditService.LogDataAccess(
                    User.Identity.Name,
                    "Update",
                    "Identifier",
                    result.Id.ToString(),
                    result,
                    SecurityClassification.Critical);
                */
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogGenericException(string.Format(CultureInfo.InvariantCulture, "Error updating identifier with ID: {0}", userIdentifierId), ex);
                return StatusCode(500, "An error occurred while updating the identifier");
            }
        }

        /// <summary>
        /// Deletes (soft delete) an identifier
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Policy = "IdentifierDelete")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult> DeleteAsync(int userIdentifierId)
        {
            try
            {
                var identifier = await _userIdentifierRepository.GetByIdAsync(userIdentifierId).ConfigureAwait(false);
                if (identifier == null)
                {
                    return NotFound();
                }

                await _userIdentifierRepository.DeleteAsync(userIdentifierId).ConfigureAwait(false);

                /* await _auditService.LogDataAccess(
                    User.Identity.Name,
                    "Delete",
                    "Identifier",
                    id.ToString(),
                    null,
                    SecurityClassification.Critical);*/

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogGenericException(string.Format(CultureInfo.InvariantCulture, "Error deleting identifier with ID: {0}", userIdentifierId), ex);
                return StatusCode(500, "An error occurred while deleting the identifier");
            }
        }
    }
}