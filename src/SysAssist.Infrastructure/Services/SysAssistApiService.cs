using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using SysAssist.Application.Api;
using SysAssist.Application.Auth;
using SysAssist.Application.Integrations;
using SysAssist.Application.Security;
using SysAssist.Contracts.Api;
using SysAssist.Contracts.Auth;
using SysAssist.Domain.Entities;
using SysAssist.Domain.Enums;
using SysAssist.Infrastructure.Auth;
using SysAssist.Infrastructure.Data;
using SysAssist.Infrastructure.Integrations;
using SysAssist.Infrastructure.Modules;
using SysAssist.Infrastructure.Security;

namespace SysAssist.Infrastructure.Services;

public sealed class SysAssistApiService(
    IPasswordHasher passwordHasher,
    IIntegrationAdapterFactory adapterFactory,
    SysAssistDbContext? dbContext = null,
    ISecretProtector? secretProtector = null,
    ILicenseService? licenseService = null,
    IConfiguration? configuration = null) : ISysAssistApiService
{
    private readonly ISecretProtector _secretProtector = secretProtector ?? NoOpSecretProtector.Instance;
    private readonly ILicenseService? _licenseService = licenseService;

    public async Task WriteSystemLogAsync(string level, string component, string message, string? correlationId, string? detailsJson, CancellationToken cancellationToken)
    {
        if (dbContext is null)
        {
            DemoStore.SystemLogs.Add(new SystemLog { Level = level, Component = component, Message = message, CorrelationId = correlationId, DetailsJson = detailsJson });
            return;
        }

        dbContext.SystemLogs.Add(new SystemLog { Level = level, Component = component, Message = message, CorrelationId = correlationId, DetailsJson = detailsJson });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<DashboardDto> GetDashboardAsync(CancellationToken cancellationToken)
    {
        var events = await ListEventsAsync(cancellationToken);
        var approvals = await ListApprovalsAsync(cancellationToken);
        var modules = await ListModulesAsync(cancellationToken);
        var notifications = await ListNotificationsAsync(cancellationToken);
        var audit = await ListAuditAsync(cancellationToken);
        var health = modules.GroupBy(module => module.HealthStatus)
            .Select(group => new HealthSummaryItemDto(group.Key, group.Count()))
            .ToArray();

        return new DashboardDto(
            events.Count(item => item.Status is "New" or "Analyzing" or "PendingApproval"),
            approvals.Count(item => item.Status == "Pending"),
            modules.Count(item => item.IsEnabled),
            health,
            events.OrderByDescending(item => item.CreatedAt).Take(8).ToArray(),
            notifications.OrderByDescending(item => item.CreatedAt).Take(8).ToArray(),
            audit.OrderByDescending(item => item.CreatedAt).Take(8).ToArray());
    }

    public async Task<IReadOnlyCollection<EventDto>> ListEventsAsync(CancellationToken cancellationToken)
    {
        if (dbContext is null)
        {
            return DemoStore.Events.Select(ToEventDto).ToArray();
        }

        return await dbContext.IncidentEvents.AsNoTracking()
            .Include(item => item.Recommendation)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => ToEventDto(item))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<EventDto?> GetEventAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await FindEventAsync(id, cancellationToken);
        return item is null ? null : ToEventDto(item);
    }

    public async Task<OperationResultDto> ReprocessEventAsync(Guid id, string actor, string? correlationId, CancellationToken cancellationToken)
    {
        var item = await FindEventAsync(id, cancellationToken);
        if (item is null)
        {
            return NotFound("Event not found.", correlationId);
        }

        item.Status = EventStatus.Analyzing;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        await AuditAsync(actor, "events.reprocess", id.ToString(), "Accepted", correlationId, cancellationToken);
        await SaveAsync(cancellationToken);

        return Ok("Event queued for reprocessing.", correlationId);
    }

    public async Task<IReadOnlyCollection<ApprovalDto>> ListApprovalsAsync(CancellationToken cancellationToken)
    {
        if (dbContext is null)
        {
            return DemoStore.Approvals.Select(ToApprovalDto).ToArray();
        }

        return await dbContext.ApprovalRequests.AsNoTracking()
            .OrderByDescending(item => item.RequestedAt)
            .Select(item => ToApprovalDto(item))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<ApprovalDto?> GetApprovalAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await FindApprovalAsync(id, cancellationToken);
        return item is null ? null : ToApprovalDto(item);
    }

    public async Task<OperationResultDto> DecideApprovalAsync(Guid id, bool approve, string actor, Guid? userId, string? comment, string? correlationId, CancellationToken cancellationToken)
    {
        var item = await FindApprovalAsync(id, cancellationToken);
        if (item is null)
        {
            return NotFound("Approval request not found.", correlationId);
        }

        if (item.Status is not ApprovalStatus.Pending)
        {
            return new OperationResultDto(false, "Approval request is already decided.", correlationId);
        }

        var incident = await FindEventAsync(item.EventId, cancellationToken);
        if (!approve)
        {
            item.Status = ApprovalStatus.Rejected;
            item.DecidedAt = DateTimeOffset.UtcNow;
            item.DecidedByUserId = userId;
            item.DecisionComment = comment;
            item.UpdatedAt = DateTimeOffset.UtcNow;
            if (incident is not null)
            {
                incident.Status = EventStatus.Rejected;
                incident.UpdatedAt = DateTimeOffset.UtcNow;
            }

            await AuditAsync(actor, "ACTION_REJECTED", id.ToString(), "Success", correlationId, cancellationToken);
            AddNotification("local", "Operator,Engineer", "Action rejected", $"Approval {id} was rejected.", NotificationStatus.LocalOnly, incident?.Id);
            await WriteSystemLogAsync("Information", "Approvals", "Approval rejected.", correlationId, $$"""{"approvalId":"{{id}}"}""", cancellationToken);
            await SaveAsync(cancellationToken);
            return Ok("Approval rejected.", correlationId);
        }

        var action = await FindActionAsync(item.ActionId, cancellationToken);
        var module = action is null ? null : await FindModuleAsync(action.ModuleId, cancellationToken);
        if (action is null || module is null)
        {
            return new OperationResultDto(false, "Approval action or module was not found.", correlationId);
        }

        var execution = !IsConnectedModule(module)
            ? new ActionExecutionResult(false, "Module is not connected. Connect and test the module before approving actions.")
            : module.SafeMode
            ? new ActionExecutionResult(false, "SafeMode is enabled; approved action was not executed.")
            : await adapterFactory.GetAdapter(module.Key).ExecuteActionAsync(
                new ActionExecutionRequest(action.ActionKey, incident?.Target, "{}", ApprovalGranted: true),
                cancellationToken);

        item.Status = execution.Success ? ApprovalStatus.Executed : ApprovalStatus.Failed;
        item.DecidedAt = DateTimeOffset.UtcNow;
        item.DecidedByUserId = userId;
        item.DecisionComment = comment;
        item.ExecutionResultJson = execution.ResultJson ?? $$"""{"success":{{execution.Success.ToString().ToLowerInvariant()}},"message":"{{execution.Message}}"}""";
        item.UpdatedAt = DateTimeOffset.UtcNow;
        if (incident is not null)
        {
            incident.Status = execution.Success ? EventStatus.Completed : EventStatus.Failed;
            incident.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await AuditAsync(actor, "ACTION_APPROVED", id.ToString(), "Success", correlationId, cancellationToken);
        await AuditAsync(actor, module.SafeMode ? "ACTION_BLOCKED" : "ACTION_EXECUTED", action.ActionKey, execution.Success ? "Success" : "Failed", correlationId, cancellationToken);
        await WriteSystemLogAsync(execution.Success ? "Information" : "Error", "Approvals", execution.Message, correlationId, item.ExecutionResultJson, cancellationToken);
        AddNotification("local", "Operator,Engineer", execution.Success ? "Action completed" : "Action failed", execution.Message, NotificationStatus.LocalOnly, incident?.Id);
        await SaveAsync(cancellationToken);

        return new OperationResultDto(execution.Success, execution.Message, correlationId);
    }

    public async Task<IReadOnlyCollection<ModuleDto>> ListModulesAsync(CancellationToken cancellationToken)
    {
        if (dbContext is null)
        {
            return DemoStore.Modules.Select(ToModuleDto).ToArray();
        }

        return await dbContext.IntegrationModules.AsNoTracking()
            .OrderBy(item => item.Key)
            .Select(item => ToModuleDto(item))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<ModuleDto?> GetModuleAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await FindModuleAsync(id, cancellationToken);
        return item is null ? null : ToModuleDto(item);
    }

    public async Task<OperationResultDto> SetModuleEnabledAsync(Guid id, bool enabled, string actor, string? correlationId, CancellationToken cancellationToken)
    {
        var item = await FindModuleAsync(id, cancellationToken);
        if (item is null)
        {
            return NotFound("Module not found.", correlationId);
        }

        if (enabled)
        {
            var validation = await ValidateModuleCanEnableAsync(item, cancellationToken);
            if (!validation.Success)
            {
                await WriteSystemLogAsync("Warning", "Modules", validation.Message, correlationId, $$"""{"module":"{{item.Key}}"}""", cancellationToken);
                return validation;
            }
        }

        item.IsEnabled = enabled;
        item.HealthStatus = enabled ? HealthStatus.Unknown : HealthStatus.Disabled;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        await SetSyntheticSettingValueAsync(item.Id, "Enabled", enabled.ToString().ToLowerInvariant(), cancellationToken);
        await AuditAsync(actor, enabled ? "MODULE_ENABLED" : "MODULE_DISABLED", item.Key, "Success", correlationId, cancellationToken);
        await SaveAsync(cancellationToken);

        return Ok(enabled ? "Module enabled." : "Module disabled.", correlationId);
    }

    public async Task<IReadOnlyCollection<ModuleSettingDto>> GetModuleSettingsAsync(Guid id, CancellationToken cancellationToken)
    {
        var module = await FindModuleAsync(id, cancellationToken);
        if (module is null)
        {
            return [];
        }

        await EnsureModuleSettingsAsync(module, cancellationToken);
        var settings = dbContext is null
            ? DemoStore.Settings.Where(item => item.ModuleId == id).ToArray()
            : await dbContext.IntegrationSettings.AsNoTracking().Where(item => item.ModuleId == id).ToArrayAsync(cancellationToken);

        return settings.Select(ToSettingDto).ToArray();
    }

    public async Task<OperationResultDto> UpdateModuleSettingsAsync(Guid id, UpdateModuleSettingsRequest request, string actor, string? correlationId, CancellationToken cancellationToken)
    {
        var module = await FindModuleAsync(id, cancellationToken);
        if (module is null)
        {
            return NotFound("Module not found.", correlationId);
        }

        await EnsureModuleSettingsAsync(module, cancellationToken);
        var settings = dbContext is null
            ? DemoStore.Settings.Where(item => item.ModuleId == id).ToArray()
            : await dbContext.IntegrationSettings.Where(item => item.ModuleId == id).ToArrayAsync(cancellationToken);

        var requestedEnabled = TryGetBoolUpdate(request, "Enabled") ?? module.IsEnabled;
        if (requestedEnabled)
        {
            var missing = settings
                .Where(setting => setting.IsRequired && string.IsNullOrWhiteSpace(UpdatedValue(request, setting)))
                .Select(setting => setting.Key)
                .ToArray();

            if (missing.Length > 0)
            {
                await WriteSystemLogAsync("Warning", "Modules", "Module settings rejected because real-mode required settings are missing.", correlationId, $$"""{"module":"{{module.Key}}","missing":"{{string.Join(",", missing)}}"}""", cancellationToken);
                return new OperationResultDto(false, $"Required settings missing for real mode: {string.Join(", ", missing)}", correlationId);
            }
        }

        foreach (var update in request.Settings)
        {
            var setting = settings.SingleOrDefault(item => item.Key.Equals(update.Key, StringComparison.OrdinalIgnoreCase));
            if (setting is not null)
            {
                var value = update.Key.Equals("UseFallbackMode", StringComparison.OrdinalIgnoreCase) ? "false" : update.Value;
                if (setting.IsSecret && update.Value == "********")
                {
                    continue;
                }

                setting.Value = setting.IsSecret ? _secretProtector.Protect(value) : value;
                setting.UpdatedAt = DateTimeOffset.UtcNow;
                ApplyModuleSetting(module, setting.Key, value);
            }
        }

        await AuditAsync(actor, "MODULE_SETTINGS_UPDATED", module.Key, "Success", correlationId, cancellationToken);
        await SaveAsync(cancellationToken);

        return Ok("Module settings updated.", correlationId);
    }

    public async Task<OperationResultDto> ReplaceModuleSecretAsync(Guid id, string key, ReplaceSecretRequest request, string actor, string? correlationId, CancellationToken cancellationToken)
    {
        var module = await FindModuleAsync(id, cancellationToken);
        if (module is null)
        {
            return NotFound("Module not found.", correlationId);
        }

        await EnsureModuleSettingsAsync(module, cancellationToken);
        var setting = dbContext is null
            ? DemoStore.Settings.SingleOrDefault(item => item.ModuleId == id && item.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
            : await dbContext.IntegrationSettings.SingleOrDefaultAsync(item => item.ModuleId == id && item.Key == key, cancellationToken);

        if (setting is null || !setting.IsSecret)
        {
            return NotFound("Secret setting not found.", correlationId);
        }

        setting.Value = _secretProtector.Protect(request.Value);
        setting.UpdatedAt = DateTimeOffset.UtcNow;
        await AuditAsync(actor, "MODULE_SETTINGS_UPDATED", module.Key, "SecretReplaced", correlationId, cancellationToken);
        await SaveAsync(cancellationToken);

        return Ok("Secret value replaced.", correlationId);
    }

    public async Task<OperationResultDto> TestModuleConnectionAsync(Guid id, string actor, string? correlationId, CancellationToken cancellationToken)
    {
        return await TouchModuleHealthAsync(id, actor, "modules.test_connection", "Connection test completed.", correlationId, cancellationToken);
    }

    public async Task<OperationResultDto> CheckModuleHealthAsync(Guid id, string actor, string? correlationId, CancellationToken cancellationToken)
    {
        return await TouchModuleHealthAsync(id, actor, "modules.health", "Module health checked.", correlationId, cancellationToken);
    }

    public async Task<OperationResultDto> FetchModuleEventsAsync(Guid id, string actor, string? correlationId, CancellationToken cancellationToken)
    {
        var module = await FindModuleAsync(id, cancellationToken);
        if (module is null)
        {
            return NotFound("Module not found.", correlationId);
        }

        if (!module.IsEnabled)
        {
            await WriteSystemLogAsync("Warning", "Modules", "Fetch events skipped because module is disabled.", correlationId, $$"""{"module":"{{module.Key}}"}""", cancellationToken);
            return new OperationResultDto(false, "Module is disabled; fetch events was skipped.", correlationId);
        }

        var adapter = adapterFactory.GetAdapter(module.Key);
        var incomingEvents = await adapter.FetchEventsAsync(cancellationToken);
        foreach (var incoming in incomingEvents)
        {
            await ProcessIncomingEventAsync(module, incoming, actor, correlationId, cancellationToken);
        }

        module.LastFetchAt = DateTimeOffset.UtcNow;
        await AuditAsync(actor, "MODULE_FETCH_EVENTS", module.Key, "Success", correlationId, cancellationToken);
        await SaveAsync(cancellationToken);

        return Ok($"Fetch events completed. Normalized events: {incomingEvents.Count}.", correlationId);
    }

    public async Task<IReadOnlyCollection<ModuleActionDto>> GetModuleActionsAsync(Guid id, CancellationToken cancellationToken)
    {
        var actions = dbContext is null
            ? DemoStore.Actions.Where(item => item.ModuleId == id).ToArray()
            : await dbContext.IntegrationModuleActions.AsNoTracking().Where(item => item.ModuleId == id).ToArrayAsync(cancellationToken);

        return actions.Select(ToActionDto).ToArray();
    }

    public async Task<OperationResultDto> SetModuleActionEnabledAsync(Guid moduleId, Guid actionId, bool enabled, string actor, string? correlationId, CancellationToken cancellationToken)
    {
        var action = await FindActionAsync(actionId, cancellationToken);
        if (action is null || action.ModuleId != moduleId)
        {
            return NotFound("Module action not found.", correlationId);
        }

        action.IsEnabled = enabled;
        action.UpdatedAt = DateTimeOffset.UtcNow;
        await AuditAsync(actor, enabled ? "modules.actions.enable" : "modules.actions.disable", action.ActionKey, "Success", correlationId, cancellationToken);
        await SaveAsync(cancellationToken);

        return Ok(enabled ? "Action enabled." : "Action disabled.", correlationId);
    }

    public async Task<OperationResultDto> ResetModuleDemoModeAsync(Guid id, string actor, string? correlationId, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        return new OperationResultDto(false, "Demo mode was removed. Configure a real connection instead.", correlationId);
    }

    public async Task<IReadOnlyCollection<ModuleActionDto>> ListActionsAsync(CancellationToken cancellationToken)
    {
        var actions = dbContext is null
            ? DemoStore.Actions.ToArray()
            : await dbContext.IntegrationModuleActions.AsNoTracking().OrderBy(item => item.ActionKey).ToArrayAsync(cancellationToken);

        return actions.Select(ToActionDto).ToArray();
    }

    public async Task<ModuleActionDto?> GetActionAsync(Guid id, CancellationToken cancellationToken)
    {
        var action = await FindActionAsync(id, cancellationToken);
        return action is null ? null : ToActionDto(action);
    }

    public async Task<OperationResultDto> ExecuteActionAsync(Guid id, ExecuteActionRequest request, string actor, string? correlationId, CancellationToken cancellationToken)
    {
        var action = await FindActionAsync(id, cancellationToken);
        if (action is null)
        {
            return NotFound("Action not found.", correlationId);
        }

        if (!action.IsEnabled)
        {
            return new OperationResultDto(false, "Action is disabled.", correlationId);
        }

        var module = await FindModuleAsync(action.ModuleId, cancellationToken);
        if (module is null)
        {
            return NotFound("Action module not found.", correlationId);
        }

        if (!IsConnectedModule(module))
        {
            return new OperationResultDto(false, "Module is not connected. Connect and test the module before running actions.", correlationId);
        }

        if (action.RequiresApproval || action.RiskLevel is RiskLevel.High or RiskLevel.Critical)
        {
            await AuditAsync(actor, "APPROVAL_REQUESTED", action.ActionKey, "Required", correlationId, cancellationToken);
            await WriteSystemLogAsync("Warning", "Actions", "Action execution blocked because approval is required.", correlationId, $$"""{"action":"{{action.ActionKey}}","risk":"{{action.RiskLevel}}"}""", cancellationToken);
            await SaveAsync(cancellationToken);
            return new OperationResultDto(false, "Action requires approval before execution.", correlationId);
        }

        var adapter = adapterFactory.GetAdapter(module.Key);
        var result = await adapter.ExecuteActionAsync(
            new ActionExecutionRequest(action.ActionKey, request.Target, request.ParametersJson, ApprovalGranted: !action.RequiresApproval),
            cancellationToken);
        await AuditAsync(actor, "ACTION_EXECUTED", action.ActionKey, result.RequiresApproval ? "PendingApproval" : result.Success ? "Success" : "Failed", correlationId, cancellationToken);
        await WriteSystemLogAsync(result.Success ? "Information" : "Error", "Actions", result.Message, correlationId, result.ResultJson, cancellationToken);
        await SaveAsync(cancellationToken);

        return new OperationResultDto(result.Success, result.Message, correlationId);
    }

    public async Task<IReadOnlyCollection<AuditEntryDto>> ListAuditAsync(CancellationToken cancellationToken)
    {
        var audit = dbContext is null
            ? DemoStore.AuditEntries.ToArray()
            : await dbContext.AuditEntries.AsNoTracking().OrderByDescending(item => item.CreatedAt).Take(200).ToArrayAsync(cancellationToken);

        return audit.Select(ToAuditDto).ToArray();
    }

    public async Task<IReadOnlyCollection<SystemLogDto>> ListLogsAsync(CancellationToken cancellationToken)
    {
        var logs = dbContext is null
            ? DemoStore.SystemLogs.ToArray()
            : await dbContext.SystemLogs.AsNoTracking().OrderByDescending(item => item.CreatedAt).Take(200).ToArrayAsync(cancellationToken);

        return logs.Select(ToLogDto).ToArray();
    }

    public async Task<IReadOnlyCollection<NotificationDto>> ListNotificationsAsync(CancellationToken cancellationToken)
    {
        var notifications = dbContext is null
            ? DemoStore.Notifications.ToArray()
            : await dbContext.NotificationMessages.AsNoTracking().OrderByDescending(item => item.CreatedAt).Take(200).ToArrayAsync(cancellationToken);

        return notifications.Select(ToNotificationDto).ToArray();
    }

    public async Task<OperationResultDto> MarkNotificationReadAsync(Guid id, string actor, string? correlationId, CancellationToken cancellationToken)
    {
        if (dbContext is null)
        {
            var demoNotification = DemoStore.Notifications.FirstOrDefault(item => item.Id == id);
            if (demoNotification is null)
            {
                return NotFound("Notification was not found.", correlationId);
            }

            demoNotification.Status = NotificationStatus.Read;
        }
        else
        {
            var notification = await dbContext.NotificationMessages.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (notification is null)
            {
                return NotFound("Notification was not found.", correlationId);
            }

            notification.Status = NotificationStatus.Read;
        }

        await AuditAsync(actor, "notifications.mark_read", id.ToString(), "Success", correlationId, cancellationToken);
        await SaveAsync(cancellationToken);
        return Ok("Notification marked as read.", correlationId);
    }

    public async Task<IReadOnlyCollection<UserDto>> ListUsersAsync(CancellationToken cancellationToken)
    {
        var users = dbContext is null
            ? DemoUsers().ToArray()
            : await dbContext.Users.AsNoTracking().Include(item => item.UserRoles).ThenInclude(item => item.Role).ToArrayAsync(cancellationToken);

        return users.Select(ToUserDto).ToArray();
    }

    private IEnumerable<User> DemoUsers()
    {
        var bootstrapAdmin = configuration is null
            ? null
            : DemoAuthData.FindUser("admin", configuration, passwordHasher);
        if (bootstrapAdmin is not null)
        {
            yield return bootstrapAdmin;
        }

        foreach (var user in DemoStore.Users)
        {
            yield return user;
        }
    }

    public async Task<UserDto> CreateUserAsync(CreateUserRequest request, string actor, string? correlationId, CancellationToken cancellationToken)
    {
        var user = new User
        {
            Login = request.Login,
            DisplayName = request.DisplayName,
            Email = request.Email,
            PasswordHash = passwordHasher.Hash(request.Password),
            IsActive = request.IsActive
        };

        if (dbContext is null)
        {
            DemoStore.Users.Add(user);
        }
        else
        {
            dbContext.Users.Add(user);
        }

        await AuditAsync(actor, "users.create", user.Login, "Success", correlationId, cancellationToken);
        await SaveAsync(cancellationToken);

        return ToUserDto(user);
    }

    public async Task<UserDto?> UpdateUserAsync(Guid id, UpdateUserRequest request, string actor, string? correlationId, CancellationToken cancellationToken)
    {
        var user = await FindUserAsync(id, cancellationToken);
        if (user is null)
        {
            return null;
        }

        user.DisplayName = request.DisplayName;
        user.Email = request.Email;
        user.IsActive = request.IsActive;
        if (!string.IsNullOrEmpty(request.Password))
        {
            user.PasswordHash = passwordHasher.Hash(request.Password);
        }

        user.UpdatedAt = DateTimeOffset.UtcNow;
        await AuditAsync(actor, "users.update", user.Login, "Success", correlationId, cancellationToken);
        await SaveAsync(cancellationToken);

        return ToUserDto(user);
    }

    public async Task<IReadOnlyCollection<RoleDto>> ListRolesAsync(CancellationToken cancellationToken)
    {
        var roles = dbContext is null
            ? DemoStore.Roles.ToArray()
            : await dbContext.Roles.AsNoTracking().OrderBy(item => item.Name).ToArrayAsync(cancellationToken);

        return roles.Select(item => new RoleDto(item.Id, item.Name, item.Description)).ToArray();
    }

    public async Task<OperationResultDto> UpdateUserRolesAsync(Guid id, UpdateUserRolesRequest request, string actor, string? correlationId, CancellationToken cancellationToken)
    {
        var user = await FindUserAsync(id, cancellationToken);
        if (user is null)
        {
            return NotFound("User not found.", correlationId);
        }

        var roles = dbContext is null
            ? DemoStore.Roles.Where(role => request.Roles.Contains(role.Name, StringComparer.OrdinalIgnoreCase)).ToArray()
            : await dbContext.Roles.Where(role => request.Roles.Contains(role.Name)).ToArrayAsync(cancellationToken);

        user.UserRoles.Clear();
        foreach (var role in roles)
        {
            user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id, Role = role });
        }

        await AuditAsync(actor, "users.roles.update", user.Login, "Success", correlationId, cancellationToken);
        await SaveAsync(cancellationToken);

        return Ok("User roles updated.", correlationId);
    }

    public async Task<DiagnosticsDto> GetDiagnosticsAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var staleAfterMinutes = Math.Max(1, configuration?.GetValue("Diagnostics:HealthStaleAfterMinutes", 30) ?? 30);
        var staleAfter = TimeSpan.FromMinutes(staleAfterMinutes);
        var modules = dbContext is null ? DemoStore.Modules.ToArray() : await dbContext.IntegrationModules.AsNoTracking().ToArrayAsync(cancellationToken);
        var approvals = dbContext is null ? DemoStore.Approvals.ToArray() : await dbContext.ApprovalRequests.AsNoTracking().ToArrayAsync(cancellationToken);
        var logs = dbContext is null ? DemoStore.SystemLogs.ToArray() : await dbContext.SystemLogs.AsNoTracking().OrderByDescending(item => item.CreatedAt).Take(50).ToArrayAsync(cancellationToken);
        var webhooks = dbContext is null ? DemoStore.Webhooks.Count : await dbContext.WebhookEvents.CountAsync(cancellationToken);
        var settings = dbContext is null ? DemoStore.Settings.ToArray() : await dbContext.IntegrationSettings.AsNoTracking().ToArrayAsync(cancellationToken);
        var actions = dbContext is null ? DemoStore.Actions.ToArray() : await dbContext.IntegrationModuleActions.AsNoTracking().ToArrayAsync(cancellationToken);
        var healthChecks = dbContext is null
            ? DemoStore.HealthChecks.ToArray()
            : await dbContext.IntegrationHealthChecks.AsNoTracking().OrderByDescending(item => item.CheckedAt).ToArrayAsync(cancellationToken);
        var enabledModules = modules.Where(module => module.IsEnabled).ToArray();
        var moduleById = modules.ToDictionary(module => module.Id);
        var latestHealth = healthChecks
            .GroupBy(item => item.ModuleId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.CheckedAt).First());
        var latestEnabledHealth = enabledModules
            .Where(module => latestHealth.ContainsKey(module.Id))
            .Select(module => latestHealth[module.Id])
            .ToArray();
        var staleHealthChecks = enabledModules.Count(module =>
            latestHealth.TryGetValue(module.Id, out var health)
            && now - health.CheckedAt > staleAfter);
        DateTimeOffset? lastDiagnosticsRunAt = latestEnabledHealth.Length == 0
            ? null
            : latestEnabledHealth.Max(health => health.CheckedAt);
        var diagnosticsFreshness = lastDiagnosticsRunAt is null
            ? "never"
            : staleHealthChecks > 0 ? "stale" : "fresh";
        var licenseStatus = _licenseService?.GetStatus();

        var warnings = new List<string>();
        warnings.AddRange(enabledModules.Where(module => module.UseFallbackMode).Select(module => $"fallback mode enabled: {module.Key}"));
        warnings.AddRange(enabledModules.Where(module => !module.SafeMode).Select(module => $"SafeMode disabled: {module.Key}"));
        warnings.AddRange(enabledModules.Where(module => module.HealthStatus is HealthStatus.Error or HealthStatus.Warning or HealthStatus.NotConfigured).Select(module => $"module health requires attention: {module.Key}={module.HealthStatus}"));
        warnings.AddRange(enabledModules.Where(module => module.HealthStatus is HealthStatus.Unknown && !latestHealth.ContainsKey(module.Id)).Select(module => $"module has no real health check yet: {module.Key}"));
        warnings.AddRange(enabledModules
            .Where(module => latestHealth.TryGetValue(module.Id, out var health) && now - health.CheckedAt > staleAfter)
            .Select(module => $"module health check is stale: {module.Key}={latestHealth[module.Id].CheckedAt:O}"));
        warnings.AddRange(settings
            .Where(setting => setting.IsRequired && string.IsNullOrWhiteSpace(setting.Value) && moduleById.TryGetValue(setting.ModuleId, out var module) && module.IsEnabled)
            .Select(setting => $"missing required setting: {moduleById[setting.ModuleId].Key}.{setting.Key}"));
        if (licenseStatus is { IsValid: false })
        {
            warnings.Add($"license invalid: {licenseStatus.Message}");
        }

        var components = new List<string>
        {
            $"databaseConnectivity={(dbContext is null ? "demo-store" : "ok")}",
            "diagnosticsEvidence=real-adapter-health",
            $"diagnosticsFreshness={diagnosticsFreshness}",
            $"lastDiagnosticsRunAt={(lastDiagnosticsRunAt is null ? "never" : lastDiagnosticsRunAt.Value.ToString("O"))}",
            $"healthStaleAfterMinutes={staleAfterMinutes}",
            $"enabledModules={enabledModules.Length}/{modules.Length}",
            $"healthyModules={enabledModules.Count(module => module.HealthStatus is HealthStatus.Healthy)}",
            $"warningModules={enabledModules.Count(module => module.HealthStatus is HealthStatus.Warning)}",
            $"errorModules={enabledModules.Count(module => module.HealthStatus is HealthStatus.Error)}",
            $"notConfiguredModules={enabledModules.Count(module => module.HealthStatus is HealthStatus.NotConfigured)}",
            $"unknownModules={enabledModules.Count(module => module.HealthStatus is HealthStatus.Unknown)}",
            $"remediationActions={actions.Length}",
            $"enabledRemediationActions={actions.Count(action => action.IsEnabled)}",
            $"modulesWithRemediationActions={actions.Select(action => action.ModuleId).Distinct().Count()}/{modules.Length}",
            $"lastHealthChecks={latestHealth.Count}",
            $"modulesWithoutHealthCheck={enabledModules.Count(module => !latestHealth.ContainsKey(module.Id))}",
            $"staleHealthChecks={staleHealthChecks}",
            $"pendingApprovals={approvals.Count(approval => approval.Status == ApprovalStatus.Pending)}",
            $"recentErrors={logs.Count(log => log.Level.Equals("Error", StringComparison.OrdinalIgnoreCase))}",
            $"pollingModules={enabledModules.Count(module => module.SupportsPolling)}",
            $"webhookEvents={webhooks}",
            $"secretProtection={(_secretProtector is NoOpSecretProtector ? "disabled" : "encrypted")}"
        };
        if (licenseStatus is not null)
        {
            components.Add($"licenseStatus={licenseStatus.Status}:{licenseStatus.Edition}");
        }

        components.AddRange(enabledModules
            .Where(module => module.HealthStatus is HealthStatus.Warning or HealthStatus.Error or HealthStatus.NotConfigured or HealthStatus.Unknown)
            .Select(module => $"diagnosticRemediation={module.Key}:{SuggestedDiagnosticsActionKey(module.Key)}"));
        components.AddRange(warnings.Select(warning => $"diagnosticWarning={warning}"));

        var status = licenseStatus is { IsValid: false }
            ? "Blocked"
            : warnings.Count == 0 && enabledModules.All(module => module.HealthStatus == HealthStatus.Healthy)
                ? "Healthy"
                : "Warnings";

        return new DiagnosticsDto(status, DateTimeOffset.UtcNow, components);
    }

    public async Task<OperationResultDto> RunDiagnosticsAsync(string actor, string? correlationId, CancellationToken cancellationToken)
    {
        var modules = dbContext is null
            ? DemoStore.Modules.Where(module => module.IsEnabled).OrderBy(module => module.Key).ToArray()
            : await dbContext.IntegrationModules.Where(module => module.IsEnabled).OrderBy(module => module.Key).ToArrayAsync(cancellationToken);

        var healthy = 0;
        var attention = 0;

        foreach (var module in modules)
        {
            IntegrationHealthResult health;
            try
            {
                health = await adapterFactory.GetAdapter(module.Key).CheckHealthAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                health = new IntegrationHealthResult(module.Key, HealthStatus.Error, ex.Message);
            }

            module.HealthStatus = health.Status;
            module.LastHealthCheckAt = DateTimeOffset.UtcNow;
            module.UpdatedAt = DateTimeOffset.UtcNow;
            AddHealthCheck(new IntegrationHealthCheck
            {
                ModuleId = module.Id,
                Status = health.Status,
                LatencyMs = health.LatencyMs,
                Message = health.Message,
                DetailsJson = health.DetailsJson
            });
            await AuditAsync(actor, "MODULE_HEALTH_CHECK", module.Key, health.Status == HealthStatus.Healthy ? "Success" : health.Status.ToString(), correlationId, cancellationToken);

            if (health.Status == HealthStatus.Healthy)
            {
                healthy++;
            }
            else
            {
                attention++;
            }
        }

        await AuditAsync(actor, "DIAGNOSTICS_RUN", "system", attention == 0 ? "Healthy" : "Warnings", correlationId, cancellationToken);
        await SaveAsync(cancellationToken);
        return new OperationResultDto(
            attention == 0,
            $"Diagnostics ran {modules.Length} real adapter checks. Healthy: {healthy}; attention: {attention}.",
            correlationId);
    }

    public Task<SupportBundleDto> GetSupportBundleAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new SupportBundleDto(DateTimeOffset.UtcNow, "sysassist-support-bundle.zip", ["incidents", "audit", "systemLogs", "integrations"]));

    public async Task<SupportBundleFileDto> GetSupportBundleFileAsync(string actor, string? correlationId, CancellationToken cancellationToken)
    {
        var generatedAt = DateTimeOffset.UtcNow;
        var diagnostics = await GetDiagnosticsAsync(cancellationToken);
        var modules = dbContext is null ? DemoStore.Modules.Select(ToModuleDto).ToArray() : await dbContext.IntegrationModules.AsNoTracking().Select(item => ToModuleDto(item)).ToArrayAsync(cancellationToken);
        var health = dbContext is null ? DemoStore.HealthChecks.ToArray() : await dbContext.IntegrationHealthChecks.AsNoTracking().OrderByDescending(item => item.CheckedAt).Take(200).ToArrayAsync(cancellationToken);
        var events = await ListEventsAsync(cancellationToken);
        var recommendations = dbContext is null ? DemoStore.Recommendations.ToArray() : await dbContext.EventRecommendations.AsNoTracking().Take(200).ToArrayAsync(cancellationToken);
        var approvals = await ListApprovalsAsync(cancellationToken);
        var actions = await ListActionsAsync(cancellationToken);
        var audit = await ListAuditAsync(cancellationToken);
        var logs = await ListLogsAsync(cancellationToken);
        var notifications = await ListNotificationsAsync(cancellationToken);
        var users = await ListUsersAsync(cancellationToken);
        var roles = await ListRolesAsync(cancellationToken);
        var license = _licenseService?.GetStatus();

        var bundle = new
        {
            generatedAt,
            product = "SysAssist",
            version = "0.6.0",
            databaseProvider = "CockroachDB",
            license,
            modules = modules.Where(module => module.IsEnabled),
            healthHistory = health.Select(item => new { item.ModuleId, item.CheckedAt, status = item.Status.ToString(), item.LatencyMs, item.Message }),
            incidents = events,
            recommendations = recommendations.Select(item => new { item.Id, item.EventId, item.Classification, item.Explanation, item.Confidence, item.ProbableCause, item.NextStep, item.SuggestedActionId }),
            approvals,
            actions,
            audit,
            logs,
            notifications,
            users,
            roles,
            diagnostics
        };

        var fileName = $"sysassist-support-bundle-{generatedAt:yyyyMMdd-HHmmss}.json";
        var json = JsonSerializer.Serialize(bundle, new JsonSerializerOptions { WriteIndented = true });
        AddSupportBundleExport(actor, fileName, """["license","modules","health","incidents","recommendations","approvals","actions","audit","logs","notifications","users","roles","diagnostics"]""");
        await AuditAsync(actor, "SUPPORT_BUNDLE_EXPORTED", fileName, "Success", correlationId, cancellationToken);
        await SaveAsync(cancellationToken);
        return new SupportBundleFileDto(fileName, json);
    }

    public async Task<OperationResultDto> ProcessWebhookAsync(string moduleKey, string rawPayload, string? signature, string actor, string? correlationId, CancellationToken cancellationToken)
    {
        var module = dbContext is null
            ? DemoStore.Modules.SingleOrDefault(item => item.Key.Equals(moduleKey, StringComparison.OrdinalIgnoreCase))
            : await dbContext.IntegrationModules.SingleOrDefaultAsync(item => item.Key == moduleKey, cancellationToken);
        if (module is null)
        {
            return NotFound("Webhook module not found.", correlationId);
        }

        var signatureValid = await ValidateWebhookSignatureAsync(module.Id, signature, cancellationToken);
        var webhook = new WebhookEvent
        {
            ModuleId = module.Id,
            Source = module.Key,
            PayloadJson = rawPayload,
            SignatureValid = signatureValid,
            ProcessingStatus = signatureValid ? "Processed" : "Rejected",
            ErrorMessage = signatureValid ? null : "Invalid webhook signature"
        };
        AddWebhook(webhook);

        if (!signatureValid)
        {
            await WriteSystemLogAsync("Warning", "Webhooks", "Webhook rejected because signature validation failed.", correlationId, $$"""{"module":"{{module.Key}}"}""", cancellationToken);
            await AuditAsync(actor, "WEBHOOK_REJECTED", module.Key, "Rejected", correlationId, cancellationToken);
            await SaveAsync(cancellationToken);
            return new OperationResultDto(false, "Webhook signature is invalid.", correlationId);
        }

        var incoming = NormalizeWebhook(module.Key, rawPayload, correlationId);
        await ProcessIncomingEventAsync(module, incoming, actor, correlationId, cancellationToken);

        await AuditAsync(actor, "WEBHOOK_PROCESSED", module.Key, "Success", correlationId, cancellationToken);
        await SaveAsync(cancellationToken);
        return Ok("Webhook processed and normalized.", correlationId);
    }

    private async Task<OperationResultDto> TouchModuleHealthAsync(Guid id, string actor, string action, string message, string? correlationId, CancellationToken cancellationToken)
    {
        var module = await FindModuleAsync(id, cancellationToken);
        if (module is null)
        {
            return NotFound("Module not found.", correlationId);
        }

        var adapter = adapterFactory.GetAdapter(module.Key);
        var health = await adapter.CheckHealthAsync(cancellationToken);
        module.HealthStatus = health.Status;
        module.LastHealthCheckAt = DateTimeOffset.UtcNow;
        module.UpdatedAt = DateTimeOffset.UtcNow;
        AddHealthCheck(new IntegrationHealthCheck { ModuleId = module.Id, Status = health.Status, LatencyMs = health.LatencyMs, Message = health.Message, DetailsJson = health.DetailsJson });
        await AuditAsync(actor, "MODULE_HEALTH_CHECK", module.Key, health.Status == HealthStatus.Healthy ? "Success" : health.Status.ToString(), correlationId, cancellationToken);
        await SaveAsync(cancellationToken);

        var succeeded = health.Status is HealthStatus.Healthy;
        var resultMessage = succeeded
            ? message
            : action == "modules.test_connection"
                ? "Connection test failed."
                : "Module health check failed.";
        return new OperationResultDto(succeeded, $"{resultMessage} Adapter: {health.Message}", correlationId);
    }

    private async Task EnsureModuleSettingsAsync(IntegrationModule module, CancellationToken cancellationToken)
    {
        var definition = ModuleCatalog.Find(module.Key);
        if (definition is null)
        {
            return;
        }

        var settings = dbContext is null
            ? DemoStore.Settings.Where(item => item.ModuleId == module.Id).ToList()
            : await dbContext.IntegrationSettings.Where(item => item.ModuleId == module.Id).ToListAsync(cancellationToken);

        foreach (var settingDefinition in definition.Settings)
        {
            if (settings.Any(setting => setting.Key.Equals(settingDefinition.Key, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var value = settingDefinition.Key switch
            {
                "Enabled" => module.IsEnabled.ToString().ToLowerInvariant(),
                "UseFallbackMode" => module.UseFallbackMode.ToString().ToLowerInvariant(),
                "SafeMode" => module.SafeMode.ToString().ToLowerInvariant(),
                "Description" => module.Description,
                _ => settingDefinition.DefaultValue
            };
            var storedValue = settingDefinition.IsSecret ? _secretProtector.Protect(value) : value;

            var setting = new IntegrationSetting
            {
                ModuleId = module.Id,
                Key = settingDefinition.Key,
                Value = storedValue,
                IsSecret = settingDefinition.IsSecret,
                IsRequired = settingDefinition.IsRequiredInRealMode,
                ValueType = settingDefinition.ValueType,
                Description = settingDefinition.Description
            };

            if (dbContext is null)
            {
                DemoStore.Settings.Add(setting);
            }
            else
            {
                dbContext.IntegrationSettings.Add(setting);
            }
        }

        await SaveAsync(cancellationToken);
    }

    private async Task SetSyntheticSettingValueAsync(Guid moduleId, string key, string value, CancellationToken cancellationToken)
    {
        var setting = dbContext is null
            ? DemoStore.Settings.SingleOrDefault(item => item.ModuleId == moduleId && item.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
            : await dbContext.IntegrationSettings.SingleOrDefaultAsync(item => item.ModuleId == moduleId && item.Key == key, cancellationToken);

        if (setting is not null)
        {
            setting.Value = value;
            setting.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    private async Task<OperationResultDto> ValidateModuleCanEnableAsync(IntegrationModule module, CancellationToken cancellationToken)
    {
        await EnsureModuleSettingsAsync(module, cancellationToken);

        if (module.UseFallbackMode)
        {
            return new OperationResultDto(false, "Fallback mode is not a real connection. Disable fallback mode and configure live settings.");
        }

        var settings = dbContext is null
            ? DemoStore.Settings.Where(item => item.ModuleId == module.Id).ToArray()
            : await dbContext.IntegrationSettings.AsNoTracking().Where(item => item.ModuleId == module.Id).ToArrayAsync(cancellationToken);
        var missing = settings
            .Where(setting => setting.IsRequired && string.IsNullOrWhiteSpace(setting.Value))
            .Select(setting => setting.Key)
            .ToArray();

        return missing.Length == 0
            ? Ok("Module can be enabled.", null)
            : new OperationResultDto(false, $"Required settings missing for real mode: {string.Join(", ", missing)}");
    }

    private static void ApplyModuleSetting(IntegrationModule module, string key, string? value)
    {
        switch (key.ToLowerInvariant())
        {
            case "enabled":
                if (bool.TryParse(value, out var enabled))
                {
                    module.IsEnabled = enabled;
                    module.HealthStatus = enabled ? HealthStatus.Unknown : HealthStatus.Disabled;
                }
                break;
            case "usefallbackmode":
                module.UseFallbackMode = false;
                break;
            case "safemode":
                if (bool.TryParse(value, out var safeMode))
                {
                    module.SafeMode = safeMode;
                }
                break;
            case "description":
                module.Description = value;
                break;
        }
    }

    private static bool? TryGetBoolUpdate(UpdateModuleSettingsRequest request, string key)
    {
        var value = request.Settings.FirstOrDefault(item => item.Key.Equals(key, StringComparison.OrdinalIgnoreCase))?.Value;
        return bool.TryParse(value, out var parsed) ? parsed : null;
    }

    private static string? UpdatedValue(UpdateModuleSettingsRequest request, IntegrationSetting setting)
    {
        var update = request.Settings.FirstOrDefault(item => item.Key.Equals(setting.Key, StringComparison.OrdinalIgnoreCase));
        return update is null || (setting.IsSecret && update.Value == "********") ? setting.Value : update.Value;
    }

    private async Task AuditAsync(string actor, string action, string resource, string result, string? correlationId, CancellationToken cancellationToken)
    {
        var entry = new AuditEntry { Actor = actor, Action = action, Resource = resource, Result = result, CorrelationId = correlationId };
        if (dbContext is null)
        {
            DemoStore.AuditEntries.Add(entry);
        }
        else
        {
            dbContext.AuditEntries.Add(entry);
        }

        await Task.CompletedTask;
    }

    private IncidentEvent AddIncidentFromIncoming(IntegrationModule module, IncomingEventDto incoming)
    {
        var incident = new IncidentEvent
        {
            ExternalEventId = incoming.ExternalEventId,
            Source = incoming.Source,
            EventType = incoming.EventType,
            Severity = incoming.Severity,
            Target = incoming.Target,
            Status = incoming.Severity == EventSeverity.Critical ? EventStatus.PendingApproval : EventStatus.New,
            CorrelationId = incoming.CorrelationId,
            Summary = incoming.Summary,
            PayloadJson = incoming.PayloadJson,
            ModuleId = module.Id
        };

        if (dbContext is null)
        {
            DemoStore.Events.Add(incident);
        }
        else
        {
            dbContext.IncidentEvents.Add(incident);
        }

        return incident;
    }

    private async Task<IncidentEvent> ProcessIncomingEventAsync(IntegrationModule module, IncomingEventDto incoming, string actor, string? correlationId, CancellationToken cancellationToken)
    {
        await AuditAsync(actor, "EVENT_RECEIVED", module.Key, "Received", correlationId, cancellationToken);
        var incident = AddIncidentFromIncoming(module, incoming);
        await AuditAsync(actor, "EVENT_NORMALIZED", incident.Id.ToString(), "Success", correlationId, cancellationToken);

        var advisor = adapterFactory.GetAdapter("local-rule-advisor") as LocalRuleAdvisorAdapter;
        var advisorResult = advisor?.Recommend(incoming);
        var action = await FindSuggestedActionAsync(module, incoming, cancellationToken);
        var recommendation = new EventRecommendation
        {
            EventId = incident.Id,
            Classification = incoming.EventType,
            Explanation = advisorResult?.Message ?? "Local rules recommend collecting diagnostics.",
            Confidence = incoming.Severity == EventSeverity.Critical ? 0.91m : 0.72m,
            ProbableCause = incoming.Summary,
            NextStep = action is null ? "Review event manually." : action.RequiresApproval ? "Request approval for suggested action." : "Run diagnostic action.",
            SuggestedActionId = action?.Id
        };
        AddRecommendation(recommendation);
        incident.RecommendationId = recommendation.Id;
        await AuditAsync(actor, "RECOMMENDATION_CREATED", recommendation.Id.ToString(), "Success", correlationId, cancellationToken);

        var requiresApproval = action is not null
            && (action.RequiresApproval || action.RiskLevel is RiskLevel.High or RiskLevel.Critical || incoming.Severity is EventSeverity.Error or EventSeverity.Critical);
        if (requiresApproval && action is not null)
        {
            incident.Status = EventStatus.PendingApproval;
            AddApproval(new ApprovalRequest
            {
                EventId = incident.Id,
                ActionId = action.Id,
                Status = ApprovalStatus.Pending
            });
            AddNotification("local", "SeniorAdmin", "Approval required", $"{incident.Summary} requires approval.", NotificationStatus.LocalOnly, incident.Id);
            await AuditAsync(actor, "APPROVAL_REQUESTED", incident.Id.ToString(), "Pending", correlationId, cancellationToken);
        }
        else if (action is not null)
        {
            incident.Status = EventStatus.PendingApproval;
            AddApproval(new ApprovalRequest
            {
                EventId = incident.Id,
                ActionId = action.Id,
                Status = ApprovalStatus.Pending
            });
            AddNotification("local", "SeniorAdmin", "Approval required", $"{incident.Summary} requires a real action decision.", NotificationStatus.LocalOnly, incident.Id);
            await AuditAsync(actor, "APPROVAL_REQUESTED", incident.Id.ToString(), "Pending", correlationId, cancellationToken);
        }

        await WriteSystemLogAsync("Information", "Events", "Incoming event normalized through workflow.", correlationId, $$"""{"eventId":"{{incident.Id}}","module":"{{module.Key}}","severity":"{{incoming.Severity}}"}""", cancellationToken);
        return incident;
    }

    private async Task<IntegrationModuleAction?> FindSuggestedActionAsync(IntegrationModule module, IncomingEventDto incoming, CancellationToken cancellationToken)
    {
        var actions = dbContext is null
            ? DemoStore.Actions.Where(item => item.ModuleId == module.Id && item.IsEnabled).ToArray()
            : await dbContext.IntegrationModuleActions.Where(item => item.ModuleId == module.Id && item.IsEnabled).ToArrayAsync(cancellationToken);

        return actions
            .OrderByDescending(action => ScoreSuggestedAction(module.Key, action.ActionKey, incoming))
            .ThenByDescending(action => incoming.Severity is EventSeverity.Error or EventSeverity.Critical ? (int)action.RiskLevel : -(int)action.RiskLevel)
            .ThenBy(action => action.ActionKey)
            .FirstOrDefault();
    }

    private static int ScoreSuggestedAction(string moduleKey, string actionKey, IncomingEventDto incoming)
    {
        var definition = RemediationActionCatalog.Find(moduleKey, actionKey);
        var text = $"{incoming.EventType} {incoming.Target} {incoming.Summary} {incoming.PayloadJson}".ToLowerInvariant();
        var score = actionKey.Equals("collect_diagnostics", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        if (definition is not null)
        {
            score += definition.Symptoms.Count(symptom => text.Contains(symptom.ToLowerInvariant(), StringComparison.Ordinal));
        }

        if (incoming.Severity is EventSeverity.Error or EventSeverity.Critical && definition?.RiskLevel is RiskLevel.High or RiskLevel.Critical)
        {
            score++;
        }

        return score;
    }

    private static string SuggestedDiagnosticsActionKey(string moduleKey) =>
        moduleKey switch
        {
            "zabbix" => "collect_diagnostics",
            "grafana" => "add_incident_annotation",
            "prometheus-alertmanager" => "verify_alert_route",
            "postgresql" => "terminate_idle_in_transaction",
            "redis" => "purge_expired_memory",
            "docker" => "restart_container",
            "nginx" => "test_nginx_config",
            "linux-host" => "restart_systemd_service",
            "http-endpoint" => "retry_endpoint_probe",
            "file-system" => "clean_old_files",
            "smtp-email" => "send_escalation_digest",
            "telegram-bot" => "send_oncall_page",
            "local-rule-advisor" => "generate_runbook",
            _ => "collect_diagnostics"
        };

    private void AddRecommendation(EventRecommendation recommendation)
    {
        if (dbContext is null)
        {
            DemoStore.Recommendations.Add(recommendation);
        }
        else
        {
            dbContext.EventRecommendations.Add(recommendation);
        }
    }

    private void AddNotification(string channel, string recipient, string subject, string body, NotificationStatus status, Guid? relatedEventId)
    {
        var notification = new NotificationMessage
        {
            Channel = channel,
            Recipient = recipient,
            Subject = subject,
            Body = body,
            Status = status,
            RelatedEventId = relatedEventId
        };

        if (dbContext is null)
        {
            DemoStore.Notifications.Add(notification);
        }
        else
        {
            dbContext.NotificationMessages.Add(notification);
        }
    }

    private void AddSupportBundleExport(string requestedBy, string fileName, string sectionsJson)
    {
        var export = new SupportBundleExport
        {
            RequestedBy = requestedBy,
            FileName = fileName,
            IncludedSectionsJson = sectionsJson
        };

        if (dbContext is null)
        {
            DemoStore.SupportBundles.Add(export);
        }
        else
        {
            dbContext.SupportBundleExports.Add(export);
        }
    }

    private void AddWebhook(WebhookEvent webhook)
    {
        if (dbContext is null)
        {
            DemoStore.Webhooks.Add(webhook);
        }
        else
        {
            dbContext.WebhookEvents.Add(webhook);
        }
    }

    private void AddApproval(ApprovalRequest approval)
    {
        if (dbContext is null)
        {
            DemoStore.Approvals.Add(approval);
        }
        else
        {
            dbContext.ApprovalRequests.Add(approval);
        }
    }

    private void AddHealthCheck(IntegrationHealthCheck healthCheck)
    {
        if (dbContext is null)
        {
            DemoStore.HealthChecks.Add(healthCheck);
        }
        else
        {
            dbContext.IntegrationHealthChecks.Add(healthCheck);
        }
    }

    private async Task<bool> ValidateWebhookSignatureAsync(Guid moduleId, string? signature, CancellationToken cancellationToken)
    {
        var secret = dbContext is null
            ? DemoStore.Settings.SingleOrDefault(item => item.ModuleId == moduleId && item.Key.Contains("WebhookSecret", StringComparison.OrdinalIgnoreCase))?.Value
            : await dbContext.IntegrationSettings
                .Where(item => item.ModuleId == moduleId && item.Key == "WebhookSecret")
                .Select(item => item.Value)
                .SingleOrDefaultAsync(cancellationToken);

        secret = _secretProtector.Unprotect(secret);
        return string.IsNullOrWhiteSpace(secret) || string.Equals(secret, signature, StringComparison.Ordinal);
    }

    private static IncomingEventDto NormalizeWebhook(string moduleKey, string rawPayload, string? correlationId)
    {
        var severity = rawPayload.Contains("critical", StringComparison.OrdinalIgnoreCase)
            ? EventSeverity.Critical
            : rawPayload.Contains("error", StringComparison.OrdinalIgnoreCase) || rawPayload.Contains("firing", StringComparison.OrdinalIgnoreCase)
                ? EventSeverity.Error
                : EventSeverity.Warning;
        var eventType = moduleKey switch
        {
            "grafana" => "grafana.webhook_alert",
            "prometheus-alertmanager" => "alertmanager.webhook_alert",
            "zabbix" => "zabbix.webhook_event",
            _ => "webhook.event"
        };

        return new IncomingEventDto(moduleKey, $"webhook-{Guid.CreateVersion7()}", eventType, severity, moduleKey, $"{moduleKey} webhook event", rawPayload, correlationId);
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (dbContext is not null)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<IncidentEvent?> FindEventAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext is null
            ? DemoStore.Events.SingleOrDefault(item => item.Id == id)
            : await dbContext.IncidentEvents.Include(item => item.Recommendation).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    private async Task<ApprovalRequest?> FindApprovalAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext is null
            ? DemoStore.Approvals.SingleOrDefault(item => item.Id == id)
            : await dbContext.ApprovalRequests.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    private async Task<IntegrationModule?> FindModuleAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext is null
            ? DemoStore.Modules.SingleOrDefault(item => item.Id == id)
            : await dbContext.IntegrationModules.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    private async Task<IntegrationModuleAction?> FindActionAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext is null
            ? DemoStore.Actions.SingleOrDefault(item => item.Id == id)
            : await dbContext.IntegrationModuleActions.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    private async Task<User?> FindUserAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext is null
            ? DemoStore.Users.SingleOrDefault(item => item.Id == id)
            : await dbContext.Users.Include(item => item.UserRoles).ThenInclude(item => item.Role).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    private static OperationResultDto Ok(string message, string? correlationId) => new(true, message, correlationId);
    private static OperationResultDto NotFound(string message, string? correlationId) => new(false, message, correlationId);
    private static EventDto ToEventDto(IncidentEvent item)
    {
        var recommendation = item.Recommendation ?? DemoStore.Recommendations.FirstOrDefault(candidate => candidate.EventId == item.Id);
        var recommendationDto = recommendation is null
            ? null
            : new EventRecommendationDto(
                recommendation.Id,
                recommendation.Classification,
                recommendation.Explanation,
                recommendation.Confidence,
                recommendation.ProbableCause,
                recommendation.NextStep,
                recommendation.SuggestedActionId);

        return new EventDto(
            item.Id,
            item.ExternalEventId,
            item.CreatedAt,
            item.Source,
            item.EventType,
            item.Severity.ToString(),
            item.Target,
            item.Status.ToString(),
            item.CorrelationId,
            item.Summary,
            item.ModuleId,
            item.PayloadJson,
            recommendationDto);
    }
    private static bool IsConnectedModule(IntegrationModule module) =>
        module.IsEnabled && module.HealthStatus == HealthStatus.Healthy;

    private static ApprovalDto ToApprovalDto(ApprovalRequest item) => new(item.Id, item.EventId, item.ActionId, item.Status.ToString(), item.RequestedAt, item.RequestedByUserId, item.DecidedAt, item.DecidedByUserId, item.DecisionComment);
    private static ModuleDto ToModuleDto(IntegrationModule item) => new(item.Id, item.Key, item.Name, item.Type.ToString(), item.Description, item.IsEnabled, item.HealthStatus.ToString(), item.LastHealthCheckAt, item.LastFetchAt, item.SupportsPolling, item.SupportsWebhooks, item.SupportsActions, item.UseFallbackMode, item.SafeMode);
    private static ModuleSettingDto ToSettingDto(IntegrationSetting item)
    {
        var hasValue = !string.IsNullOrWhiteSpace(item.Value);
        return new ModuleSettingDto(item.Id, item.Key, item.IsSecret && hasValue ? "********" : item.Value, item.IsSecret, item.IsRequired, item.ValueType, item.Description, hasValue);
    }
    private static ModuleActionDto ToActionDto(IntegrationModuleAction item) => new(item.Id, item.ModuleId, item.ActionKey, item.Name, item.Description, item.RiskLevel.ToString(), item.RequiresApproval, item.IsEnabled);
    private static AuditEntryDto ToAuditDto(AuditEntry item) => new(item.Id, item.CreatedAt, item.Actor, item.Action, item.Resource, item.Result, item.CorrelationId);
    private static SystemLogDto ToLogDto(SystemLog item) => new(item.Id, item.CreatedAt, item.Level, item.Component, item.Message, item.CorrelationId, item.DetailsJson);
    private static NotificationDto ToNotificationDto(NotificationMessage item) => new(item.Id, item.CreatedAt, item.Channel, item.Recipient, item.Subject, item.Status.ToString(), item.RelatedEventId);
    private static UserDto ToUserDto(User item) => new(item.Id, item.Login, item.DisplayName, item.Email, item.IsActive, item.UserRoles.Select(role => role.Role?.Name).Where(role => role is not null).Select(role => role!).ToArray());
}

internal static class DemoStore
{
    public static readonly List<Role> Roles =
    [
        new() { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Name = "Admin", Description = "Full platform administration." },
        new() { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Name = "Operator", Description = "Dashboard and event operations." },
        new() { Id = Guid.Parse("33333333-3333-3333-3333-333333333333"), Name = "Engineer", Description = "Integrations and diagnostics." },
        new() { Id = Guid.Parse("44444444-4444-4444-4444-444444444444"), Name = "SeniorAdmin", Description = "High-risk approvals." },
        new() { Id = Guid.Parse("55555555-5555-5555-5555-555555555555"), Name = "Auditor", Description = "Audit read-only." }
    ];

    public static readonly List<User> Users = [];
    public static readonly List<IntegrationModule> Modules = BuildModules();
    public static readonly List<IntegrationSetting> Settings = Modules
        .SelectMany(module => (ModuleCatalog.Find(module.Key)?.Settings ?? [])
            .Select(setting => new IntegrationSetting
            {
                ModuleId = module.Id,
                Key = setting.Key,
                Value = setting.Key switch
                {
                    "Enabled" => module.IsEnabled.ToString().ToLowerInvariant(),
                    "UseFallbackMode" => module.UseFallbackMode.ToString().ToLowerInvariant(),
                    "SafeMode" => module.SafeMode.ToString().ToLowerInvariant(),
                    "Description" => module.Description,
                    _ => setting.DefaultValue
                },
                IsSecret = setting.IsSecret,
                IsRequired = setting.IsRequiredInRealMode,
                ValueType = setting.ValueType,
                Description = setting.Description
            }))
        .ToList();
    public static readonly List<IntegrationModuleAction> Actions = BuildActions();
    public static readonly List<IncidentEvent> Events = [];
    public static readonly List<ApprovalRequest> Approvals = [];
    public static readonly List<EventRecommendation> Recommendations = [];
    public static readonly List<IntegrationHealthCheck> HealthChecks = [];
    public static readonly List<WebhookEvent> Webhooks = [];
    public static readonly List<SupportBundleExport> SupportBundles = [];
    public static readonly List<AuditEntry> AuditEntries = [];
    public static readonly List<SystemLog> SystemLogs = [];
    public static readonly List<NotificationMessage> Notifications = [];

    private static List<IntegrationModule> BuildModules()
    {
        return ModuleCatalog.Modules.Select(definition => new IntegrationModule
        {
            Id = Guid.CreateVersion7(),
            Key = definition.Key,
            Name = definition.Name,
            Type = definition.Type,
            Description = definition.Description,
            IsEnabled = true,
            HealthStatus = HealthStatus.NotConfigured,
            SupportsPolling = definition.SupportsPolling,
            SupportsWebhooks = definition.SupportsWebhooks,
            SupportsActions = definition.SupportsActions,
            UseFallbackMode = false,
            SafeMode = true
        }).ToList();
    }

    private static List<IntegrationModuleAction> BuildActions()
    {
        return Modules
            .SelectMany(module => RemediationActionCatalog.ForModule(module.Key).Select(definition => new IntegrationModuleAction
            {
                ModuleId = module.Id,
                ActionKey = definition.ActionKey,
                Name = definition.Name,
                Description = definition.Description,
                RiskLevel = definition.RiskLevel,
                RequiresApproval = definition.RequiresApproval,
                IsEnabled = true,
                ParameterSchemaJson = definition.ParameterSchemaJson
            }))
            .ToList();
    }
}
