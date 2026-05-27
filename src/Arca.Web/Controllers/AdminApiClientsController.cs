using Arca.Application.Common;
using Arca.Application.ExternalApi;
using Arca.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Arca.Web.Controllers;

[ApiController]
[Authorize(Policy = KnownPermissions.ApiKeysManage)]
[Route("api/admin/integrations/api-clients")]
public sealed class AdminApiClientsController(ApiClientService apiClientService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateApiClientCommand request,
        CancellationToken cancellationToken)
    {
        var result = await apiClientService.CreateAsync(request, cancellationToken);
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
        var result = await apiClientService.ListAsync(tenantId, new PageRequest(page, pageSize, search), cancellationToken);
        if (result.IsFailure || result.Value is null)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(PagedResponse("clients", result.Value));
    }

    [HttpDelete("{apiClientId:guid}")]
    public async Task<IActionResult> Disable(
        Guid apiClientId,
        [FromQuery] Guid tenantId,
        CancellationToken cancellationToken)
    {
        var result = await apiClientService.DisableAsync(tenantId, apiClientId, cancellationToken);
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
