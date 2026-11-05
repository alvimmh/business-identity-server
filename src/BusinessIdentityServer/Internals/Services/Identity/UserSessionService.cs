using System.Security.Claims;
using BusinessIdentityServer.Internals.Constants.Authorization;
using BusinessIdentityServer.Internals.Constants.Storage;
using BusinessIdentityServer.Internals.Extensions.Domain;
using BusinessIdentityServer.Internals.Extensions.Logging;
using BusinessIdentityServer.Internals.Extensions.Web;
using BusinessIdentityServer.Internals.Models.DatabaseModels;
using BusinessIdentityServer.Internals.Services.Email;
using BusinessIdentityServer.Internals.Services.Identity.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;

namespace BusinessIdentityServer.Internals.Services.Identity;

/// <summary>
/// Service managing user authentication sessions, login verification, AMR claims, and session lifecycle.
/// </summary>
public sealed class UserSessionService : IUserSessionService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly EmailService _emailService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ITempDataDictionaryFactory _tempDataDictionaryFactory;
    private readonly ILogger<UserSessionService> _logger;

    // evaluates on-demand per call, avoiding stale references across asynchronous contexts
    private HttpContext? HttpContext => _httpContextAccessor.HttpContext;

    // evaluates on-demand per call, avoiding stale references across asynchronous contexts
    private ITempDataDictionary? TempData
    {
        get
        {
            HttpContext? httpContext = _httpContextAccessor.HttpContext;

            return httpContext != null ? _tempDataDictionaryFactory.GetTempData(httpContext) : null;
        }
    }

    public UserSessionService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        EmailService emailService,
        IHttpContextAccessor httpContextAccessor,
        ITempDataDictionaryFactory tempDataDictionaryFactory,
        ILogger<UserSessionService> logger
    )
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _emailService = emailService;
        _httpContextAccessor = httpContextAccessor;
        _tempDataDictionaryFactory = tempDataDictionaryFactory;
        _logger = logger;
    }

    /// <summary>
    /// Signs in the user or initiates time-limited 2FA email challenge if two-factor is enabled
    /// </summary>
    public async Task<string?> ManageTOTPSignInSuccessAsync(ApplicationUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        try
        {
            bool twoFactorAuthenticationEnabled = await _userManager.GetTwoFactorEnabledAsync(user);

            if (!twoFactorAuthenticationEnabled)
            {
                return await SignInAsync(user, twoFactorEnabled: false);
            }

            string emailVerificationCode = await _userManager.GenerateTwoFactorTokenAsync(
                user,
                CustomTokenOptions.GenericTOTPTokenProvider
            );

            string? resendEmailRecordId = await _emailService.SendNewSessionVerificationEmailAsync(
                user,
                emailVerificationCode
            );

            await ResetSignInAttemptsAsync(user);

            TempData?.Set(TempDataKeys.UserEmail, user.GetRequiredEmail());

            if (!string.IsNullOrWhiteSpace(resendEmailRecordId))
            {
                TempData?.Set(TempDataKeys.ResendEmailRecordId, resendEmailRecordId);
            }

            return RouteEndpointService.GenerateRouteUrl("EmailChallenge", "Authentication", "Access");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to process TOTP sign-in for user {UserId}", user.Id);

            TempData?.SetErrorMessage("Something went wrong. Please try again.");

            return null;
        }
    }

    /// <summary>
    /// Completes the sign-in flow after successful secondary email challenge verification.
    /// Cleans up any resend email records and completes sign-in.
    /// </summary>
    public async Task<string?> ManageEmailSignInSuccessAsync(
        ApplicationUser user,
        string? resendEmailRecordId = null)
    {
        ArgumentNullException.ThrowIfNull(user);

        try
        {
            string? redirectRoute = await SignInAsync(user, twoFactorEnabled: true);

            if (redirectRoute == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(resendEmailRecordId))
            {
                await _emailService.DeleteEmailRecordAsync(resendEmailRecordId, user);
            }

            return redirectRoute;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to complete email sign-in for user {UserId}", user.Id);

            TempData?.SetErrorMessage("Something went wrong while completing sign-in. Please try again.");

            return null;
        }
    }

    /// <summary>
    /// Signs in the user, updates security stamp and timestamps, and adds the appropriate RFC 8176 AMR claim
    /// ("mfa" if secondary verification was completed; otherwise "otp" for single-factor authenticator app sign-in).
    /// </summary>
    private async Task<string?> SignInAsync(ApplicationUser user, bool twoFactorEnabled = false)
    {
        ArgumentNullException.ThrowIfNull(user);

        try
        {
            IdentityResult updateSecurityStamp = await _userManager.UpdateSecurityStampAsync(user);

            if (!updateSecurityStamp.Succeeded)
            {
                _logger.LogIdentityErrors(updateSecurityStamp, "update security stamp", user.Id);

                TempData?.SetErrorMessage("Something went wrong. Please try again.");

                return null;
            }

            Claim amrClaim = CreateAMRClaim(twoFactorEnabled);

            _logger.LogInformation("Setting 'amr' claim value: '{AmrValue}' for user {UserId}", amrClaim.Value, user.Id);

            await _signInManager.SignInWithClaimsAsync(user, isPersistent: false, [amrClaim]);

            user.RecordSignInTimestamp();

            IdentityResult updateUser = await _userManager.UpdateAsync(user);

            if (!updateUser.Succeeded)
            {
                _logger.LogIdentityErrors(updateUser, "update user sign-in timestamp", user.Id);

                TempData?.SetErrorMessage("Something went wrong. Please try again.");

                await SignOutAsync(user);

                return null;
            }

            await ResetSignInAttemptsAsync(user);

            TempData?.Clear();

            return RouteEndpointService.GenerateRouteUrl("Dashboard", "Pages", "General");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to sign in user {UserId}", user.Id);

            await SignOutAsync(user);

            TempData?.SetErrorMessage("Something went wrong. Please try again.");

            return null;
        }
    }

    /// <summary>
    /// Signs out the user, invalidates the security stamp to terminate active sessions,
    /// and clears temporary state.
    /// </summary>
    public async Task SignOutAsync(ApplicationUser? activeUser = null)
    {
        try
        {
            ApplicationUser? user = activeUser ?? await GetActiveUserAsync();

            if (user != null)
            {
                IdentityResult updateSecurityStamp = await _userManager.UpdateSecurityStampAsync(user);

                if (!updateSecurityStamp.Succeeded)
                {
                    _logger.LogIdentityErrors(updateSecurityStamp, "update security stamp during sign out", user.Id, LogLevel.Warning);
                }
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to update security stamp during sign out; proceeding with local sign out");
        }

        TempData?.Clear();

        await _signInManager.SignOutAsync();
    }

    /// <summary>
    /// Verifies prerequisite factors to determine if the user is eligible to sign in
    /// </summary>
    public async Task<bool> VerifySignInPrerequisitesAsync(ApplicationUser? user)
    {
        if (user == null)
        {
            _logger.LogWarning("Sign-in prerequisites check failed: user is null.");

            return false;
        }

        if (user.Archived)
        {
            _logger.LogWarning("User with id {UserId} is archived.", user.Id);

            return false;
        }

        if (user.Blocked)
        {
            _logger.LogWarning("User with id {UserId} is blocked.", user.Id);

            return false;
        }

        if (string.IsNullOrWhiteSpace(user.Email))
        {
            _logger.LogWarning("User with id {UserId} has missing or empty email.", user.Id);

            return false;
        }

        if (!user.EmailConfirmed)
        {
            _logger.LogWarning("User with id {UserId} has unconfirmed email.", user.Id);

            return false;
        }

        if (!user.AccountRegistered)
        {
            _logger.LogWarning("User with id {UserId} has unregistered account.", user.Id);

            await _emailService.SendAccountNotRegisteredEmailAsync(user);

            return false;
        }

        if (user.RequiresAuthenticatorReset)
        {
            _logger.LogWarning("User with id {UserId} needs to reset TOTP access authenticator.", user.Id);

            await _emailService.SendResetTOTPAccessReminderEmailAsync(user);

            return false;
        }

        try
        {
            if (await _userManager.IsLockedOutAsync(user))
            {
                _logger.LogWarning("User with id {UserId} is currently locked out.", user.Id);

                return false;
            }

            if (!await _signInManager.CanSignInAsync(user))
            {
                _logger.LogWarning("User with id {UserId} cannot sign in (not allowed by SignInManager).", user.Id);

                return false;
            }

            return true;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to verify sign-in prerequisites for user {UserId}", user.Id);

            return false;
        }
    }

    /// <summary>
    /// Records an unsuccessful sign-in attempt, notifies the user via email if the account is locked out,
    /// and returns whether the attempt was successfully recorded and whether the user is now locked out.
    /// </summary>
    public async Task<(bool Succeeded, bool IsLockedOut)> RecordUnsuccessfulSignInAttemptAsync(ApplicationUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        try
        {
            IdentityResult saveUnsuccessfulAttemptResult = await _userManager.AccessFailedAsync(user);

            if (!saveUnsuccessfulAttemptResult.Succeeded)
            {
                _logger.LogIdentityErrors(saveUnsuccessfulAttemptResult, "record unsuccessful sign-in attempt", user.Id);

                return (Succeeded: false, IsLockedOut: false);
            }

            bool isUserLockedOut = await _userManager.IsLockedOutAsync(user);

            if (isUserLockedOut)
            {
                _logger.LogWarning("User {UserId} has been locked out due to consecutive failed sign-in attempts.", user.Id);

                await _emailService.SendAccountLockedOutEmailAsync(user);
            }

            return (Succeeded: true, IsLockedOut: isUserLockedOut);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to record unsuccessful sign-in attempt for user {UserId}", user.Id);

            return (Succeeded: false, IsLockedOut: false);
        }
    }

    /// <summary>
    /// Resets the user's failed access count as a non-blocking best-effort operation
    /// </summary>
    private async Task<bool> ResetSignInAttemptsAsync(ApplicationUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        try
        {
            IdentityResult resetAttemptsResult = await _userManager.ResetAccessFailedCountAsync(user);

            if (!resetAttemptsResult.Succeeded)
            {
                _logger.LogIdentityErrors(resetAttemptsResult, "reset access failed count", user.Id, LogLevel.Warning);

                return false;
            }

            return true;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to reset access failed count for user {UserId}", user.Id);

            return false;
        }
    }

    /// <summary>
    /// Resolves the challenged user from an active session or from TempData email
    /// </summary>
    public async Task<(ApplicationUser? User, bool HasActiveSession)> ResolveChallengedUserAsync()
    {
        try
        {
            ApplicationUser? user = await GetActiveUserAsync();

            if (user != null)
            {
                return (user, HasActiveSession: true);
            }

            string? userEmail = TempData?.GetString(TempDataKeys.UserEmail);

            if (string.IsNullOrWhiteSpace(userEmail))
            {
                return (null, HasActiveSession: false);
            }

            user = await _userManager.FindByEmailAsync(userEmail);

            return (user, HasActiveSession: false);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to resolve challenged user");

            return (null, HasActiveSession: false);
        }
    }

    /// <summary>
    /// Retrieves the currently signed-in user if an active session exists
    /// </summary>
    public async Task<ApplicationUser?> GetActiveUserAsync()
    {
        try
        {
            ClaimsPrincipal? principal = HttpContext?.User;

            if (principal == null || !_signInManager.IsSignedIn(principal))
            {
                return null;
            }

            return await _userManager.GetUserAsync(principal);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to retrieve active user");

            return null;
        }
    }

    /// <summary>
    /// Checks if the current user session is actively signed in
    /// </summary>
    public bool CheckActiveSession()
    {
        ClaimsPrincipal? principal = HttpContext?.User;

        if (principal == null)
        {
            return false;
        }

        return _signInManager.IsSignedIn(principal);
    }

    /// <summary>
    /// Refreshes the user's current session and clears temporary state
    /// Call this method instead of _signInManager.RefreshSignInAsync()
    /// </summary>
    public async Task<bool> RefreshUserSessionAsync(ApplicationUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        try
        {
            await _signInManager.RefreshSignInAsync(user);

            TempData?.Clear();

            return true;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to refresh user session for user {UserId}", user.Id);

            return false;
        }
    }

    private static Claim CreateAMRClaim(bool twoFactorEnabled)
    {
        string amrValue = twoFactorEnabled ? ProjectClaimValues.AMRTypeMFA : ProjectClaimValues.AMRTypeOTP;

        return new Claim(ProjectClaimTypes.AuthenticationMethod, amrValue);
    }
}
