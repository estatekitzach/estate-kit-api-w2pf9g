namespace EstateKit.Core.Constants
{
    /// <summary>
    /// Static class containing standardized error codes used throughout the EstateKit Personal Information API 
    /// for consistent error handling and client communication. Each error code maps to specific error scenarios 
    /// with corresponding HTTP status codes and recovery actions.
    /// </summary>
    public static class ErrorCodes
    {
        /// <summary>
        /// Error code KEY000 (HTTP 404) indicating requested encryption key was not found in the system.
        /// Recovery Action: Generate new key or verify key identifier.
        /// </summary>
        public const string KEYINVALID = "KEY000";

        /// <summary>
        /// Error code KEY001 (HTTP 404) indicating requested encryption key was not found in the system.
        /// Recovery Action: Generate new key or verify key identifier.
        /// </summary>
        public const string KEYNOTFOUND = "KEY001";

        /// <summary>
        /// Error code KEY002 (HTTP 409) indicating key rotation operation is currently in progress.
        /// Recovery Action: Retry request after a brief delay.
        /// </summary>
        public const string KEYROTATIONINPROGRESS = "KEY002";

        /// <summary>
        /// Error code ENC001 (HTTP 400) indicating invalid format of input data for encryption/decryption.
        /// Recovery Action: Validate input format against API specifications.
        /// </summary>
        public const string INVALIDINPUTFORMAT = "ENC001";

        /// <summary>
        /// Error code AUTH001 (HTTP 401) indicating invalid or expired OAuth authentication token.
        /// Recovery Action: Refresh authentication token or re-authenticate.
        /// </summary>
        public const string INVALIDOAUTHTOKEN = "AUTH001";

        /// <summary>
        /// Error code SYS001 (HTTP 500) indicating AWS KMS service error or unavailability.
        /// Recovery Action: Contact system support or retry after service restoration.
        /// </summary>
        public const string KMSSERVICEERROR = "SYS001";

    }
}
