using BusinessIdentityServer.Internals.Constants.Storage;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;

namespace BusinessIdentityServer.Internals.DependencyInjectionExtensions;

public static class RegisterAuthentication
{
    // registers and configures services for authentication
    public static IServiceCollection AddProjectAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
            options.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
            options.DefaultSignOutScheme = IdentityConstants.ApplicationScheme;
            options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
            options.DefaultForbidScheme = IdentityConstants.ApplicationScheme;
            options.DefaultScheme = IdentityConstants.ApplicationScheme;
        });

        services.Configure<SecurityStampValidatorOptions>(options =>
        {
            options.ValidationInterval = TimeSpan.FromSeconds(0);
        });

        services.ConfigureApplicationCookie(options =>
        {
            options.ReturnUrlParameter = "returnurl";
            options.LoginPath = "/access/authentication/signin";
            options.AccessDeniedPath = "/access/authentication/accessdenied";

            // configure authentication cookie options
            options.Cookie.Name = AuthenticationCookieOptions.Name;
            options.Cookie.Domain = AuthenticationCookieOptions.Domain;
            options.Cookie.Path = AuthenticationCookieOptions.Path;
            options.Cookie.HttpOnly = AuthenticationCookieOptions.HttpOnly;
            options.Cookie.IsEssential = AuthenticationCookieOptions.IsEssential;
            options.Cookie.SecurePolicy = AuthenticationCookieOptions.SecurePolicy;
            options.Cookie.SameSite = AuthenticationCookieOptions.SameSite;
            options.Cookie.MaxAge = AuthenticationCookieOptions.Duration;
            options.ExpireTimeSpan = AuthenticationCookieOptions.Duration;
            options.SlidingExpiration = false;
        });

        services.PostConfigure<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme, options =>
        {
            options.Cookie.SameSite = SameSiteMode.Lax;
        });

        return services;
    }
}
