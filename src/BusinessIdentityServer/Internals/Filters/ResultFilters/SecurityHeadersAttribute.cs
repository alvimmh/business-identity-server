using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Net.Http.Headers;

namespace BusinessIdentityServer.Internals.Filters.ResultFilters;

// enforces OWASP-recommended HTTP security headers on rendered HTML views.

public sealed class SecurityHeadersAttribute : ActionFilterAttribute
{
    private const string ContentPolicy =
        "default-src 'self' challenges.cloudflare.com; " +
        "object-src 'none'; " +
        "frame-ancestors 'none'; " +
        "sandbox allow-forms allow-same-origin allow-scripts; " +
        "base-uri 'self';";

    public override void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is not ViewResult)
            return;

        IHeaderDictionary headers = context.HttpContext.Response.Headers;

        // https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/X-Content-Type-Options
        headers.TryAdd(HeaderNames.XContentTypeOptions, "nosniff");

        // https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/X-Frame-Options
        headers.TryAdd(HeaderNames.XFrameOptions, "DENY");

        // https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Content-Security-Policy
        headers.TryAdd(HeaderNames.ContentSecurityPolicy, ContentPolicy);

        // https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Referrer-Policy
        headers.TryAdd("Referrer-Policy", "no-referrer");
    }
}
