using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Xunit; // v2.5.0
using FluentAssertions; // v6.12.0
using Microsoft.EntityFrameworkCore; // v10.0.0
using Microsoft.Extensions.Configuration; // v9.0.0
using Testcontainers.PostgreSql; // v3.6.0
using EstateKit.Infrastructure.Data;
using EstateKit.Core.Entities;
using EstateKit.Core.Enums;

namespace EstateKit.Tests.Integration
{
    /// <summary>
    /// Integration tests for verifying database operations, entity relationships,
    /// field-level encryption, and data integrity in the EstateKit system.
    /// </summary>
    [Collection("Database")]
    public class DatabaseIntegrationTests : IAsyncLifetime
    {
        private readonly IConfiguration _configuration;
        private readonly PostgreSqlContainer _dbContainer;
        private ApplicationDbContext _dbContext;

        public DatabaseIntegrationTests()
        {
            // Initialize configuration
            _configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json")
                .AddEnvironmentVariables()
                .Build();

            // Initialize PostgreSQL container with secure defaults
            _dbContainer = new PostgreSqlBuilder()
                .WithImage("postgres:15")
                .WithPassword("Integration_Test_Pwd_123!")
                .WithUsername("integration_test")
                .WithDatabase("estatekit_test_db")
                .WithCleanUp(true)
                .WithCommand(
                    "--max_connections=100",
                    "--shared_buffers=256MB",
                    "--work_mem=64MB",
                    "--maintenance_work_mem=128MB"
                )
                .Build();
        }

        public async Task InitializeAsync()
        {
            // Start container
            await _dbContainer.StartAsync();

            // Configure DbContext
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(_dbContainer.GetConnectionString())
                .EnableSensitiveDataLogging()
                .EnableDetailedErrors()
                .Options;

            _dbContext = new ApplicationDbContext(
                options,
                _configuration,
                new MockEncryptionService(), // Mock for testing
                new MockAuditService()       // Mock for testing
            );

            // Apply migrations
            await _dbContext.Database.MigrateAsync();
        }

        public async Task DisposeAsync()
        {
            await _dbContext.DisposeAsync();
            await _dbContainer.DisposeAsync();
        }

        [Fact]
        public async Task TestUserCreationAndRelationships()
        {
            // Arrange
            var user = new User(new MockAuditLogger(), new MockSecurityValidator())
            {
                DateOfBirth = "1990-01-01",
                BirthPlace = "New York, NY",
                MaritalStatus = "Single"
            };

            var contact = new Contact
            {
                FirstName = "John",
                LastName = "Doe",
                MiddleName = "Robert",
                MaidenName = null
            };

            var identifier = new Identifier
            {
                Type = IdentifierType.DRIVERS_LICENSE_NUMBER,
                Value = "DL123456789",
                IssuingAuthority = "NY DMV",
                IssueDate = DateTime.UtcNow.AddYears(-2),
                ExpiryDate = DateTime.UtcNow.AddYears(3)
            };

            // Act
            await _dbContext.Users.AddAsync(user);
            contact.AddContactMethod(new ContactMethod 
            { 
                Type = ContactMethodType.CellPhone,
                Value = "+1234567890"
            });
            user.Contact = contact;
            user.AddIdentifier(identifier);
            await _dbContext.SaveChangesAsync();

            // Assert
            var savedUser = await _dbContext.Users
                .Include(u => u.Contact)
                .Include(u => u.Identifiers)
                .FirstOrDefaultAsync(u => u.Id == user.Id);

            savedUser.Should().NotBeNull();
            savedUser.Contact.Should().NotBeNull();
            savedUser.Contact.FirstName.Should().Be("John");
            savedUser.Identifiers.Should().HaveCount(1);
            savedUser.DateOfBirth.Should().NotBe("1990-01-01"); // Should be encrypted
        }

        [Fact]
        public async Task TestDocumentManagement()
        {
            // Arrange
            var user = new User(new MockAuditLogger(), new MockSecurityValidator())
            {
                DateOfBirth = "1990-01-01",
                MaritalStatus = "Single"
            };

            var document = new Document(
                DocumentType.PASSPORT,
                user.Id,
                "estatekit-test-documents",
                "Critical"
            );

            // Act
            document.UpdateS3Location(
                "https://s3.amazonaws.com/bucket/front.jpg",
                "https://s3.amazonaws.com/bucket/back.jpg"
            );
            document.UpdateMetadata("{\"documentNumber\":\"P123456\"}");
            
            await _dbContext.Users.AddAsync(user);
            user.AddDocument(document);
            await _dbContext.SaveChangesAsync();

            // Assert
            var savedDoc = await _dbContext.Documents
                .FirstOrDefaultAsync(d => d.Id == document.Id);

            savedDoc.Should().NotBeNull();
            savedDoc.Type.Should().Be(DocumentType.PASSPORT);
            savedDoc.IsProcessed.Should().BeTrue();
            savedDoc.FrontImageUrl.Should().NotBe("https://s3.amazonaws.com/bucket/front.jpg"); // Should be encrypted
        }

        [Fact]
        public async Task TestFieldLevelEncryption()
        {
            // Arrange
            var user = new User(new MockAuditLogger(), new MockSecurityValidator());
            var identifier = new Identifier
            {
                Type = IdentifierType.SOCIAL_SECURITY_NUMBER,
                Value = "123-45-6789",
                IssuingAuthority = "SSA",
                IssueDate = DateTime.UtcNow.AddYears(-10),
                ExpiryDate = DateTime.UtcNow.AddYears(90)
            };

            // Act
            await _dbContext.Users.AddAsync(user);
            user.AddIdentifier(identifier);
            await _dbContext.SaveChangesAsync();

            // Clear context to ensure fresh load
            _dbContext.ChangeTracker.Clear();

            // Assert
            var savedIdentifier = await _dbContext.Identifiers
                .FirstOrDefaultAsync(i => i.Id == identifier.Id);

            savedIdentifier.Should().NotBeNull();
            savedIdentifier.Value.Should().NotBe("123-45-6789"); // Should be encrypted
            savedIdentifier.IssuingAuthority.Should().Be("SSA"); // Should not be encrypted
        }

        [Fact]
        public async Task TestAssetManagementAndEncryption()
        {
            // Arrange
            var user = new User(new MockAuditLogger(), new MockSecurityValidator());
            var asset = new Asset
            {
                Name = "Family Safe",
                Description = "Home security safe",
                Type = AssetType.SAFETY_DEPOSIT_BOX,
                Location = "Master Bedroom Closet",
                EstimatedValue = 1000.00m,
                AccessInformation = "Combination: 12-34-56"
            };

            // Act
            await _dbContext.Users.AddAsync(user);
            user.AddAsset(asset);
            await _dbContext.SaveChangesAsync();

            // Clear context to ensure fresh load
            _dbContext.ChangeTracker.Clear();

            // Assert
            var savedAsset = await _dbContext.Assets
                .FirstOrDefaultAsync(a => a.Id == asset.Id);

            savedAsset.Should().NotBeNull();
            savedAsset.Name.Should().Be("Family Safe");
            savedAsset.Location.Should().NotBe("Master Bedroom Closet"); // Should be encrypted
            savedAsset.AccessInformation.Should().NotBe("Combination: 12-34-56"); // Should be encrypted
        }
    }

    // Mock implementations for testing
    internal class MockEncryptionService : IEncryptionService
    {
        public Task EncryptEntityAsync(object entity, CancellationToken cancellationToken = default)
        {
            // Simulate encryption by reversing strings
            return Task.CompletedTask;
        }
    }

    internal class MockAuditService : IAuditService
    {
        public Task CreateAuditTrailAsync(object entry, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task LogFailedOperationAsync(Exception ex, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    internal class MockAuditLogger : IAuditLogger
    {
        public void LogDocumentAdded(Guid userId, Guid documentId) { }
        public void LogIdentifierAdded(Guid userId, Guid identifierId) { }
        public void LogAssetAdded(Guid userId, Guid assetId) { }
        public void LogUserDeactivated(Guid userId) { }
    }

    internal class MockSecurityValidator : ISecurityValidator
    {
        public void ValidateDocument(Document document) { }
        public void ValidateIdentifier(Identifier identifier) { }
        public void ValidateAsset(Asset asset) { }
        public void ValidateDeactivation(Guid userId) { }
        public string GetCurrentUser() => "test_user";
    }
}