using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc; // v9.0.0
using Microsoft.AspNetCore.Authorization; // v9.0.0
using Microsoft.Extensions.Logging; // v9.0.0
using Microsoft.AspNetCore.RateLimit; // v9.0.0
using Microsoft.ApplicationInsights; // v9.0.0
using EstateKit.Core.Entities;
using EstateKit.Core.Interfaces;
using EstateKit.Data.API.Services;

namespace EstateKit.Data.API.Controllers
{
    /// <summary>
    /// Controller handling secure user data operations with field-level encryption,
    /// comprehensive audit logging, and performance monitoring.
    /// </summary>
    [ApiController]
    [Route("api/v1/users")]
    [Authorize]
    [EnableRateLimiting("standard")]
    [ResponseCache(Duration = 60, VaryByQueryKeys = new[] { "id" })]
    public class UserController : ControllerBase
    {
        private readonly IUserRepository _userRepository;
        private readonly EncryptionService _encryptionService;
        private readonly ILogger<UserController> _logger;
        private readonly TelemetryClient _telemetryClient;

        public UserController(
            IUserRepository userRepository,
            EncryptionService encryptionService,
            ILogger<UserController> logger,
            TelemetryClient telemetryClient)
        {
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
        }

        /// <summary>
        /// Retrieves a user by ID with decrypted sensitive fields
        /// </summary>
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<User>> GetByIdAsync(Guid id)
        {
            var correlationId = Guid.NewGuid().ToString();
            using var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId,
                ["UserId"] = id
            });

            try
            {
                _logger.LogInformation("Retrieving user with ID: {UserId}", id);
                var startTime = DateTime.UtcNow;

                var user = await _userRepository.GetByIdAsync(id);
                if (user == null)
                {
                    _logger.LogWarning("User not found with ID: {UserId}", id);
                    return NotFound();
                }

                // Decrypt sensitive fields
                var context = new EncryptionContext
                {
                    EntityType = "User",
                    EntityId = id.ToString(),
                    UserId = User.Identity.Name
                };

                user.DateOfBirth = await _encryptionService.DecryptSensitiveField(
                    user.DateOfBirth, "dateOfBirth", User.Identity.Name, context);
                user.BirthPlace = await _encryptionService.DecryptSensitiveField(
                    user.BirthPlace, "birthPlace", User.Identity.Name, context);

                var duration = DateTime.UtcNow - startTime;
                _telemetryClient.TrackMetric("UserRetrieval", duration.TotalMilliseconds);

                _logger.LogInformation(
                    "Successfully retrieved user {UserId} in {Duration}ms",
                    id,
                    duration.TotalMilliseconds);

                return Ok(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user with ID: {UserId}", id);
                return StatusCode(500, "An error occurred while processing your request");
            }
        }

        /// <summary>
        /// Creates a new user with encrypted sensitive fields
        /// </summary>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<User>> CreateAsync([FromBody] User user)
        {
            var correlationId = Guid.NewGuid().ToString();
            using var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId,
                ["UserEmail"] = user.Contact?.ContactMethods?.FirstOrDefault()?.Value
            });

            try
            {
                if (user == null)
                {
                    _logger.LogWarning("Invalid user data provided");
                    return BadRequest("User data is required");
                }

                _logger.LogInformation("Creating new user");
                var startTime = DateTime.UtcNow;

                // Encrypt sensitive fields
                var context = new EncryptionContext
                {
                    EntityType = "User",
                    EntityId = user.Id.ToString(),
                    UserId = User.Identity.Name
                };

                user.DateOfBirth = await _encryptionService.EncryptSensitiveField(
                    user.DateOfBirth, "dateOfBirth", User.Identity.Name, context);
                user.BirthPlace = await _encryptionService.EncryptSensitiveField(
                    user.BirthPlace, "birthPlace", User.Identity.Name, context);

                var createdUser = await _userRepository.AddAsync(user);

                var duration = DateTime.UtcNow - startTime;
                _telemetryClient.TrackMetric("UserCreation", duration.TotalMilliseconds);

                _logger.LogInformation(
                    "Successfully created user {UserId} in {Duration}ms",
                    createdUser.Id,
                    duration.TotalMilliseconds);

                return CreatedAtAction(
                    nameof(GetByIdAsync),
                    new { id = createdUser.Id },
                    createdUser);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating new user");
                return StatusCode(500, "An error occurred while processing your request");
            }
        }

        /// <summary>
        /// Updates an existing user with encrypted sensitive fields
        /// </summary>
        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<User>> UpdateAsync(Guid id, [FromBody] User user)
        {
            var correlationId = Guid.NewGuid().ToString();
            using var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId,
                ["UserId"] = id
            });

            try
            {
                if (user == null || id != user.Id)
                {
                    _logger.LogWarning("Invalid update request for user ID: {UserId}", id);
                    return BadRequest("Invalid user data");
                }

                var existingUser = await _userRepository.GetByIdAsync(id);
                if (existingUser == null)
                {
                    _logger.LogWarning("User not found for update with ID: {UserId}", id);
                    return NotFound();
                }

                _logger.LogInformation("Updating user with ID: {UserId}", id);
                var startTime = DateTime.UtcNow;

                // Encrypt sensitive fields
                var context = new EncryptionContext
                {
                    EntityType = "User",
                    EntityId = id.ToString(),
                    UserId = User.Identity.Name
                };

                user.DateOfBirth = await _encryptionService.EncryptSensitiveField(
                    user.DateOfBirth, "dateOfBirth", User.Identity.Name, context);
                user.BirthPlace = await _encryptionService.EncryptSensitiveField(
                    user.BirthPlace, "birthPlace", User.Identity.Name, context);

                var updatedUser = await _userRepository.UpdateAsync(user);

                var duration = DateTime.UtcNow - startTime;
                _telemetryClient.TrackMetric("UserUpdate", duration.TotalMilliseconds);

                _logger.LogInformation(
                    "Successfully updated user {UserId} in {Duration}ms",
                    id,
                    duration.TotalMilliseconds);

                return Ok(updatedUser);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user with ID: {UserId}", id);
                return StatusCode(500, "An error occurred while processing your request");
            }
        }

        /// <summary>
        /// Soft deletes a user with audit logging
        /// </summary>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteAsync(Guid id)
        {
            var correlationId = Guid.NewGuid().ToString();
            using var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId,
                ["UserId"] = id
            });

            try
            {
                _logger.LogInformation("Deleting user with ID: {UserId}", id);
                var startTime = DateTime.UtcNow;

                var success = await _userRepository.DeleteAsync(id);
                if (!success)
                {
                    _logger.LogWarning("User not found for deletion with ID: {UserId}", id);
                    return NotFound();
                }

                var duration = DateTime.UtcNow - startTime;
                _telemetryClient.TrackMetric("UserDeletion", duration.TotalMilliseconds);

                _logger.LogInformation(
                    "Successfully deleted user {UserId} in {Duration}ms",
                    id,
                    duration.TotalMilliseconds);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user with ID: {UserId}", id);
                return StatusCode(500, "An error occurred while processing your request");
            }
        }
    }
}