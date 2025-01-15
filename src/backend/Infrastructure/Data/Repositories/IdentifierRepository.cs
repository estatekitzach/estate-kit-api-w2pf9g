using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using EstateKit.Core.Entities;
using EstateKit.Core.Interfaces;
using EstateKit.Core.Enums;
using EstateKit.Infrastructure.Security;
using Polly;
using Polly.Retry;

namespace EstateKit.Infrastructure.Data.Repositories
{
    /// <summary>
    /// Repository implementation for managing government-issued identification documents with
    /// comprehensive field-level encryption, validation, and audit logging capabilities.
    /// </summary>
    public class IdentifierRepository : IIdentifierRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly IEncryptionProvider _encryptionProvider;
        private readonly ILogger<IdentifierRepository> _logger;
        private readonly AsyncRetryPolicy _retryPolicy;
        private readonly EncryptionContext _encryptionContext;

        public IdentifierRepository(
            ApplicationDbContext context,
            IEncryptionProvider encryptionProvider,
            ILogger<IdentifierRepository> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _encryptionProvider = encryptionProvider ?? throw new ArgumentNullException(nameof(encryptionProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Configure retry policy for transient failures
            _retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(3, retryAttempt => 
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (exception, timeSpan, retryCount, context) =>
                    {
                        _logger.LogWarning(exception, 
                            "Retry {RetryCount} after {Delay}s due to {Message}", 
                            retryCount, timeSpan.TotalSeconds, exception.Message);
                    });

            _encryptionContext = new EncryptionContext
            {
                EntityType = "Identifier",
                SecurityLevel = "Critical",
                KeyRotationDays = 90
            };
        }

        /// <summary>
        /// Retrieves an identifier by its unique ID with decryption and validation
        /// </summary>
        public async Task<Identifier> GetByIdAsync(Guid id)
        {
            try
            {
                _logger.LogDebug("Retrieving identifier with ID: {Id}", id);

                var identifier = await _retryPolicy.ExecuteAsync(async () =>
                    await _context.Identifiers
                        .AsNoTracking()
                        .FirstOrDefaultAsync(i => i.Id == id && i.IsActive));

                if (identifier == null)
                {
                    _logger.LogInformation("Identifier not found with ID: {Id}", id);
                    return null;
                }

                // Decrypt sensitive fields
                identifier.Value = await _encryptionProvider.DecryptField(
                    new EncryptedValue { EncryptedData = identifier.Value },
                    "Value",
                    _encryptionContext);

                _logger.LogInformation("Successfully retrieved identifier with ID: {Id}", id);
                return identifier;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving identifier with ID: {Id}", id);
                throw;
            }
        }

        /// <summary>
        /// Retrieves all active identifiers for a specific user with decryption
        /// </summary>
        public async Task<IEnumerable<Identifier>> GetByUserIdAsync(Guid userId)
        {
            try
            {
                _logger.LogDebug("Retrieving identifiers for user: {UserId}", userId);

                var identifiers = await _retryPolicy.ExecuteAsync(async () =>
                    await _context.Identifiers
                        .AsNoTracking()
                        .Where(i => i.UserId == userId && i.IsActive)
                        .ToListAsync());

                // Decrypt sensitive fields for each identifier
                foreach (var identifier in identifiers)
                {
                    identifier.Value = await _encryptionProvider.DecryptField(
                        new EncryptedValue { EncryptedData = identifier.Value },
                        "Value",
                        _encryptionContext);
                }

                _logger.LogInformation("Retrieved {Count} identifiers for user: {UserId}", 
                    identifiers.Count, userId);
                return identifiers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving identifiers for user: {UserId}", userId);
                throw;
            }
        }

        /// <summary>
        /// Retrieves all active identifiers of a specific type for a user with decryption
        /// </summary>
        public async Task<IEnumerable<Identifier>> GetByTypeAsync(Guid userId, IdentifierType type)
        {
            try
            {
                _logger.LogDebug("Retrieving identifiers of type {Type} for user: {UserId}", 
                    type, userId);

                var identifiers = await _retryPolicy.ExecuteAsync(async () =>
                    await _context.Identifiers
                        .AsNoTracking()
                        .Where(i => i.UserId == userId && i.Type == type && i.IsActive)
                        .ToListAsync());

                // Decrypt sensitive fields for each identifier
                foreach (var identifier in identifiers)
                {
                    identifier.Value = await _encryptionProvider.DecryptField(
                        new EncryptedValue { EncryptedData = identifier.Value },
                        "Value",
                        _encryptionContext);
                }

                _logger.LogInformation(
                    "Retrieved {Count} identifiers of type {Type} for user: {UserId}",
                    identifiers.Count, type, userId);
                return identifiers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Error retrieving identifiers of type {Type} for user: {UserId}", 
                    type, userId);
                throw;
            }
        }

        /// <summary>
        /// Adds a new identifier with encryption and validation
        /// </summary>
        public async Task<Identifier> AddAsync(Identifier identifier)
        {
            try
            {
                if (identifier == null)
                    throw new ArgumentNullException(nameof(identifier));

                if (!identifier.Validate())
                    throw new InvalidOperationException("Identifier validation failed");

                _logger.LogDebug("Adding new identifier of type {Type}", identifier.Type);

                // Encrypt sensitive fields
                identifier.Value = (await _encryptionProvider.EncryptField(
                    identifier.Value,
                    "Value",
                    _encryptionContext)).EncryptedData;

                await _retryPolicy.ExecuteAsync(async () =>
                {
                    await _context.Identifiers.AddAsync(identifier);
                    await _context.SaveChangesAsync();
                    return true;
                });

                _logger.LogInformation(
                    "Successfully added new identifier with ID: {Id}", 
                    identifier.Id);
                return identifier;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding new identifier");
                throw;
            }
        }

        /// <summary>
        /// Updates an existing identifier with encryption and validation
        /// </summary>
        public async Task<Identifier> UpdateAsync(Identifier identifier)
        {
            try
            {
                if (identifier == null)
                    throw new ArgumentNullException(nameof(identifier));

                if (!identifier.Validate())
                    throw new InvalidOperationException("Identifier validation failed");

                _logger.LogDebug("Updating identifier with ID: {Id}", identifier.Id);

                // Encrypt sensitive fields
                identifier.Value = (await _encryptionProvider.EncryptField(
                    identifier.Value,
                    "Value",
                    _encryptionContext)).EncryptedData;

                await _retryPolicy.ExecuteAsync(async () =>
                {
                    _context.Identifiers.Update(identifier);
                    await _context.SaveChangesAsync();
                    return true;
                });

                _logger.LogInformation(
                    "Successfully updated identifier with ID: {Id}", 
                    identifier.Id);
                return identifier;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating identifier with ID: {Id}", identifier.Id);
                throw;
            }
        }

        /// <summary>
        /// Performs a soft delete of an identifier
        /// </summary>
        public async Task<bool> DeleteAsync(Guid id)
        {
            try
            {
                _logger.LogDebug("Soft deleting identifier with ID: {Id}", id);

                var success = await _retryPolicy.ExecuteAsync(async () =>
                {
                    var identifier = await _context.Identifiers
                        .FirstOrDefaultAsync(i => i.Id == id && i.IsActive);

                    if (identifier == null)
                        return false;

                    identifier.IsActive = false;
                    await _context.SaveChangesAsync();
                    return true;
                });

                if (success)
                {
                    _logger.LogInformation("Successfully soft deleted identifier with ID: {Id}", id);
                }
                else
                {
                    _logger.LogWarning("Identifier not found for deletion with ID: {Id}", id);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting identifier with ID: {Id}", id);
                throw;
            }
        }
    }
}