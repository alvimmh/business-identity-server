using BusinessIdentityServer.Internals.Captcha;
using BusinessIdentityServer.Internals.Captcha.Filters;
using BusinessIdentityServer.Internals.Services;
using BusinessIdentityServer.Internals.Services.Email;
using Microsoft.Extensions.DependencyInjection;

namespace BusinessIdentityServer.Internals.DependencyInjectionExtensions;

public static class RegisterServices
{
    // registers project services to the DI
    public static IServiceCollection AddProjectServices(this IServiceCollection services)
    {
        // captcha provider with resilient HTTP client and filter
        services.AddHttpClient<CloudflareCaptchaProvider>();
        services.AddScoped<ValidateCaptchaFilter>();

        // route enumeration and totp challenge detection service
        services.AddSingleton<RouteEndpointService>();

        // email delivery and audit tracking services
        services.AddSingleton<SMTPService>();
        services.AddScoped<EmailService>();

        return services;
    }
}
