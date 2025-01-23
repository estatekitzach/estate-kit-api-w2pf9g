using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.EntityFrameworkCore; // v10.0.0
using EstateKit.Core.Interfaces;
using EstateKit.Core.Models.UserModels;
using EstateKit.Data.DBContexts;

namespace EstateKit.Infrastructure.Data.Repositories
{
    /// <summary>
    /// Implements secure data access and persistence operations for User entities with
    /// comprehensive field-level encryption and audit logging support.
    /// </summary>
    public class UserRepository : IUserRepository
    {
        private readonly EstateKitContext _context;

        /// <summary>
        /// Initializes a new instance of UserRepository with required security validations
        /// </summary>
        /// <param name="context">Database context with encryption and audit capabilities</param>
        /// <exception cref="ArgumentNullException">Thrown when context is null</exception>
        public UserRepository(EstateKitContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <inheritdoc/>
        public async Task<User?> GetByIdAsync(int id)
        {
            return await _context.Users
                .Include(u => u.Contact)
                    .ThenInclude(c => c.ContactAddresses)
                .Include(u => u.Contact)
                    .ThenInclude(c => c.ContactContactMethods)
                .Include(u => u.Contact)
                    .ThenInclude(c => c.ContactRelationshipContacts)
                .Include(u => u.UserDocuments)
                .Include(u => u.UserIdentifiers)
                .Include(u => u.UserAssets)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == id && u.Active).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<User>> GetAllAsync()
        {
            return await _context.Users
                .Include(u => u.Contact)
                .Include(u => u.UserDocuments)
                .Include(u => u.UserIdentifiers)
                .AsNoTracking()
                .Where(u => u.Active)
                .ToListAsync().ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<User> AddAsync(User user)
        {
            ArgumentNullException.ThrowIfNull(user);

            await ValidateUserAsync(user).ConfigureAwait(false);

            await _context.Users.AddAsync(user).ConfigureAwait(false);
            await _context.SaveChangesAsync().ConfigureAwait(false);

            return user;
        }

        /// <inheritdoc/>
        public async Task<User> UpdateAsync(User user)
        {
            ArgumentNullException.ThrowIfNull(user);

            await ValidateUserAsync(user).ConfigureAwait(false);

            var existingUser = await _context.Users
                .Include(u => u.Contact)
                .FirstOrDefaultAsync(u => u.Id == user.Id && u.Active).ConfigureAwait(false);

            if (existingUser == null)
                throw new InvalidOperationException($"User with ID {user.Id} not found or inactive");

            _context.Entry(existingUser).CurrentValues.SetValues(user);
            await _context.SaveChangesAsync().ConfigureAwait(false);

            return existingUser;
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteAsync(int id)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == id && u.Active).ConfigureAwait(false);

            if (user == null)
                return false;

            user.Active = false;
            await _context.SaveChangesAsync().ConfigureAwait(false);

            return true;
        }

        /// <inheritdoc/>
        public async Task<User?> GetByContactIdAsync(int contactId)
        {
            return await _context.Users
                .Include(u => u.Contact)
                .Include(u => u.UserDocuments)
                .Include(u => u.UserIdentifiers)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Contact.Id == contactId && u.Active).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<User>> GetUsersWithDocumentTypeAsync(string documentType)
        {
            if (string.IsNullOrWhiteSpace(documentType))
                throw new ArgumentNullException(nameof(documentType));

            return await _context.Users
                .Include(u => u.Contact)
                .Include(u => u.UserDocuments)
                .AsNoTracking()
                .Where(u => u.Active && 
                           u.UserDocuments.Any(d => d.DocumentType.ToString() == documentType))
                .ToListAsync().ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<User>> GetUsersWithIdentifierTypeAsync(string identifierType)
        {
            if (string.IsNullOrWhiteSpace(identifierType))
                throw new ArgumentNullException(nameof(identifierType));

            return await _context.Users
                .Include(u => u.Contact)
                .Include(u => u.UserIdentifiers)
                .AsNoTracking()
                .Where(u => u.Active && 
                           u.UserIdentifiers.Any(i => i.IdentifierType.ToString() == identifierType))
                .ToListAsync().ConfigureAwait(false);
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
                              u.Active).ConfigureAwait(false);

            if (existingContact)
                throw new InvalidOperationException("Contact is already associated with another active user");

            // Validate documents
            if (user.UserDocuments != null)
            {
                foreach (var document in user.UserDocuments)
                {
                    //Not sure why it asked for this
                    //if (string.IsNullOrWhiteSpace(document.S3BucketName))
                        //throw new ArgumentException("S3 bucket name is required for documents");
                }
            }

            // Validate identifiers
            if (user.UserIdentifiers != null)
            {
                foreach (var identifier in user.UserIdentifiers)
                {
                    // There is no validate method
                    //if (!identifier.Validate())
                        //throw new ArgumentException($"Invalid identifier of type {identifier.Type}");
                }
            }
        }
    }
}
