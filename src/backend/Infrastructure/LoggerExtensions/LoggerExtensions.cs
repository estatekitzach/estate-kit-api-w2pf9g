
using Microsoft.Extensions.Logging;

namespace EstateKit.Infrastructure.Logger.Extensions
{
    public static class BaseLoggerExtensions
    {
        #region Private methods
        /*private static readonly Action<ILogger, long, string, Exception?> _logKeyNotFoundException =
            LoggerMessage.Define<long, string>(
                LogLevel.Error,
                new EventId(1, nameof(Vault.Core.Exceptions.KeyNotFoundException)),
                "Cache hit for contact {ContactId}");*/

        private static readonly Action<ILogger, string, Exception?> _logException =
            LoggerMessage.Define<string>(
                LogLevel.Error,
                new EventId(1, nameof(Exception)),
                "An exception occured. Message was {Message}");

        private static readonly Action<ILogger, string, Exception?> _logInformation =
           LoggerMessage.Define<string>(LogLevel.Information, new EventId(1, nameof(LogGenericInformation)), "An exception occured. Message was {Message}");

        private static readonly Action<ILogger, string, Exception?> _logWarning =
          LoggerMessage.Define<string>(LogLevel.Warning, new EventId(1, nameof(LogGenericWarning)), "An warning occured. Message was {Message}");

        private static readonly Action<ILogger, string, Exception?> _logDebugInformation =
           LoggerMessage.Define<string>(LogLevel.Information, new EventId(1, nameof(LogGenericDebug)), "Debug information. Message was {Message}");

        private static readonly Action<ILogger, long, Exception?> _cacheHitForContactId =
               LoggerMessage.Define<long>(LogLevel.Information, new EventId(1, nameof(LogCacheHitForContact)),
                   "Cache hit for contact {ContactId}");


        #endregion

        public static void LogGenericException(this ILogger logger, string message, Exception? exception) =>
           _logException(logger, message, exception);

        public static void LogGenericInformation(this ILogger logger, string message) =>
          _logInformation(logger, message, null);

        public static void LogGenericWarning(this ILogger logger, string message) =>
         _logWarning(logger, message, null);

        public static void LogGenericDebug(this ILogger logger, string message) =>
         _logDebugInformation(logger, message, null);

        public static void LogCacheHitForContact(this ILogger logger, long contactId, Exception? exception) =>
            _cacheHitForContactId(logger, contactId, exception);

        
    }
}
