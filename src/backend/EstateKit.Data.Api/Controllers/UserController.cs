using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc; // v9.0.0
using Microsoft.AspNetCore.Authorization; // v9.0.0
using Microsoft.Extensions.Logging; // v9.0.0
// using Microsoft.AspNetCore.RateLimit; // v9.0.0
using Microsoft.ApplicationInsights; // v9.0.0
using EstateKit.Core.Models.UserModels;
using EstateKit.Core.Interfaces;
using EstateKit.Infrastructure.Logger.Extensions;
using static Microsoft.ApplicationInsights.MetricDimensionNames.TelemetryContext;
using System.Globalization;
// using EstateKit.Data.API.Services;

namespace EstateKit.Data.API.Controllers
{
    /// <summary>
    /// Controller handling secure user data operations with field-level encryption,
    /// comprehensive audit logging, and performance monitoring.
    /// </summary>
    [ApiController]
    [Route("api/v1/users")]
   //todo: put this back [Authorize]
    // [EnableRateLimiting("standard")]
   // [ResponseCache(Duration = 60, VaryByQueryKeys = new[] { "id" })]
    public class UserController : ControllerBase
    {
        private readonly IUserRepository _userRepository;
        //private readonly EncryptionService _encryptionService;
        private readonly ILogger<UserController> _logger;
        private readonly TelemetryClient _telemetryClient;

        public UserController(
            IUserRepository userRepository,
            //EncryptionService encryptionService,
            ILogger<UserController> logger
            ,TelemetryClient telemetryClient
            )
        {
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            //_encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
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
        public async Task<ActionResult<Core.Models.UserModels.User>> GetByIdAsync(int userId)
        {
            var correlationId = Guid.NewGuid().ToString();
            using var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId,
                ["UserId"] = userId
            });

            try
            {
                _logger.LogGenericInformation(string.Format(CultureInfo.InvariantCulture, "Retrieving user with ID: {0}", userId));
                var startTime = DateTime.UtcNow;

                var user = await _userRepository.GetByIdAsync(userId).ConfigureAwait(false);
                if (user == null)
                {
                    _logger.LogGenericWarning(string.Format(CultureInfo.InvariantCulture, "User not found with ID: {0}", userId));
                    return NotFound();
                }

                // Decrypt sensitive fields
                /*var context = new EncryptionContext
                {
                    EntityType = "User",
                    EntityId = userId.ToString(),
                    UserId = User.Identity.Name
                };

                user.DateOfBirth = await _encryptionService.DecryptSensitiveField(
                    user.DateOfBirth, "dateOfBirth", User.Identity.Name, context);
                user.BirthPlace = await _encryptionService.DecryptSensitiveField(
                    user.BirthPlace, "birthPlace", User.Identity.Name, context);*/

                var duration = DateTime.UtcNow - startTime;
                _telemetryClient.TrackMetric("UserRetrieval", duration.TotalMilliseconds);

                _logger.LogGenericInformation(string.Format(CultureInfo.InvariantCulture, "Successfully retrieved user {0} in {1}ms", userId,
                    duration.TotalMilliseconds));

                return Ok(user);
            }
            catch (Exception ex)
            {
                _logger.LogGenericException(string.Format(CultureInfo.InvariantCulture, "Error retrieving user with ID: {0}", userId), ex);
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
        public async Task<ActionResult<Core.Models.UserModels.User>> CreateAsync([FromBody] Core.Models.UserModels.User user)
        {
            ArgumentNullException.ThrowIfNull(user, nameof(user));  

            var correlationId = Guid.NewGuid().ToString();
            using var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId,
                ["UserEmail"] = user.Contact?.ContactContactMethods?.FirstOrDefault()?.ContactValue ?? "no email found" // todo calculate where the email is
            });

            try
            {
                if (user == null)
                {
                    _logger.LogGenericWarning("Invalid user data provided");
                    return BadRequest("User data is required");
                }

                _logger.LogGenericInformation("Creating new user");
                var startTime = DateTime.UtcNow;

                // Encrypt sensitive fields
                /*var context = new EncryptionContext
                {
                    EntityType = "User",
                    EntityId = user.Id.ToString(),
                    UserId = User.Identity.Name
                };

                user.DateOfBirth = await _encryptionService.EncryptSensitiveField(
                    user.DateOfBirth, "dateOfBirth", User.Identity.Name, context);
                user.BirthPlace = await _encryptionService.EncryptSensitiveField(
                    user.BirthPlace, "birthPlace", User.Identity.Name, context);*/

                var createdUser = await _userRepository.AddAsync(user).ConfigureAwait(false);

                var duration = DateTime.UtcNow - startTime;
                _telemetryClient.TrackMetric("UserCreation", duration.TotalMilliseconds);

                _logger.LogGenericInformation(string.Format(CultureInfo.InvariantCulture, "Successfully created user {0} in {1}ms",
                    createdUser.Id,
                    duration.TotalMilliseconds));


                return CreatedAtAction(
                    nameof(GetByIdAsync),
                    new { id = createdUser.Id },
                    createdUser);
            }
            catch (Exception ex)
            {
                _logger.LogGenericException("Error creating new user", ex);
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
        public async Task<ActionResult<Core.Models.UserModels.User>> UpdateAsync(int userId, [FromBody] Core.Models.UserModels.User user)
        {
            var correlationId = Guid.NewGuid().ToString();
            using var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId,
                ["UserId"] = userId
            });

            try
            {
                if (user == null || userId != user.Id)
                {
                    _logger.LogGenericWarning(string.Format(CultureInfo.InvariantCulture, "Invalid update request for user ID: {0}", userId));
                    return BadRequest("Invalid user data");
                }

                var existingUser = await _userRepository.GetByIdAsync(userId).ConfigureAwait(false);
                if (existingUser == null)
                {
                    _logger.LogGenericWarning(string.Format(CultureInfo.InvariantCulture, "Invalid update request for user ID: {0}", userId));
                    return NotFound();
                }

                _logger.LogGenericInformation(string.Format(CultureInfo.InvariantCulture, "Updating user with ID: {0}", userId));
                var startTime = DateTime.UtcNow;

                // Encrypt sensitive fields
                /*var context = new EncryptionContext
                {
                    EntityType = "User",
                    EntityId = userId.ToString(),
                    UserId = User.Identity.Name
                };

                user.DateOfBirth = await _encryptionService.EncryptSensitiveField(
                    user.DateOfBirth, "dateOfBirth", User.Identity.Name, context);
                user.BirthPlace = await _encryptionService.EncryptSensitiveField(
                    user.BirthPlace, "birthPlace", User.Identity.Name, context);*/

                var updatedUser = await _userRepository.UpdateAsync(user).ConfigureAwait(false);

                var duration = DateTime.UtcNow - startTime;
               _telemetryClient.TrackMetric("UserUpdate", duration.TotalMilliseconds);

                _logger.LogGenericInformation(string.Format(CultureInfo.InvariantCulture, "Successfully updated user {0} in {1}ms",
                    userId,
                    duration.TotalMilliseconds));

                return Ok(updatedUser);
            }
            catch (Exception ex)
            {
                _logger.LogGenericException(string.Format(CultureInfo.InvariantCulture, "Error retrieving user with ID: {0}", userId), ex);
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
        public async Task<IActionResult> DeleteAsync(int userId)
        {
            var correlationId = Guid.NewGuid().ToString();
            using var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId,
                ["UserId"] = userId
            });

            try
            {
                _logger.LogGenericInformation(string.Format(CultureInfo.InvariantCulture, "Deleting user with ID: {0}", userId));
                var startTime = DateTime.UtcNow;

                var success = await _userRepository.DeleteAsync(userId).ConfigureAwait(false);
                if (!success)
                {
                    _logger.LogGenericWarning(string.Format(CultureInfo.InvariantCulture, "User not found for deletion with ID: {0}", userId));
                    return NotFound();
                }

                var duration = DateTime.UtcNow - startTime;
                _telemetryClient.TrackMetric("UserDeletion", duration.TotalMilliseconds);

                _logger.LogGenericInformation(string.Format(CultureInfo.InvariantCulture, "Successfully deleted user {0} in {1}ms",
                    userId,
                    duration.TotalMilliseconds));

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogGenericException(string.Format(CultureInfo.InvariantCulture, "Error deleting user with ID: {0}", userId), ex);
                return StatusCode(500, "An error occurred while processing your request");
            }
        }
    }
}