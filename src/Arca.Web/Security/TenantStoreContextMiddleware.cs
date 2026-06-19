using Arca.Application.Security;
using Microsoft.Extensions.Primitives;

namespace Arca.Web.Security;

public sealed class TenantStoreContextMiddleware(RequestDelegate next)
{
    private const string TenantHeaderName = "X-Tenant-Id";
    private const string StoreHeaderName = "X-Store-Id";

    public async Task InvokeAsync(
        HttpContext httpContext,
        TenantStoreRequestContext requestContext,
        ICurrentUserService currentUser,
        ITenantAccessService tenantAccessService)
    {
        if (!ShouldResolveContext(httpContext))
        {
            await next(httpContext);
            return;
        }

        var tenantResult = ReadOptionalGuid(httpContext.Request.Headers[TenantHeaderName], "Tenant context is invalid.");
        if (tenantResult.Error is not null)
        {
            await WriteBadRequestAsync(httpContext, tenantResult.Error);
            return;
        }

        var storeResult = ReadOptionalGuid(httpContext.Request.Headers[StoreHeaderName], "Store context is invalid.");
        if (storeResult.Error is not null)
        {
            await WriteBadRequestAsync(httpContext, storeResult.Error);
            return;
        }

        var tenantId = tenantResult.Value;
        var storeId = storeResult.Value;

        if (storeId is not null && tenantId is null)
        {
            await WriteBadRequestAsync(httpContext, "Tenant context is required when store context is provided.");
            return;
        }

        if (tenantId is not null)
        {
            if (!await tenantAccessService.TenantExistsAsync(tenantId.Value, httpContext.RequestAborted))
            {
                await WriteBadRequestAsync(httpContext, "Tenant context is invalid.");
                return;
            }

            if (currentUser.UserId is null
                || (!currentUser.IsSuperAdmin
                    && !await tenantAccessService.UserHasAccessToTenantAsync(
                        currentUser.UserId.Value,
                        tenantId.Value,
                        httpContext.RequestAborted)))
            {
                await WriteForbiddenAsync(httpContext, "User does not have access to this tenant.");
                return;
            }
        }

        if (tenantId is not null && storeId is not null)
        {
            if (!await tenantAccessService.StoreBelongsToTenantAsync(
                    tenantId.Value,
                    storeId.Value,
                    httpContext.RequestAborted))
            {
                await WriteBadRequestAsync(httpContext, "Store context is invalid.");
                return;
            }

            if (currentUser.UserId is null
                || (!currentUser.IsSuperAdmin
                    && !await tenantAccessService.UserHasAccessToStoreAsync(
                        currentUser.UserId.Value,
                        tenantId.Value,
                        storeId.Value,
                        httpContext.RequestAborted)))
            {
                await WriteForbiddenAsync(httpContext, "User does not have access to this store.");
                return;
            }
        }

        requestContext.SetTenant(tenantId);
        requestContext.SetStore(storeId);

        await next(httpContext);
    }

    private static bool ShouldResolveContext(HttpContext httpContext) =>
        httpContext.User.Identity?.IsAuthenticated == true
        && httpContext.Request.Path.StartsWithSegments("/api/admin", StringComparison.OrdinalIgnoreCase);

    private static (Guid? Value, string? Error) ReadOptionalGuid(StringValues values, string error)
    {
        var value = values.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(value))
        {
            return (null, null);
        }

        return Guid.TryParse(value, out var parsed) ? (parsed, null) : (null, error);
    }

    private static Task WriteBadRequestAsync(HttpContext httpContext, string error)
    {
        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        return httpContext.Response.WriteAsJsonAsync(new { error });
    }

    private static Task WriteForbiddenAsync(HttpContext httpContext, string error)
    {
        httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
        return httpContext.Response.WriteAsJsonAsync(new { error });
    }
}
