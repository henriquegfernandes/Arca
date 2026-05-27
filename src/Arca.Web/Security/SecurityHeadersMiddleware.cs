namespace Arca.Web.Security;

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        headers.TryAdd("X-Content-Type-Options", "nosniff");
        headers.TryAdd("X-Frame-Options", "DENY");
        headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
        headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
        headers.TryAdd("X-Permitted-Cross-Domain-Policies", "none");

        if (!context.Request.Path.StartsWithSegments("/admin/assets"))
        {
            headers.TryAdd("Cache-Control", "no-store, no-cache");
        }

        await next(context);
    }
}
