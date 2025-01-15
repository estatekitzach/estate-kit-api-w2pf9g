using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Moq;
using FluentAssertions;
using HotChocolate;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.DataProtection;
using EstateKit.Core.Entities;
using EstateKit.Core.Interfaces;
using EstateKit.Core.Enums;
using EstateKit.Business.API.GraphQL.Queries;

namespace EstateKit.Business.API.Tests.GraphQL.Queries
{
    /// <summary>
    /// Comprehensive unit tests for UserQueries GraphQL resolver with security validation
    /// </summary>
    public class UserQueriesTests : IDisposable
    {
        private readonly Mock<IUserRepository> _mockUserRepository;
        private readonly Mock<IDataProtectionProvider> _mockDataProtection;
        private readonly Mock<ILogger<UserQueries>> _mockLogger;
        private readonly IMemoryCache _memoryCache;
        private readonly UserQueries _userQueries;

        public UserQueriesTests()
        {
            _mockUserRepository = new Mock<IUserRepository>(MockBehavior.Strict);
            _mockDataProtection = new Mock<IDataProtectionProvider>();
            _mockLogger = new Mock<ILogger<UserQueries>>();
            _memoryCache = new MemoryCache(new MemoryCacheOptions());
            
            _userQueries = new UserQueries(
                _mockUserRepository.Object,
                _mockDataProtection.Object,
                _mockLogger.Object,
                _memoryCache
            );
        }

        [Fact]
        public async Task GetUserByIdAsync_ValidId_ReturnsUser()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var expectedUser = CreateTestUser(userId);

            _mockUserRepository
                .Setup(r => r.GetByIdAsync(userId))
                .ReturnsAsync(expectedUser);

            // Act
            var result = await _userQueries.GetUserByIdAsync(userId);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().Be(userId);
            result.Contact.Should().NotBeNull();
            result.Documents.Should().NotBeNull();
            result.Identifiers.Should().NotBeNull();
            result.Assets.Should().NotBeNull();

            // Verify repository call
            _mockUserRepository.Verify(r => r.GetByIdAsync(userId), Times.Once);
        }

        [Fact]
        public async Task GetUserByIdAsync_CacheHit_ReturnsCachedUser()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var expectedUser = CreateTestUser(userId);

            // Seed cache
            var cacheKey = $"user_{userId}";
            _memoryCache.Set(cacheKey, expectedUser);

            // Act
            var result = await _userQueries.GetUserByIdAsync(userId);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().Be(userId);

            // Verify repository was not called
            _mockUserRepository.Verify(r => r.GetByIdAsync(It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task GetUsersAsync_ReturnsAllUsers()
        {
            // Arrange
            var expectedUsers = new List<User>
            {
                CreateTestUser(Guid.NewGuid()),
                CreateTestUser(Guid.NewGuid())
            };

            _mockUserRepository
                .Setup(r => r.GetAllAsync())
                .ReturnsAsync(expectedUsers);

            // Act
            var result = await _userQueries.GetUsersAsync();

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(2);
            
            // Verify repository call
            _mockUserRepository.Verify(r => r.GetAllAsync(), Times.Once);
        }

        [Fact]
        public async Task GetUserByContactIdAsync_ValidId_ReturnsUser()
        {
            // Arrange
            var contactId = Guid.NewGuid();
            var expectedUser = CreateTestUser(Guid.NewGuid());

            _mockUserRepository
                .Setup(r => r.GetByContactIdAsync(contactId))
                .ReturnsAsync(expectedUser);

            // Act
            var result = await _userQueries.GetUserByContactIdAsync(contactId);

            // Assert
            result.Should().NotBeNull();
            result.Contact.Id.Should().Be(contactId);

            // Verify repository call
            _mockUserRepository.Verify(r => r.GetByContactIdAsync(contactId), Times.Once);
        }

        [Fact]
        public async Task GetUsersWithDocumentTypeAsync_ValidType_ReturnsFilteredUsers()
        {
            // Arrange
            var documentType = DocumentType.DRIVERS_LICENSE;
            var expectedUsers = new List<User>
            {
                CreateTestUserWithDocument(Guid.NewGuid(), documentType)
            };

            _mockUserRepository
                .Setup(r => r.GetUsersWithDocumentTypeAsync(documentType.ToString()))
                .ReturnsAsync(expectedUsers);

            // Act
            var result = await _userQueries.GetUsersWithDocumentTypeAsync(documentType);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(1);
            
            // Verify repository call
            _mockUserRepository.Verify(r => r.GetUsersWithDocumentTypeAsync(documentType.ToString()), Times.Once);
        }

        [Fact]
        public async Task GetUserByIdAsync_InvalidId_ReturnsNull()
        {
            // Arrange
            var userId = Guid.NewGuid();

            _mockUserRepository
                .Setup(r => r.GetByIdAsync(userId))
                .ReturnsAsync((User?)null);

            // Act
            var result = await _userQueries.GetUserByIdAsync(userId);

            // Assert
            result.Should().BeNull();
            
            // Verify repository call
            _mockUserRepository.Verify(r => r.GetByIdAsync(userId), Times.Once);
        }

        [Fact]
        public async Task GetUsersAsync_RepositoryException_ThrowsException()
        {
            // Arrange
            _mockUserRepository
                .Setup(r => r.GetAllAsync())
                .ThrowsAsync(new Exception("Database error"));

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => _userQueries.GetUsersAsync());
            
            // Verify repository call
            _mockUserRepository.Verify(r => r.GetAllAsync(), Times.Once);
        }

        private User CreateTestUser(Guid userId)
        {
            var contact = new Contact
            {
                FirstName = "John",
                LastName = "Doe",
                MiddleName = "Robert",
                MaidenName = null
            };

            var user = new User(
                new Mock<IAuditLogger>().Object,
                new Mock<ISecurityValidator>().Object
            )
            {
                Contact = contact,
                DateOfBirth = "1980-01-01",
                BirthPlace = "New York",
                MaritalStatus = "Married"
            };

            return user;
        }

        private User CreateTestUserWithDocument(Guid userId, DocumentType documentType)
        {
            var user = CreateTestUser(userId);
            var document = new Document(documentType, userId, "test-bucket", "critical");
            user.AddDocument(document);
            return user;
        }

        public void Dispose()
        {
            _memoryCache.Dispose();
        }
    }
}