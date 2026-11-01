using System.Diagnostics.CodeAnalysis;
using BusinessIdentityServer.Internals.Constants.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;

namespace BusinessIdentityServer.Internals.Services;

/// <summary>
/// Service facilitating application route enumeration, return URL validation, and dynamic route generation.
/// Registered as a singleton service.
/// </summary>
public sealed class RouteEndpointService
{
    public IReadOnlySet<string> EndpointRoutes { get; }
    public IReadOnlySet<string> EndpointRoutesRequiringTOTPChallenge { get; }

    public RouteEndpointService(EndpointDataSource endpointDataSource)
    {
        HashSet<string> routes = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> totpRoutes = new(StringComparer.OrdinalIgnoreCase);

        PopulateEndpointRoutes(endpointDataSource, routes, totpRoutes);

        EndpointRoutes = routes;
        EndpointRoutesRequiringTOTPChallenge = totpRoutes;
    }

    /// <summary>
    ///     Enumerates all routes and saves them in the class for usage.
    /// </summary>
    private static void PopulateEndpointRoutes(
        EndpointDataSource endpointDataSource,
        HashSet<string> routes,
        HashSet<string> totpRoutes
    )
    {
        IEnumerable<RouteEndpoint> dataSourceRouteEndpoints = endpointDataSource.Endpoints.OfType<RouteEndpoint>();

        foreach (RouteEndpoint routeEndpoint in dataSourceRouteEndpoints)
        {
            if (TryGetRoutePath(routeEndpoint, out string? routePath))
            {
                routes.Add(routePath);

                bool routeEndpointRequiresTOTPChallenge = routeEndpoint
                    .Metadata
                    .OfType<AuthorizeAttribute>()
                    .Any(metadata => metadata.Policy == Policies.TOTPChallenge);

                if (routeEndpointRequiresTOTPChallenge)
                {
                    totpRoutes.Add(routePath);
                }
            }
        }
    }

    private static bool TryGetRoutePath(RouteEndpoint endpoint, [NotNullWhen(true)] out string? routePath)
    {
        IReadOnlyDictionary<string, object?> values = endpoint.RoutePattern.RequiredValues;

        string? area = values.GetValueOrDefault("area")?.ToString();
        string? controller = values.GetValueOrDefault("controller")?.ToString();
        string? action = values.GetValueOrDefault("action")?.ToString();

        if (!string.IsNullOrEmpty(area) && !string.IsNullOrEmpty(controller) && !string.IsNullOrEmpty(action))
        {
            routePath = $"/{area}/{controller}/{action}";
            return true;
        }

        routePath = null;
        return false;
    }

    /// <summary>
    ///     Checks whether a return url is valid for redirect against registered application endpoints.
    /// </summary>
    public bool IsValidReturnUrl(string? returnUrl)
    {
        // 1. Must be a safe local relative URL (rejects external domains and protocol-relative exploits like //evil.com, /\\evil.com, ~//evil.com, and ~/\\evil.com)
        if (!IsLocalUrl(returnUrl))
        {
            return false;
        }

        // 2. Allow home / root redirects
        if (returnUrl == "/" || returnUrl == "~/")
        {
            return true;
        }

        // 3. Normalize the URL path by stripping query parameters, URL fragments, and leading '~'
        int delimiterIndex = returnUrl.IndexOfAny(['?', '#']);
        string path = delimiterIndex != -1 ? returnUrl[..delimiterIndex] : returnUrl;

        if (path.StartsWith('~'))
        {
            path = path[1..];
        }

        // 4. Allow OpenID Connect / OAuth2 protocol endpoints (e.g. /connect/authorize)
        if (path.StartsWith("/connect/authorize", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // 5. Check if the exact path matches a registered application route endpoint
        if (EndpointRoutes.Contains(path))
        {
            return true;
        }

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
            string baseRoute = path[..fourthSlashIndex];

            return EndpointRoutes.Contains(baseRoute);
        }

        return false;
    }

    /// <summary>
    ///     Determines whether the specified URL is local to the application.
    ///     Follows ASP.NET Core's UrlHelper.IsLocalUrl security specifications.
    /// </summary>
    public static bool IsLocalUrl([NotNullWhen(true)] string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

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
    ///     Generates a route URL with the provided action, controller, area, and optional query string.
    /// </summary>
    public static string GenerateRouteUrl(string action, string controller, string area, string? queryString = null)
    {
        string routeUrl = $"~/{area}/{controller}/{action}";

        return AddQueryString(routeUrl, queryString);
    }

    /// <summary>
    ///     Generates a route URL with the provided route path and optional query string.
    /// </summary>
    public static string GenerateRouteUrl(string route, string? queryString = null)
    {
        return AddQueryString(route, queryString);
    }

    private static string AddQueryString(string routeUrl, string? queryString)
    {
        if (!string.IsNullOrWhiteSpace(queryString))
        {
            string normalizedQuery = queryString.TrimStart('?');
            char separator = routeUrl.Contains('?') ? '&' : '?';

            return $"{routeUrl}{separator}{normalizedQuery}";
        }

        return routeUrl;
    }
}
