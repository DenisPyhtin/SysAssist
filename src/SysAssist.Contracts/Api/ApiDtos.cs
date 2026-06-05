namespace SysAssist.Contracts.Api;

public sealed record DashboardDto(
    int ActiveEventsCount,
    int PendingApprovalsCount,
    int EnabledModulesCount,
    IReadOnlyCollection<HealthSummaryItemDto> HealthSummary,
    IReadOnlyCollection<EventDto> RecentEvents,
    IReadOnlyCollection<NotificationDto> RecentNotifications,
    IReadOnlyCollection<AuditEntryDto> RecentAudit);

public sealed record HealthSummaryItemDto(string Status, int Count);

public sealed record EventDto(
    Guid Id,
    string? ExternalEventId,
    DateTimeOffset CreatedAt,
    string Source,
    string EventType,
    string Severity,
    string Target,
    string Status,
    string? CorrelationId,
    string Summary,
    Guid? ModuleId,
    string? PayloadJson,
    EventRecommendationDto? Recommendation);

public sealed record EventRecommendationDto(
    Guid Id,
    string Classification,
    string Explanation,
    decimal Confidence,
    string? ProbableCause,
    string? NextStep,
    Guid? SuggestedActionId);

public sealed record ApprovalDto(
    Guid Id,
    Guid EventId,
    Guid ActionId,
    string Status,
    DateTimeOffset RequestedAt,
    Guid? RequestedByUserId,
    DateTimeOffset? DecidedAt,
    Guid? DecidedByUserId,
    string? DecisionComment);

public sealed record DecisionRequest(string? Comment);

public sealed record ModuleDto(
    Guid Id,
    string Key,
    string Name,
    string Type,
    string? Description,
    bool IsEnabled,
    string HealthStatus,
    DateTimeOffset? LastHealthCheckAt,
    DateTimeOffset? LastFetchAt,
    bool SupportsPolling,
    bool SupportsWebhooks,
    bool SupportsActions,
    bool UseFallbackMode,
    bool SafeMode);

public sealed record ModuleSettingDto(
    Guid Id,
    string Key,
    string? Value,
    bool IsSecret,
    bool IsRequired,
    string ValueType,
    string? Description,
    bool HasValue);

public sealed record UpdateModuleSettingDto(string Key, string? Value);

public sealed record UpdateModuleSettingsRequest(IReadOnlyCollection<UpdateModuleSettingDto> Settings);

public sealed record ReplaceSecretRequest(string Value);

public sealed record ModuleActionDto(
    Guid Id,
    Guid ModuleId,
    string ActionKey,
    string Name,
    string? Description,
    string RiskLevel,
    bool RequiresApproval,
    bool IsEnabled);

public sealed record ExecuteActionRequest(string? Target, string? ParametersJson);

public sealed record OperationResultDto(bool Success, string Message, string? CorrelationId = null);

public sealed record AuditEntryDto(Guid Id, DateTimeOffset CreatedAt, string Actor, string Action, string Resource, string Result, string? CorrelationId);

public sealed record SystemLogDto(Guid Id, DateTimeOffset CreatedAt, string Level, string Component, string Message, string? CorrelationId, string? DetailsJson = null);

public sealed record NotificationDto(Guid Id, DateTimeOffset CreatedAt, string Channel, string Recipient, string? Subject, string Status, Guid? RelatedEventId);

public sealed record DiagnosticsDto(string Status, DateTimeOffset CheckedAt, IReadOnlyCollection<string> Components);

public sealed record ProductionReadinessDto(string Status, DateTimeOffset CheckedAt, IReadOnlyCollection<ProductionReadinessGateDto> Gates);

public sealed record ProductionReadinessGateDto(string Key, bool Passed, string Message, bool Required = true);

public sealed record SupportBundleDto(DateTimeOffset GeneratedAt, string FileName, IReadOnlyCollection<string> IncludedSections);

public sealed record SupportBundleFileDto(string FileName, string ContentJson);

public sealed record LicenseStatusDto(
    string Status,
    string Edition,
    string? TenantId,
    string? Subject,
    DateTimeOffset? ExpiresAt,
    bool IsValid,
    IReadOnlyCollection<string> Features,
    int? ModuleLimit,
    string Message,
    string Fingerprint);

public sealed record SabotageScenarioDto(
    string Key,
    string Name,
    string Description,
    string ModuleKey,
    string Severity,
    string EventType,
    string Target,
    string Summary,
    bool RequiresEnabledModule,
    bool LikelyCreatesApproval);
