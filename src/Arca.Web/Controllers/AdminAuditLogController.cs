using Arca.Application.AuditLog;
using Arca.Application.Common;
using Arca.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Arca.Web.Controllers;

[ApiController]
[Authorize(Policy = "SuperAdmin")]
[Route("api/admin/audit-logs")]
public sealed class AdminAuditLogController(AuditLogService auditLogService) : ControllerBase
{
    [Authorize(Policy = KnownPermissions.AuditView)]
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid? tenantId,
        [FromQuery] Guid? storeId,
        [FromQuery] Guid? userId,
        [FromQuery] string? entityName,
        [FromQuery] string? action,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await auditLogService.ListAsync(
            tenantId, storeId, userId, entityName, action, dateFrom, dateTo,
            new PageRequest(page, pageSize, search), cancellationToken);

        if (result.IsFailure || result.Value is null)
            return BadRequest(new { error = result.Error });

        return Ok(PagedResponse("logs", result.Value));
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
