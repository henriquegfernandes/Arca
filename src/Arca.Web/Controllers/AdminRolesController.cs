using Arca.Application.Common;
using Arca.Application.Security;
using Arca.Application.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Arca.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/admin/roles")]
public sealed class AdminRolesController(
    RoleManagementService roleManagementService,
    ICurrentUserService currentUserService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = KnownPermissions.RolesView)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? tenantId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await roleManagementService.ListRolesAsync(tenantId, new PageRequest(page, pageSize, search), cancellationToken);
        if (result.IsFailure || result.Value is null)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(PagedResponse("roles", result.Value));
    }

    [HttpGet("permissions")]
    [Authorize(Policy = KnownPermissions.RolesView)]
    public async Task<IActionResult> ListPermissions(CancellationToken cancellationToken)
    {
        var result = await roleManagementService.ListPermissionsAsync(cancellationToken);
        if (result.IsFailure || result.Value is null)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(new { permissions = result.Value });
    }

    [HttpPost]
    [Authorize(Policy = KnownPermissions.RolesManage)]
    public async Task<IActionResult> Create(
        [FromBody] CreateRoleCommand request,
        CancellationToken cancellationToken)
    {
        var command = new CreateRoleCommand
        {
            TenantId = request.TenantId,
            Name = request.Name,
            Description = request.Description,
            Scope = request.Scope,
            Permissions = request.Permissions,
            RequestedByUserId = currentUserService.UserId,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString()
        };

        var result = await roleManagementService.CreateRoleAsync(command, cancellationToken);
        if (result.IsFailure || result.Value is null)
        {
            return BadRequest(new { error = result.Error });
        }

        return Created($"/api/admin/roles/{result.Value.Id}", result.Value);
    }

    [HttpPut("{roleId:guid}/permissions")]
    [Authorize(Policy = KnownPermissions.RolesManage)]
    public async Task<IActionResult> UpdatePermissions(
        Guid roleId,
        [FromBody] UpdateRolePermissionsCommand request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateRolePermissionsCommand
        {
            RoleId = roleId,
            Permissions = request.Permissions,
            RequestedByUserId = currentUserService.UserId,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString()
        };

        var result = await roleManagementService.UpdateRolePermissionsAsync(command, cancellationToken);
        if (result.IsFailure || result.Value is null)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    [HttpDelete("{roleId:guid}")]
    [Authorize(Policy = KnownPermissions.RolesManage)]
    public async Task<IActionResult> Disable(Guid roleId, CancellationToken cancellationToken)
    {
        var result = await roleManagementService.DisableRoleAsync(roleId, cancellationToken);
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
}
