using Arca.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Arca.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/admin/context")]
public sealed class AdminContextController(
    ICurrentUserService currentUserService,
    ICurrentTenantService currentTenantService,
    ICurrentStoreService currentStoreService,
    IUserContextRepository userContextRepository) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is null)
        {
            return Unauthorized(new { error = "User is not authenticated." });
        }

        var context = await userContextRepository.GetAsync(
            currentUserService.UserId.Value,
            currentTenantService.TenantId,
            currentStoreService.StoreId,
            cancellationToken);

        return context is null
            ? Unauthorized(new { error = "User was not found." })
            : Ok(context);
    }
}
