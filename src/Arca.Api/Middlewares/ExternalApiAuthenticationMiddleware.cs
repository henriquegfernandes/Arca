using Arca.Application.Abstractions.ExternalApi;
using Arca.Application.ExternalApi;

namespace Arca.Api.Middlewares;

public sealed class ExternalApiAuthenticationMiddleware(
    RequestDelegate next,
    IApiKeyHasher apiKeyHasher)
{
    public async Task InvokeAsync(
        HttpContext context,
        IApiClientRepository apiClientRepository,
        IExternalApiClientContextAccessor clientContextAccessor)
    {
        if (!context.Request.Path.StartsWithSegments("/api/external"))
        {
            await next(context);
            return;
        }

        var apiKey = ExtractApiKey(context.Request);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "API key is required." });
            return;
        }

        var apiKeyHash = apiKeyHasher.HashApiKey(apiKey);
        var client = await apiClientRepository.AuthenticateAsync(apiKeyHash, context.RequestAborted);
        if (client is null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid API key." });
            return;
        }

        clientContextAccessor.Client = client;
        context.Items["ExternalApiClient"] = client;

        await apiClientRepository.TouchLastUsedAsync(client.Id, context.RequestAborted);
        await next(context);
    }

    private static string? ExtractApiKey(HttpRequest request)
    {
        if (request.Headers.TryGetValue("X-Api-Key", out var apiKeyValues))
        {
            return apiKeyValues.FirstOrDefault();
        }

        var authorization = request.Headers.Authorization.FirstOrDefault();
        if (authorization is not null && authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return authorization["Bearer ".Length..].Trim();
        }

        return null;
    }
}
