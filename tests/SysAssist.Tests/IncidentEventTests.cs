using SysAssist.Domain.Enums;
using SysAssist.Domain.Entities;

namespace SysAssist.Tests;

public sealed class IncidentEventTests
{
    [Fact]
    public void Create_ShouldInitializeOpenIncident()
    {
        var incident = new IncidentEvent
        {
            Source = "cloud-watch",
            EventType = "db.failover",
            Severity = EventSeverity.Error,
            Target = "db-primary",
            Status = EventStatus.New,
            Summary = "Database failover detected"
        };

        Assert.Equal(EventStatus.New, incident.Status);
        Assert.Equal(EventSeverity.Error, incident.Severity);
        Assert.NotEqual(Guid.Empty, incident.Id);
    }
}
