using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Abstractions;
using Polly;
using EstateKit.Core.Entities;
using EstateKit.Core.Interfaces;
using EstateKit.Infrastructure.Security;

namespace EstateKit.Infrastructure.Data.Repositories
{
    /// <summary>
    /// Enhanced repository implementation for Contact entity data access operations with security, caching, and performance optimizations
    /// </summary>
    public sealed class ContactRepository : IContactRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly IEncryptionProvider _encryptionProvider;
        private readonly IDistributedCache _cacheProvider;
        private readonly ILogger<ContactRepository> _logger;
        private readonly IAsyncPolicy _retryPolicy;
        private const string CACHE_KEY_PREFIX = "contact_";
        private static readonly TimeSpan CACHE_DURATION = TimeSpan.FromMinutes(5);

        public ContactRepository(
            ApplicationDbContext context,
            IEncryptionProvider encryptionProvider,
            IDistributedCache cacheProvider,
            ILogger<ContactRepository> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _encryptionProvider = encryptionProvider ?? throw new ArgumentNullException(nameof(encryptionProvider));
            _cacheProvider = cacheProvider ?? throw new ArgumentNullException(nameof(cacheProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _retryPolicy = Policy
                .Handle<DbUpdateException>()
                .Or<DbUpdateConcurrencyException>()
                .WaitAndRetryAsync(3, retryAttempt => 
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
        }

        public async Task<Contact?> GetByIdAsync(Guid id)
        {
            try
            {
                var cacheKey = $"{CACHE_KEY_PREFIX}{id}";
                var cachedContact = await _cacheProvider.GetAsync<Contact>(cacheKey);
                
                if (cachedContact != null)
                {
                    _logger.LogDebug("Cache hit for contact {ContactId}", id);
                    return await DecryptContactFields(cachedContact);
                }

                var contact = await _context.Contacts
                    .Include(c => c.Addresses)
                    .Include(c => c.ContactMethods)
                    .Include(c => c.Relationships)
                    .Include(c => c.Citizenships)
                    .Include(c => c.Companies)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (contact != null)
                {
                    await _cacheProvider.SetAsync(cacheKey, contact, CACHE_DURATION);
                    return await DecryptContactFields(contact);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving contact {ContactId}", id);
                throw;
            }
        }

        public async Task<IEnumerable<Contact>> GetAllAsync(int? skip = null, int? take = null)
        {
            try
            {
                var query = _context.Contacts
                    .Include(c => c.Addresses)
                    .Include(c => c.ContactMethods)
                    .AsNoTracking();

                if (skip.HasValue)
                    query = query.Skip(skip.Value);
                if (take.HasValue)
                    query = query.Take(take.Value);

                var contacts = await query.ToListAsync();
                var decryptedContacts = new List<Contact>();

                foreach (var contact in contacts)
                {
                    decryptedContacts.Add(await DecryptContactFields(contact));
                }

                return decryptedContacts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving contacts");
                throw;
            }
        }

        public async Task<Contact> AddAsync(Contact contact)
        {
            if (contact == null) throw new ArgumentNullException(nameof(contact));

            try
            {
                var encryptedContact = await EncryptContactFields(contact);
                
                await _retryPolicy.ExecuteAsync(async () =>
                {
                    _context.Contacts.Add(encryptedContact);
                    await _context.SaveChangesAsync();
                });

                var cacheKey = $"{CACHE_KEY_PREFIX}{contact.Id}";
                await _cacheProvider.SetAsync(cacheKey, encryptedContact, CACHE_DURATION);

                return await DecryptContactFields(encryptedContact);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding contact");
                throw;
            }
        }

        public async Task<Contact> UpdateAsync(Contact contact)
        {
            if (contact == null) throw new ArgumentNullException(nameof(contact));

            try
            {
                var encryptedContact = await EncryptContactFields(contact);
                
                await _retryPolicy.ExecuteAsync(async () =>
                {
                    _context.Entry(encryptedContact).State = EntityState.Modified;
                    await _context.SaveChangesAsync();
                });

                var cacheKey = $"{CACHE_KEY_PREFIX}{contact.Id}";
                await _cacheProvider.RemoveAsync(cacheKey);

                return await DecryptContactFields(encryptedContact);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating contact {ContactId}", contact.Id);
                throw;
            }
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            try
            {
                var contact = await _context.Contacts
                    .Include(c => c.Relationships)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (contact == null) return false;

                await _retryPolicy.ExecuteAsync(async () =>
                {
                    _context.Contacts.Remove(contact);
                    await _context.SaveChangesAsync();
                });

                var cacheKey = $"{CACHE_KEY_PREFIX}{id}";
                await _cacheProvider.RemoveAsync(cacheKey);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting contact {ContactId}", id);
                throw;
            }
        }

        public async Task<IEnumerable<Contact>> GetByNameAsync(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                throw new ArgumentException("Search term cannot be empty", nameof(searchTerm));

            try
            {
                var contacts = await _context.Contacts
                    .Include(c => c.ContactMethods)
                    .Where(c => EF.Functions.Like(c.FirstName, $"%{searchTerm}%") ||
                               EF.Functions.Like(c.LastName, $"%{searchTerm}%") ||
                               EF.Functions.Like(c.MaidenName, $"%{searchTerm}%"))
                    .AsNoTracking()
                    .ToListAsync();

                var decryptedContacts = new List<Contact>();
                foreach (var contact in contacts)
                {
                    decryptedContacts.Add(await DecryptContactFields(contact));
                }

                return decryptedContacts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching contacts with term {SearchTerm}", searchTerm);
                throw;
            }
        }

        public async Task<IEnumerable<Contact>> GetRelatedContactsAsync(Guid contactId)
        {
            try
            {
                var relationships = await _context.Contacts
                    .Include(c => c.Relationships)
                    .ThenInclude(r => r.RelatedContact)
                    .Where(c => c.Id == contactId)
                    .SelectMany(c => c.Relationships)
                    .ToListAsync();

                var relatedContacts = relationships.Select(r => r.RelatedContact).ToList();
                var decryptedContacts = new List<Contact>();

                foreach (var contact in relatedContacts)
                {
                    decryptedContacts.Add(await DecryptContactFields(contact));
                }

                return decryptedContacts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving related contacts for {ContactId}", contactId);
                throw;
            }
        }

        private async Task<Contact> EncryptContactFields(Contact contact)
        {
            var context = new EncryptionContext { EntityType = "Contact", EntityId = contact.Id.ToString() };

            contact.FirstName = (await _encryptionProvider.EncryptField(contact.FirstName, "FirstName", context)).EncryptedData;
            contact.LastName = (await _encryptionProvider.EncryptField(contact.LastName, "LastName", context)).EncryptedData;
            
            if (!string.IsNullOrEmpty(contact.MiddleName))
                contact.MiddleName = (await _encryptionProvider.EncryptField(contact.MiddleName, "MiddleName", context)).EncryptedData;
            
            if (!string.IsNullOrEmpty(contact.MaidenName))
                contact.MaidenName = (await _encryptionProvider.EncryptField(contact.MaidenName, "MaidenName", context)).EncryptedData;

            return contact;
        }

        private async Task<Contact> DecryptContactFields(Contact contact)
        {
            var context = new EncryptionContext { EntityType = "Contact", EntityId = contact.Id.ToString() };

            var decryptedContact = new Contact
            {
                Id = contact.Id,
                FirstName = await _encryptionProvider.DecryptField(new EncryptedValue { EncryptedData = contact.FirstName }, "FirstName", context),
                LastName = await _encryptionProvider.DecryptField(new EncryptedValue { EncryptedData = contact.LastName }, "LastName", context)
            };

            if (!string.IsNullOrEmpty(contact.MiddleName))
                decryptedContact.MiddleName = await _encryptionProvider.DecryptField(new EncryptedValue { EncryptedData = contact.MiddleName }, "MiddleName", context);

            if (!string.IsNullOrEmpty(contact.MaidenName))
                decryptedContact.MaidenName = await _encryptionProvider.DecryptField(new EncryptedValue { EncryptedData = contact.MaidenName }, "MaidenName", context);

            decryptedContact.Addresses = contact.Addresses;
            decryptedContact.ContactMethods = contact.ContactMethods;
            decryptedContact.Relationships = contact.Relationships;
            decryptedContact.Citizenships = contact.Citizenships;
            decryptedContact.Companies = contact.Companies;

            return decryptedContact;
        }
    }
}