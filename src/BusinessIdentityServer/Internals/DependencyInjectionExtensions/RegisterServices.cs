using BusinessIdentityServer.Internals.Captcha;
using BusinessIdentityServer.Internals.Captcha.Filters;

namespace BusinessIdentityServer.Internals.DependencyInjectionExtensions;

public static class RegisterServices
{
    // registers project services to the DI
    public static IServiceCollection AddProjectServices(this IServiceCollection services)
    {
        // captcha provider with resilient HTTP client and filter
        services.AddHttpClient<CloudflareCaptchaProvider>();
        services.AddScoped<ValidateCaptchaFilter>();

        return services;
    }
}
