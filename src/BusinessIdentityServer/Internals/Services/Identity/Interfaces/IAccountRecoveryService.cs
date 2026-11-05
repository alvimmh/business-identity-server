using BusinessIdentityServer.Internals.Models.DatabaseModels;
using BusinessIdentityServer.Internals.Models.InputModels;
using BusinessIdentityServer.Internals.Models.ServiceModels;

namespace BusinessIdentityServer.Internals.Services.Identity.Interfaces;

/// <summary>
/// Service managing emergency TOTP access recovery codes and authenticator credential resets.
/// </summary>
public interface IAccountRecoveryService
{
    /// <summary>
    /// Manages the creation of the view model or redirect route for the RecoverTOTPAccessChallenge GET action.
    /// </summary>
    Task<ServiceResult<TOTPAccessRecoveryChallengeInputModel>> ManageTOTPAccessRecoveryChallengeAsync(string defaultRoute);

    /// <summary>
    /// Verifies and redeems a single-use TOTP access recovery code.
    /// </summary>
    Task<bool> VerifyTOTPAccessRecoveryCodeAsync(ApplicationUser? user, string verificationCode);

    /// <summary>
    /// Generates distinct single-use TOTP access recovery codes for the user.
    /// </summary>
    Task<IReadOnlyCollection<string>> GenerateTOTPRecoveryCodesAsync(ApplicationUser user, int numberOfRecoveryCodes = 3);

    /// <summary>
    /// Initiates the TOTP authenticator reset workflow upon successful recovery code redemption.
    /// </summary>
    Task<string?> ManageRecoveryCodeVerificationSuccessAsync(ApplicationUser user);
}

