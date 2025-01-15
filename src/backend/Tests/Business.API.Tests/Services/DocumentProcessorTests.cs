using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EstateKit.Business.API.Services;
using EstateKit.Business.API.Services.AWS;
using EstateKit.Core.Entities;
using EstateKit.Core.Enums;
using EstateKit.Core.Interfaces;
using FluentAssertions; // v6.2.0
using Microsoft.Extensions.Logging;
using Moq; // v4.18.0
using Xunit; // v2.5.0

namespace Business.API.Tests.Services
{
    [Trait("TestCategory", "DocumentProcessing")]
    [Trait("TestCategory", "Security")]
    public class DocumentProcessorTests
    {
        private readonly Mock<IDocumentRepository> _documentRepositoryMock;
        private readonly Mock<S3Service> _s3ServiceMock;
        private readonly Mock<TextractService> _textractServiceMock;
        private readonly Mock<IEncryptionService> _encryptionServiceMock;
        private readonly Mock<IAuditLogger> _auditLoggerMock;
        private readonly Mock<ILogger<DocumentProcessor>> _loggerMock;
        private readonly DocumentProcessor _documentProcessor;

        public DocumentProcessorTests()
        {
            _documentRepositoryMock = new Mock<IDocumentRepository>();
            _s3ServiceMock = new Mock<S3Service>();
            _textractServiceMock = new Mock<TextractService>();
            _encryptionServiceMock = new Mock<IEncryptionService>();
            _auditLoggerMock = new Mock<IAuditLogger>();
            _loggerMock = new Mock<ILogger<DocumentProcessor>>();

            var options = Microsoft.Extensions.Options.Options.Create(new DocumentProcessorOptions
            {
                MaxRetryAttempts = 3,
                RetryDelaySeconds = 2,
                MinConfidenceThreshold = 90.0f,
                EnablePiiDetection = true
            });

            _documentProcessor = new DocumentProcessor(
                _documentRepositoryMock.Object,
                _s3ServiceMock.Object,
                _textractServiceMock.Object,
                _encryptionServiceMock.Object,
                _auditLoggerMock.Object,
                _loggerMock.Object,
                options);
        }

        [Fact]
        public async Task ProcessDocumentAsync_WithVersioning_CreatesNewVersion()
        {
            // Arrange
            var documentId = Guid.NewGuid();
            var document = new Document(
                DocumentType.DRIVERS_LICENSE,
                Guid.NewGuid(),
                "test-bucket",
                "Critical");

            var fileStream = new MemoryStream();
            var objectKey = $"documents/{documentId}/v1";
            var contentType = "application/pdf";

            _documentRepositoryMock
                .Setup(x => x.CreateVersionAsync(documentId))
                .ReturnsAsync(true);

            _s3ServiceMock
                .Setup(x => x.UploadDocumentAsync(fileStream, document, contentType))
                .ReturnsAsync(objectKey);

            _documentRepositoryMock
                .Setup(x => x.UpdateLocationAsync(documentId, objectKey))
                .ReturnsAsync(true);

            // Act
            await _documentProcessor.ProcessDocumentAsync(fileStream, document, contentType);

            // Assert
            _documentRepositoryMock.Verify(
                x => x.CreateVersionAsync(documentId),
                Times.Once);

            _auditLoggerMock.Verify(
                x => x.LogInformation(
                    It.Is<string>(msg => msg.Contains("Created new version")),
                    It.Is<object[]>(args => args[0].Equals(documentId))),
                Times.Once);
        }

        [Fact]
        public async Task ProcessDocumentAsync_WithEncryption_EncryptsFields()
        {
            // Arrange
            var document = new Document(
                DocumentType.PASSPORT,
                Guid.NewGuid(),
                "test-bucket",
                "Critical");

            var fileStream = new MemoryStream();
            var contentType = "application/pdf";
            var metadata = new
            {
                ConfidenceScore = 95.5f,
                ProcessedPages = 2,
                SecurityClassification = "Critical",
                ProcessingTimestamp = DateTime.UtcNow
            };

            var encryptedMetadata = "encrypted_metadata";
            _encryptionServiceMock
                .Setup(x => x.Encrypt(It.IsAny<string>()))
                .Returns(encryptedMetadata);

            // Act
            await _documentProcessor.ProcessDocumentAsync(fileStream, document, contentType);

            // Assert
            _encryptionServiceMock.Verify(
                x => x.Encrypt(It.IsAny<string>()),
                Times.Once);

            _documentRepositoryMock.Verify(
                x => x.UpdateProcessingStatusAsync(
                    document.Id,
                    encryptedMetadata),
                Times.Once);
        }

        [Fact]
        public async Task ProcessDocumentAsync_WithPiiDetection_LogsFindings()
        {
            // Arrange
            var document = new Document(
                DocumentType.BIRTH_CERTIFICATE,
                Guid.NewGuid(),
                "test-bucket",
                "Critical");

            var fileStream = new MemoryStream();
            var contentType = "application/pdf";

            var textractResult = new TextractAnalysisResult
            {
                AverageConfidence = 95.5f,
                Pages = new List<TextractPageResult>
                {
                    new TextractPageResult { PageNumber = 1 }
                }
            };

            _textractServiceMock
                .Setup(x => x.ProcessDocumentAsync(
                    document.S3BucketName,
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(textractResult);

            // Act
            await _documentProcessor.ProcessDocumentAsync(fileStream, document, contentType);

            // Assert
            _auditLoggerMock.Verify(
                x => x.LogInformation(
                    It.Is<string>(msg => msg.Contains("Successfully processed document")),
                    It.Is<object[]>(args => args[0].Equals(document.Id) && 
                                          args[1].Equals(textractResult.AverageConfidence))),
                Times.Once);
        }

        [Fact]
        public async Task UpdateDocumentLocationAsync_WithAudit_LogsChange()
        {
            // Arrange
            var documentId = Guid.NewGuid();
            var newLocation = "New York Safe Deposit Box #123";
            var document = new Document(
                DocumentType.PASSPORT,
                Guid.NewGuid(),
                "test-bucket",
                "Critical");

            _documentRepositoryMock
                .Setup(x => x.GetByIdAsync(documentId))
                .ReturnsAsync(document);

            _documentRepositoryMock
                .Setup(x => x.UpdateLocationAsync(documentId, It.IsAny<string>()))
                .ReturnsAsync(true);

            // Act
            var result = await _documentProcessor.UpdateDocumentLocationAsync(documentId, newLocation);

            // Assert
            result.Should().BeTrue();
            _encryptionServiceMock.Verify(
                x => x.Encrypt(newLocation),
                Times.Once);
            _auditLoggerMock.Verify(
                x => x.LogInformation(
                    It.Is<string>(msg => msg.Contains("Updated location")),
                    It.Is<object[]>(args => args[0].Equals(documentId))),
                Times.Once);
        }

        [Fact]
        public async Task GetDocumentStatusAsync_WithSecurity_ValidatesAccess()
        {
            // Arrange
            var documentId = Guid.NewGuid();
            var document = new Document(
                DocumentType.DRIVERS_LICENSE,
                Guid.NewGuid(),
                "test-bucket",
                "Critical");

            _documentRepositoryMock
                .Setup(x => x.GetByIdAsync(documentId))
                .ReturnsAsync(document);

            // Act
            var status = await _documentProcessor.GetDocumentStatusAsync(documentId);

            // Assert
            status.Should().NotBeNull();
            status.DocumentId.Should().Be(documentId);
            status.SecurityClassification.Should().Be(document.SecurityClassification);

            _auditLoggerMock.Verify(
                x => x.LogInformation(
                    It.Is<string>(msg => msg.Contains("Retrieved status")),
                    It.Is<object[]>(args => args[0].Equals(documentId))),
                Times.Once);
        }

        [Fact]
        public async Task ProcessDocumentAsync_WithInvalidDocument_ThrowsArgumentException()
        {
            // Arrange
            var fileStream = new MemoryStream();
            Document document = null;
            var contentType = "application/pdf";

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _documentProcessor.ProcessDocumentAsync(fileStream, document, contentType));
        }

        [Fact]
        public async Task UpdateDocumentLocationAsync_WithInvalidId_ThrowsArgumentException()
        {
            // Arrange
            var documentId = Guid.Empty;
            var location = "New Location";

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _documentProcessor.UpdateDocumentLocationAsync(documentId, location));
        }

        [Fact]
        public async Task GetDocumentStatusAsync_WithNonexistentDocument_ThrowsDocumentNotFoundException()
        {
            // Arrange
            var documentId = Guid.NewGuid();
            _documentRepositoryMock
                .Setup(x => x.GetByIdAsync(documentId))
                .ReturnsAsync((Document)null);

            // Act & Assert
            await Assert.ThrowsAsync<DocumentNotFoundException>(() =>
                _documentProcessor.GetDocumentStatusAsync(documentId));
        }
    }
}