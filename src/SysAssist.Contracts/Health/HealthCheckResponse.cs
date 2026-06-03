namespace SysAssist.Contracts.Health;

public sealed record HealthCheckResponse(
    string Status,
    string Service,
    string Environment,
    DateTimeOffset TimestampUtc);
