using SysAssist.Contracts.Api;
using SysAssist.Contracts.Auth;

namespace SysAssist.Application.Api;

public interface ISysAssistApiService
{
    Task WriteSystemLogAsync(string level, string component, string message, string? correlationId, string? detailsJson, CancellationToken cancellationToken);
    Task<DashboardDto> GetDashboardAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<EventDto>> ListEventsAsync(CancellationToken cancellationToken);
    Task<EventDto?> GetEventAsync(Guid id, CancellationToken cancellationToken);
    Task<OperationResultDto> ReprocessEventAsync(Guid id, string actor, string? correlationId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ApprovalDto>> ListApprovalsAsync(CancellationToken cancellationToken);
    Task<ApprovalDto?> GetApprovalAsync(Guid id, CancellationToken cancellationToken);
    Task<OperationResultDto> DecideApprovalAsync(Guid id, bool approve, string actor, Guid? userId, string? comment, string? correlationId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ModuleDto>> ListModulesAsync(CancellationToken cancellationToken);
    Task<ModuleDto?> GetModuleAsync(Guid id, CancellationToken cancellationToken);
    Task<OperationResultDto> SetModuleEnabledAsync(Guid id, bool enabled, string actor, string? correlationId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ModuleSettingDto>> GetModuleSettingsAsync(Guid id, CancellationToken cancellationToken);
    Task<OperationResultDto> UpdateModuleSettingsAsync(Guid id, UpdateModuleSettingsRequest request, string actor, string? correlationId, CancellationToken cancellationToken);
    Task<OperationResultDto> ReplaceModuleSecretAsync(Guid id, string key, ReplaceSecretRequest request, string actor, string? correlationId, CancellationToken cancellationToken);
    Task<OperationResultDto> TestModuleConnectionAsync(Guid id, string actor, string? correlationId, CancellationToken cancellationToken);
    Task<OperationResultDto> CheckModuleHealthAsync(Guid id, string actor, string? correlationId, CancellationToken cancellationToken);
    Task<OperationResultDto> FetchModuleEventsAsync(Guid id, string actor, string? correlationId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ModuleActionDto>> GetModuleActionsAsync(Guid id, CancellationToken cancellationToken);
    Task<OperationResultDto> SetModuleActionEnabledAsync(Guid moduleId, Guid actionId, bool enabled, string actor, string? correlationId, CancellationToken cancellationToken);
    Task<OperationResultDto> ResetModuleDemoModeAsync(Guid id, string actor, string? correlationId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ModuleActionDto>> ListActionsAsync(CancellationToken cancellationToken);
    Task<ModuleActionDto?> GetActionAsync(Guid id, CancellationToken cancellationToken);
    Task<OperationResultDto> ExecuteActionAsync(Guid id, ExecuteActionRequest request, string actor, string? correlationId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<AuditEntryDto>> ListAuditAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<SystemLogDto>> ListLogsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<NotificationDto>> ListNotificationsAsync(CancellationToken cancellationToken);
    Task<OperationResultDto> MarkNotificationReadAsync(Guid id, string actor, string? correlationId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<UserDto>> ListUsersAsync(CancellationToken cancellationToken);
    Task<UserDto> CreateUserAsync(CreateUserRequest request, string actor, string? correlationId, CancellationToken cancellationToken);
    Task<UserDto?> UpdateUserAsync(Guid id, UpdateUserRequest request, string actor, string? correlationId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<RoleDto>> ListRolesAsync(CancellationToken cancellationToken);
    Task<OperationResultDto> UpdateUserRolesAsync(Guid id, UpdateUserRolesRequest request, string actor, string? correlationId, CancellationToken cancellationToken);
    Task<DiagnosticsDto> GetDiagnosticsAsync(CancellationToken cancellationToken);
    Task<OperationResultDto> RunDiagnosticsAsync(string actor, string? correlationId, CancellationToken cancellationToken);
    Task<SupportBundleDto> GetSupportBundleAsync(CancellationToken cancellationToken);
    Task<SupportBundleFileDto> GetSupportBundleFileAsync(string actor, string? correlationId, CancellationToken cancellationToken);
    Task<OperationResultDto> ProcessWebhookAsync(string moduleKey, string rawPayload, string? signature, string actor, string? correlationId, CancellationToken cancellationToken);
}
