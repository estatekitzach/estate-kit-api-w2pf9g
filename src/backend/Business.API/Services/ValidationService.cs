using System;
using System.Linq;
using System.Threading.Tasks;
using FluentValidation; // v11.0.0
using Microsoft.Extensions.Logging; // v9.0.0
using EstateKit.Core.Entities;
using EstateKit.Core.Enums;
using System.Text.RegularExpressions;

namespace EstateKit.Business.API.Services
{
    /// <summary>
    /// Provides comprehensive validation services for all business entities with security enforcement,
    /// business rule validation, and data integrity checks.
    /// </summary>
    public sealed class ValidationService
    {
        private readonly ILogger<ValidationService> _logger;
        private readonly IValidator<User> _userValidator;
        private readonly IValidator<Document> _documentValidator;
        private readonly IValidator<Contact> _contactValidator;

        public ValidationService(
            ILogger<ValidationService> logger,
            IValidator<User> userValidator,
            IValidator<Document> documentValidator,
            IValidator<Contact> contactValidator)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _userValidator = userValidator ?? throw new ArgumentNullException(nameof(userValidator));
            _documentValidator = documentValidator ?? throw new ArgumentNullException(nameof(documentValidator));
            _contactValidator = contactValidator ?? throw new ArgumentNullException(nameof(contactValidator));
        }

        /// <summary>
        /// Validates user data including contact information, relationships, and security requirements
        /// </summary>
        public async Task<ValidationResult> ValidateUser(User user)
        {
            _logger.LogInformation("Starting user validation for ID: {UserId}", user?.Id);

            if (user == null)
                return new ValidationResult("User cannot be null");

            var validationResult = await _userValidator.ValidateAsync(user);

            if (!validationResult.IsValid)
            {
                _logger.LogWarning("User validation failed for ID: {UserId}. Errors: {Errors}", 
                    user.Id, string.Join(", ", validationResult.Errors));
                return validationResult;
            }

            // Validate contact information
            if (user.Contact != null)
            {
                var contactValidation = await ValidateContact(user.Contact);
                if (!contactValidation.IsValid)
                {
                    return contactValidation;
                }
            }

            // Validate documents
            if (user.Documents != null && user.Documents.Any())
            {
                foreach (var document in user.Documents)
                {
                    var documentValidation = await ValidateDocument(document);
                    if (!documentValidation.IsValid)
                    {
                        return documentValidation;
                    }
                }
            }

            // Validate identifiers
            if (user.Identifiers != null && user.Identifiers.Any())
            {
                foreach (var identifier in user.Identifiers)
                {
                    if (!identifier.Validate())
                    {
                        return new ValidationResult($"Invalid identifier: {identifier.Type}");
                    }
                }
            }

            _logger.LogInformation("User validation successful for ID: {UserId}", user.Id);
            return validationResult;
        }

        /// <summary>
        /// Validates document data including OCR requirements and security classifications
        /// </summary>
        public async Task<ValidationResult> ValidateDocument(Document document)
        {
            _logger.LogInformation("Starting document validation for ID: {DocumentId}", document?.Id);

            if (document == null)
                return new ValidationResult("Document cannot be null");

            var validationResult = await _documentValidator.ValidateAsync(document);

            if (!validationResult.IsValid)
            {
                _logger.LogWarning("Document validation failed for ID: {DocumentId}. Errors: {Errors}", 
                    document.Id, string.Join(", ", validationResult.Errors));
                return validationResult;
            }

            // Validate document type specific requirements
            switch (document.Type)
            {
                case DocumentType.DRIVERS_LICENSE:
                case DocumentType.PASSPORT:
                    if (string.IsNullOrEmpty(document.FrontImageUrl))
                        return new ValidationResult($"Front image required for {document.Type}");
                    if (string.IsNullOrEmpty(document.BackImageUrl) && document.Type == DocumentType.DRIVERS_LICENSE)
                        return new ValidationResult("Back image required for driver's license");
                    break;
            }

            // Validate S3 URLs
            if (!Uri.TryCreate(document.FrontImageUrl, UriKind.Absolute, out _))
                return new ValidationResult("Invalid front image URL");
            if (!string.IsNullOrEmpty(document.BackImageUrl) && !Uri.TryCreate(document.BackImageUrl, UriKind.Absolute, out _))
                return new ValidationResult("Invalid back image URL");

            _logger.LogInformation("Document validation successful for ID: {DocumentId}", document.Id);
            return validationResult;
        }

        /// <summary>
        /// Validates contact information including relationships and communication methods
        /// </summary>
        public async Task<ValidationResult> ValidateContact(Contact contact)
        {
            _logger.LogInformation("Starting contact validation for ID: {ContactId}", contact?.Id);

            if (contact == null)
                return new ValidationResult("Contact cannot be null");

            var validationResult = await _contactValidator.ValidateAsync(contact);

            if (!validationResult.IsValid)
            {
                _logger.LogWarning("Contact validation failed for ID: {ContactId}. Errors: {Errors}", 
                    contact.Id, string.Join(", ", validationResult.Errors));
                return validationResult;
            }

            // Validate contact methods
            if (contact.ContactMethods != null && contact.ContactMethods.Any())
            {
                foreach (var method in contact.ContactMethods)
                {
                    if (!ValidateContactMethod(method))
                    {
                        return new ValidationResult($"Invalid contact method: {method.Type}");
                    }
                }
            }

            // Validate relationships
            if (contact.Relationships != null && contact.Relationships.Any())
            {
                foreach (var relationship in contact.Relationships)
                {
                    if (!ValidateRelationship(relationship))
                    {
                        return new ValidationResult($"Invalid relationship: {relationship.Type}");
                    }
                }
            }

            _logger.LogInformation("Contact validation successful for ID: {ContactId}", contact.Id);
            return validationResult;
        }

        /// <summary>
        /// Validates complex business rules across multiple entities
        /// </summary>
        public ValidationResult ValidateBusinessRules(object entity, string operationType)
        {
            _logger.LogInformation("Starting business rule validation for type: {EntityType}, operation: {Operation}", 
                entity.GetType().Name, operationType);

            try
            {
                switch (entity)
                {
                    case User user:
                        return ValidateUserBusinessRules(user, operationType);
                    case Document document:
                        return ValidateDocumentBusinessRules(document, operationType);
                    case Contact contact:
                        return ValidateContactBusinessRules(contact, operationType);
                    default:
                        return new ValidationResult($"Unsupported entity type: {entity.GetType().Name}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Business rule validation failed for {EntityType}", entity.GetType().Name);
                return new ValidationResult($"Business rule validation failed: {ex.Message}");
            }
        }

        private ValidationResult ValidateUserBusinessRules(User user, string operationType)
        {
            // Validate required relationships based on marital status
            if (user.MaritalStatus == "Married" && 
                !user.Contact.Relationships.Any(r => r.Type == RelationshipType.SPOUSE))
            {
                return new ValidationResult("Married users must have a spouse relationship defined");
            }

            // Validate required documents based on user type
            var requiredDocuments = new[] { DocumentType.DRIVERS_LICENSE, DocumentType.PASSPORT };
            if (!user.Documents.Any(d => requiredDocuments.Contains(d.Type)))
            {
                return new ValidationResult("User must have at least one primary government ID");
            }

            return new ValidationResult();
        }

        private ValidationResult ValidateDocumentBusinessRules(Document document, string operationType)
        {
            // Validate OCR requirements
            if (operationType == "Create" && document.IsProcessed && string.IsNullOrEmpty(document.Metadata))
            {
                return new ValidationResult("Processed documents must have metadata");
            }

            // Validate security classification
            if (string.IsNullOrEmpty(document.SecurityClassification))
            {
                return new ValidationResult("Security classification is required");
            }

            return new ValidationResult();
        }

        private ValidationResult ValidateContactBusinessRules(Contact contact, string operationType)
        {
            // Validate required contact methods
            if (!contact.ContactMethods.Any(cm => 
                cm.Type == ContactMethodType.CellPhone || 
                cm.Type == ContactMethodType.WorkPhone))
            {
                return new ValidationResult("At least one phone number is required");
            }

            // Validate name fields
            if (string.IsNullOrWhiteSpace(contact.FirstName) || string.IsNullOrWhiteSpace(contact.LastName))
            {
                return new ValidationResult("First name and last name are required");
            }

            return new ValidationResult();
        }

        private bool ValidateContactMethod(ContactMethod method)
        {
            if (method == null || string.IsNullOrEmpty(method.Value))
                return false;

            switch (method.Type)
            {
                case ContactMethodType.CellPhone:
                case ContactMethodType.WorkPhone:
                case ContactMethodType.HomePhone:
                    return Regex.IsMatch(method.Value, @"^\+?[1-9]\d{1,14}$");
                case ContactMethodType.WorkEmail:
                case ContactMethodType.PersonalEmail:
                    return Regex.IsMatch(method.Value, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
                default:
                    return false;
            }
        }

        private bool ValidateRelationship(Relationship relationship)
        {
            if (relationship == null || !Enum.IsDefined(typeof(RelationshipType), relationship.Type))
                return false;

            // Validate relationship consistency
            if (relationship.Type == RelationshipType.SPOUSE)
            {
                // Ensure bi-directional spouse relationship
                return true; // Simplified for example
            }

            return true;
        }
    }
}