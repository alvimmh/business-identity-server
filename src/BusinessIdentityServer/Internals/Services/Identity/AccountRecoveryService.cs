using BusinessIdentityServer.Internals.Constants.Account;
using BusinessIdentityServer.Internals.Constants.DataProtection;
using BusinessIdentityServer.Internals.Constants.Storage;
using BusinessIdentityServer.Internals.Extensions.Domain;
using BusinessIdentityServer.Internals.Extensions.Logging;
using BusinessIdentityServer.Internals.Extensions.Web;
using BusinessIdentityServer.Internals.Models.DatabaseModels;
using BusinessIdentityServer.Internals.Models.InputModels;
using BusinessIdentityServer.Internals.Models.ServiceModels;
using BusinessIdentityServer.Internals.Services.DataProtection;
using BusinessIdentityServer.Internals.Services.Identity.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;

namespace BusinessIdentityServer.Internals.Services.Identity;

/// <summary>
/// Service managing emergency TOTP access recovery codes and authenticator credential resets
/// </summary>
public sealed class AccountRecoveryService : IAccountRecoveryService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUserSessionService _userSessionService;
    private readonly ITimeLimitedDataProtectionService _timeLimitedDataProtectionService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ITempDataDictionaryFactory _tempDataDictionaryFactory;
    private readonly ILogger<AccountRecoveryService> _logger;

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

    public AccountRecoveryService(
        UserManager<ApplicationUser> userManager,
        IUserSessionService userSessionService,
        ITimeLimitedDataProtectionService timeLimitedDataProtectionService,
        IHttpContextAccessor httpContextAccessor,
        ITempDataDictionaryFactory tempDataDictionaryFactory,
        ILogger<AccountRecoveryService> logger
    )
    {
        _userManager = userManager;
        _userSessionService = userSessionService;
        _timeLimitedDataProtectionService = timeLimitedDataProtectionService;
        _httpContextAccessor = httpContextAccessor;
        _tempDataDictionaryFactory = tempDataDictionaryFactory;
        _logger = logger;
    }

    /// <summary>
    /// Manages the RecoverTOTPAccessChallenge GET action
    /// </summary>
    public async Task<ServiceResult<TOTPAccessRecoveryChallengeInputModel>> ManageTOTPAccessRecoveryChallengeAsync(string defaultRoute)
    {
        try
        {
            (ApplicationUser? user, bool hasActiveSession) = await _userSessionService.ResolveChallengedUserAsync();

            if (user == null || !ValidateTOTPRecoveryPrerequisites(user))
            {
                _logger.LogWarning("Unable to resolve valid user for TOTP access recovery challenge; redirecting to {DefaultRoute}", defaultRoute);

                return ServiceResult<TOTPAccessRecoveryChallengeInputModel>.Redirect(defaultRoute);
            }

            if (!hasActiveSession)
            {
                // retain TempData so page reload keeps user on
                // the same page as user is not signed in
                TempData?.RetainKey(TempDataKeys.UserEmail);
            }

            return ServiceResult<TOTPAccessRecoveryChallengeInputModel>.View(
                new TOTPAccessRecoveryChallengeInputModel
                {
                    Email = user.GetRequiredEmail()
                }
            );
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to manage TOTP access recovery challenge; redirecting to {DefaultRoute}", defaultRoute);

            TempData?.SetErrorMessage("Something went wrong. Please try again.");

            return ServiceResult<TOTPAccessRecoveryChallengeInputModel>.Redirect(defaultRoute);
        }
    }

    /// <summary>
    /// Initiates the TOTP authenticator reset workflow upon successful recovery code redemption:
    /// marks the user as requiring a reset, signs out the session, and redirects to TOTP setup.
    /// </summary>
    public async Task<string?> ManageRecoveryCodeVerificationSuccessAsync(ApplicationUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        try
        {
            user.RequiresAuthenticatorReset = true;

            IdentityResult updateUser = await _userManager.UpdateAsync(user);

            if (!updateUser.Succeeded)
            {
                _logger.LogIdentityErrors(updateUser, "update RequiresAuthenticatorReset status", user.Id);

                TempData?.SetErrorMessage("Something went wrong. Please try again with a different recovery code.");

                return null;
            }

            await _userSessionService.SignOutAsync(user);

            string totpRegistrationSessionToken = _timeLimitedDataProtectionService.Protect(
                user.GetRequiredEmail(),
                ProtectionPurpose.TotpRegistrationSession,
                TimeSpan.FromSeconds(AccountOptions.TempDataLifetimeInSeconds)
            );

            TempData?.Set(TempDataKeys.TotpRegistrationSession, totpRegistrationSessionToken);

            return RouteEndpointService.GenerateRouteUrl("RegisterTOTPAccess", "SignUp", "Enroll");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to complete recovery code verification workflow for user {UserId}", user.Id);

            TempData?.Remove(TempDataKeys.TotpRegistrationSession);

            TempData?.SetErrorMessage("Something went wrong. Please try again with a different recovery code.");

            return null;
        }
    }

    /// <summary>
    /// Generates distinct single-use TOTP access recovery codes for the user
    /// </summary>
    public async Task<IReadOnlyCollection<string>> GenerateTOTPRecoveryCodesAsync(ApplicationUser user, int numberOfRecoveryCodes = 3)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (numberOfRecoveryCodes <= 0)
        {
            return [];
        }

        try
        {
            const int maxAttempts = 3;

            int attempt = 0;

            while (attempt < maxAttempts)
            {
                attempt++;

                IEnumerable<string>? generatedCodes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(
                    user,
                    numberOfRecoveryCodes
                );

                // if generation failed (database/store error), exit immediately. Do not loop!
                if (generatedCodes == null)
                {
                    _logger.LogError("Failed to generate recovery codes for user {UserId}", user.Id);

                    return [];
                }

                IReadOnlyCollection<string> distinctCodes = generatedCodes as IReadOnlyCollection<string>
                    ?? generatedCodes.ToArray();

                // happy path: received the full requested number of distinct codes
                if (distinctCodes.Count >= numberOfRecoveryCodes)
                {
                    return distinctCodes;
                }

                _logger.LogWarning(
                    "GenerateNewTwoFactorRecoveryCodesAsync returned {Actual} codes due to duplicate removal, requested {Requested}. Retrying attempt {Attempt}/{MaxAttempts}.",
                    distinctCodes.Count,
                    numberOfRecoveryCodes,
                    attempt,
                    maxAttempts
                );
            }

            _logger.LogError(
                "Could not generate {Requested} distinct recovery codes for user {UserId} after {MaxAttempts} attempts.",
                numberOfRecoveryCodes,
                user.Id,
                maxAttempts
            );

            return [];
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to generate recovery codes for user {UserId}", user.Id);

            return [];
        }
    }

    /// <summary>
    /// Verifies and redeems a single-use TOTP access recovery code
    /// </summary>
    public async Task<bool> VerifyTOTPAccessRecoveryCodeAsync(ApplicationUser? user, string verificationCode)
    {
        if (user == null)
        {
            _logger.LogWarning("TOTP recovery code redemption aborted: user is null.");

            return false;
        }

        if (string.IsNullOrWhiteSpace(verificationCode))
        {
            _logger.LogWarning("TOTP recovery code redemption aborted: recovery code is null or empty for user {UserId}.", user.Id);

            return false;
        }

        try
        {
            string sanitizedCode = verificationCode.Trim();

            IdentityResult redeemResult = await _userManager.RedeemTwoFactorRecoveryCodeAsync(user, sanitizedCode);

            if (!redeemResult.Succeeded)
            {
                _logger.LogIdentityErrors(redeemResult, "redeem user TOTP access recovery code", user.Id, LogLevel.Warning);

                return false;
            }

            return true;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to redeem TOTP access recovery code for user {UserId}", user.Id);

            return false;
        }
    }

    /// <summary>
    /// Validates user eligibility for TOTP access recovery challenge
    /// </summary>
    private bool ValidateTOTPRecoveryPrerequisites(ApplicationUser user)
    {
        if (string.IsNullOrWhiteSpace(user.Email))
        {
            _logger.LogWarning("User {UserId} has missing email for TOTP recovery", user.Id);

            return false;
        }

        if (!user.EmailConfirmed)
        {
            _logger.LogWarning("User {UserId} has unconfirmed email for TOTP recovery", user.Id);

            return false;
        }

        // user has confirmed email but never finished registering their account
        if (!user.AccountRegistered)
        {
            _logger.LogWarning("User {UserId} has not completed account registration for TOTP recovery", user.Id);

            return false;
        }

        return true;
    }
}
