using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore; // v10.0.0
using Microsoft.Security.Encryption; // v5.0.0
using Microsoft.Logging.Audit; // v3.0.0
using EstateKit.Core.Interfaces;
using EstateKit.Core.Entities;

namespace EstateKit.Infrastructure.Data.Repositories
{
    /// <summary>
    /// Implements secure document persistence operations with field-level encryption,
    /// comprehensive audit logging, and high-performance data access.
    /// </summary>
    public class DocumentRepository : IDocumentRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly IEncryptionService _encryptionService;
        private readonly IAuditService _auditService;

        public DocumentRepository(
            ApplicationDbContext context,
            IEncryptionService encryptionService,
            IAuditService auditService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        }

        public async Task<Document> GetByIdAsync(Guid id)
        {
            if (id == Guid.Empty)
                throw new ArgumentException("Invalid document ID", nameof(id));

            var document = await _context.Documents
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == id);

            if (document != null)
            {
                await _encryptionService.DecryptEntityAsync(document);
                await _auditService.LogAccessAsync("Document", id, "Retrieved");
            }

            return document;
        }

        public async Task<IEnumerable<Document>> GetByUserIdAsync(Guid userId)
        {
            if (userId == Guid.Empty)
                throw new ArgumentException("Invalid user ID", nameof(userId));

            var documents = await _context.Documents
                .AsNoTracking()
                .Where(d => d.UserId == userId)
                .ToListAsync();

            foreach (var document in documents)
            {
                await _encryptionService.DecryptEntityAsync(document);
            }

            await _auditService.LogBulkAccessAsync("Document", userId, documents.Count, "Retrieved");

            return documents;
        }

        public async Task<Document> AddAsync(Document document)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            await _encryptionService.EncryptEntityAsync(document);

            await _context.Documents.AddAsync(document);
            await _context.SaveChangesAsync();

            await _auditService.LogOperationAsync(
                "Document",
                document.Id,
                "Created",
                new { DocumentType = document.Type, UserId = document.UserId }
            );

            return document;
        }

        public async Task<bool> UpdateAsync(Document document)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            var existingDocument = await _context.Documents
                .FirstOrDefaultAsync(d => d.Id == document.Id);

            if (existingDocument == null)
                return false;

            await _encryptionService.EncryptEntityAsync(document);

            _context.Entry(existingDocument).CurrentValues.SetValues(document);
            var result = await _context.SaveChangesAsync() > 0;

            if (result)
            {
                await _auditService.LogOperationAsync(
                    "Document",
                    document.Id,
                    "Updated",
                    new { DocumentType = document.Type, UserId = document.UserId }
                );
            }

            return result;
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            if (id == Guid.Empty)
                throw new ArgumentException("Invalid document ID", nameof(id));

            var document = await _context.Documents
                .FirstOrDefaultAsync(d => d.Id == id);

            if (document == null)
                return false;

            _context.Documents.Remove(document);
            var result = await _context.SaveChangesAsync() > 0;

            if (result)
            {
                await _auditService.LogOperationAsync(
                    "Document",
                    id,
                    "Deleted",
                    new { DocumentType = document.Type, UserId = document.UserId }
                );
            }

            return result;
        }

        public async Task<bool> UpdateProcessingStatusAsync(Guid id, string metadata)
        {
            if (id == Guid.Empty)
                throw new ArgumentException("Invalid document ID", nameof(id));

            if (string.IsNullOrEmpty(metadata))
                throw new ArgumentException("Metadata cannot be null or empty", nameof(metadata));

            var document = await _context.Documents
                .FirstOrDefaultAsync(d => d.Id == id);

            if (document == null)
                return false;

            var encryptedMetadata = await _encryptionService.EncryptFieldAsync(metadata);
            document.UpdateMetadata(encryptedMetadata);

            var result = await _context.SaveChangesAsync() > 0;

            if (result)
            {
                await _auditService.LogOperationAsync(
                    "Document",
                    id,
                    "ProcessingStatusUpdated",
                    new { UserId = document.UserId, IsProcessed = true }
                );
            }

            return result;
        }

        public async Task<bool> UpdateLocationAsync(Guid id, string location)
        {
            if (id == Guid.Empty)
                throw new ArgumentException("Invalid document ID", nameof(id));

            if (string.IsNullOrEmpty(location))
                throw new ArgumentException("Location cannot be null or empty", nameof(location));

            var document = await _context.Documents
                .FirstOrDefaultAsync(d => d.Id == id);

            if (document == null)
                return false;

            var encryptedLocation = await _encryptionService.EncryptFieldAsync(location);
            document.UpdateLocation(encryptedLocation);

            var result = await _context.SaveChangesAsync() > 0;

            if (result)
            {
                await _auditService.LogOperationAsync(
                    "Document",
                    id,
                    "LocationUpdated",
                    new { UserId = document.UserId, Location = location }
                );
            }

            return result;
        }
    }
}