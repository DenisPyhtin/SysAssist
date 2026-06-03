using SysAssist.Application.Incidents;
using SysAssist.Domain.Entities;

namespace SysAssist.Infrastructure.Repositories;

public sealed class DemoIncidentRepository : IIncidentRepository
{
    public Task<IReadOnlyCollection<IncidentEvent>> ListRecentAsync(int take, CancellationToken cancellationToken)
        =>
        Task.FromResult<IReadOnlyCollection<IncidentEvent>>([]);
}
