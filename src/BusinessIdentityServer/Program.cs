namespace BusinessIdentityServer;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        Startup startup = new Startup(builder.Configuration, builder.Environment);
        
        startup.ConfigureServices(builder.Services);

        var app = builder.Build();
        
        startup.Configure(app);

        app.Run();
    }
}
