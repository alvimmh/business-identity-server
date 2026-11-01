using BusinessIdentityServer.Internals.Models.DatabaseModels;

namespace BusinessIdentityServer.Internals.Models.Extensions;

/// <summary>
/// Domain extension methods for <see cref="ApplicationUser"/>.
/// </summary>
public static class ApplicationUserExtensions
{
    /// <summary>
    /// Returns the user's email address or throws if null, empty, or whitespace.
    /// </summary>
    public static string GetRequiredEmail(this ApplicationUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        string? email = user.Email;

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new InvalidOperationException($"User '{user.Id}' does not have a valid email address configured.");
        }

        return email;
    }

    /// <summary>
    /// Returns a suitable display name for the user, falling back from FirstName to UserName to the provided fallback.
    /// </summary>
    public static string GetDisplayName(this ApplicationUser user, string fallback = "User")
    {
        ArgumentNullException.ThrowIfNull(user);

        if (!string.IsNullOrWhiteSpace(user.FirstName))
        {
            return user.FirstName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(user.UserName))
        {
            return user.UserName.Trim();
        }

        return fallback;
    }
}

