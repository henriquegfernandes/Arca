using Arca.Application.Common;
using Arca.Application.ExternalApi;
using Arca.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Arca.Web.Controllers;

[ApiController]
[Authorize(Policy = KnownPermissions.ApiKeysManage)]
[Route("api/admin/integrations/api-clients")]
public sealed class AdminApiClientsController(
    ApiClientService apiClientService,
    ICurrentUserService currentUserService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateApiClientCommand request,
        CancellationToken cancellationToken)
    {
        var tenantScope = ResolveTenantScope(request.TenantId);
        if (!currentUserService.IsSuperAdmin && tenantScope.Error is not null)
        {
            return tenantScope.Error;
        }

        var command = new CreateApiClientCommand
        {
            TenantId = currentUserService.IsSuperAdmin ? request.TenantId : tenantScope.TenantId!.Value,
            StoreId = request.StoreId,
            Name = request.Name,
            Permissions = request.Permissions
        };

        var result = await apiClientService.CreateAsync(command, cancellationToken);
        if (result.IsFailure || result.Value is null)
        {
            return BadRequest(new { error = result.Error });
        }

        return Created($"/api/admin/integrations/api-clients/{result.Value.Id}", result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid tenantId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var tenantScope = ResolveTenantScope(tenantId);
        if (tenantScope.Error is not null)
        {
            return tenantScope.Error;
        }

        var result = await apiClientService.ListAsync(tenantScope.TenantId!.Value, new PageRequest(page, pageSize, search), cancellationToken);
        if (result.IsFailure || result.Value is null)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(PagedResponse("clients", result.Value));
    }

    [HttpPut("{apiClientId:guid}")]
    public async Task<IActionResult> Update(
        Guid apiClientId,
        [FromBody] UpdateApiClientCommand request,
        CancellationToken cancellationToken)
    {
        var tenantScope = ResolveTenantScope(request.TenantId);
        if (tenantScope.Error is not null)
        {
            return tenantScope.Error;
        }

        var command = new UpdateApiClientCommand
        {
            ApiClientId = apiClientId,
            TenantId = tenantScope.TenantId!.Value,
            StoreId = request.StoreId,
            Name = request.Name,
            IsActive = request.IsActive,
            Permissions = request.Permissions,
            RequestedByUserId = currentUserService.UserId,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString()
        };

        var result = await apiClientService.UpdateAsync(command, cancellationToken);
        if (result.IsFailure || result.Value is null)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    [HttpDelete("{apiClientId:guid}")]
    public async Task<IActionResult> Disable(
        Guid apiClientId,
        [FromQuery] Guid tenantId,
        CancellationToken cancellationToken)
    {
        var tenantScope = ResolveTenantScope(tenantId);
        if (tenantScope.Error is not null)
        {
            return tenantScope.Error;
        }

        var result = await apiClientService.DisableAsync(tenantScope.TenantId!.Value, apiClientId, cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Error });
        }

        return NoContent();
    }

    [HttpDelete("{apiClientId:guid}/delete")]
    public async Task<IActionResult> Delete(
        Guid apiClientId,
        [FromQuery] Guid tenantId,
        CancellationToken cancellationToken)
    {
        var tenantScope = ResolveTenantScope(tenantId);
        if (tenantScope.Error is not null)
        {
            return tenantScope.Error;
        }

        var result = await apiClientService.DeleteAsync(
            new DeleteApiClientCommand
            {
                ApiClientId = apiClientId,
                TenantId = tenantScope.TenantId!.Value,
                RequestedByUserId = currentUserService.UserId,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent = Request.Headers.UserAgent.ToString()
            },
            cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Error });
        }

        return NoContent();
    }

    private static IDictionary<string, object> PagedResponse<T>(string itemsPropertyName, PagedResult<T> result) =>
        new Dictionary<string, object>
        {
            [itemsPropertyName] = result.Items,
            ["pagination"] = new
            {
                result.Page,
                result.PageSize,
                result.TotalCount,
                result.TotalPages
            }
        };

    private (Guid? TenantId, IActionResult? Error) ResolveTenantScope(Guid requestedTenantId)
    {
        if (currentUserService.IsSuperAdmin)
        {
            return requestedTenantId == Guid.Empty
                ? (null, BadRequest(new { error = "TenantId is required." }))
                : (requestedTenantId, null);
        }

        var tenantId = currentUserService.CurrentTenantId
            ?? (requestedTenantId == Guid.Empty ? null : requestedTenantId);
        return tenantId is null
            ? (null, BadRequest(new { error = "Tenant context is required." }))
            : (tenantId, null);
    }
}
