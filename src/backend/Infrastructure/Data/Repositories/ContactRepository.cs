using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Distributed;
using Polly;
using EstateKit.Core.Models.ContactModels;
using EstateKit.Core.Interfaces;
using EstateKit.Infrastructure.Logger.Extensions;
using System.Globalization;
using EstateKit.Data.DBContexts;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace EstateKit.Infrastructure.Data.Repositories
{
    /// <summary>
    /// Enhanced repository implementation for Contact entity data access operations with security, caching, and performance optimizations
    /// </summary>
    public sealed class ContactRepository : IContactRepository
    {
        private readonly EstateKit.Data.DBContexts.EstateKitContext _context;
        //private readonly IEncryptionProvider _encryptionProvider;
        // private readonly IDistributedCache _cacheProvider;
        private readonly ILogger<ContactRepository> _logger;
        //private readonly IAsyncPolicy _retryPolicy;
        private const string CACHE_KEY_PREFIX = "contact_";
        // private static readonly TimeSpan CACHE_DURATION = TimeSpan.FromMinutes(5);

        public ContactRepository(
            EstateKitContext context,
            //IEncryptionProvider encryptionProvider,
            //IDistributedCache cacheProvider,
            ILogger<ContactRepository> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            //_encryptionProvider = encryptionProvider ?? throw new ArgumentNullException(nameof(encryptionProvider));
            //_cacheProvider = cacheProvider ?? throw new ArgumentNullException(nameof(cacheProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            /*_retryPolicy = Policy
                .Handle<DbUpdateException>()
                .Or<DbUpdateConcurrencyException>()
                .WaitAndRetryAsync(3, retryAttempt => 
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))); */
        }

        public async Task<Contact?> GetByIdAsync(int id)
        {
            try
            {
                var cacheKey = $"{CACHE_KEY_PREFIX}{id}";
                /* var cachedContactBytes = await _cacheProvider.GetAsync(cacheKey).ConfigureAwait(false);

                if (cachedContactBytes != null)
                {
                    var cachedContact = System.Text.Json.JsonSerializer.Deserialize<Contact>(cachedContactBytes);
                    _logger.LogCacheHitForContact(id, null);
                    if(cachedContact != null)
                    { return DecryptContactFields(cachedContact); }
                }*/

                var contact = await _context.Contacts
                    .Include(c => c.ContactAddresses)
                    .Include(c => c.ContactContactMethods)
                    .Include(c => c.ContactRelationshipContacts)
                    .Include(c => c.ContactCitizenships)
                    .FirstOrDefaultAsync(c => c.Id == id).ConfigureAwait(false);

                if (contact != null)
                {
                    var contactBytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(contact);
                    //await _cacheProvider.SetAsync(cacheKey, contactBytes, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CACHE_DURATION }).ConfigureAwait(false);
                    return DecryptContactFields(contact);
                }

                return null;
            }
            catch (Exception ex)
            {
                string message = $"Error retrieving contact {id}";
                _logger.LogGenericException( string.Format(CultureInfo.InvariantCulture, "{0} - {1}", ex.Message, message), ex);
                throw;
            }
        }


        public async Task<IEnumerable<Contact>> GetAllAsync(int? skip = null, int? take = null)
        {
            try
            {
                var query = _context.Contacts
                    .Include(c => c.ContactAddresses)
                    .Include(c => c.ContactContactMethods)
                    .AsNoTracking();

                if (skip.HasValue)
                    query = query.Skip(skip.Value);
                if (take.HasValue)
                    query = query.Take(take.Value);

                var contacts = await query.ToListAsync().ConfigureAwait(false);
                var decryptedContacts = new List<Contact>();

                foreach (var contact in contacts)
                {
                    decryptedContacts.Add(DecryptContactFields(contact));
                }

                return decryptedContacts;
            }
            catch (Exception ex)
            {
                _logger.LogGenericException("Error retrieving contacts", ex);
                throw;
            }
        }

        public async Task<Contact> AddAsync(Contact contact)
        {
            ArgumentNullException.ThrowIfNull(contact);

            try
            {
                var encryptedContact = EncryptContactFields(contact);
                
               // await _retryPolicy.ExecuteAsync(async () =>
                //{
                    _context.Contacts.Add(encryptedContact);
                    await _context.SaveChangesAsync().ConfigureAwait(false);
                //}).ConfigureAwait(false);

                var cacheKey = $"{CACHE_KEY_PREFIX}{contact.Id}";
                // TODO: How to we add this to cacheawait _cacheProvider.SetAsync(cacheKey, encryptedContact, CACHE_DURATION).ConfigureAwait(false);

                return DecryptContactFields(encryptedContact);
            }
            catch (Exception ex)
            {
                _logger.LogGenericException("Error adding contact", ex);
                throw;
            }
        }

        public async Task<Contact> UpdateAsync(Contact contact)
        {
            ArgumentNullException.ThrowIfNull(contact);

            try
            {
                var encryptedContact = EncryptContactFields(contact);
                
                //await _retryPolicy.ExecuteAsync(async () =>
                //{
                    _context.Entry(encryptedContact).State = EntityState.Modified;
                    await _context.SaveChangesAsync().ConfigureAwait(false);
                //}).ConfigureAwait(false);

                var cacheKey = $"{CACHE_KEY_PREFIX}{contact.Id}";
                // await _cacheProvider.RemoveAsync(cacheKey).ConfigureAwait(false);

                return DecryptContactFields(encryptedContact);
            }
            catch (Exception ex)
            {
                var message = string.Format(CultureInfo.InvariantCulture, "Error updating contact {0}", contact.Id);    
                _logger.LogGenericException(message, ex);
                throw;
            }
        }

        public async Task<bool> DeleteAsync(int id)
        {
            try
            {
                var contact = await _context.Contacts
                    .Include(c => c.ContactRelationshipContacts)
                    .FirstOrDefaultAsync(c => c.Id == id).ConfigureAwait(false);

                if (contact == null) return false;

               //await _retryPolicy.ExecuteAsync(async () =>
               // {
                    _context.Contacts.Remove(contact);
                    await _context.SaveChangesAsync().ConfigureAwait(false);
                //}).ConfigureAwait(false);

                var cacheKey = $"{CACHE_KEY_PREFIX}{id}";
                // await _cacheProvider.RemoveAsync(cacheKey).ConfigureAwait(false);

                return true;
            }
            catch (Exception ex)
            {
                var message = string.Format(CultureInfo.InvariantCulture, "Error deleting contact {0}", id);
                _logger.LogGenericException(message, ex);
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
                    .Include(c => c.ContactContactMethods)
                    .Where(c => EF.Functions.Like(c.FirstName, $"%{searchTerm}%") ||
                               EF.Functions.Like(c.LastName, $"%{searchTerm}%") ||
                               EF.Functions.Like(c.MaidenName, $"%{searchTerm}%"))
                    .AsNoTracking()
                    .ToListAsync().ConfigureAwait(false);

                var decryptedContacts = new List<Contact>();
                foreach (var contact in contacts)
                {
                    decryptedContacts.Add(DecryptContactFields(contact));
                }

                return decryptedContacts;
            }
            catch (Exception ex)
            {
                var message = string.Format(CultureInfo.InvariantCulture, "Error searching contacts with term {0}", searchTerm);
                _logger.LogGenericException(message, ex);
                throw;
            }
        }

        public async Task<IEnumerable<Contact>> GetRelatedContactsAsync(int contactId)
        {
            try
            {
                var relationships = await _context.Contacts
                    .Include(c => c.ContactRelationshipContacts)
                    .ThenInclude(r => r.RelatedContact)
                    .Where(c => c.Id == contactId)
                    .SelectMany(c => c.ContactRelationshipContacts)
                    .ToListAsync().ConfigureAwait(false);

                var relatedContacts = relationships.Select(r => r.RelatedContact).ToList();
                var decryptedContacts = new List<Contact>();

                foreach (var contact in relatedContacts)
                {
                    decryptedContacts.Add(DecryptContactFields(contact));
                }

                return decryptedContacts;
            }
            catch (Exception ex)
            {
                var message = string.Format(CultureInfo.InvariantCulture, "Error retrieving related contacts for {0}", contactId);
                _logger.LogGenericException(message, ex);
                throw;
            }
        }


#pragma warning disable CA1822 // Mark members as static
        private Contact EncryptContactFields(Contact contact)
#pragma warning restore CA1822 // Mark members as static
        {

            // TOODO: Implement the encryption of all the fields marked as sensitive data. 
           /* var context = new EncryptionContext { EntityType = "Contact", EntityId = contact.Id.ToString() };

            contact.FirstName = (await _encryptionProvider.EncryptField(contact.FirstName, "FirstName", context)).EncryptedData;
            contact.LastName = (await _encryptionProvider.EncryptField(contact.LastName, "LastName", context)).EncryptedData;
            
            if (!string.IsNullOrEmpty(contact.MiddleName))
                contact.MiddleName = (await _encryptionProvider.EncryptField(contact.MiddleName, "MiddleName", context)).EncryptedData;
            
            if (!string.IsNullOrEmpty(contact.MaidenName))
                contact.MaidenName = (await _encryptionProvider.EncryptField(contact.MaidenName, "MaidenName", context)).EncryptedData; */

            return contact;
        }

#pragma warning disable CA1822 // Mark members as static
        private Contact DecryptContactFields(Contact contact)
#pragma warning restore CA1822 // Mark members as static
        {
            // TOODO: Implement the encryption of all the fields marked as sensitive data. 
            /*var context = new EncryptionContext { EntityType = "Contact", EntityId = contact.Id.ToString() };

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
            decryptedContact.Companies = contact.Companies;*/

            return contact;
        }

        public Task<bool> DeleteAsync(Guid id)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<Contact>> GetRelatedContactsAsync(Guid contactId)
        {
            throw new NotImplementedException();
        }
    }
}
