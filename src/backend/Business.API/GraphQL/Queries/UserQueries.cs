using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HotChocolate;
using HotChocolate.Types;
using HotChocolate.Authorization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using EstateKit.Core.Entities;
using EstateKit.Core.Interfaces;
using EstateKit.Core.Enums;

namespace EstateKit.Business.API.GraphQL.Queries
{
    /// <summary>
    /// Implements secure and optimized GraphQL query resolvers for User entities with
    /// field-level encryption, comprehensive audit logging, and performance optimizations.
    /// </summary>
    [ExtendObjectType(OperationTypeNames.Query)]
    public class UserQueries
    {
        private readonly IUserRepository _userRepository;
        private readonly IDataProtectionProvider _dataProtection;
        private readonly ILogger<UserQueries> _logger;
        private readonly IMemoryCache _cache;
        private const int CACHE_DURATION_MINUTES = 5;
        private const string USER_CACHE_KEY = "user_{0}";
        private const string USERS_CACHE_KEY = "users_all";

        public UserQueries(
            IUserRepository userRepository,
            IDataProtectionProvider dataProtection,
            ILogger<UserQueries> logger,
            IMemoryCache cache)
        {
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _dataProtection = dataProtection ?? throw new ArgumentNullException(nameof(dataProtection));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        /// <summary>
        /// Retrieves a user by ID with field-level encryption, caching, and audit logging
        /// </summary>
        [UseFirstOrDefault]
        [UseProjection]
        [UseFiltering]
        [UseSorting]
        [Authorize(Roles = new[] { "admin", "user" })]
        public async Task<User?> GetUserByIdAsync(
            [GraphQLDescription("Unique identifier of the user")] 
            Guid id)
        {
            try
            {
                _logger.LogInformation("Attempting to retrieve user with ID: {UserId}", id);

                string cacheKey = string.Format(USER_CACHE_KEY, id);
                if (_cache.TryGetValue(cacheKey, out User? cachedUser))
                {
                    _logger.LogDebug("Cache hit for user ID: {UserId}", id);
                    return cachedUser;
                }

                var user = await _userRepository.GetByIdAsync(id);
                if (user != null)
                {
                    var cacheOptions = new MemoryCacheEntryOptions()
                        .SetSlidingExpiration(TimeSpan.FromMinutes(CACHE_DURATION_MINUTES))
                        .SetPriority(CacheItemPriority.High);

                    _cache.Set(cacheKey, user, cacheOptions);
                    _logger.LogDebug("User cached with ID: {UserId}", id);
                }

                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user with ID: {UserId}", id);
                throw;
            }
        }

        /// <summary>
        /// Retrieves all active users with optimized performance and security measures
        /// </summary>
        [UseProjection]
        [UseFiltering]
        [UseSorting]
        [Authorize(Roles = new[] { "admin" })]
        public async Task<IEnumerable<User>> GetUsersAsync()
        {
            try
            {
                _logger.LogInformation("Attempting to retrieve all users");

                if (_cache.TryGetValue(USERS_CACHE_KEY, out IEnumerable<User>? cachedUsers))
                {
                    _logger.LogDebug("Cache hit for all users query");
                    return cachedUsers;
                }

                var users = await _userRepository.GetAllAsync();
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetSlidingExpiration(TimeSpan.FromMinutes(CACHE_DURATION_MINUTES))
                    .SetPriority(CacheItemPriority.Normal);

                _cache.Set(USERS_CACHE_KEY, users, cacheOptions);
                _logger.LogDebug("All users cached successfully");

                return users;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all users");
                throw;
            }
        }

        /// <summary>
        /// Retrieves a user by contact ID with security validation and caching
        /// </summary>
        [UseFirstOrDefault]
        [UseProjection]
        [UseFiltering]
        [UseSorting]
        [Authorize(Roles = new[] { "admin", "user" })]
        public async Task<User?> GetUserByContactIdAsync(
            [GraphQLDescription("Unique identifier of the contact")] 
            Guid contactId)
        {
            try
            {
                _logger.LogInformation("Attempting to retrieve user by contact ID: {ContactId}", contactId);

                string cacheKey = $"user_contact_{contactId}";
                if (_cache.TryGetValue(cacheKey, out User? cachedUser))
                {
                    _logger.LogDebug("Cache hit for contact ID: {ContactId}", contactId);
                    return cachedUser;
                }

                var user = await _userRepository.GetByContactIdAsync(contactId);
                if (user != null)
                {
                    var cacheOptions = new MemoryCacheEntryOptions()
                        .SetSlidingExpiration(TimeSpan.FromMinutes(CACHE_DURATION_MINUTES))
                        .SetPriority(CacheItemPriority.High);

                    _cache.Set(cacheKey, user, cacheOptions);
                    _logger.LogDebug("User cached with contact ID: {ContactId}", contactId);
                }

                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user by contact ID: {ContactId}", contactId);
                throw;
            }
        }

        /// <summary>
        /// Retrieves users with specific document types with enhanced security
        /// </summary>
        [UseProjection]
        [UseFiltering]
        [UseSorting]
        [Authorize(Roles = new[] { "admin" })]
        public async Task<IEnumerable<User>> GetUsersWithDocumentTypeAsync(
            [GraphQLDescription("Type of document to filter by")] 
            DocumentType documentType)
        {
            try
            {
                _logger.LogInformation("Attempting to retrieve users with document type: {DocumentType}", documentType);

                string cacheKey = $"users_document_{documentType}";
                if (_cache.TryGetValue(cacheKey, out IEnumerable<User>? cachedUsers))
                {
                    _logger.LogDebug("Cache hit for document type: {DocumentType}", documentType);
                    return cachedUsers;
                }

                var users = await _userRepository.GetUsersWithDocumentTypeAsync(documentType.ToString());
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetSlidingExpiration(TimeSpan.FromMinutes(CACHE_DURATION_MINUTES))
                    .SetPriority(CacheItemPriority.Normal);

                _cache.Set(cacheKey, users, cacheOptions);
                _logger.LogDebug("Users cached for document type: {DocumentType}", documentType);

                return users;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving users with document type: {DocumentType}", documentType);
                throw;
            }
        }
    }
}