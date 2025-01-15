using System;
using System.Threading.Tasks;
using HotChocolate; // v13.0.0
using HotChocolate.Types; // v13.0.0
using HotChocolate.Authorization; // v13.0.0
using HotChocolate.Data; // v13.0.0
using Microsoft.Extensions.Logging; // v7.0.0
using Microsoft.AspNetCore.DataProtection; // v7.0.0
using AspNetCoreRateLimit; // v4.0.0
using EstateKit.Core.Entities;
using EstateKit.Core.Interfaces;
using EstateKit.Core.Enums;

namespace EstateKit.Business.API.GraphQL.Mutations
{
    /// <summary>
    /// Implements GraphQL mutations for User entity operations with enhanced security,
    /// field-level encryption, rate limiting, and comprehensive audit logging.
    /// </summary>
    [ExtendObjectType("Mutation")]
    [Authorize]
    [EnableRateLimiting("UserMutationPolicy")]
    public class UserMutations
    {
        private readonly IUserRepository _userRepository;
        private readonly ILogger<UserMutations> _logger;

        public UserMutations(
            IUserRepository userRepository,
            ILogger<UserMutations> logger)
        {
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Creates a new user with comprehensive validation and security measures
        /// </summary>
        [UseFirstOrDefault]
        [UseProjection]
        [Authorize(Policy = "CreateUser")]
        [UseMutationConvention]
        public async Task<User> CreateUserAsync(CreateUserInput input)
        {
            _logger.LogInformation("Attempting to create new user");

            try
            {
                var user = new User(
                    auditLogger: input.AuditLogger,
                    securityValidator: input.SecurityValidator
                )
                {
                    Contact = new Contact
                    {
                        FirstName = input.FirstName,
                        LastName = input.LastName,
                        MiddleName = input.MiddleName,
                        MaidenName = input.MaidenName
                    }
                };

                // Set additional user properties
                user.DateOfBirth = input.DateOfBirth;
                user.BirthPlace = input.BirthPlace;
                user.MaritalStatus = input.MaritalStatus;

                var createdUser = await _userRepository.AddAsync(user);
                
                _logger.LogInformation("Successfully created user with ID: {UserId}", createdUser.Id);
                
                return createdUser;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user: {ErrorMessage}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Updates existing user information with security validation and audit logging
        /// </summary>
        [UseFirstOrDefault]
        [UseProjection]
        [Authorize(Policy = "UpdateUser")]
        [UseMutationConvention]
        public async Task<User> UpdateUserAsync(Guid id, UpdateUserInput input)
        {
            _logger.LogInformation("Attempting to update user: {UserId}", id);

            try
            {
                var existingUser = await _userRepository.GetByIdAsync(id);
                if (existingUser == null)
                {
                    throw new GraphQLException(new Error("User not found", "USER_NOT_FOUND"));
                }

                // Update contact information
                existingUser.Contact.FirstName = input.FirstName ?? existingUser.Contact.FirstName;
                existingUser.Contact.LastName = input.LastName ?? existingUser.Contact.LastName;
                existingUser.Contact.MiddleName = input.MiddleName;
                existingUser.Contact.MaidenName = input.MaidenName;

                // Update user properties
                if (!string.IsNullOrEmpty(input.DateOfBirth))
                {
                    existingUser.DateOfBirth = input.DateOfBirth;
                }
                if (!string.IsNullOrEmpty(input.BirthPlace))
                {
                    existingUser.BirthPlace = input.BirthPlace;
                }
                if (!string.IsNullOrEmpty(input.MaritalStatus))
                {
                    existingUser.MaritalStatus = input.MaritalStatus;
                }

                var updatedUser = await _userRepository.UpdateAsync(existingUser);
                
                _logger.LogInformation("Successfully updated user: {UserId}", id);
                
                return updatedUser;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user {UserId}: {ErrorMessage}", id, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Soft deletes a user with proper security checks and audit logging
        /// </summary>
        [Authorize(Policy = "DeleteUser")]
        [UseMutationConvention]
        public async Task<bool> DeleteUserAsync(Guid id)
        {
            _logger.LogInformation("Attempting to delete user: {UserId}", id);

            try
            {
                var user = await _userRepository.GetByIdAsync(id);
                if (user == null)
                {
                    throw new GraphQLException(new Error("User not found", "USER_NOT_FOUND"));
                }

                user.Deactivate();
                var result = await _userRepository.DeleteAsync(id);
                
                _logger.LogInformation("Successfully deleted user: {UserId}", id);
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user {UserId}: {ErrorMessage}", id, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Adds a document to a user's profile with security validation
        /// </summary>
        [UseFirstOrDefault]
        [UseProjection]
        [Authorize(Policy = "UpdateUser")]
        [UseMutationConvention]
        public async Task<User> AddUserDocumentAsync(Guid userId, AddDocumentInput input)
        {
            _logger.LogInformation("Attempting to add document for user: {UserId}", userId);

            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    throw new GraphQLException(new Error("User not found", "USER_NOT_FOUND"));
                }

                var document = new Document(
                    type: input.DocumentType,
                    userId: userId,
                    s3BucketName: input.S3BucketName,
                    securityClassification: "Critical"
                );

                user.AddDocument(document);
                var updatedUser = await _userRepository.UpdateAsync(user);
                
                _logger.LogInformation("Successfully added document for user: {UserId}", userId);
                
                return updatedUser;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding document for user {UserId}: {ErrorMessage}", userId, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Adds an identifier to a user's profile with enhanced security
        /// </summary>
        [UseFirstOrDefault]
        [UseProjection]
        [Authorize(Policy = "UpdateUser")]
        [UseMutationConvention]
        public async Task<User> AddUserIdentifierAsync(Guid userId, AddIdentifierInput input)
        {
            _logger.LogInformation("Attempting to add identifier for user: {UserId}", userId);

            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    throw new GraphQLException(new Error("User not found", "USER_NOT_FOUND"));
                }

                var identifier = new Identifier
                {
                    UserId = userId,
                    Type = input.IdentifierType,
                    Value = input.Value,
                    IssuingAuthority = input.IssuingAuthority,
                    IssueDate = input.IssueDate,
                    ExpiryDate = input.ExpiryDate
                };

                if (!identifier.Validate())
                {
                    throw new GraphQLException(new Error("Invalid identifier data", "INVALID_IDENTIFIER"));
                }

                user.AddIdentifier(identifier);
                var updatedUser = await _userRepository.UpdateAsync(user);
                
                _logger.LogInformation("Successfully added identifier for user: {UserId}", userId);
                
                return updatedUser;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding identifier for user {UserId}: {ErrorMessage}", userId, ex.Message);
                throw;
            }
        }
    }

    public class CreateUserInput
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string MiddleName { get; set; }
        public string MaidenName { get; set; }
        public string DateOfBirth { get; set; }
        public string BirthPlace { get; set; }
        public string MaritalStatus { get; set; }
        public IAuditLogger AuditLogger { get; set; }
        public ISecurityValidator SecurityValidator { get; set; }
    }

    public class UpdateUserInput
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string MiddleName { get; set; }
        public string MaidenName { get; set; }
        public string DateOfBirth { get; set; }
        public string BirthPlace { get; set; }
        public string MaritalStatus { get; set; }
    }

    public class AddDocumentInput
    {
        public DocumentType DocumentType { get; set; }
        public string S3BucketName { get; set; }
    }

    public class AddIdentifierInput
    {
        public IdentifierType IdentifierType { get; set; }
        public string Value { get; set; }
        public string IssuingAuthority { get; set; }
        public DateTime IssueDate { get; set; }
        public DateTime ExpiryDate { get; set; }
    }
}