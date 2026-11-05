using BusinessIdentityServer.Internals.Models.DatabaseModels;
using BusinessIdentityServer.Internals.Models.InputModels;
using BusinessIdentityServer.Internals.Models.ServiceModels;

namespace BusinessIdentityServer.Internals.Services.Identity.Interfaces;

/// <summary>
/// Service managing TOTP authenticator challenges, secondary email challenges, session elevation claims, and QR codes.
/// </summary>
public interface ITotpChallengeService
{
    /// <summary>
    /// Manages the creation of the view model or redirect route for the EmailChallenge GET action.
    /// </summary>
    Task<ServiceResult<EmailChallengeInputModel>> ManageEmailChallengeAsync(string defaultRoute, string? returnUrl = null);

    /// <summary>
    /// Authorizes elevated session access after successful TOTP challenge verification by setting expiry claims.
    /// </summary>
    Task<string?> ManageTOTPChallengeSuccessAsync(ApplicationUser user, string? targetRoute = null);

    /// <summary>
    /// Verifies a two-factor verification code for the specified user and token provider.
    /// </summary>
    Task<bool> VerifyTOTPCodeAsync(ApplicationUser? user, string tokenProvider, string verificationCode);

    /// <summary>
    /// Generates the QR code URI for authenticator app configuration.
    /// </summary>
    string? GenerateQRCodeUri(string email, string authenticatorKey);

    /// <summary>
    /// Generates the QR code URI for authenticator app configuration for the specified user.
    /// </summary>
    string? GenerateQRCodeUri(ApplicationUser? user, string authenticatorKey);

    /// <summary>
    /// Confirms the user's email address, cleans up resend email records, and returns the enrollment route.
    /// </summary>
    Task<string?> ManageEmailVerificationSuccessAsync(ApplicationUser user, string? resendEmailRecordId = null);
}

