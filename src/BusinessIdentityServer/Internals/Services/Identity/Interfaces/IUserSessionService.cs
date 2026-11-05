using BusinessIdentityServer.Internals.Models.DatabaseModels;

namespace BusinessIdentityServer.Internals.Services.Identity.Interfaces;

/// <summary>
/// Service managing user authentication sessions, login verification, AMR claims, and session lifecycle.
/// </summary>
public interface IUserSessionService
{
    /// <summary>
    /// Checks if the current user session is actively signed in.
    /// </summary>
    bool CheckActiveSession();

    /// <summary>
    /// Retrieves the currently signed-in user if an active session exists.
    /// </summary>
    Task<ApplicationUser?> GetActiveUserAsync();

    /// <summary>
    /// Resolves the challenged user from an active session or from TempData email.
    /// </summary>
    Task<(ApplicationUser? User, bool HasActiveSession)> ResolveChallengedUserAsync();

    /// <summary>
    /// Verifies prerequisite factors to determine if the user is eligible to sign in.
    /// </summary>
    Task<bool> VerifySignInPrerequisitesAsync(ApplicationUser? user);

    /// <summary>
    /// Records an unsuccessful sign-in attempt, notifies the user via email if locked out,
    /// and returns whether the attempt was successfully recorded and whether the user is now locked out.
    /// </summary>
    Task<(bool Succeeded, bool IsLockedOut)> RecordUnsuccessfulSignInAttemptAsync(ApplicationUser user);

    /// <summary>
    /// Signs in the user or initiates time-limited 2FA email challenge if two-factor is enabled.
    /// </summary>
    Task<string?> ManageTOTPSignInSuccessAsync(ApplicationUser user);

    /// <summary>
    /// Completes the sign-in flow after successful secondary email challenge verification.
    /// </summary>
    Task<string?> ManageEmailSignInSuccessAsync(ApplicationUser user, string? resendEmailRecordId = null);

    /// <summary>
    /// Signs out the user, invalidates the security stamp to terminate active sessions, and clears temporary state.
    /// </summary>
    Task SignOutAsync(ApplicationUser? activeUser = null);

    /// <summary>
    /// Refreshes the user's current session and clears temporary state.
    /// </summary>
    Task<bool> RefreshUserSessionAsync(ApplicationUser user);
}

