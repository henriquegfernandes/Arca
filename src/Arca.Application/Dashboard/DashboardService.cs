using Arca.Application.Abstractions.Dashboard;
using Arca.Application.Common;
using Arca.Application.Security;

namespace Arca.Application.Dashboard;

public sealed class DashboardService(
    IDashboardRepository repository,
    ICurrentUserService currentUser)
{
    public async Task<Result<DashboardSummaryDto>> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated)
        {
            return Result<DashboardSummaryDto>.Failure("User is not authenticated.");
        }

        var tenantId = currentUser.CurrentTenantId;
        var storeId = currentUser.CurrentStoreId;

        if (!currentUser.IsSuperAdmin && tenantId is null)
        {
            return Result<DashboardSummaryDto>.Failure("Tenant context is required.");
        }

        var summary = await repository.GetSummaryAsync(
            new DashboardSummaryQuery(tenantId, storeId, currentUser.IsSuperAdmin),
            cancellationToken);

        return Result<DashboardSummaryDto>.Success(summary);
    }
}
