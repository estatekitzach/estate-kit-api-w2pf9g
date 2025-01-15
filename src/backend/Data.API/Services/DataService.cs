using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore; // v10.0.0
using Microsoft.Extensions.Logging; // v9.0.0
using EstateKit.Infrastructure.Data;
using EstateKit.Core.Entities;
using EstateKit.Data.API.Services;
using EstateKit.Infrastructure.Security;

namespace EstateKit.Data.API.Services
{
    /// <summary>
    /// Core service that orchestrates secure data access operations with field-level encryption,
    /// comprehensive audit logging, and robust validation for the Data API.
    /// </summary>
    public sealed class DataService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly EncryptionService _encryptionService;
        private readonly AuditService _auditService;
        private readonly ILogger<DataService> _logger;

        public DataService(
            ApplicationDbContext dbContext,
            EncryptionService encryptionService,
            AuditService auditService,
            ILogger<DataService> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Retrieves an entity by ID with decryption and audit logging
        /// </summary>
        public async Task<T> GetEntityAsync<T>(Guid id, string userId) where T : class
        {
            if (id == Guid.Empty) throw new ArgumentException("Invalid ID", nameof(id));
            if (string.IsNullOrEmpty(userId)) throw new ArgumentNullException(nameof(userId));

            try
            {
                var entity = await _dbContext.Set<T>().FindAsync(id);
                if (entity == null) return null;

                // Decrypt sensitive fields if entity has encrypted attributes
                if (entity.GetType().GetCustomAttributes(typeof(EncryptedAttribute), true).Any())
                {
                    var context = new EncryptionContext
                    {
                        EntityType = typeof(T).Name,
                        EntityId = id.ToString(),
                        UserId = userId
                    };

                    await DecryptEntityFieldsAsync(entity, context);
                }

                // Log data access
                await _auditService.LogDataAccess(
                    userId,
                    "Read",
                    typeof(T).Name,
                    id.ToString(),
                    null,
                    SecurityClassification.Internal);

                return entity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving entity {Type} with ID {Id}", typeof(T).Name, id);
                throw;
            }
        }

        /// <summary>
        /// Creates a new entity with encryption and audit logging
        /// </summary>
        public async Task<T> CreateEntityAsync<T>(T entity, string userId) where T : class
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            if (string.IsNullOrEmpty(userId)) throw new ArgumentNullException(nameof(userId));

            using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                // Encrypt sensitive fields if entity has encrypted attributes
                if (entity.GetType().GetCustomAttributes(typeof(EncryptedAttribute), true).Any())
                {
                    var context = new EncryptionContext
                    {
                        EntityType = typeof(T).Name,
                        EntityId = GetEntityId(entity).ToString(),
                        UserId = userId
                    };

                    await EncryptEntityFieldsAsync(entity, context);
                }

                _dbContext.Set<T>().Add(entity);
                await _dbContext.SaveChangesAsync();

                // Log creation
                await _auditService.LogDataAccess(
                    userId,
                    "Create",
                    typeof(T).Name,
                    GetEntityId(entity).ToString(),
                    entity,
                    SecurityClassification.Internal);

                await transaction.CommitAsync();
                return entity;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error creating entity {Type}", typeof(T).Name);
                throw;
            }
        }

        /// <summary>
        /// Updates an existing entity with encryption and audit logging
        /// </summary>
        public async Task<T> UpdateEntityAsync<T>(T entity, string userId) where T : class
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            if (string.IsNullOrEmpty(userId)) throw new ArgumentNullException(nameof(userId));

            using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                var entityId = GetEntityId(entity);
                var originalEntity = await _dbContext.Set<T>().FindAsync(entityId);
                if (originalEntity == null)
                    throw new InvalidOperationException($"Entity {typeof(T).Name} with ID {entityId} not found");

                // Track changes for audit logging
                var changes = GetEntityChanges(originalEntity, entity);

                // Encrypt sensitive fields if entity has encrypted attributes
                if (entity.GetType().GetCustomAttributes(typeof(EncryptedAttribute), true).Any())
                {
                    var context = new EncryptionContext
                    {
                        EntityType = typeof(T).Name,
                        EntityId = entityId.ToString(),
                        UserId = userId
                    };

                    await EncryptEntityFieldsAsync(entity, context);
                }

                _dbContext.Entry(originalEntity).CurrentValues.SetValues(entity);
                await _dbContext.SaveChangesAsync();

                // Log update with changes
                await _auditService.LogDataAccess(
                    userId,
                    "Update",
                    typeof(T).Name,
                    entityId.ToString(),
                    changes,
                    SecurityClassification.Internal);

                await transaction.CommitAsync();
                return entity;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error updating entity {Type}", typeof(T).Name);
                throw;
            }
        }

        /// <summary>
        /// Performs a soft delete of an entity with audit logging
        /// </summary>
        public async Task<bool> DeleteEntityAsync<T>(Guid id, string userId) where T : class
        {
            if (id == Guid.Empty) throw new ArgumentException("Invalid ID", nameof(id));
            if (string.IsNullOrEmpty(userId)) throw new ArgumentNullException(nameof(userId));

            using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                var entity = await _dbContext.Set<T>().FindAsync(id);
                if (entity == null) return false;

                // Perform soft delete if supported
                var isActiveProperty = entity.GetType().GetProperty("IsActive");
                if (isActiveProperty != null)
                {
                    isActiveProperty.SetValue(entity, false);
                }
                else
                {
                    _dbContext.Set<T>().Remove(entity);
                }

                await _dbContext.SaveChangesAsync();

                // Log deletion
                await _auditService.LogDataAccess(
                    userId,
                    "Delete",
                    typeof(T).Name,
                    id.ToString(),
                    null,
                    SecurityClassification.Internal);

                await transaction.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error deleting entity {Type} with ID {Id}", typeof(T).Name, id);
                throw;
            }
        }

        private async Task EncryptEntityFieldsAsync<T>(T entity, EncryptionContext context) where T : class
        {
            var encryptedFields = entity.GetType()
                .GetProperties()
                .Where(p => p.GetCustomAttributes(typeof(EncryptedFieldAttribute), true).Any());

            foreach (var field in encryptedFields)
            {
                var value = field.GetValue(entity)?.ToString();
                if (!string.IsNullOrEmpty(value))
                {
                    var encryptedValue = await _encryptionService.EncryptSensitiveField(
                        value,
                        field.Name,
                        context.UserId,
                        context);
                    field.SetValue(entity, encryptedValue);
                }
            }
        }

        private async Task DecryptEntityFieldsAsync<T>(T entity, EncryptionContext context) where T : class
        {
            var encryptedFields = entity.GetType()
                .GetProperties()
                .Where(p => p.GetCustomAttributes(typeof(EncryptedFieldAttribute), true).Any());

            foreach (var field in encryptedFields)
            {
                var encryptedValue = field.GetValue(entity)?.ToString();
                if (!string.IsNullOrEmpty(encryptedValue))
                {
                    var decryptedValue = await _encryptionService.DecryptSensitiveField(
                        encryptedValue,
                        field.Name,
                        context.UserId,
                        context);
                    field.SetValue(entity, decryptedValue);
                }
            }
        }

        private Guid GetEntityId<T>(T entity) where T : class
        {
            var idProperty = entity.GetType().GetProperty("Id");
            if (idProperty == null)
                throw new InvalidOperationException($"Entity {typeof(T).Name} does not have an Id property");

            return (Guid)idProperty.GetValue(entity);
        }

        private object GetEntityChanges<T>(T original, T updated) where T : class
        {
            var changes = new Dictionary<string, object>();
            var properties = typeof(T).GetProperties();

            foreach (var property in properties)
            {
                var originalValue = property.GetValue(original);
                var updatedValue = property.GetValue(updated);

                if (!Equals(originalValue, updatedValue))
                {
                    changes[property.Name] = new
                    {
                        Original = originalValue,
                        Updated = updatedValue
                    };
                }
            }

            return changes;
        }
    }
}