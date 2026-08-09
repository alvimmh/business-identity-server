using BusinessIdentityServer.Internals.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BusinessIdentityServer.Internals.DependencyInjectionExtensions;

// registers the main dbcontext for the application
public static class RegisterMainDbContext
{
    public static IServiceCollection AddMainDbContext(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment
    ) {
        string? mainDbHost = configuration["main_database:host"];
        string? mainDbName = configuration["main_database:name"];
        int mainDbPort;

        bool isMainDbPortValid = int.TryParse(configuration["main_database:port"], out mainDbPort);

        if (string.IsNullOrWhiteSpace(mainDbHost) ||
            string.IsNullOrWhiteSpace(mainDbName) ||
            !isMainDbPortValid
        ) {
            throw new NullReferenceException("Main database connection string is missing.");
        }

        string? mainDbUserName = configuration["main_database:username"];
        string? mainDbPassword = configuration["main_database:password"];

        if (string.IsNullOrWhiteSpace(mainDbUserName) || string.IsNullOrWhiteSpace(mainDbPassword))
            throw new NullReferenceException("Main database credentials are missing.");

        NpgsqlConnectionStringBuilder mainDbConnectionBuilder = new NpgsqlConnectionStringBuilder
        {
            Host = mainDbHost,
            Port = mainDbPort,
            Database = mainDbName,
            Username = mainDbUserName,
            Password = mainDbPassword
        };

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(
                mainDbConnectionBuilder.ConnectionString,
                o => o.MigrationsAssembly(typeof(Startup).Assembly.FullName)
            )
        );

        if (environment.IsDevelopment())
            services.AddDatabaseDeveloperPageExceptionFilter();

        return services;
    }
}
