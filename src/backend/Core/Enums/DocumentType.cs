using System;

namespace EstateKit.Core.Enums
{
    /// <summary>
    /// Defines the types of documents that can be processed in the EstateKit system.
    /// Each document type has specific security classifications and processing requirements.
    /// </summary>
    /// <remarks>
    /// Security Classification: Internal
    /// Audit Requirements: All usage must be tracked with detailed audit logging
    /// Version: Stable - Values are permanent and backward compatibility is required
    /// </remarks>
    [Serializable]
    public enum DocumentType
    {
        /// <summary>
        /// Government-issued driver's license requiring secure handling and OCR processing.
        /// Security Level: Critical
        /// OCR Required: Yes
        /// Audit Level: Detailed
        /// </summary>
        DRIVERSLICENSE = 0,

        /// <summary>
        /// Government-issued passport document with strict security requirements.
        /// Security Level: Critical
        /// OCR Required: Yes
        /// Audit Level: Detailed
        /// </summary>
        PASSPORT = 1,

        /// <summary>
        /// Official birth certificate requiring secure storage and verification.
        /// Security Level: Critical
        /// OCR Required: Yes
        /// Audit Level: Detailed
        /// </summary>
        BIRTHCERTIFICATE = 2
    }
}
