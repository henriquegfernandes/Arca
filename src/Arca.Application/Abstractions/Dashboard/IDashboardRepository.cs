using Arca.Application.Dashboard;

namespace Arca.Application.Abstractions.Dashboard;

public interface IDashboardRepository
{
    Task<DashboardSummaryDto> GetSummaryAsync(
        DashboardSummaryQuery query,
        CancellationToken cancellationToken = default);
}
