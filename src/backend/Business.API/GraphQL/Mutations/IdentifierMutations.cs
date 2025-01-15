// External package versions:
// HotChocolate v13.0.0
// HotChocolate.Types v13.0.0
// System.Threading.Tasks v9.0.0

using System;
using System.Threading.Tasks;
using HotChocolate;
using HotChocolate.Types;
using HotChocolate.Authorization;
using EstateKit.Core.Entities;
using EstateKit.Core.Interfaces;
using EstateKit.Core.Enums;
using EstateKit.Business.API.GraphQL.Types;
using EstateKit.Core.Security;
using EstateKit.Core.Logging;

namespace EstateKit.Business.API.GraphQL.Mutations
{
    /// <summary>
    /// Implements GraphQL mutations for managing government-issued identification documents
    /// with comprehensive security controls, field-level encryption, and audit logging.
    /// </summary>
    [ExtendObjectType("Mutation")]
    public class IdentifierMutations
    {
        private readonly IIdentifierRepository _identifierRepository;
        private readonly IEncryptionService _encryptionService;
        private readonly IAuditLogger _auditLogger;

        public IdentifierMutations(
            IIdentifierRepository identifierRepository,
            IEncryptionService encryptionService,
            IAuditLogger auditLogger)
        {
            _identifierRepository = identifierRepository ?? 
                throw new ArgumentNullException(nameof(identifierRepository));
            _encryptionService = encryptionService ?? 
                throw new ArgumentNullException(nameof(encryptionService));
            _auditLogger = auditLogger ?? 
                throw new ArgumentNullException(nameof(auditLogger));
        }

        /// <summary>
        /// Creates a new government-issued identification record with field-level encryption
        /// and comprehensive audit logging.
        /// </summary>
        [UseFirstOrDefault]
        [UseProjection]
        [Authorize(Policy = "IdentifierCreate")]
        [RateLimit(1000, 60)]
        [AuditLog("Identifier.Create")]
        public async Task<Identifier> AddIdentifierAsync(
            Guid userId,
            IdentifierType type,
            string value,
            string issuingAuthority,
            DateTime issueDate,
            DateTime expiryDate)
        {
            // Validate input parameters
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Identifier value cannot be empty", nameof(value));
            
            if (string.IsNullOrWhiteSpace(issuingAuthority))
                throw new ArgumentException("Issuing authority cannot be empty", nameof(issuingAuthority));

            // Create new identifier with encrypted sensitive data
            var identifier = new Identifier
            {
                UserId = userId,
                Type = type,
                Value = await _encryptionService.EncryptAsync(value),
                IssuingAuthority = issuingAuthority,
                IssueDate = issueDate,
                ExpiryDate = expiryDate
            };

            // Validate the identifier data
            if (!identifier.Validate())
            {
                await _auditLogger.LogSecurityEventAsync(
                    "Identifier.ValidationFailed",
                    userId,
                    new { Type = type });
                throw new ValidationException("Invalid identifier data");
            }

            try
            {
                // Persist the identifier with encrypted fields
                var result = await _identifierRepository.AddAsync(identifier);

                await _auditLogger.LogSecurityEventAsync(
                    "Identifier.Created",
                    userId,
                    new { Id = result.Id, Type = type });

                return result;
            }
            catch (Exception ex)
            {
                await _auditLogger.LogSecurityEventAsync(
                    "Identifier.CreateFailed",
                    userId,
                    new { Type = type, Error = ex.Message });
                throw;
            }
        }

        /// <summary>
        /// Updates an existing government-issued identification record with encryption
        /// key rotation and concurrency handling.
        /// </summary>
        [UseFirstOrDefault]
        [UseProjection]
        [Authorize(Policy = "IdentifierUpdate")]
        [RateLimit(500, 60)]
        [AuditLog("Identifier.Update")]
        [ConcurrencyCheck]
        public async Task<Identifier> UpdateIdentifierAsync(
            Guid id,
            string value,
            string issuingAuthority,
            DateTime issueDate,
            DateTime expiryDate)
        {
            var existingIdentifier = await _identifierRepository.GetByIdAsync(id);
            if (existingIdentifier == null)
                throw new NotFoundException("Identifier not found");

            try
            {
                // Re-encrypt sensitive data with key rotation if needed
                var encryptedValue = await _encryptionService.EncryptWithRotationAsync(
                    value,
                    existingIdentifier.Value);

                // Update identifier with new encrypted values
                existingIdentifier.Update(
                    encryptedValue,
                    issuingAuthority,
                    issueDate,
                    expiryDate);

                var result = await _identifierRepository.UpdateAsync(existingIdentifier);

                await _auditLogger.LogSecurityEventAsync(
                    "Identifier.Updated",
                    existingIdentifier.UserId,
                    new { Id = id, Type = existingIdentifier.Type });

                return result;
            }
            catch (Exception ex)
            {
                await _auditLogger.LogSecurityEventAsync(
                    "Identifier.UpdateFailed",
                    existingIdentifier.UserId,
                    new { Id = id, Error = ex.Message });
                throw;
            }
        }

        /// <summary>
        /// Performs soft deletion of government-issued identification record with
        /// cascade handling and audit logging.
        /// </summary>
        [Authorize(Policy = "IdentifierDelete")]
        [RateLimit(100, 60)]
        [AuditLog("Identifier.Delete")]
        public async Task<bool> DeleteIdentifierAsync(Guid id)
        {
            var identifier = await _identifierRepository.GetByIdAsync(id);
            if (identifier == null)
                throw new NotFoundException("Identifier not found");

            try
            {
                var result = await _identifierRepository.DeleteAsync(id);

                await _auditLogger.LogSecurityEventAsync(
                    "Identifier.Deleted",
                    identifier.UserId,
                    new { Id = id, Type = identifier.Type });

                return result;
            }
            catch (Exception ex)
            {
                await _auditLogger.LogSecurityEventAsync(
                    "Identifier.DeleteFailed",
                    identifier.UserId,
                    new { Id = id, Error = ex.Message });
                throw;
            }
        }
    }
}