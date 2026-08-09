namespace BusinessIdentityServer;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-10.0#custom-prefix-for-environment-variables
        builder.Configuration.AddEnvironmentVariables("SECRET_");

        Startup startup = new Startup(builder.Configuration, builder.Environment);
        
        startup.ConfigureServices(builder.Services);

        var app = builder.Build();
        
        startup.Configure(app);

        app.Run();
    }
}
