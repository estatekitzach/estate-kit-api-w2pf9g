using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using EstateKit.Core.Models.UserModels ;
using EstateKit.Core.Interfaces;
using EstateKit.Infrastructure.Logger.Extensions;
using System.Globalization;
using static Microsoft.ApplicationInsights.MetricDimensionNames.TelemetryContext;
// using EstateKit.Data.API.Services;

namespace EstateKit.Data.API.Controllers
{
    /// <summary>
    /// REST API controller for secure document management with field-level encryption,
    /// comprehensive audit logging, and high-performance caching.
    /// </summary>
    [ApiController]
    [Route("api/v1/[controller]")]
    [Authorize(Policy = "DocumentAccess")]
    [EnableRateLimiting("document-policy")]
    public class UserDocumentController : ControllerBase
    {
        private readonly IUserDocumentRepository _userDocumentRepository;
        //private readonly EncryptionService _encryptionService;
        //private readonly AuditService _auditService;
        //private readonly IValidator<Document> _validator;
        private readonly IMemoryCache _cache;
        private readonly ILogger<UserDocumentController> _logger;

        private const int CACHE_DURATION_MINUTES = 30;
        private const string CACHE_KEY_PREFIX = "doc_";

        public UserDocumentController(
            IUserDocumentRepository userDocumentRepository,
            //EncryptionService encryptionService,
            //AuditService auditService,
            //IValidator<Document> validator,
            IMemoryCache cache,
            ILogger<UserDocumentController> logger)
        {
            _userDocumentRepository = userDocumentRepository ?? throw new ArgumentNullException(nameof(userDocumentRepository));
            //_encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
            //_auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
            //_validator = validator ?? throw new ArgumentNullException(nameof(validator));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Retrieves a document by ID with field-level decryption and audit logging
        /// </summary>
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ResponseCache(Duration = CACHE_DURATION_MINUTES)]
        public async Task<ActionResult<UserDocument>> GetDocumentAsync(int userDocumentId)
        {
            try
            {
                var cacheKey = $"{CACHE_KEY_PREFIX}{userDocumentId}";
                if (_cache.TryGetValue(cacheKey, out var cachedDocument))
                {
                    /* await _auditService.LogDataAccess(
                        userId,
                        "GetDocument",
                        "Document",
                        id.ToString(),
                        null,
                        SecurityClassification.Critical); */
                    return Ok(cachedDocument as UserDocument);
                }

                var userDocument = await _userDocumentRepository.GetByIdAsync(userDocumentId).ConfigureAwait(false);
                if (userDocument == null)
                {
                    return NotFound();
                }

                /* todo check if the user has access to the document 
                 * if (document.UserId.ToString() != userId)
                {
                    await _auditService.LogSecurityEvent(
                        userId,
                        SecurityEventType.UnauthorizedAccess,
                        $"Attempted access to document {id}",
                        new SecurityContext { DocumentId = id },
                        SecuritySeverity.High);
                    return Forbid();
                }

                // Decrypt sensitive fields
                var context = new EncryptionContext
                {
                    EntityType = "Document",
                    EntityId = id.ToString(),
                    UserId = userId
                };

                document = await DecryptDocumentFields(document, context); */

                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetSlidingExpiration(TimeSpan.FromMinutes(CACHE_DURATION_MINUTES))
                    .SetPriority(CacheItemPriority.High);

                _cache.Set(cacheKey, userDocument, cacheOptions);

                /*await _auditService.LogDataAccess(
                    userId,
                    "GetDocument",
                    "Document",
                    id.ToString(),
                    null,
                    SecurityClassification.Critical); */

                return Ok(userDocument);
            }
            catch (Exception ex)
            {
                _logger.LogGenericException(string.Format(CultureInfo.InvariantCulture, "Error retrieving document {0}", userDocumentId), ex);
                throw;
            }
        }

        /// <summary>
        /// Retrieves all documents for a user with field-level decryption
        /// </summary>
        [HttpGet("user/{userId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ResponseCache(Duration = CACHE_DURATION_MINUTES)]
        public async Task<ActionResult<IEnumerable<UserDocument>>> GetUserDocumentsAsync(int userId)
        {
            try
            {
                /* todo: check if user id has access to this document 
                 * var currentUserId = User.FindFirst("sub")?.Value;
                if (string.IsNullOrEmpty(currentUserId) || currentUserId != userId.ToString())
                {
                    return Forbid();
                } */

                /* todo: should we cache documents?
                 * var cacheKey = $"{CACHE_KEY_PREFIX}user_{userId}";
                if (_cache.TryGetValue(cacheKey, out IEnumerable<UserDocument> cachedDocuments))
                {
                    await _auditService.LogDataAccess(
                        currentUserId,
                        "GetUserDocuments",
                        "Document",
                        userId.ToString(),
                        null,
                        SecurityClassification.Critical);
                    return Ok(cachedDocuments);
                }*/

                var userDocuments = await _userDocumentRepository.GetByUserIdAsync(userId).ConfigureAwait(false);
                /*var context = new EncryptionContext
                {
                    EntityType = "Document",
                    UserId = currentUserId
                };

                var decryptedDocuments = new List<Document>();
                foreach (var document in documents)
                {
                    context.EntityId = document.Id.ToString();
                    var decryptedDoc = await DecryptDocumentFields(document, context);
                    decryptedDocuments.Add(decryptedDoc);
                }

                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetSlidingExpiration(TimeSpan.FromMinutes(CACHE_DURATION_MINUTES))
                    .SetPriority(CacheItemPriority.High);

                _cache.Set(cacheKey, decryptedDocuments, cacheOptions);

                await _auditService.LogDataAccess(
                    currentUserId,
                    "GetUserDocuments",
                    "Document",
                    userId.ToString(),
                    null,
                    SecurityClassification.Critical);*/

                return Ok(userDocuments);
            }
            catch (Exception ex)
            {
                _logger.LogGenericException(string.Format(CultureInfo.InvariantCulture, "Error retrieving documents for user {0}", userId), ex);
                throw;
            }
        }

        /// <summary>
        /// Creates a new document with field-level encryption and validation
        /// </summary>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<UserDocument>> CreateDocumentAsync([FromBody] UserDocument userDocument)
        {
            try
            {
                /* todo check for user access
                 * var userId = User.FindFirst("sub")?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Forbid();
                }

                /*var validationResult = await _validator.ValidateAsync(document);
                if (!validationResult.IsValid)
                {
                    return BadRequest(validationResult.Errors);
                }*/

                /*var context = new EncryptionContext
                {
                    EntityType = "Document",
                    EntityId = document.Id.ToString(),
                    UserId = userId
                };

                document = await EncryptDocumentFields(document, context);*/
                var createdDocument = await _userDocumentRepository.AddAsync(userDocument).ConfigureAwait(false);

               /*wait _auditService.LogDataAccess(
                    userId,
                    "CreateDocument",
                    "Document",
                    createdDocument.Id.ToString(),
                    document,
                    SecurityClassification.Critical); */

                return CreatedAtAction(
                    nameof(GetDocumentAsync),
                    new { id = createdDocument.Id },
                    createdDocument);
            }
            catch (Exception ex)
            {
                _logger.LogGenericException(string.Format(CultureInfo.InvariantCulture, "Error creating document"), ex);
                throw;
            }
        }

        /// <summary>
        /// Updates an existing document with field-level encryption and validation
        /// </summary>
        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<UserDocument>> UpdateDocumentAsync(int userDocumentId, [FromBody] UserDocument userDocument)
        {
            try
            {
                var userId = User.FindFirst("sub")?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Forbid();
                }

                var existingDocument = await _userDocumentRepository.GetByIdAsync(userDocumentId).ConfigureAwait(false);
                if (existingDocument == null)
                {
                    return NotFound();
                }

                /* if (existingDocument.UserId.ToString() != userId)
                {
                    await _auditService.LogSecurityEvent(
                        userId,
                        SecurityEventType.UnauthorizedAccess,
                        $"Attempted update of document {id}",
                        new SecurityContext { DocumentId = id },
                        SecuritySeverity.High);
                    return Forbid();
                } 

                var validationResult = await _validator.ValidateAsync(document);
                if (!validationResult.IsValid)
                {
                    return BadRequest(validationResult.Errors);
                }

                var context = new EncryptionContext
                {
                    EntityType = "Document",
                    EntityId = id.ToString(),
                    UserId = userId
                };

                document = await EncryptDocumentFields(document, context);*/
                await _userDocumentRepository.UpdateAsync(userDocument).ConfigureAwait(false);

                _cache.Remove($"{CACHE_KEY_PREFIX}{userDocumentId}");

                /* await _auditService.LogDataAccess(
                    userId,
                    "UpdateDocument",
                    "Document",
                    id.ToString(),
                    document,
                    SecurityClassification.Critical); */

                return Ok(userDocument);
            }
            catch (Exception ex)
            {
                _logger.LogGenericException(string.Format(CultureInfo.InvariantCulture, "Error updating document {0}", userDocumentId), ex);
                throw;
            }
        }

        /// <summary>
        /// Deletes a document with security validation and audit logging
        /// </summary>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> DeleteDocumentAsync(int userDocumentId)
        {
            try
            {
                var userId = User.FindFirst("sub")?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Forbid();
                }

                var document = await _userDocumentRepository.GetByIdAsync(userDocumentId).ConfigureAwait(false);
                if (document == null)
                {
                    return NotFound();
                }

                /* todo: have check if the user has access
                 * if (document.UserId.ToString() != userId)
                {
                    await _auditService.LogSecurityEvent(
                        userId,
                        SecurityEventType.UnauthorizedAccess,
                        $"Attempted deletion of document {id}",
                        new SecurityContext { DocumentId = id },
                        SecuritySeverity.High);
                    return Forbid();
                } */

                await _userDocumentRepository.DeleteAsync(userDocumentId).ConfigureAwait(false);

                _cache.Remove($"{CACHE_KEY_PREFIX}{userDocumentId}");

                /* await _auditService.LogDataAccess(
                    userId,
                    "DeleteDocument",
                    "Document",
                    id.ToString(),
                    null,
                    SecurityClassification.Critical); */

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogGenericException(string.Format(CultureInfo.InvariantCulture, "Error deleting document {0}", userDocumentId), ex);
                throw;
            }
        }

        /* this will call the encryption context, which will contact the vault API with the user id and the list of strings 
         * to encrypt
         * private async Task<UserDocument> EncryptDocumentFields(Document document, EncryptionContext context)
        {
            if (document.FrontImageUrl != null)
            {
                document.FrontImageUrl = await _encryptionService.EncryptSensitiveField(
                    document.FrontImageUrl, "FrontImageUrl", context.UserId, context);
            }

            if (document.BackImageUrl != null)
            {
                document.BackImageUrl = await _encryptionService.EncryptSensitiveField(
                    document.BackImageUrl, "BackImageUrl", context.UserId, context);
            }

            if (document.Location != null)
            {
                document.Location = await _encryptionService.EncryptSensitiveField(
                    document.Location, "Location", context.UserId, context);
            }

            if (document.Metadata != null)
            {
                document.Metadata = await _encryptionService.EncryptSensitiveField(
                    document.Metadata, "Metadata", context.UserId, context);
            }

            return document;
        }

        private async Task<Document> DecryptDocumentFields(Document document, EncryptionContext context)
        {
            if (document.FrontImageUrl != null)
            {
                document.FrontImageUrl = await _encryptionService.DecryptSensitiveField(
                    document.FrontImageUrl, "FrontImageUrl", context.UserId, context);
            }

            if (document.BackImageUrl != null)
            {
                document.BackImageUrl = await _encryptionService.DecryptSensitiveField(
                    document.BackImageUrl, "BackImageUrl", context.UserId, context);
            }

            if (document.Location != null)
            {
                document.Location = await _encryptionService.DecryptSensitiveField(
                    document.Location, "Location", context.UserId, context);
            }

            if (document.Metadata != null)
            {
                document.Metadata = await _encryptionService.DecryptSensitiveField(
                    document.Metadata, "Metadata", context.UserId, context);
            }

            return document;
        } */
    }
}