// System v9.0.0
// System.Collections.Generic v9.0.0
// System.ComponentModel.DataAnnotations v9.0.0
// EstateKit.Security v1.0.0
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EstateKit.Core.Enums;
using EstateKit.Security.Attributes;

namespace EstateKit.Core.Entities
{
    /// <summary>
    /// Represents a contact entity in the estate planning system with personal information,
    /// relationships, and enhanced security features including field-level encryption.
    /// </summary>
    [Table("Contacts")]
    public class Contact
    {
        /// <summary>
        /// Unique identifier for the contact
        /// </summary>
        [Key]
        public Guid Id { get; private set; }

        /// <summary>
        /// First name of the contact - encrypted at rest
        /// </summary>
        [Required]
        [MaxLength(100)]
        [Encrypt]
        public string FirstName { get; set; }

        /// <summary>
        /// Last name of the contact - encrypted at rest
        /// </summary>
        [Required]
        [MaxLength(100)]
        [Encrypt]
        public string LastName { get; set; }

        /// <summary>
        /// Optional middle name of the contact - encrypted at rest
        /// </summary>
        [MaxLength(100)]
        [Encrypt]
        public string MiddleName { get; set; }

        /// <summary>
        /// Optional maiden name of the contact - encrypted at rest
        /// </summary>
        [MaxLength(100)]
        [Encrypt]
        public string MaidenName { get; set; }

        /// <summary>
        /// Collection of addresses associated with the contact
        /// </summary>
        [Required]
        public ICollection<Address> Addresses { get; private set; }

        /// <summary>
        /// Collection of citizenships held by the contact
        /// </summary>
        [Required]
        public ICollection<Citizenship> Citizenships { get; private set; }

        /// <summary>
        /// Collection of companies associated with the contact
        /// </summary>
        public ICollection<Company> Companies { get; private set; }

        /// <summary>
        /// Collection of contact methods (phone numbers, email addresses) for the contact
        /// </summary>
        [Required]
        public ICollection<ContactMethod> ContactMethods { get; private set; }

        /// <summary>
        /// Collection of relationships with other contacts
        /// </summary>
        public ICollection<Relationship> Relationships { get; private set; }

        /// <summary>
        /// Timestamp when the contact was created
        /// </summary>
        public DateTime CreatedAt { get; private set; }

        /// <summary>
        /// Timestamp when the contact was last updated
        /// </summary>
        public DateTime? UpdatedAt { get; private set; }

        /// <summary>
        /// Initializes a new instance of the Contact class with default collections
        /// </summary>
        public Contact()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTime.UtcNow;
            Addresses = new List<Address>();
            Citizenships = new List<Citizenship>();
            Companies = new List<Company>();
            ContactMethods = new List<ContactMethod>();
            Relationships = new List<Relationship>();
        }

        /// <summary>
        /// Adds a new address to the contact's collection of addresses
        /// </summary>
        /// <param name="address">The address to add</param>
        /// <exception cref="ArgumentNullException">Thrown when address is null</exception>
        /// <exception cref="ArgumentException">Thrown when address is invalid</exception>
        public void AddAddress(Address address)
        {
            if (address == null)
                throw new ArgumentNullException(nameof(address));

            if (string.IsNullOrWhiteSpace(address.StreetAddress))
                throw new ArgumentException("Street address is required", nameof(address));

            Addresses.Add(address);
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Adds a new contact method to the contact's collection of contact methods
        /// </summary>
        /// <param name="contactMethod">The contact method to add</param>
        /// <exception cref="ArgumentNullException">Thrown when contactMethod is null</exception>
        /// <exception cref="ArgumentException">Thrown when contact method is invalid or duplicate</exception>
        public void AddContactMethod(ContactMethod contactMethod)
        {
            if (contactMethod == null)
                throw new ArgumentNullException(nameof(contactMethod));

            if (string.IsNullOrWhiteSpace(contactMethod.Value))
                throw new ArgumentException("Contact method value is required", nameof(contactMethod));

            // Check for duplicates
            foreach (var existing in ContactMethods)
            {
                if (existing.Type == contactMethod.Type && existing.Value == contactMethod.Value)
                    throw new ArgumentException("This contact method already exists", nameof(contactMethod));
            }

            ContactMethods.Add(contactMethod);
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Adds a new relationship between this contact and another contact
        /// </summary>
        /// <param name="relatedContact">The contact to establish a relationship with</param>
        /// <param name="relationshipType">The type of relationship</param>
        /// <exception cref="ArgumentNullException">Thrown when relatedContact is null</exception>
        /// <exception cref="ArgumentException">Thrown when relationship already exists</exception>
        public void AddRelationship(Contact relatedContact, RelationshipType relationshipType)
        {
            if (relatedContact == null)
                throw new ArgumentNullException(nameof(relatedContact));

            if (relatedContact.Id == Id)
                throw new ArgumentException("Cannot create relationship with self", nameof(relatedContact));

            // Check for existing relationship
            foreach (var existing in Relationships)
            {
                if (existing.RelatedContactId == relatedContact.Id && existing.Type == relationshipType)
                    throw new ArgumentException("This relationship already exists", nameof(relatedContact));
            }

            var relationship = new Relationship
            {
                RelatedContactId = relatedContact.Id,
                Type = relationshipType,
                CreatedAt = DateTime.UtcNow
            };

            Relationships.Add(relationship);
            UpdatedAt = DateTime.UtcNow;
        }
    }
}