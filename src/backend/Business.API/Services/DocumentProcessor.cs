using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.AuditLogger;
using EstateKit.Core.Entities;
using EstateKit.Core.Interfaces;
using EstateKit.Business.API.Services.AWS;
using Polly;
using Polly.Retry;
using System.Text.Json;

namespace EstateKit.Business.API.Services
{
    /// <summary>
    /// Service that orchestrates secure document processing operations including encrypted storage,
    /// OCR processing with PII detection, version control, and comprehensive audit logging.
    /// </summary>
    public sealed class DocumentProcessor
    {
        private readonly IDocumentRepository _documentRepository;
        private readonly S3Service _s3Service;
        private readonly TextractService _textractService;
        private readonly IEncryptionService _encryptionService;
        private readonly IAuditLogger _auditLogger;
        private readonly ILogger<DocumentProcessor> _logger;
        private readonly DocumentProcessorOptions _options;
        private readonly IAsyncPolicy _retryPolicy;

        public DocumentProcessor(
            IDocumentRepository documentRepository,
            S3Service s3Service,
            TextractService textractService,
            IEncryptionService encryptionService,
            IAuditLogger auditLogger,
            ILogger<DocumentProcessor> logger,
            IOptions<DocumentProcessorOptions> options)
        {
            _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
            _s3Service = s3Service ?? throw new ArgumentNullException(nameof(s3Service));
            _textractService = textractService ?? throw new ArgumentNullException(nameof(textractService));
            _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
            _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

            _retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(
                    3,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (exception, timeSpan, retryCount, context) =>
                    {
                        _logger.LogWarning(
                            exception,
                            "Retry {RetryCount} after {Delay}ms for document processing",
                            retryCount,
                            timeSpan.TotalMilliseconds);
                    });
        }

        /// <summary>
        /// Processes a document with enhanced security, versioning, and PII detection
        /// </summary>
        public async Task<Document> ProcessDocumentAsync(
            Stream fileStream,
            Document document,
            string contentType,
            CancellationToken cancellationToken = default)
        {
            if (fileStream == null) throw new ArgumentNullException(nameof(fileStream));
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (string.IsNullOrEmpty(contentType)) throw new ArgumentNullException(nameof(contentType));

            using var scope = _logger.BeginScope(
                "Processing document {DocumentId} of type {DocumentType}",
                document.Id,
                document.Type);

            try
            {
                // Create new document version
                await _documentRepository.CreateVersionAsync(document.Id);
                _auditLogger.LogInformation(
                    "Created new version for document {DocumentId}",
                    document.Id);

                // Upload to S3 with encryption
                var objectKey = await _s3Service.UploadDocumentAsync(fileStream, document, contentType);
                await _documentRepository.UpdateLocationAsync(document.Id, objectKey);

                // Generate encrypted presigned URL
                var presignedUrl = await _s3Service.GetPresignedUrlAsync(objectKey);
                var encryptedUrl = _encryptionService.Encrypt(presignedUrl);

                // Process with OCR and PII detection
                var textractResult = await _textractService.ProcessDocumentAsync(
                    document.S3BucketName,
                    objectKey,
                    cancellationToken);

                // Update document metadata with encrypted sensitive fields
                var metadata = new
                {
                    ConfidenceScore = textractResult.AverageConfidence,
                    ProcessedPages = textractResult.Pages.Count,
                    SecurityClassification = document.SecurityClassification,
                    ProcessingTimestamp = DateTime.UtcNow
                };

                var encryptedMetadata = _encryptionService.Encrypt(
                    JsonSerializer.Serialize(metadata));

                await _documentRepository.UpdateProcessingStatusAsync(
                    document.Id,
                    encryptedMetadata);

                _auditLogger.LogInformation(
                    "Successfully processed document {DocumentId} with confidence {Confidence}",
                    document.Id,
                    textractResult.AverageConfidence);

                // Return updated document
                return await _documentRepository.GetByIdAsync(document.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to process document {DocumentId}",
                    document.Id);
                throw;
            }
        }

        /// <summary>
        /// Retrieves the current processing status with security validation
        /// </summary>
        public async Task<DocumentStatus> GetDocumentStatusAsync(Guid documentId)
        {
            if (documentId == Guid.Empty)
                throw new ArgumentException("Invalid document ID", nameof(documentId));

            try
            {
                var document = await _documentRepository.GetByIdAsync(documentId);
                if (document == null)
                {
                    throw new DocumentNotFoundException(documentId);
                }

                var status = new DocumentStatus
                {
                    DocumentId = documentId,
                    IsProcessed = document.IsProcessed,
                    ProcessingStatus = document.ProcessingStatus,
                    SecurityClassification = document.SecurityClassification
                };

                _auditLogger.LogInformation(
                    "Retrieved status for document {DocumentId}: {Status}",
                    documentId,
                    status.ProcessingStatus);

                return status;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to retrieve status for document {DocumentId}",
                    documentId);
                throw;
            }
        }

        /// <summary>
        /// Updates document location with security validation and audit logging
        /// </summary>
        public async Task<bool> UpdateDocumentLocationAsync(Guid documentId, string location)
        {
            if (documentId == Guid.Empty)
                throw new ArgumentException("Invalid document ID", nameof(documentId));
            if (string.IsNullOrEmpty(location))
                throw new ArgumentNullException(nameof(location));

            try
            {
                var document = await _documentRepository.GetByIdAsync(documentId);
                if (document == null)
                {
                    throw new DocumentNotFoundException(documentId);
                }

                // Encrypt location if classified as sensitive
                var encryptedLocation = document.SecurityClassification == "Critical" 
                    ? _encryptionService.Encrypt(location)
                    : location;

                var success = await _documentRepository.UpdateLocationAsync(
                    documentId,
                    encryptedLocation);

                if (success)
                {
                    _auditLogger.LogInformation(
                        "Updated location for document {DocumentId}",
                        documentId);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to update location for document {DocumentId}",
                    documentId);
                throw;
            }
        }
    }

    public class DocumentStatus
    {
        public Guid DocumentId { get; set; }
        public bool IsProcessed { get; set; }
        public string ProcessingStatus { get; set; }
        public string SecurityClassification { get; set; }
    }

    public class DocumentNotFoundException : Exception
    {
        public DocumentNotFoundException(Guid documentId)
            : base($"Document not found: {documentId}") { }
    }

    public class DocumentProcessorOptions
    {
        public int MaxRetryAttempts { get; set; } = 3;
        public int RetryDelaySeconds { get; set; } = 2;
        public float MinConfidenceThreshold { get; set; } = 90.0f;
        public bool EnablePiiDetection { get; set; } = true;
    }
}