// External package versions:
// System v9.0.0

using System;

namespace EstateKit.Core.Enums
{
    /// <summary>
    /// Enumerates the types of government-issued identification documents that require 
    /// critical security protection and field-level encryption in the EstateKit system.
    /// All values in this enum represent data classified as Critical security level
    /// requiring HSM storage and comprehensive audit logging.
    /// </summary>
    [Serializable]
    public enum IdentifierType
    {
        /// <summary>
        /// Driver's license identification number requiring critical protection and field-level encryption.
        /// Used for primary government ID verification and tracking.
        /// </summary>
        DRIVERSLICENSENUMBER,

        /// <summary>
        /// Passport identification number requiring critical protection and field-level encryption.
        /// Used for international identification and travel document tracking.
        /// </summary>
        PASSPORTID,

        /// <summary>
        /// Social Security number requiring critical protection and field-level encryption.
        /// Used for federal identification and tax purposes.
        /// </summary>
        SOCIALSECURITYNUMBER,

        /// <summary>
        /// State-issued identification number requiring critical protection and field-level encryption.
        /// Used for non-driver state identification tracking.
        /// </summary>
        STATEIDNUMBER,

        /// <summary>
        /// Military identification number requiring critical protection and field-level encryption.
        /// Used for tracking active duty and veteran identification.
        /// </summary>
        MILITARYIDNUMBER,

        /// <summary>
        /// Tax identification number requiring critical protection and field-level encryption.
        /// Used for business and tax-related identification tracking.
        /// </summary>
        TAXIDNUMBER,

        /// <summary>
        /// Birth certificate number requiring critical protection and field-level encryption.
        /// Used for vital records and proof of birth documentation.
        /// </summary>
        BIRTHCERTIFICATENUMBER,

        /// <summary>
        /// Naturalization certificate number requiring critical protection and field-level encryption.
        /// Used for citizenship and immigration status tracking.
        /// </summary>
        NATURALIZATIONNUMBER
    }
}
