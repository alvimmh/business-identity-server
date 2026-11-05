using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace BusinessIdentityServer.Internals.Extensions.Logging;

/// <summary>
/// Extension methods for <see cref="ILogger"/> providing structured application logging helpers.
/// </summary>
public static class LoggerExtensions
{
    /// <summary>
    /// Logs structured identity errors when an identity operation fails.
    /// </summary>
    public static void LogIdentityErrors(
        this ILogger logger,
        IdentityResult result,
        string operation,
        string userId,
        LogLevel logLevel = LogLevel.Error)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(result);

        logger.Log(logLevel, "Failed to {Operation} for user {UserId}", operation, userId);

        foreach (IdentityError error in result.Errors)
        {
            logger.Log(logLevel, "{Operation} error [{Code}]: {Description}", operation, error.Code, error.Description);
        }
    }
}

