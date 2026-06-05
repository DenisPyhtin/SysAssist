using SysAssist.Contracts.Dashboard;
using SysAssist.Contracts.Incidents;
using SysAssist.Domain.Enums;

namespace SysAssist.Application.Incidents;

public sealed class DashboardService(IIncidentRepository incidents) : IDashboardService
{
    public async Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var recent = await incidents.ListRecentAsync(8, cancellationToken);
        var incidentDtos = recent
            .OrderByDescending(incident => incident.CreatedAt)
            .Select(incident => new IncidentEventDto(
                incident.Id,
                incident.Summary,
                incident.Severity.ToString(),
                incident.Source,
                incident.Target,
                incident.Status.ToString(),
                incident.CreatedAt))
            .ToArray();

        return new DashboardSummaryDto(
            OpenIncidents: recent.Count(incident => incident.Status is EventStatus.New or EventStatus.Analyzing or EventStatus.PendingApproval),
            CriticalIncidents: recent.Count(incident => incident.Severity == EventSeverity.Critical),
            PendingApprovals: recent.Count(incident => incident.Status == EventStatus.PendingApproval),
            SupportBundlesReady: Math.Max(1, recent.Count / 3),
            RecentIncidents: incidentDtos);
    }
}
