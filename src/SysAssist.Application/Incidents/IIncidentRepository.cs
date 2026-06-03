using SysAssist.Domain.Entities;

namespace SysAssist.Application.Incidents;

public interface IIncidentRepository
{
    Task<IReadOnlyCollection<IncidentEvent>> ListRecentAsync(int take, CancellationToken cancellationToken);
}
