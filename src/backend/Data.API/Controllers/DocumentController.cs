using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using FluentValidation;
using EstateKit.Core.Entities;
using EstateKit.Core.Interfaces;
using EstateKit.Data.API.Services;

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
    public class DocumentController : ControllerBase
    {
        private readonly IDocumentRepository _documentRepository;
        private readonly EncryptionService _encryptionService;
        private readonly AuditService _auditService;
        private readonly IValidator<Document> _validator;
        private readonly IMemoryCache _cache;
        private readonly ILogger<DocumentController> _logger;

        private const int CACHE_DURATION_MINUTES = 30;
        private const string CACHE_KEY_PREFIX = "doc_";

        public DocumentController(
            IDocumentRepository documentRepository,
            EncryptionService encryptionService,
            AuditService auditService,
            IValidator<Document> validator,
            IMemoryCache cache,
            ILogger<DocumentController> logger)
        {
            _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
            _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
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
        public async Task<ActionResult<Document>> GetDocumentAsync(Guid id)
        {
            try
            {
                var userId = User.FindFirst("sub")?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Forbid();
                }

                var cacheKey = $"{CACHE_KEY_PREFIX}{id}";
                if (_cache.TryGetValue(cacheKey, out Document cachedDocument))
                {
                    await _auditService.LogDataAccess(
                        userId,
                        "GetDocument",
                        "Document",
                        id.ToString(),
                        null,
                        SecurityClassification.Critical);
                    return Ok(cachedDocument);
                }

                var document = await _documentRepository.GetByIdAsync(id);
                if (document == null)
                {
                    return NotFound();
                }

                if (document.UserId.ToString() != userId)
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

                document = await DecryptDocumentFields(document, context);

                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetSlidingExpiration(TimeSpan.FromMinutes(CACHE_DURATION_MINUTES))
                    .SetPriority(CacheItemPriority.High);

                _cache.Set(cacheKey, document, cacheOptions);

                await _auditService.LogDataAccess(
                    userId,
                    "GetDocument",
                    "Document",
                    id.ToString(),
                    null,
                    SecurityClassification.Critical);

                return Ok(document);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving document {DocumentId}", id);
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
        public async Task<ActionResult<IEnumerable<Document>>> GetUserDocumentsAsync(Guid userId)
        {
            try
            {
                var currentUserId = User.FindFirst("sub")?.Value;
                if (string.IsNullOrEmpty(currentUserId) || currentUserId != userId.ToString())
                {
                    return Forbid();
                }

                var cacheKey = $"{CACHE_KEY_PREFIX}user_{userId}";
                if (_cache.TryGetValue(cacheKey, out IEnumerable<Document> cachedDocuments))
                {
                    await _auditService.LogDataAccess(
                        currentUserId,
                        "GetUserDocuments",
                        "Document",
                        userId.ToString(),
                        null,
                        SecurityClassification.Critical);
                    return Ok(cachedDocuments);
                }

                var documents = await _documentRepository.GetByUserIdAsync(userId);
                var context = new EncryptionContext
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
                    SecurityClassification.Critical);

                return Ok(decryptedDocuments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving documents for user {UserId}", userId);
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
        public async Task<ActionResult<Document>> CreateDocumentAsync([FromBody] Document document)
        {
            try
            {
                var userId = User.FindFirst("sub")?.Value;
                if (string.IsNullOrEmpty(userId))
                {
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
                    EntityId = document.Id.ToString(),
                    UserId = userId
                };

                document = await EncryptDocumentFields(document, context);
                var createdDocument = await _documentRepository.AddAsync(document);

                await _auditService.LogDataAccess(
                    userId,
                    "CreateDocument",
                    "Document",
                    createdDocument.Id.ToString(),
                    document,
                    SecurityClassification.Critical);

                return CreatedAtAction(
                    nameof(GetDocumentAsync),
                    new { id = createdDocument.Id },
                    createdDocument);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating document");
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
        public async Task<ActionResult<Document>> UpdateDocumentAsync(Guid id, [FromBody] Document document)
        {
            try
            {
                var userId = User.FindFirst("sub")?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Forbid();
                }

                var existingDocument = await _documentRepository.GetByIdAsync(id);
                if (existingDocument == null)
                {
                    return NotFound();
                }

                if (existingDocument.UserId.ToString() != userId)
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

                document = await EncryptDocumentFields(document, context);
                await _documentRepository.UpdateAsync(document);

                _cache.Remove($"{CACHE_KEY_PREFIX}{id}");
                _cache.Remove($"{CACHE_KEY_PREFIX}user_{userId}");

                await _auditService.LogDataAccess(
                    userId,
                    "UpdateDocument",
                    "Document",
                    id.ToString(),
                    document,
                    SecurityClassification.Critical);

                return Ok(document);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating document {DocumentId}", id);
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
        public async Task<ActionResult> DeleteDocumentAsync(Guid id)
        {
            try
            {
                var userId = User.FindFirst("sub")?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Forbid();
                }

                var document = await _documentRepository.GetByIdAsync(id);
                if (document == null)
                {
                    return NotFound();
                }

                if (document.UserId.ToString() != userId)
                {
                    await _auditService.LogSecurityEvent(
                        userId,
                        SecurityEventType.UnauthorizedAccess,
                        $"Attempted deletion of document {id}",
                        new SecurityContext { DocumentId = id },
                        SecuritySeverity.High);
                    return Forbid();
                }

                await _documentRepository.DeleteAsync(id);

                _cache.Remove($"{CACHE_KEY_PREFIX}{id}");
                _cache.Remove($"{CACHE_KEY_PREFIX}user_{userId}");

                await _auditService.LogDataAccess(
                    userId,
                    "DeleteDocument",
                    "Document",
                    id.ToString(),
                    null,
                    SecurityClassification.Critical);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting document {DocumentId}", id);
                throw;
            }
        }

        private async Task<Document> EncryptDocumentFields(Document document, EncryptionContext context)
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
        }
    }
}