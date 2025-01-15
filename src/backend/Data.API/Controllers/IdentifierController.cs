using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using EstateKit.Core.Entities;
using EstateKit.Core.Interfaces;
using EstateKit.Data.API.Services;

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
    public class IdentifierController : ControllerBase
    {
        private readonly IIdentifierRepository _repository;
        private readonly EncryptionService _encryptionService;
        private readonly ILogger<IdentifierController> _logger;
        private readonly AuditService _auditService;

        public IdentifierController(
            IIdentifierRepository repository,
            EncryptionService encryptionService,
            ILogger<IdentifierController> logger,
            AuditService auditService)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        }

        /// <summary>
        /// Retrieves a specific identifier by ID with decrypted sensitive fields
        /// </summary>
        [HttpGet("{id}")]
        [Authorize(Policy = "IdentifierRead")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<Identifier>> GetByIdAsync(Guid id)
        {
            try
            {
                _logger.LogInformation("Retrieving identifier with ID: {Id}", id);

                var identifier = await _repository.GetByIdAsync(id);
                if (identifier == null)
                {
                    _logger.LogWarning("Identifier not found with ID: {Id}", id);
                    return NotFound();
                }

                await _auditService.LogDataAccess(
                    User.Identity.Name,
                    "Read",
                    "Identifier",
                    id.ToString(),
                    null,
                    SecurityClassification.Critical);

                return Ok(identifier);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving identifier with ID: {Id}", id);
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
        public async Task<ActionResult<IEnumerable<Identifier>>> GetByUserIdAsync(Guid userId)
        {
            try
            {
                _logger.LogInformation("Retrieving identifiers for user: {UserId}", userId);

                var identifiers = await _repository.GetByUserIdAsync(userId);

                await _auditService.LogDataAccess(
                    User.Identity.Name,
                    "ReadAll",
                    "Identifier",
                    userId.ToString(),
                    null,
                    SecurityClassification.Critical);

                return Ok(identifiers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving identifiers for user: {UserId}", userId);
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
        public async Task<ActionResult<Identifier>> CreateAsync([FromBody] Identifier identifier)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                if (!identifier.Validate())
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
                    encryptionContext);

                var result = await _repository.AddAsync(identifier);

                await _auditService.LogDataAccess(
                    User.Identity.Name,
                    "Create",
                    "Identifier",
                    result.Id.ToString(),
                    result,
                    SecurityClassification.Critical);

                return CreatedAtAction(nameof(GetByIdAsync), new { id = result.Id }, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating identifier");
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
        public async Task<ActionResult<Identifier>> UpdateAsync(Guid id, [FromBody] Identifier identifier)
        {
            try
            {
                if (id != identifier.Id)
                {
                    return BadRequest("ID mismatch");
                }

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var existing = await _repository.GetByIdAsync(id);
                if (existing == null)
                {
                    return NotFound();
                }

                if (!identifier.Validate())
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
                    encryptionContext);

                var result = await _repository.UpdateAsync(identifier);

                await _auditService.LogDataAccess(
                    User.Identity.Name,
                    "Update",
                    "Identifier",
                    result.Id.ToString(),
                    result,
                    SecurityClassification.Critical);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating identifier with ID: {Id}", id);
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
        public async Task<ActionResult> DeleteAsync(Guid id)
        {
            try
            {
                var identifier = await _repository.GetByIdAsync(id);
                if (identifier == null)
                {
                    return NotFound();
                }

                await _repository.DeleteAsync(id);

                await _auditService.LogDataAccess(
                    User.Identity.Name,
                    "Delete",
                    "Identifier",
                    id.ToString(),
                    null,
                    SecurityClassification.Critical);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting identifier with ID: {Id}", id);
                return StatusCode(500, "An error occurred while deleting the identifier");
            }
        }
    }
}