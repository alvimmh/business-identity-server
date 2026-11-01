using BusinessIdentityServer.Internals.Constants.Authorization;
using BusinessIdentityServer.Internals.TokenProviders;
using Microsoft.AspNetCore.Identity;

namespace BusinessIdentityServer.Internals.DependencyInjectionExtensions;

public static class RegisterTokenProviders
{
    /// <summary>
    /// Registers the custom Otp.NET powered TOTP token provider with ASP.NET Core Identity.
    /// </summary>
    public static IdentityBuilder AddProjectTokenProviders(this IdentityBuilder identityBuilder)
    {
        Type genericTOTPTokenProviderType = typeof(GenericTOTPTokenProvider<>).MakeGenericType(identityBuilder.UserType);

        return identityBuilder.AddTokenProvider(CustomTokenOptions.GenericTOTPTokenProvider, genericTOTPTokenProviderType);
    }
}

