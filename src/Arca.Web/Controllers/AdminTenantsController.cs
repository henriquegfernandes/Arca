using Arca.Application.Common;
using Arca.Application.Security;
using Arca.Application.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Arca.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/admin/tenants")]
public sealed class AdminTenantsController(
    TenantSetupService tenantSetupService,
    TenantManagementService tenantManagementService,
    ICurrentUserService currentUserService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = KnownPermissions.TenantsView)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await tenantManagementService.ListTenantsAsync(new PageRequest(page, pageSize, search), cancellationToken);
        if (result.IsFailure || result.Value is null)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(PagedResponse("tenants", result.Value));
    }

    [HttpGet("{tenantId:guid}")]
    [Authorize(Policy = KnownPermissions.TenantsView)]
    public async Task<IActionResult> Get(Guid tenantId, CancellationToken cancellationToken)
    {
        var result = await tenantManagementService.GetTenantAsync(tenantId, cancellationToken);
        if (result.IsFailure || result.Value is null)
        {
            return NotFound(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    [HttpGet("{tenantId:guid}/stores")]
    [Authorize(Policy = KnownPermissions.StoresView)]
    public async Task<IActionResult> ListStores(
        Guid tenantId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await tenantManagementService.ListStoresAsync(tenantId, new PageRequest(page, pageSize, search), cancellationToken);
        if (result.IsFailure || result.Value is null)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(PagedResponse("stores", result.Value));
    }

    [HttpPost("{tenantId:guid}/stores")]
    [Authorize(Policy = KnownPermissions.StoresCreate)]
    public async Task<IActionResult> CreateStore(
        Guid tenantId,
        [FromBody] CreateStoreCommand request,
        CancellationToken cancellationToken)
    {
        var command = new CreateStoreCommand
        {
            TenantId = tenantId,
            Name = request.Name,
            Code = request.Code,
            Document = request.Document,
            Phone = request.Phone,
            Email = request.Email,
            AddressLine = request.AddressLine,
            City = request.City,
            State = request.State,
            ZipCode = request.ZipCode,
            Type = request.Type,
            RequestedByUserId = currentUserService.UserId,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString()
        };

        var result = await tenantManagementService.CreateStoreAsync(command, cancellationToken);
        if (result.IsFailure || result.Value is null)
        {
            return BadRequest(new { error = result.Error });
        }

        return Created($"/api/admin/tenants/{tenantId}/stores/{result.Value.Id}", result.Value);
    }

    [HttpPut("{tenantId:guid}/stores/{storeId:guid}")]
    [Authorize(Policy = KnownPermissions.StoresEdit)]
    public async Task<IActionResult> UpdateStore(
        Guid tenantId,
        Guid storeId,
        [FromBody] UpdateStoreCommand request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateStoreCommand
        {
            TenantId = tenantId,
            StoreId = storeId,
            Name = request.Name,
            Code = request.Code,
            Document = request.Document,
            Phone = request.Phone,
            Email = request.Email,
            AddressLine = request.AddressLine,
            City = request.City,
            State = request.State,
            ZipCode = request.ZipCode,
            Type = request.Type,
            RequestedByUserId = currentUserService.UserId,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString()
        };

        var result = await tenantManagementService.UpdateStoreAsync(command, cancellationToken);
        if (result.IsFailure || result.Value is null)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    [HttpDelete("{tenantId:guid}/stores/{storeId:guid}")]
    [Authorize(Policy = KnownPermissions.StoresDisable)]
    public async Task<IActionResult> DisableStore(
        Guid tenantId,
        Guid storeId,
        CancellationToken cancellationToken)
    {
        var result = await tenantManagementService.DisableStoreAsync(tenantId, storeId, cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Error });
        }

        return NoContent();
    }

    [HttpPost("setup")]
    [Authorize(Policy = KnownPermissions.TenantsSetup)]
    public async Task<IActionResult> Setup(
        [FromBody] CreateTenantSetupCommand request,
        CancellationToken cancellationToken)
    {
        var command = new CreateTenantSetupCommand
        {
            Company = request.Company,
            Settings = request.Settings,
            Stores = request.Stores,
            Administrator = request.Administrator,
            Catalog = request.Catalog,
            RequestedByUserId = currentUserService.UserId,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString()
        };

        var result = await tenantSetupService.SetupAsync(command, cancellationToken);
        if (result.IsFailure || result.Value is null)
        {
            return BadRequest(new { error = result.Error });
        }

        return Created($"/api/admin/tenants/{result.Value.TenantId}", result.Value);
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
}
