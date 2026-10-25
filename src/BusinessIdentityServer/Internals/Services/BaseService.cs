using System.Diagnostics.CodeAnalysis;
using BusinessIdentityServer.Internals.Constants.Account;
using BusinessIdentityServer.Internals.Constants.Storage;
using BusinessIdentityServer.Internals.Models.ServiceModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;

namespace BusinessIdentityServer.Internals.Services;

public abstract class BaseService
{
    private protected HttpContext? HttpContext;
    private protected IConfiguration? Configuration;
    private protected ITempDataDictionary? TempData;

    public BaseService()
    {
    }

    public BaseService(IHttpContextAccessor httpContextAccessor)
    {
        HttpContext = httpContextAccessor.HttpContext;
    }

    public BaseService(
        IHttpContextAccessor httpContextAccessor,
        ITempDataDictionaryFactory tempDataDictionaryFactory
    )
    {
        HttpContext = httpContextAccessor.HttpContext;

        if (HttpContext != null)
        {
            TempData = tempDataDictionaryFactory.GetTempData(HttpContext);
        }
    }

    public BaseService(
        IHttpContextAccessor httpContextAccessor,
        IConfiguration configuration,
        ITempDataDictionaryFactory tempDataDictionaryFactory
    )
    {
        HttpContext = httpContextAccessor.HttpContext;
        Configuration = configuration;

        if (HttpContext != null)
        {
            TempData = tempDataDictionaryFactory.GetTempData(HttpContext);
        }
    }

    /// <summary>
    ///     Calls the base method AddOrRetainTempData() to add the captcha site key
    ///     to the TempData so it can be accessed in the view.
    /// </summary>
    public void AddCaptchaSiteKeyToTempData()
    {
        if (Configuration != null)
        {
            AddOrRetainTempData(
                TempDataKeys.CloudflareCaptchaSiteKey,
                Configuration["cloudflare_captcha:site_key"] ?? string.Empty
            );
        }
    }

    /// <summary>
    ///     Adds a TempData so it can be accessed in the view.
    ///     If the TempData already exists, then this method retains it.
    /// </summary>
    private protected void AddOrRetainTempData(string key, object value)
    {
        if (TempData == null)
            return;

        if (TempData.ContainsKey(key))
        {
            TempData.Keep(key);
        }
        else
        {
            TempData[key] = value;
        }
    }

    /// <summary>
    ///     Sets a DateTime object in the TempData that acts as the expiry
    ///     date/time for the TempData.
    /// </summary>
    private protected void SetTempDataExpiryDateTime()
    {
        if (TempData == null)
            return;

        DateTime expiryDateTime = DateTime.UtcNow.AddSeconds(AccountOptions.TempDataLifetimeInSeconds);
        TempData[TempDataKeys.TempDataExpiryDateTime] = expiryDateTime;
    }

    /// <summary>
    ///     Checks whether a return url is valid for redirect.
    /// </summary>
    private protected bool IsValidReturnUrl(string? returnUrl, IReadOnlySet<string> routeEndpoints)
    {
        // 1. Must be a safe local relative URL (rejects external domains and protocol-relative exploits like //evil.com, /\\evil.com, ~//evil.com, and ~/\\evil.com)
        if (!IsLocalUrl(returnUrl))
            return false;

        // 2. Allow home / root redirects
        if (returnUrl == "/" || returnUrl == "~/")
            return true;

        // 3. Normalize the URL path by stripping query parameters and leading '~'
        int queryStringIndex = returnUrl.IndexOf('?');
        string path = queryStringIndex != -1 ? returnUrl.Substring(0, queryStringIndex) : returnUrl;

        if (path.StartsWith('~'))
            path = path.Substring(1);

        // 4. Allow OpenID Connect / OAuth2 protocol endpoints (e.g. /connect/authorize)
        if (path.StartsWith("/connect/authorize", StringComparison.OrdinalIgnoreCase))
            return true;

        // 5. Check if the exact path matches a registered application route endpoint
        if (routeEndpoints.Contains(path))
            return true;

        // 6. If the path contains extra route parameters, extract the 3-segment base route:
        // /{area}/{controller}/{action}
        int slashCount = 0;
        int fourthSlashIndex = -1;

        for (int i = 0; i < path.Length; i++)
        {
            if (path[i] == '/')
            {
                slashCount++;
                if (slashCount == 4)
                {
                    fourthSlashIndex = i;
                    break;
                }
            }
        }

        if (fourthSlashIndex != -1)
        {
            string baseRoute = path.Substring(0, fourthSlashIndex);
            return routeEndpoints.Contains(baseRoute);
        }

        return false;
    }

    /// <summary>
    ///     Determines whether the specified URL is local to the application.
    ///     Follows ASP.NET Core's UrlHelper.IsLocalUrl security specifications.
    /// </summary>
    private static bool IsLocalUrl([NotNullWhen(true)] string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        // Allows "/" or "/foo" but rejects "//" or "/\"
        if (url[0] == '/')
        {
            return url.Length == 1 || (url[1] != '/' && url[1] != '\\');
        }

        // Allows "~/" or "~/foo" but rejects "~//" or "~/\'
        if (url[0] == '~' && url.Length > 1 && url[1] == '/')
        {
            return url.Length == 2 || (url[2] != '/' && url[2] != '\\');
        }

        return false;
    }

    /// <summary>
    ///     Wraps a view model in a ServiceResult indicating view rendering.
    /// </summary>
    private protected ServiceResult<T> ViewResult<T>(T viewModel) => ServiceResult<T>.View(viewModel);

    /// <summary>
    ///     Wraps a redirect route in a ServiceResult indicating an action redirect.
    /// </summary>
    private protected ServiceResult<T> RedirectResult<T>(string redirectRoute) => ServiceResult<T>.Redirect(redirectRoute);

    /// <summary>
    ///     Generates an absolute url with the provided information.
    /// </summary>
    private protected string GenerateAbsoluteLocalUrl(string action, string controller, string area)
    {
        string? rootUrl = Config.GetApplicationUrl();
        return $"{rootUrl}/{area}/{controller}/{action}";
    }

    /// <summary>
    ///     Generates a url with the provided parameters.
    /// </summary>
    private protected string GenerateRouteUrl(string action, string controller, string area, string? queryString = null)
    {
        string routeUrl = $"~/{area}/{controller}/{action}";
        return AddQueryString(routeUrl, queryString);
    }

    /// <summary>
    ///     Generates a url with the provided parameters. This is an overload method.
    /// </summary>
    private protected string GenerateRouteUrl(string route, string? queryString = null)
    {
        return AddQueryString(route, queryString);
    }

    private string AddQueryString(string routeUrl, string? queryString)
    {
        if (!string.IsNullOrWhiteSpace(queryString))
            return $"{routeUrl}?{queryString}";

        return routeUrl;
    }
}
