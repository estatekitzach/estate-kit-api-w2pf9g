using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using EstateKit.Core.Entities;
using EstateKit.Core.Interfaces;
using EstateKit.Core.Enums;
using EstateKit.Data.API.Services;
using EstateKit.Data.API.Controllers;
using EstateKit.Infrastructure.Security;

namespace EstateKit.Data.API.Tests.Controllers
{
    /// <summary>
    /// Comprehensive test suite for DocumentController verifying secure document management,
    /// field-level encryption, audit logging, and security validation.
    /// </summary>
    public class DocumentControllerTests
    {
        private readonly Mock<IDocumentRepository> _mockDocumentRepository;
        private readonly Mock<EncryptionService> _mockEncryptionService;
        private readonly Mock<AuditService> _mockAuditService;
        private readonly Mock<IValidator<Document>> _mockValidator;
        private readonly Mock<IMemoryCache> _mockCache;
        private readonly Mock<ILogger<DocumentController>> _mockLogger;
        private readonly DocumentController _controller;
        private readonly string _testUserId;
        private readonly Guid _testDocumentId;

        public DocumentControllerTests()
        {
            _mockDocumentRepository = new Mock<IDocumentRepository>();
            _mockEncryptionService = new Mock<EncryptionService>();
            _mockAuditService = new Mock<AuditService>();
            _mockValidator = new Mock<IValidator<Document>>();
            _mockCache = new Mock<IMemoryCache>();
            _mockLogger = new Mock<ILogger<DocumentController>>();
            _testUserId = Guid.NewGuid().ToString();
            _testDocumentId = Guid.NewGuid();

            _controller = new DocumentController(
                _mockDocumentRepository.Object,
                _mockEncryptionService.Object,
                _mockAuditService.Object,
                _mockValidator.Object,
                _mockCache.Object,
                _mockLogger.Object);

            // Setup default user claims
            var claims = new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity(
                    new[] { new System.Security.Claims.Claim("sub", _testUserId) }));
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
                {
                    User = claims
                }
            };
        }

        [Fact]
        public async Task GetDocumentAsync_WithValidSecurityContext_ReturnsDecryptedDocument()
        {
            // Arrange
            var document = new Document(DocumentType.DRIVERS_LICENSE, Guid.Parse(_testUserId), "test-bucket", "Critical")
            {
                FrontImageUrl = "encrypted_front_url",
                BackImageUrl = "encrypted_back_url",
                Location = "encrypted_location"
            };

            _mockDocumentRepository
                .Setup(r => r.GetByIdAsync(_testDocumentId))
                .ReturnsAsync(document);

            _mockEncryptionService
                .Setup(e => e.DecryptSensitiveField(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<EncryptionContext>()))
                .ReturnsAsync("decrypted_value");

            // Act
            var result = await _controller.GetDocumentAsync(_testDocumentId);

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var returnedDocument = okResult.Value.Should().BeOfType<Document>().Subject;

            returnedDocument.Should().NotBeNull();
            returnedDocument.UserId.ToString().Should().Be(_testUserId);

            _mockAuditService.Verify(a => a.LogDataAccess(
                _testUserId,
                "GetDocument",
                "Document",
                _testDocumentId.ToString(),
                null,
                SecurityClassification.Critical), Times.Once);
        }

        [Fact]
        public async Task GetDocumentAsync_WithInvalidSecurityContext_ReturnsForbidden()
        {
            // Arrange
            var document = new Document(DocumentType.DRIVERS_LICENSE, Guid.NewGuid(), "test-bucket", "Critical");
            
            _mockDocumentRepository
                .Setup(r => r.GetByIdAsync(_testDocumentId))
                .ReturnsAsync(document);

            // Act
            var result = await _controller.GetDocumentAsync(_testDocumentId);

            // Assert
            result.Result.Should().BeOfType<ForbidResult>();

            _mockAuditService.Verify(a => a.LogSecurityEvent(
                _testUserId,
                SecurityEventType.UnauthorizedAccess,
                It.IsAny<string>(),
                It.IsAny<SecurityContext>(),
                SecuritySeverity.High), Times.Once);
        }

        [Fact]
        public async Task GetDocumentAsync_WithCachedDocument_ReturnsCachedValue()
        {
            // Arrange
            var document = new Document(DocumentType.DRIVERS_LICENSE, Guid.Parse(_testUserId), "test-bucket", "Critical");
            var cacheEntry = Mock.Of<ICacheEntry>();

            _mockCache
                .Setup(c => c.TryGetValue(It.IsAny<string>(), out document))
                .Returns(true);

            // Act
            var result = await _controller.GetDocumentAsync(_testDocumentId);

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            
            _mockDocumentRepository.Verify(r => r.GetByIdAsync(It.IsAny<Guid>()), Times.Never);
            _mockEncryptionService.Verify(
                e => e.DecryptSensitiveField(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EncryptionContext>()),
                Times.Never);
        }

        [Fact]
        public async Task CreateDocumentAsync_WithValidDocument_ReturnsCreatedDocument()
        {
            // Arrange
            var document = new Document(DocumentType.PASSPORT, Guid.Parse(_testUserId), "test-bucket", "Critical");
            
            _mockValidator
                .Setup(v => v.ValidateAsync(It.IsAny<Document>()))
                .ReturnsAsync(new FluentValidation.Results.ValidationResult());

            _mockEncryptionService
                .Setup(e => e.EncryptSensitiveField(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<EncryptionContext>()))
                .ReturnsAsync("encrypted_value");

            _mockDocumentRepository
                .Setup(r => r.AddAsync(It.IsAny<Document>()))
                .ReturnsAsync(document);

            // Act
            var result = await _controller.CreateDocumentAsync(document);

            // Assert
            var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
            var createdDocument = createdResult.Value.Should().BeOfType<Document>().Subject;

            createdDocument.Should().NotBeNull();
            createdDocument.UserId.ToString().Should().Be(_testUserId);

            _mockAuditService.Verify(a => a.LogDataAccess(
                _testUserId,
                "CreateDocument",
                "Document",
                It.IsAny<string>(),
                It.IsAny<object>(),
                SecurityClassification.Critical), Times.Once);
        }

        [Fact]
        public async Task UpdateDocumentAsync_WithValidDocument_ReturnsUpdatedDocument()
        {
            // Arrange
            var existingDocument = new Document(DocumentType.BIRTH_CERTIFICATE, Guid.Parse(_testUserId), "test-bucket", "Critical");
            var updatedDocument = new Document(DocumentType.BIRTH_CERTIFICATE, Guid.Parse(_testUserId), "test-bucket", "Critical");

            _mockDocumentRepository
                .Setup(r => r.GetByIdAsync(_testDocumentId))
                .ReturnsAsync(existingDocument);

            _mockValidator
                .Setup(v => v.ValidateAsync(It.IsAny<Document>()))
                .ReturnsAsync(new FluentValidation.Results.ValidationResult());

            _mockEncryptionService
                .Setup(e => e.EncryptSensitiveField(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<EncryptionContext>()))
                .ReturnsAsync("encrypted_value");

            // Act
            var result = await _controller.UpdateDocumentAsync(_testDocumentId, updatedDocument);

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var returnedDocument = okResult.Value.Should().BeOfType<Document>().Subject;

            returnedDocument.Should().NotBeNull();
            returnedDocument.UserId.ToString().Should().Be(_testUserId);

            _mockCache.Verify(c => c.Remove(It.IsAny<string>()), Times.Exactly(2));
            
            _mockAuditService.Verify(a => a.LogDataAccess(
                _testUserId,
                "UpdateDocument",
                "Document",
                _testDocumentId.ToString(),
                It.IsAny<object>(),
                SecurityClassification.Critical), Times.Once);
        }

        [Fact]
        public async Task DeleteDocumentAsync_WithValidDocument_ReturnsNoContent()
        {
            // Arrange
            var document = new Document(DocumentType.DRIVERS_LICENSE, Guid.Parse(_testUserId), "test-bucket", "Critical");

            _mockDocumentRepository
                .Setup(r => r.GetByIdAsync(_testDocumentId))
                .ReturnsAsync(document);

            _mockDocumentRepository
                .Setup(r => r.DeleteAsync(_testDocumentId))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.DeleteDocumentAsync(_testDocumentId);

            // Assert
            result.Should().BeOfType<NoContentResult>();

            _mockCache.Verify(c => c.Remove(It.IsAny<string>()), Times.Exactly(2));
            
            _mockAuditService.Verify(a => a.LogDataAccess(
                _testUserId,
                "DeleteDocument",
                "Document",
                _testDocumentId.ToString(),
                null,
                SecurityClassification.Critical), Times.Once);
        }

        [Fact]
        public async Task GetUserDocumentsAsync_WithValidUser_ReturnsDecryptedDocuments()
        {
            // Arrange
            var userId = Guid.Parse(_testUserId);
            var documents = new List<Document>
            {
                new Document(DocumentType.DRIVERS_LICENSE, userId, "test-bucket", "Critical"),
                new Document(DocumentType.PASSPORT, userId, "test-bucket", "Critical")
            };

            _mockDocumentRepository
                .Setup(r => r.GetByUserIdAsync(userId))
                .ReturnsAsync(documents);

            _mockEncryptionService
                .Setup(e => e.DecryptSensitiveField(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<EncryptionContext>()))
                .ReturnsAsync("decrypted_value");

            // Act
            var result = await _controller.GetUserDocumentsAsync(userId);

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var returnedDocuments = okResult.Value.Should().BeAssignableTo<IEnumerable<Document>>().Subject;

            returnedDocuments.Should().HaveCount(2);
            
            _mockAuditService.Verify(a => a.LogDataAccess(
                _testUserId,
                "GetUserDocuments",
                "Document",
                userId.ToString(),
                null,
                SecurityClassification.Critical), Times.Once);
        }
    }
}