// Version: System 9.0.0
using System;

namespace EstateKit.Core.Enums
{
    /// <summary>
    /// Enumerates the different types of contact methods available for a person in the estate planning system.
    /// This enum provides standardized categorization for both phone numbers and email addresses.
    /// </summary>
    public enum ContactMethodType
    {
        /// <summary>
        /// Represents a work telephone number used for business-related communication
        /// </summary>
        WorkPhone,

        /// <summary>
        /// Represents a home telephone number used for residential contact
        /// </summary>
        HomePhone,

        /// <summary>
        /// Represents a mobile/cell phone number for personal mobile communication
        /// </summary>
        CellPhone,

        /// <summary>
        /// Represents a work email address used for business-related electronic communication
        /// </summary>
        WorkEmail,

        /// <summary>
        /// Represents a personal email address used for private electronic communication
        /// </summary>
        PersonalEmail
    }
}