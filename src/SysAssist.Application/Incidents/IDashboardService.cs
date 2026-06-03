using SysAssist.Contracts.Dashboard;

namespace SysAssist.Application.Incidents;

public interface IDashboardService
{
    Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken);
}
