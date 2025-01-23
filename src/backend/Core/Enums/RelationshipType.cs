// System v9.0.0 - Core .NET functionality for enum definition and attributes
using System;

namespace EstateKit.Core.Enums
{
    /// <summary>
    /// Defines the standardized relationship types between contacts in the estate planning system.
    /// Used for categorizing and tracking family relationships, legal relationships, and other
    /// personal connections between individuals in the context of estate planning.
    /// </summary>
    [Serializable]
    public enum RelationshipType
    {
        /// <summary>
        /// Represents no relationship
        /// </summary>
        NONE = 0,

        /// <summary>
        /// Represents a legally recognized marital relationship between contacts
        /// </summary>
        SPOUSE = 1,

        /// <summary>
        /// Represents a maternal relationship from one contact to another
        /// </summary>
        MOTHER = 2,

        /// <summary>
        /// Represents a paternal relationship from one contact to another
        /// </summary>
        FATHER = 3,

        /// <summary>
        /// Represents a parent-child relationship where the referenced contact is the child
        /// </summary>
        CHILD = 4,

        /// <summary>
        /// Represents a female sibling relationship between contacts
        /// </summary>
        SISTER = 5,

        /// <summary>
        /// Represents a male sibling relationship between contacts
        /// </summary>
        BROTHER = 6,

        /// <summary>
        /// Represents a grandparent relationship to another contact
        /// </summary>
        GRANDPARENT = 7,

        /// <summary>
        /// Represents a grandchild relationship to another contact
        /// </summary>
        GRANDCHILD = 8,

        /// <summary>
        /// Represents an aunt relationship between contacts
        /// </summary>
        AUNT = 9,

        /// <summary>
        /// Represents an uncle relationship between contacts
        /// </summary>
        UNCLE = 10,

        /// <summary>
        /// Represents a cousin relationship between contacts
        /// </summary>
        COUSIN = 11,

        /// <summary>
        /// Represents a legal guardian relationship between contacts for estate planning purposes
        /// </summary>
        GUARDIAN = 12,

        /// <summary>
        /// Represents any other significant relationship type not covered by the specific categories
        /// </summary>
        OTHER = 99
    }
}
