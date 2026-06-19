using System.Text.Json;
using Arca.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Primitives;

namespace Arca.Web.Security;

public sealed class PermissionAuthorizationHandler(
    ICurrentUserService currentUser,
    IPermissionService permissionService,
    IHttpContextAccessor httpContextAccessor) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is null)
        {
            return;
        }

        if (currentUser.IsSuperAdmin)
        {
            context.Succeed(requirement);
            return;
        }

        var requestScope = await ResolveRequestScopeAsync();
        var hasPermission = await permissionService.HasPermissionAsync(
            currentUser.UserId.Value,
            requirement.Permission,
            currentUser.CurrentTenantId ?? requestScope.TenantId,
            currentUser.CurrentStoreId ?? requestScope.StoreId);

        if (hasPermission)
        {
            context.Succeed(requirement);
        }
    }

    private async Task<(Guid? TenantId, Guid? StoreId)> ResolveRequestScopeAsync()
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return (null, null);
        }

        var request = httpContext.Request;

        var tenantId =
            TryReadGuid(httpContext.GetRouteValue("tenantId")?.ToString()) ??
            TryReadGuid(request.Query["tenantId"]) ??
            TryReadGuid(request.Headers["X-Tenant-Id"]);

        var storeId =
            TryReadGuid(httpContext.GetRouteValue("storeId")?.ToString()) ??
            TryReadGuid(request.Query["storeId"]) ??
            TryReadGuid(request.Headers["X-Store-Id"]);

        if (tenantId is not null && storeId is not null)
        {
            return (tenantId, storeId);
        }

        if (request.HasFormContentType)
        {
            var form = await request.ReadFormAsync();
            tenantId ??= TryReadGuid(form["TenantId"]) ?? TryReadGuid(form["tenantId"]);
            storeId ??= TryReadGuid(form["StoreId"]) ?? TryReadGuid(form["storeId"]);
            return (tenantId, storeId);
        }

        if (!IsJsonRequest(request) || request.Body is null)
        {
            return (tenantId, storeId);
        }

        request.EnableBuffering();
        request.Body.Position = 0;

        using var document = await JsonDocument.ParseAsync(request.Body);
        request.Body.Position = 0;

        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return (tenantId, storeId);
        }

        tenantId ??= TryReadGuid(document.RootElement, "tenantId") ?? TryReadGuid(document.RootElement, "TenantId");
        storeId ??= TryReadGuid(document.RootElement, "storeId") ?? TryReadGuid(document.RootElement, "StoreId");

        return (tenantId, storeId);
    }

    private static bool IsJsonRequest(HttpRequest request) =>
        request.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true
        && (request.ContentLength ?? 0) > 0;

    private static Guid? TryReadGuid(StringValues values) =>
        TryReadGuid(values.FirstOrDefault());

    private static Guid? TryReadGuid(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            ? TryReadGuid(property.GetString())
            : null;
    }

    private static Guid? TryReadGuid(string? value)
    {
        return Guid.TryParse(value, out var parsed) ? parsed : null;
    }
}
