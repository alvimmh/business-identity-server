using System.Security.Claims;
using System.Text.Encodings.Web;
using BusinessIdentityServer.Internals.Constants.Account;
using BusinessIdentityServer.Internals.Constants.Authorization;
using BusinessIdentityServer.Internals.Constants.DataProtection;
using BusinessIdentityServer.Internals.Constants.Storage;
using BusinessIdentityServer.Internals.Extensions.Domain;
using BusinessIdentityServer.Internals.Extensions.Logging;
using BusinessIdentityServer.Internals.Extensions.Web;
using BusinessIdentityServer.Internals.Models.DatabaseModels;
using BusinessIdentityServer.Internals.Models.InputModels;
using BusinessIdentityServer.Internals.Models.ServiceModels;
using BusinessIdentityServer.Internals.Services.DataProtection;
using BusinessIdentityServer.Internals.Services.Email;
using BusinessIdentityServer.Internals.Services.Identity.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BusinessIdentityServer.Internals.Services.Identity;

/// <summary>
/// Service managing TOTP authenticator challenges, secondary email challenges, session elevation claims, and QR codes.
/// </summary>
public sealed class TotpChallengeService : ITotpChallengeService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUserSessionService _userSessionService;
    private readonly EmailService _emailService;
    private readonly ITimeLimitedDataProtectionService _timeLimitedDataProtectionService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _configuration;
    private readonly ITempDataDictionaryFactory _tempDataDictionaryFactory;
    private readonly UrlEncoder _urlEncoder;
    private readonly ILogger<TotpChallengeService> _logger;

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

    public TotpChallengeService(
        UserManager<ApplicationUser> userManager,
        IUserSessionService userSessionService,
        EmailService emailService,
        ITimeLimitedDataProtectionService timeLimitedDataProtectionService,
        IHttpContextAccessor httpContextAccessor,
        IConfiguration configuration,
        ITempDataDictionaryFactory tempDataDictionaryFactory,
        UrlEncoder urlEncoder,
        ILogger<TotpChallengeService> logger
    )
    {
        _userManager = userManager;
        _userSessionService = userSessionService;
        _emailService = emailService;
        _timeLimitedDataProtectionService = timeLimitedDataProtectionService;
        _httpContextAccessor = httpContextAccessor;
        _configuration = configuration;
        _tempDataDictionaryFactory = tempDataDictionaryFactory;
        _urlEncoder = urlEncoder;
        _logger = logger;
    }

    /// <summary>
    /// Manages the creation of the view model or redirect route for the EmailChallenge GET action
    /// </summary>
    public async Task<ServiceResult<EmailChallengeInputModel>> ManageEmailChallengeAsync(string defaultRoute, string? returnUrl = null)
    {
        try
        {
            (ApplicationUser? user, bool hasActiveSession) = await _userSessionService.ResolveChallengedUserAsync();

            if (user == null || !ValidateEmailChallengePrerequisites(user))
            {
                _logger.LogWarning("Unable to resolve valid user for email challenge; redirecting to {DefaultRoute}", defaultRoute);

                return ServiceResult<EmailChallengeInputModel>.Redirect(defaultRoute);
            }

            string resendEmailRecordId = TempData?.GetString(TempDataKeys.ResendEmailRecordId) ?? string.Empty;

            if (!hasActiveSession)
            {
                // retain TempData as user isn't signed in
                // so page reload keeps user on the same page
                TempData?.RetainKey(TempDataKeys.UserEmail);
                TempData?.RetainKey(TempDataKeys.ResendEmailRecordId);
            }

            return ServiceResult<EmailChallengeInputModel>.View(
                new EmailChallengeInputModel
                {
                    Email = user.GetRequiredEmail(),
                    ResendEmailRecordId = resendEmailRecordId,
                    ReturnUrl = returnUrl
                }
            );
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to manage email challenge; redirecting to {DefaultRoute}", defaultRoute);

            TempData?.SetErrorMessage("Something went wrong. Please try again.");

            return ServiceResult<EmailChallengeInputModel>.Redirect(defaultRoute);
        }
    }

    /// <summary>
    /// Confirms the user's email address, cleans up any resend email records, and returns the enrollment route.
    /// </summary>
    public async Task<string?> ManageEmailVerificationSuccessAsync(
        ApplicationUser user,
        string? resendEmailRecordId = null)
    {
        ArgumentNullException.ThrowIfNull(user);

        try
        {
            if (!await ConfirmUserEmailAsync(user))
            {
                TempData?.SetErrorMessage("Error confirming email address. Please try again.");

                return null;
            }

            if (!string.IsNullOrWhiteSpace(resendEmailRecordId))
            {
                await _emailService.DeleteEmailRecordAsync(resendEmailRecordId, user);
            }

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
            _logger.LogError(exception, "Failed to complete email verification for user {UserId}", user.Id);

            TempData?.Remove(TempDataKeys.TotpRegistrationSession);

            TempData?.SetErrorMessage("Something went wrong while confirming your email. Please try again.");

            return null;
        }
    }

    /// <summary>
    /// Authorizes elevated session access after successful TOTP challenge verification by replacing any existing
    /// authorization expiry claims, refreshing the session cookie, and returning the target route.
    /// </summary>
    public async Task<string?> ManageTOTPChallengeSuccessAsync(ApplicationUser user, string? targetRoute = null)
    {
        ArgumentNullException.ThrowIfNull(user);

        try
        {
            if (HttpContext?.User == null)
            {
                _logger.LogWarning("Cannot add TOTP authorization claim: active session context is null for user {UserId}", user.Id);

                TempData?.SetErrorMessage("Your session has expired. Please sign in again.");

                return null;
            }

            IList<Claim> userClaims = await _userManager.GetClaimsAsync(user);

            Claim[] claimsToRemove = userClaims
                .Where(claim => claim.Type == ProjectClaimTypes.TOTPAuthorizationExpiry)
                .ToArray();

            if (claimsToRemove.Length > 0)
            {
                IdentityResult removeClaimsResult = await _userManager.RemoveClaimsAsync(user, claimsToRemove);

                if (!removeClaimsResult.Succeeded)
                {
                    _logger.LogIdentityErrors(removeClaimsResult, "delete existing TOTP authorization claims", user.Id);

                    TempData?.SetErrorMessage("Something went wrong. Please try again later.");

                    return null;
                }
            }

            Claim claimToAdd = CreateTOTPAuthorizationExpiryClaim();

            IdentityResult addClaimResult = await _userManager.AddClaimAsync(user, claimToAdd);

            if (!addClaimResult.Succeeded)
            {
                _logger.LogIdentityErrors(addClaimResult, "add TOTP authorization expiry claim", user.Id);

                TempData?.SetErrorMessage("Something went wrong. Please try again later.");

                return null;
            }

            bool refreshSucceeded = await _userSessionService.RefreshUserSessionAsync(user);

            if (!refreshSucceeded)
            {
                TempData?.SetErrorMessage("Something went wrong. Please try again later.");

                return null;
            }

            return !string.IsNullOrWhiteSpace(targetRoute)
                ? targetRoute
                : RouteEndpointService.GenerateRouteUrl("Dashboard", "Pages", "General");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to authorize elevated TOTP challenge access for user {UserId}", user.Id);

            TempData?.SetErrorMessage("Something went wrong. Please try again later.");

            return null;
        }
    }

    /// <summary>
    /// Verifies a two-factor verification code for the specified user and token provider
    /// </summary>
    public async Task<bool> VerifyTOTPCodeAsync(ApplicationUser? user, string tokenProvider, string verificationCode)
    {
        if (user == null)
        {
            _logger.LogWarning("Two-factor code verification aborted: user is null.");

            return false;
        }

        if (string.IsNullOrWhiteSpace(tokenProvider))
        {
            _logger.LogWarning("Two-factor code verification aborted: token provider is missing for user {UserId}.", user.Id);

            return false;
        }

        if (string.IsNullOrWhiteSpace(verificationCode))
        {
            _logger.LogWarning("Two-factor code verification aborted: verification code is null or empty for user {UserId} using provider {TokenProvider}.", user.Id, tokenProvider);

            return false;
        }

        try
        {
            string sanitizedCode = verificationCode.Trim();

            bool isCodeValid = await _userManager.VerifyTwoFactorTokenAsync(user, tokenProvider, sanitizedCode);

            if (!isCodeValid)
            {
                _logger.LogWarning("Verification failed for user {UserId} using provider {TokenProvider}", user.Id, tokenProvider);
            }

            return isCodeValid;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to verify two-factor token for user {UserId} using provider {TokenProvider}", user.Id, tokenProvider);

            return false;
        }
    }

    /// <summary>
    /// Validates user eligibility for email challenge (sign-in or sign-up)
    /// </summary>
    private bool ValidateEmailChallengePrerequisites(ApplicationUser user)
    {
        if (string.IsNullOrWhiteSpace(user.Email))
        {
            _logger.LogWarning("User {UserId} has missing email for email challenge", user.Id);

            return false;
        }

        // user has confirmed email but never finished registering their account
        if (user.EmailConfirmed && !user.AccountRegistered)
        {
            _logger.LogWarning("User {UserId} has confirmed email but incomplete account registration for email challenge", user.Id);

            return false;
        }

        bool isSigningIn = user.EmailConfirmed && user.AccountRegistered;
        bool isSigningUp = !user.EmailConfirmed && !user.AccountRegistered;

        return isSigningIn || isSigningUp;
    }

    /// <summary>
    /// Generates the QR code URI for authenticator app configuration
    /// </summary>
    public string? GenerateQRCodeUri(string email, string authenticatorKey)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            _logger.LogWarning("Failed to generate QR code URI: recipient email address is null or empty.");

            return null;
        }

        if (string.IsNullOrWhiteSpace(authenticatorKey))
        {
            _logger.LogWarning("Failed to generate QR code URI: authenticator key is null or empty for recipient '{Recipient}'.", email);

            return null;
        }

        string rawIssuer = _configuration["APPLICATION_SHORT_NAME"] ?? string.Empty;

        string issuer = !string.IsNullOrWhiteSpace(rawIssuer) ? rawIssuer.Trim() : AccountOptions.DefaultAuthenticatorIssuer;

        string encodedIssuer = _urlEncoder.Encode(issuer);

        string encodedEmail = _urlEncoder.Encode(email.Trim());

        string sanitizedKey = authenticatorKey.Replace(" ", string.Empty);

        return $"otpauth://totp/{encodedIssuer}:{encodedEmail}?secret={sanitizedKey}&issuer={encodedIssuer}&digits=6";
    }

    /// <summary>
    /// Generates the QR code URI for authenticator app configuration for the specified user
    /// </summary>
    public string? GenerateQRCodeUri(ApplicationUser? user, string authenticatorKey)
    {
        if (user == null)
        {
            _logger.LogWarning("Failed to generate QR code URI: user is null.");

            return null;
        }

        if (string.IsNullOrWhiteSpace(user.Email))
        {
            _logger.LogWarning("Failed to generate QR code URI: user {UserId} has missing or empty email.", user.Id);

            return null;
        }

        return GenerateQRCodeUri(user.Email, authenticatorKey);
    }

    /// <summary>
    /// Confirms the user's email address and updates persistence state
    /// </summary>
    private async Task<bool> ConfirmUserEmailAsync(ApplicationUser user)
    {
        user.EmailConfirmed = true;

        IdentityResult updateUser = await _userManager.UpdateAsync(user);

        if (!updateUser.Succeeded)
        {
            _logger.LogIdentityErrors(updateUser, "update user email confirmation status", user.Id);

            return false;
        }

        return true;
    }

    private static Claim CreateTOTPAuthorizationExpiryClaim()
    {
        DateTime expiryDateTime = DateTime.UtcNow.AddSeconds(AccountOptions.TOTPAuthorizationDurationInSeconds);

        return new Claim(ProjectClaimTypes.TOTPAuthorizationExpiry, expiryDateTime.ToString("O"));
    }
}
