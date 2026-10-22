namespace BusinessIdentityServer;

public static class Config
{
    public static string? GetApplicationDomain() => Startup.Configuration["application_domain"];

    public static string? GetApplicationUrl() => Startup.Configuration["application_url"];
}
