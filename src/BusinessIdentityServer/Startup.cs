using BusinessIdentityServer.Internals.DependencyInjectionExtensions;
using BusinessIdentityServer.Internals.Filters.ResultFilters;

namespace BusinessIdentityServer;

public class Startup
{
    public static IConfiguration Configuration { get; private set; } = null!;
    public static IWebHostEnvironment Environment { get; private set; } = null!;

    public Startup(IConfiguration configuration, IWebHostEnvironment environment)
    {
        Configuration = configuration;
        Environment = environment;
    }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddControllersWithViews(options =>
        {
            options.Filters.Add<SecurityHeadersAttribute>();
        });

        services.AddForwardedHeadersMiddleware(Environment);

        services.AddMainDbContext(
            Configuration,
            Environment
        );
    }

    public void Configure(IApplicationBuilder app)
    {
        // The Forwarded headers middleware must run before other middleware
        // https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0#middleware-order
        app.UseForwardedHeaders();

        if (Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
            app.UseMigrationsEndPoint();
        }
        else
        {
            app.UseExceptionHandler("/clientservices/correspondence/error");
            
            app.UseStatusCodePagesWithRedirects("~/clientservices/correspondence/error?errortype={0}");
            
            app.UseHsts();
        }

        app.UseHttpsRedirection();

        app.UseStaticFiles();

        app.UseRouting();

        app.UseAuthentication();

        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllerRoute(
                name: "Areas",
                pattern: "{area=access}/{controller=authentication}/{action=signin}/{id?}"
            );
        });
    }
}