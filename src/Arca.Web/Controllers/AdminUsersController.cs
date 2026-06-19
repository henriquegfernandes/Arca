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
        var tenantScope = ResolveTenantScope(tenantId);
        if (tenantScope.Error is not null)
        {
            return tenantScope.Error;
        }

        var result = await userManagementService.ListUsersAsync(tenantScope.TenantId, new PageRequest(page, pageSize, search), cancellationToken);
        if (result.IsFailure || result.Value is null)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(PagedResponse("users", result.Value));
    }

    [HttpGet("roles")]
    [Authorize(Policy = KnownPermissions.UsersAssignRoles)]
    public async Task<IActionResult> ListRoles(
        [FromQuery] Guid? tenantId,
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

        var result = await userManagementService.ListRolesAsync(
            tenantScope.TenantId,
            new PageRequest(page, pageSize, search),
            includeSystemRoles: currentUserService.IsSuperAdmin,
            cancellationToken);
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
        var tenantScope = ResolveTenantScope(request.TenantId);
        if (!currentUserService.IsSuperAdmin && tenantScope.Error is not null)
        {
            return tenantScope.Error;
        }

        var command = new CreateUserCommand
        {
            FullName = request.FullName,
            Email = request.Email,
            Phone = request.Phone,
            TemporaryPassword = request.TemporaryPassword,
            RoleId = request.RoleId,
            TenantId = currentUserService.IsSuperAdmin ? request.TenantId : tenantScope.TenantId,
            StoreId = request.StoreId,
            RequestedByUserId = currentUserService.UserId,
            RequestedByIsSuperAdmin = currentUserService.IsSuperAdmin,
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

    [HttpPut("{userId:guid}")]
    [Authorize(Policy = KnownPermissions.UsersEdit)]
    public async Task<IActionResult> Update(
        Guid userId,
        [FromBody] UpdateUserCommand request,
        CancellationToken cancellationToken)
    {
        var tenantScope = ResolveTenantScope(request.TenantId);
        if (!currentUserService.IsSuperAdmin && tenantScope.Error is not null)
        {
            return tenantScope.Error;
        }

        var command = new UpdateUserCommand
        {
            UserId = userId,
            FullName = request.FullName,
            Email = request.Email,
            Phone = request.Phone,
            RoleId = request.RoleId,
            TenantId = currentUserService.IsSuperAdmin ? request.TenantId : tenantScope.TenantId,
            StoreId = request.StoreId,
            RequestedByUserId = currentUserService.UserId,
            RequestedByIsSuperAdmin = currentUserService.IsSuperAdmin,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString()
        };

        var result = await userManagementService.UpdateUserAsync(command, cancellationToken);
        if (result.IsFailure || result.Value is null)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    [HttpDelete("{userId:guid}")]
    [Authorize(Policy = KnownPermissions.UsersDisable)]
    public async Task<IActionResult> Disable(Guid userId, CancellationToken cancellationToken)
    {
        var tenantScope = ResolveTenantScope(null);
        if (!currentUserService.IsSuperAdmin && tenantScope.Error is not null)
        {
            return tenantScope.Error;
        }

        var result = await userManagementService.DisableUserAsync(
            new DisableUserCommand
            {
                UserId = userId,
                TenantId = tenantScope.TenantId,
                RequestedByIsSuperAdmin = currentUserService.IsSuperAdmin
            },
            cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Error });
        }

        return NoContent();
    }

    [HttpPost("{userId:guid}/activate")]
    [Authorize(Policy = KnownPermissions.UsersDisable)]
    public async Task<IActionResult> Activate(Guid userId, CancellationToken cancellationToken)
    {
        var tenantScope = ResolveTenantScope(null);
        if (!currentUserService.IsSuperAdmin && tenantScope.Error is not null)
        {
            return tenantScope.Error;
        }

        var result = await userManagementService.ActivateUserAsync(
            new DisableUserCommand
            {
                UserId = userId,
                TenantId = tenantScope.TenantId,
                RequestedByIsSuperAdmin = currentUserService.IsSuperAdmin
            },
            cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Error });
        }

        return NoContent();
    }

    [HttpPost("{userId:guid}/change-password")]
    [Authorize(Policy = KnownPermissions.UsersChangePassword)]
    public async Task<IActionResult> ChangePassword(
        Guid userId,
        [FromBody] ChangeUserPasswordCommand request,
        CancellationToken cancellationToken)
    {
        var command = new ChangeUserPasswordCommand
        {
            UserId = userId,
            NewPassword = request.NewPassword,
            ConfirmPassword = request.ConfirmPassword,
            RequirePasswordChangeOnNextLogin = request.RequirePasswordChangeOnNextLogin,
            TenantId = currentUserService.CurrentTenantId ?? request.TenantId,
            RequestedByUserId = currentUserService.UserId,
            RequestedByIsSuperAdmin = currentUserService.IsSuperAdmin,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString()
        };

        var result = await userManagementService.ChangePasswordAsync(command, cancellationToken);
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

    private (Guid? TenantId, IActionResult? Error) ResolveTenantScope(Guid? requestedTenantId)
    {
        if (currentUserService.IsSuperAdmin)
        {
            return (requestedTenantId, null);
        }

        var tenantId = currentUserService.CurrentTenantId ?? requestedTenantId;
        return tenantId is null
            ? (null, BadRequest(new { error = "Tenant context is required." }))
            : (tenantId, null);
    }
}
