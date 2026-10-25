using System.Diagnostics.CodeAnalysis;
using BusinessIdentityServer.Internals.Constants.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;

namespace BusinessIdentityServer.Internals.Services;

// class facilitating route enumeration
public class RouteEndpointService
{
    public IReadOnlySet<string> EndpointRoutes { get; }
    public IReadOnlySet<string> EndpointRoutesRequiringTOTPChallenge { get; }

    public RouteEndpointService(EndpointDataSource endpointDataSource)
    {
        HashSet<string> routes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> totpRoutes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        PopulateEndpointRoutes(endpointDataSource, routes, totpRoutes);

        EndpointRoutes = routes;
        EndpointRoutesRequiringTOTPChallenge = totpRoutes;
    }

    /// <summary>
    ///     Enumerates all routes and saves them in the class for usage.
    ///     The class is registered as a singleton service for the application.
    /// </summary>
    private void PopulateEndpointRoutes(
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
}
