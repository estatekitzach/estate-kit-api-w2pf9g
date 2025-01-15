using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.EntityFrameworkCore; // v10.0.0
using EstateKit.Core.Interfaces;
using EstateKit.Core.Entities;

namespace EstateKit.Infrastructure.Data.Repositories
{
    /// <summary>
    /// Implements secure data access and persistence operations for User entities with
    /// comprehensive field-level encryption and audit logging support.
    /// </summary>
    public class UserRepository : IUserRepository
    {
        private readonly ApplicationDbContext _context;

        /// <summary>
        /// Initializes a new instance of UserRepository with required security validations
        /// </summary>
        /// <param name="context">Database context with encryption and audit capabilities</param>
        /// <exception cref="ArgumentNullException">Thrown when context is null</exception>
        public UserRepository(ApplicationDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <inheritdoc/>
        public async Task<User?> GetByIdAsync(Guid id)
        {
            return await _context.Users
                .Include(u => u.Contact)
                    .ThenInclude(c => c.Addresses)
                .Include(u => u.Contact)
                    .ThenInclude(c => c.ContactMethods)
                .Include(u => u.Contact)
                    .ThenInclude(c => c.Relationships)
                .Include(u => u.Documents)
                .Include(u => u.Identifiers)
                .Include(u => u.Assets)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == id && u.IsActive);
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<User>> GetAllAsync()
        {
            return await _context.Users
                .Include(u => u.Contact)
                .Include(u => u.Documents)
                .Include(u => u.Identifiers)
                .AsNoTracking()
                .Where(u => u.IsActive)
                .ToListAsync();
        }

        /// <inheritdoc/>
        public async Task<User> AddAsync(User user)
        {
            if (user == null)
                throw new ArgumentNullException(nameof(user));

            await ValidateUserAsync(user);

            await _context.Users.AddAsync(user);
            await _context.SaveChangesAsync();

            return user;
        }

        /// <inheritdoc/>
        public async Task<User> UpdateAsync(User user)
        {
            if (user == null)
                throw new ArgumentNullException(nameof(user));

            await ValidateUserAsync(user);

            var existingUser = await _context.Users
                .Include(u => u.Contact)
                .FirstOrDefaultAsync(u => u.Id == user.Id && u.IsActive);

            if (existingUser == null)
                throw new InvalidOperationException($"User with ID {user.Id} not found or inactive");

            _context.Entry(existingUser).CurrentValues.SetValues(user);
            await _context.SaveChangesAsync();

            return existingUser;
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteAsync(Guid id)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == id && u.IsActive);

            if (user == null)
                return false;

            user.Deactivate();
            await _context.SaveChangesAsync();

            return true;
        }

        /// <inheritdoc/>
        public async Task<User?> GetByContactIdAsync(Guid contactId)
        {
            return await _context.Users
                .Include(u => u.Contact)
                .Include(u => u.Documents)
                .Include(u => u.Identifiers)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Contact.Id == contactId && u.IsActive);
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<User>> GetUsersWithDocumentTypeAsync(string documentType)
        {
            if (string.IsNullOrWhiteSpace(documentType))
                throw new ArgumentNullException(nameof(documentType));

            return await _context.Users
                .Include(u => u.Contact)
                .Include(u => u.Documents)
                .AsNoTracking()
                .Where(u => u.IsActive && 
                           u.Documents.Any(d => d.Type.ToString() == documentType))
                .ToListAsync();
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<User>> GetUsersWithIdentifierTypeAsync(string identifierType)
        {
            if (string.IsNullOrWhiteSpace(identifierType))
                throw new ArgumentNullException(nameof(identifierType));

            return await _context.Users
                .Include(u => u.Contact)
                .Include(u => u.Identifiers)
                .AsNoTracking()
                .Where(u => u.IsActive && 
                           u.Identifiers.Any(i => i.Type.ToString() == identifierType))
                .ToListAsync();
        }

        /// <summary>
        /// Validates user data integrity and business rules
        /// </summary>
        private async Task ValidateUserAsync(User user)
        {
            // Validate contact information
            if (user.Contact == null)
                throw new ArgumentException("Contact information is required");

            // Check for duplicate contact
            var existingContact = await _context.Users
                .AsNoTracking()
                .AnyAsync(u => u.Contact.Id == user.Contact.Id && 
                              u.Id != user.Id && 
                              u.IsActive);

            if (existingContact)
                throw new InvalidOperationException("Contact is already associated with another active user");

            // Validate documents
            if (user.Documents != null)
            {
                foreach (var document in user.Documents)
                {
                    if (string.IsNullOrWhiteSpace(document.S3BucketName))
                        throw new ArgumentException("S3 bucket name is required for documents");
                }
            }

            // Validate identifiers
            if (user.Identifiers != null)
            {
                foreach (var identifier in user.Identifiers)
                {
                    if (!identifier.Validate())
                        throw new ArgumentException($"Invalid identifier of type {identifier.Type}");
                }
            }
        }
    }
}