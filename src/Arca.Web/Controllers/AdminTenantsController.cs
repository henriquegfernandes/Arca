using Arca.Application.Security;
using Arca.Application.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Arca.Web.Controllers;

[ApiController]
[Authorize(Policy = "SuperAdmin")]
[Route("api/admin/tenants")]
public sealed class AdminTenantsController(
    TenantSetupService tenantSetupService,
    ICurrentUserService currentUserService) : ControllerBase
{
    [HttpPost("setup")]
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
}
