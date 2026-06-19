using Arca.Application.Abstractions.ExternalApi;
using Arca.Application.ExternalApi;

namespace Arca.Api.Middlewares;

public sealed class ExternalApiRequestLoggingMiddleware(RequestDelegate next)
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

        try
        {
            await next(context);
        }
        finally
        {
            var client = clientContextAccessor.Client;
            await apiClientRepository.LogRequestAsync(
                new ExternalApiRequestLogData(
                    client?.Id,
                    client?.TenantId,
                    client?.StoreId,
                    context.Request.Path + context.Request.QueryString,
                    context.Request.Method,
                    context.Response.StatusCode,
                    context.Connection.RemoteIpAddress?.ToString(),
                    context.Request.Headers.UserAgent.ToString()),
                CancellationToken.None);
        }
    }
}
