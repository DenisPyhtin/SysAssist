using SysAssist.Contracts.Incidents;

namespace SysAssist.Contracts.Dashboard;

public sealed record DashboardSummaryDto(
    int OpenIncidents,
    int CriticalIncidents,
    int PendingApprovals,
    int SupportBundlesReady,
    IReadOnlyCollection<IncidentEventDto> RecentIncidents);
