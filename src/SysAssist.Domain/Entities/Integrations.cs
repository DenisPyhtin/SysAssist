using SysAssist.Domain.Common;
using SysAssist.Domain.Enums;

namespace SysAssist.Domain.Entities;

public sealed class IntegrationModule : Entity
{
    public required string Key { get; set; }
    public required string Name { get; set; }
    public ModuleType Type { get; set; }
    public string? Description { get; set; }
    public bool IsEnabled { get; set; }
    public HealthStatus HealthStatus { get; set; } = HealthStatus.Unknown;
    public DateTimeOffset? LastHealthCheckAt { get; set; }
    public DateTimeOffset? LastFetchAt { get; set; }
    public bool SupportsPolling { get; set; }
    public bool SupportsWebhooks { get; set; }
    public bool SupportsActions { get; set; }
    public bool UseFallbackMode { get; set; }
    public bool SafeMode { get; set; } = true;
    public ICollection<IntegrationSetting> Settings { get; set; } = [];
    public ICollection<IntegrationHealthCheck> HealthChecks { get; set; } = [];
    public ICollection<IntegrationModuleAction> Actions { get; set; } = [];
}

public sealed class IntegrationSetting : Entity
{
    public Guid ModuleId { get; set; }
    public IntegrationModule? Module { get; set; }
    public required string Key { get; set; }
    public string? Value { get; set; }
    public bool IsSecret { get; set; }
    public bool IsRequired { get; set; }
    public required string ValueType { get; set; }
    public string? Description { get; set; }
}

public sealed class IntegrationHealthCheck : Entity
{
    public Guid ModuleId { get; set; }
    public IntegrationModule? Module { get; set; }
    public DateTimeOffset CheckedAt { get; set; } = DateTimeOffset.UtcNow;
    public HealthStatus Status { get; set; } = HealthStatus.Unknown;
    public int? LatencyMs { get; set; }
    public string? Message { get; set; }
    public string? DetailsJson { get; set; }
}

public sealed class IntegrationModuleAction : Entity
{
    public Guid ModuleId { get; set; }
    public IntegrationModule? Module { get; set; }
    public required string ActionKey { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public RiskLevel RiskLevel { get; set; } = RiskLevel.Low;
    public bool RequiresApproval { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string? ParameterSchemaJson { get; set; }
}
