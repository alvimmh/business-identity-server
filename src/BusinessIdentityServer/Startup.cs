namespace BusinessIdentityServer;

public class Startup
{
    public IConfiguration Configuration { get; }
    public IWebHostEnvironment Environment { get; }

    public Startup(IConfiguration configuration, IWebHostEnvironment environment)
    {
        Configuration = configuration;
        Environment = environment;
    }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddControllersWithViews();

        if (Environment.IsDevelopment())
            services.AddDatabaseDeveloperPageExceptionFilter();
    }

    public void Configure(IApplicationBuilder app)
    {
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

        app.UseForwardedHeaders();

        app.UseStaticFiles();

        app.UseRouting();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllerRoute(
                name: "Areas",
                pattern: "{area=access}/{controller=authentication}/{action=signin}/{id?}"
            );
        });
    }
}