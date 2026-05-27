using Arca.Application.Common;
using Arca.Application.Security;
using Arca.Application.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Arca.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/admin/users")]
public sealed class AdminUsersController(
    UserManagementService userManagementService,
    ICurrentUserService currentUserService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = KnownPermissions.UsersView)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? tenantId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await userManagementService.ListUsersAsync(tenantId, new PageRequest(page, pageSize, search), cancellationToken);
        if (result.IsFailure || result.Value is null)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(PagedResponse("users", result.Value));
    }

    [HttpGet("roles")]
    [Authorize(Policy = KnownPermissions.RolesView)]
    public async Task<IActionResult> ListRoles(
        [FromQuery] Guid? tenantId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await userManagementService.ListRolesAsync(tenantId, new PageRequest(page, pageSize, search), cancellationToken);
        if (result.IsFailure || result.Value is null)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(PagedResponse("roles", result.Value));
    }

    [HttpPost]
    [Authorize(Policy = KnownPermissions.UsersCreate)]
    public async Task<IActionResult> Create(
        [FromBody] CreateUserCommand request,
        CancellationToken cancellationToken)
    {
        var command = new CreateUserCommand
        {
            FullName = request.FullName,
            Email = request.Email,
            Phone = request.Phone,
            TemporaryPassword = request.TemporaryPassword,
            RoleId = request.RoleId,
            TenantId = request.TenantId,
            StoreId = request.StoreId,
            RequestedByUserId = currentUserService.UserId,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString()
        };

        var result = await userManagementService.CreateUserAsync(command, cancellationToken);
        if (result.IsFailure || result.Value is null)
        {
            return BadRequest(new { error = result.Error });
        }

        return Created($"/api/admin/users/{result.Value.Id}", result.Value);
    }

    [HttpDelete("{userId:guid}")]
    [Authorize(Policy = KnownPermissions.UsersDisable)]
    public async Task<IActionResult> Disable(Guid userId, CancellationToken cancellationToken)
    {
        var result = await userManagementService.DisableUserAsync(userId, cancellationToken);
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
