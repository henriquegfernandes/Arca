namespace Arca.Api.Middlewares;

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        headers.TryAdd("X-Content-Type-Options", "nosniff");
        headers.TryAdd("X-Frame-Options", "DENY");
        headers.TryAdd("Referrer-Policy", "no-referrer");
        headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
        headers.TryAdd("Cache-Control", "no-store");

        await next(context);
    }
}
