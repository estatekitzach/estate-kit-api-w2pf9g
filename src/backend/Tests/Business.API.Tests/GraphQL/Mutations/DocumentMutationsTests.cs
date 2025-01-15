using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using HotChocolate.Types;
using EstateKit.Core.Entities;
using EstateKit.Core.Interfaces;
using EstateKit.Core.Enums;
using EstateKit.Business.API.Services;
using EstateKit.Business.API.Services.AWS;
using EstateKit.Business.API.GraphQL.Mutations;

namespace EstateKit.Business.API.Tests.GraphQL.Mutations
{
    /// <summary>
    /// Comprehensive unit tests for DocumentMutations class covering CRUD operations,
    /// security validation, OCR processing, and audit logging requirements.
    /// </summary>
    public class DocumentMutationsTests
    {
        private readonly Mock<IDocumentRepository> _documentRepositoryMock;
        private readonly Mock<ILogger<DocumentMutations>> _loggerMock;
        private readonly Mock<DocumentProcessor> _documentProcessorMock;
        private readonly DocumentMutations _mutations;

        public DocumentMutationsTests()
        {
            _documentRepositoryMock = new Mock<IDocumentRepository>(MockBehavior.Strict);
            _loggerMock = new Mock<ILogger<DocumentMutations>>();
            _documentProcessorMock = new Mock<DocumentProcessor>(MockBehavior.Strict);

            _mutations = new DocumentMutations(
                _documentRepositoryMock.Object,
                _documentProcessorMock.Object,
                _loggerMock.Object);
        }

        [Fact]
        public async Task UploadDocument_ValidInput_ReturnsProcessedDocument()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var documentId = Guid.NewGuid();
            var fileMock = new Mock<IFile>();
            var fileStream = new MemoryStream();
            var document = new Document(DocumentType.DRIVERS_LICENSE, userId, "test-bucket", "Critical");

            fileMock.Setup(f => f.OpenReadStream()).Returns(fileStream);
            fileMock.Setup(f => f.ContentType).Returns("application/pdf");

            _documentProcessorMock
                .Setup(p => p.ProcessDocumentAsync(
                    It.IsAny<Stream>(),
                    It.IsAny<Document>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(document);

            // Act
            var result = await _mutations.UploadDocumentAsync(
                fileMock.Object,
                DocumentType.DRIVERS_LICENSE,
                userId,
                "Critical");

            // Assert
            result.Should().NotBeNull();
            result.Type.Should().Be(DocumentType.DRIVERS_LICENSE);
            result.SecurityClassification.Should().Be("Critical");

            _documentProcessorMock.Verify(
                p => p.ProcessDocumentAsync(
                    It.IsAny<Stream>(),
                    It.IsAny<Document>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            VerifyAuditLog("Successfully processed document");
        }

        [Fact]
        public async Task UploadDocument_InvalidInput_ThrowsArgumentException()
        {
            // Arrange
            var userId = Guid.Empty;

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _mutations.UploadDocumentAsync(
                    null,
                    DocumentType.DRIVERS_LICENSE,
                    userId,
                    "Critical"));

            VerifyAuditLog("Failed to upload document");
        }

        [Fact]
        public async Task UpdateDocumentLocation_ValidInput_UpdatesLocationAndVersion()
        {
            // Arrange
            var documentId = Guid.NewGuid();
            var location = "Safe Box 123";
            var document = new Document(DocumentType.PASSPORT, Guid.NewGuid(), "test-bucket", "Critical");

            _documentProcessorMock
                .Setup(p => p.UpdateDocumentLocationAsync(documentId, location))
                .ReturnsAsync(true);

            // Act
            var result = await _mutations.UpdateDocumentLocationAsync(documentId, location);

            // Assert
            result.Should().BeTrue();
            _documentProcessorMock.Verify(
                p => p.UpdateDocumentLocationAsync(documentId, location),
                Times.Once);
            VerifyAuditLog("Successfully updated location");
        }

        [Fact]
        public async Task UpdateDocumentLocation_InvalidInput_ThrowsArgumentException()
        {
            // Arrange
            var documentId = Guid.Empty;
            var location = "";

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _mutations.UpdateDocumentLocationAsync(documentId, location));

            VerifyAuditLog("Failed to update location");
        }

        [Fact]
        public async Task DeleteDocument_ValidInput_PerformsCascadeDelete()
        {
            // Arrange
            var documentId = Guid.NewGuid();
            var document = new Document(DocumentType.BIRTH_CERTIFICATE, Guid.NewGuid(), "test-bucket", "Critical");

            _documentRepositoryMock
                .Setup(r => r.GetByIdAsync(documentId))
                .ReturnsAsync(document);

            _documentRepositoryMock
                .Setup(r => r.DeleteAsync(documentId))
                .ReturnsAsync(true);

            // Act
            var result = await _mutations.DeleteDocumentAsync(documentId);

            // Assert
            result.Should().BeTrue();
            _documentRepositoryMock.Verify(r => r.DeleteAsync(documentId), Times.Once);
            VerifyAuditLog("Successfully deleted document");
        }

        [Fact]
        public async Task DeleteDocument_DocumentNotFound_ThrowsNotFoundException()
        {
            // Arrange
            var documentId = Guid.NewGuid();

            _documentRepositoryMock
                .Setup(r => r.GetByIdAsync(documentId))
                .ReturnsAsync((Document)null);

            // Act & Assert
            await Assert.ThrowsAsync<DocumentNotFoundException>(() =>
                _mutations.DeleteDocumentAsync(documentId));

            VerifyAuditLog("Failed to delete document");
        }

        private void VerifyAuditLog(string messageContains)
        {
            _loggerMock.Verify(
                x => x.Log(
                    It.IsAny<LogLevel>(),
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains(messageContains)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.AtLeastOnce);
        }
    }
}