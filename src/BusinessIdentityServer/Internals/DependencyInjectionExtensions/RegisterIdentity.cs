using BusinessIdentityServer.Internals.Data;
using BusinessIdentityServer.Internals.Models.DatabaseModels;
using Microsoft.AspNetCore.Identity;

namespace BusinessIdentityServer.Internals.DependencyInjectionExtensions;

public static class RegisterIdentity
{
    // registers and configures the ASP.NET Identity system
    public static IServiceCollection AddProjectIdentity(this IServiceCollection services)
    {
        services.AddIdentity<ApplicationUser, IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();


        services.Configure<IdentityOptions>(options =>
        {
            options.User.RequireUniqueEmail = true;

            options.SignIn.RequireConfirmedEmail = true;

            options.Lockout.MaxFailedAccessAttempts = 3;
        });

        return services;
    }
}
