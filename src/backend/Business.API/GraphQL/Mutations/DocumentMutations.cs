using System;
using System.Threading.Tasks;
using HotChocolate;
using HotChocolate.Types;
using HotChocolate.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using EstateKit.Core.Entities;
using EstateKit.Core.Interfaces;
using EstateKit.Core.Enums;
using EstateKit.Business.API.Services;
using EstateKit.Business.API.Services.AWS;

namespace EstateKit.Business.API.GraphQL.Mutations
{
    /// <summary>
    /// Implements GraphQL mutations for secure document management with comprehensive
    /// validation, versioning, audit logging, and field-level encryption.
    /// </summary>
    [ExtendObjectType("Mutation")]
    public class DocumentMutations
    {
        private readonly IDocumentRepository _documentRepository;
        private readonly DocumentProcessor _documentProcessor;
        private readonly ILogger<DocumentMutations> _logger;

        public DocumentMutations(
            IDocumentRepository documentRepository,
            DocumentProcessor documentProcessor,
            ILogger<DocumentMutations> logger)
        {
            _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
            _documentProcessor = documentProcessor ?? throw new ArgumentNullException(nameof(documentProcessor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Securely uploads and processes a new document with OCR and PII detection
        /// </summary>
        [UseFirstOrDefault]
        [UseProjection]
        [Authorize(Policy = "DocumentUpload")]
        [UseRateLimiter(MaxRequests = 100, Window = 60)]
        public async Task<Document> UploadDocumentAsync(
            IFile file,
            DocumentType type,
            Guid userId,
            string securityClassification = "Critical")
        {
            using var scope = _logger.BeginScope(
                "Uploading document for user {UserId} of type {DocumentType}",
                userId, type);

            try
            {
                // Validate input parameters
                if (file == null)
                    throw new ArgumentNullException(nameof(file));
                if (userId == Guid.Empty)
                    throw new ArgumentException("Invalid user ID", nameof(userId));

                // Create new document with security parameters
                var document = new Document(type, userId, "estatekit-documents", securityClassification);

                // Process document through secure pipeline
                using var stream = file.OpenReadStream();
                var processedDocument = await _documentProcessor.ProcessDocumentAsync(
                    stream,
                    document,
                    file.ContentType);

                _logger.LogInformation(
                    "Successfully processed document {DocumentId} for user {UserId}",
                    processedDocument.Id, userId);

                return processedDocument;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to upload document for user {UserId} of type {DocumentType}",
                    userId, type);
                throw;
            }
        }

        /// <summary>
        /// Updates the physical location of a document with security validation
        /// </summary>
        [UseFirstOrDefault]
        [Authorize(Policy = "DocumentUpdate")]
        [UseRateLimiter(MaxRequests = 500, Window = 60)]
        public async Task<bool> UpdateDocumentLocationAsync(
            Guid documentId,
            string location)
        {
            using var scope = _logger.BeginScope(
                "Updating location for document {DocumentId}",
                documentId);

            try
            {
                if (documentId == Guid.Empty)
                    throw new ArgumentException("Invalid document ID", nameof(documentId));
                if (string.IsNullOrEmpty(location))
                    throw new ArgumentNullException(nameof(location));

                var success = await _documentProcessor.UpdateDocumentLocationAsync(
                    documentId,
                    location);

                if (success)
                {
                    _logger.LogInformation(
                        "Successfully updated location for document {DocumentId}",
                        documentId);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to update location for document {DocumentId}",
                    documentId);
                throw;
            }
        }

        /// <summary>
        /// Securely deletes a document with comprehensive audit logging
        /// </summary>
        [Authorize(Policy = "DocumentDelete")]
        [UseRateLimiter(MaxRequests = 100, Window = 60)]
        public async Task<bool> DeleteDocumentAsync(Guid documentId)
        {
            using var scope = _logger.BeginScope(
                "Deleting document {DocumentId}",
                documentId);

            try
            {
                if (documentId == Guid.Empty)
                    throw new ArgumentException("Invalid document ID", nameof(documentId));

                // Retrieve document to verify existence and get S3 location
                var document = await _documentRepository.GetByIdAsync(documentId);
                if (document == null)
                {
                    throw new DocumentNotFoundException(documentId);
                }

                // Delete from repository (includes audit logging)
                var success = await _documentRepository.DeleteAsync(documentId);

                if (success)
                {
                    _logger.LogInformation(
                        "Successfully deleted document {DocumentId}",
                        documentId);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to delete document {DocumentId}",
                    documentId);
                throw;
            }
        }
    }

    public class DocumentNotFoundException : Exception
    {
        public DocumentNotFoundException(Guid documentId)
            : base($"Document not found: {documentId}") { }
    }
}