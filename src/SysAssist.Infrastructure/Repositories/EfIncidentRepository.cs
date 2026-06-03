using Microsoft.EntityFrameworkCore;
using SysAssist.Application.Incidents;
using SysAssist.Domain.Entities;
using SysAssist.Infrastructure.Data;

namespace SysAssist.Infrastructure.Repositories;

public sealed class EfIncidentRepository(SysAssistDbContext dbContext) : IIncidentRepository
{
    public async Task<IReadOnlyCollection<IncidentEvent>> ListRecentAsync(int take, CancellationToken cancellationToken)
    {
        return await dbContext.IncidentEvents
            .AsNoTracking()
            .OrderByDescending(incident => incident.CreatedAt)
            .Take(take)
            .ToArrayAsync(cancellationToken);
    }
}
