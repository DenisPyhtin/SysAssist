namespace SysAssist.Contracts.Incidents;

public sealed record IncidentEventDto(
    Guid Id,
    string Title,
    string Severity,
    string SourceKind,
    string SourceName,
    string Status,
    DateTimeOffset CreatedAt);
