using SysAssist.Domain.Enums;

namespace SysAssist.Application.Integrations;

public sealed record IntegrationHealthResult(
    string ModuleKey,
    HealthStatus Status,
    string Message,
    int? LatencyMs = null,
    string? DetailsJson = null);

public sealed record IncomingEventDto(
    string Source,
    string? ExternalEventId,
    string EventType,
    EventSeverity Severity,
    string Target,
    string Summary,
    string? PayloadJson = null,
    string? CorrelationId = null);

public sealed record ActionExecutionRequest(
    string ActionKey,
    string? Target,
    string? ParametersJson,
    bool ApprovalGranted = false);

public sealed record ActionExecutionResult(
    bool Success,
    string Message,
    bool RequiresApproval = false,
    string? ResultJson = null);

public sealed record AdapterModuleState(
    string ModuleKey,
    bool IsEnabled,
    bool UseFallbackMode,
    bool SafeMode,
    IReadOnlyDictionary<string, string?> Settings);
