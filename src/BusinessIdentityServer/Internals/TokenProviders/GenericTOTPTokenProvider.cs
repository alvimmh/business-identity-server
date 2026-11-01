using System.Text;
using BusinessIdentityServer.Internals.Constants.Authorization;
using Microsoft.AspNetCore.Identity;
using OtpNet;

namespace BusinessIdentityServer.Internals.TokenProviders;

/// <summary>
/// Custom TOTP token provider powered by Otp.NET (RFC 6238) for time-limited 6-digit codes.
/// </summary>
/// <typeparam name="TUser">The user entity type.</typeparam>
public class GenericTOTPTokenProvider<TUser> : TotpSecurityStampBasedTokenProvider<TUser> where TUser : class
{
    private const int OtpDigits = 6;

    /// <summary>
    /// Checks if a two-factor authentication token can be generated for the specified user.
    /// </summary>
    public override async Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<TUser> userManager, TUser user)
    {
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(user);

        string? userEmail = await userManager.GetEmailAsync(user);

        return !string.IsNullOrWhiteSpace(userEmail) && await userManager.IsEmailConfirmedAsync(user);
    }

    /// <summary>
    /// Generates a time-limited 6-digit TOTP code for the specified user and purpose.
    /// </summary>
    public override async Task<string> GenerateAsync(string purpose, UserManager<TUser> userManager, TUser user)
    {
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(user);

        byte[] secret = await CreateCombinedSecretAsync(purpose, userManager, user);

        var totp = new Totp(secret, step: CustomTokenOptions.DefaultTokenLifetimeInSeconds, totpSize: OtpDigits);

        return totp.ComputeTotp();
    }

    /// <summary>
    /// Validates the submitted 6-digit TOTP code against the computed RFC 6238 window.
    /// </summary>
    public override async Task<bool> ValidateAsync(string purpose, string token, UserManager<TUser> userManager, TUser user)
    {
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(user);

        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        byte[] secret = await CreateCombinedSecretAsync(purpose, userManager, user);

        var totp = new Totp(secret, step: CustomTokenOptions.DefaultTokenLifetimeInSeconds, totpSize: OtpDigits);

        return totp.VerifyTotp(token.Trim(), out _, new VerificationWindow(previous: 1, future: 0));
    }

    /// <summary>
    /// Returns entropy used to ensure purpose isolation across different token operations.
    /// </summary>
    public override async Task<string> GetUserModifierAsync(string purpose, UserManager<TUser> userManager, TUser user)
    {
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(user);

        string userId = await userManager.GetUserIdAsync(user);

        return $"Token:{purpose}:{userId}";
    }

    /// <summary>
    /// Combines the user's security token and entropy modifier into a single secret byte array.
    /// This ensures the resulting TOTP secret is tied to the user's security stamp and strictly
    /// isolated by purpose and user ID.
    /// </summary>
    private async Task<byte[]> CreateCombinedSecretAsync(string purpose, UserManager<TUser> userManager, TUser user)
    {
        byte[] securityToken = await userManager.CreateSecurityTokenAsync(user);

        string modifier = await GetUserModifierAsync(purpose, userManager, user);

        byte[] modifierBytes = Encoding.UTF8.GetBytes(modifier);

        byte[] combinedSecret = new byte[securityToken.Length + modifierBytes.Length];

        Buffer.BlockCopy(securityToken, 0, combinedSecret, 0, securityToken.Length);

        Buffer.BlockCopy(modifierBytes, 0, combinedSecret, securityToken.Length, modifierBytes.Length);

        return combinedSecret;
    }
}

