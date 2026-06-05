using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SysAssist.Application.Security;
using SysAssist.Domain.Entities;
using SysAssist.Domain.Enums;
using SysAssist.Infrastructure.Auth;
using SysAssist.Infrastructure.Modules;
using SysAssist.Infrastructure.Security;

namespace SysAssist.Infrastructure.Data;

public static class SysAssistDbSeeder
{
    public static async Task SeedAsync(
        SysAssistDbContext dbContext,
        IConfiguration? configuration = null,
        ISecretProtector? secretProtector = null,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        secretProtector ??= NoOpSecretProtector.Instance;
        var forceEnableModules = configuration?.GetValue("SysAssist:ForceEnableModulesOnStartup", false) ?? false;
        var resetBootstrapAdminPassword = configuration?.GetValue("SysAssist:ResetBootstrapAdminPasswordOnStartup", false) ?? false;
        var bootstrapAdminPassword = BootstrapAdminPassword(configuration);

        var roles = new[]
        {
            ("Admin", "Full platform administration."),
            ("Operator", "Daily incident operations."),
            ("Engineer", "Technical remediation and diagnostics."),
            ("SeniorAdmin", "High-risk approvals and privileged actions."),
            ("Auditor", "Read-only audit and evidence review.")
        };

        foreach (var (name, description) in roles)
        {
            if (!await dbContext.Roles.AnyAsync(role => role.Name == name, cancellationToken))
            {
                dbContext.Roles.Add(new Role { Name = name, Description = description, CreatedAt = now });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var adminRole = await dbContext.Roles.SingleAsync(role => role.Name == "Admin", cancellationToken);
        var admin = await dbContext.Users.SingleOrDefaultAsync(user => user.Login == "admin", cancellationToken);
        var passwordHasher = new PasswordHasher();
        if (admin is null)
        {
            EnsureStrongBootstrapAdminPassword(bootstrapAdminPassword);
            admin = new User
            {
                Login = "admin",
                DisplayName = "SysAssist Admin",
                Email = "admin@sysassist.local",
                PasswordHash = passwordHasher.Hash(bootstrapAdminPassword!),
                IsActive = true,
                CreatedAt = now
            };
            dbContext.Users.Add(admin);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        else if (resetBootstrapAdminPassword)
        {
            EnsureStrongBootstrapAdminPassword(bootstrapAdminPassword);
            admin.PasswordHash = passwordHasher.Hash(bootstrapAdminPassword!);
            admin.IsActive = true;
            admin.UpdatedAt = now;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (!await dbContext.UserRoles.AnyAsync(userRole => userRole.UserId == admin.Id && userRole.RoleId == adminRole.Id, cancellationToken))
        {
            dbContext.UserRoles.Add(new UserRole { UserId = admin.Id, RoleId = adminRole.Id });
        }

        foreach (var module in BuildModules(now))
        {
            var existing = await dbContext.IntegrationModules.SingleOrDefaultAsync(item => item.Key == module.Key, cancellationToken);
            if (existing is null)
            {
                dbContext.IntegrationModules.Add(module);
                continue;
            }

            existing.Name = module.Name;
            existing.Type = module.Type;
            existing.Description = module.Description;
            existing.SupportsPolling = module.SupportsPolling;
            existing.SupportsWebhooks = module.SupportsWebhooks;
            existing.SupportsActions = module.SupportsActions;
            existing.UseFallbackMode = false;
            if (forceEnableModules)
            {
                existing.IsEnabled = true;
                existing.HealthStatus = existing.LastHealthCheckAt is null ? HealthStatus.Unknown : existing.HealthStatus;
            }

            existing.SafeMode = true;
            existing.UpdatedAt = now;
            if (forceEnableModules && existing.HealthStatus == HealthStatus.Disabled)
            {
                existing.HealthStatus = HealthStatus.Unknown;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var modules = await dbContext.IntegrationModules.ToDictionaryAsync(module => module.Key, cancellationToken);
        var existingSettings = (await dbContext.IntegrationSettings
                .ToArrayAsync(cancellationToken))
            .ToDictionary(setting => (setting.ModuleId, setting.Key), StringTupleComparer.OrdinalIgnoreCase);
        var existingActions = (await dbContext.IntegrationModuleActions
                .ToArrayAsync(cancellationToken))
            .ToDictionary(action => (action.ModuleId, action.ActionKey), StringTupleComparer.OrdinalIgnoreCase);
        var existingActionKeys = existingActions
            .Select(action => action.Key)
            .ToHashSet(StringTupleComparer.OrdinalIgnoreCase);

        foreach (var module in modules.Values)
        {
            SeedSettings(dbContext, module, now, existingSettings, configuration, secretProtector);
            SeedActions(dbContext, module, now, existingActions, existingActionKeys);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await CleanupSeedOperationsAsync(dbContext, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static IReadOnlyCollection<IntegrationModule> BuildModules(DateTimeOffset now) =>
        ModuleCatalog.Modules.Select(module => Module(module, now)).ToArray();

    private static IntegrationModule Module(ModuleDefinition definition, DateTimeOffset now) =>
        new()
        {
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
            SafeMode = true,
            CreatedAt = now
        };

    private static void SeedSettings(
        SysAssistDbContext dbContext,
        IntegrationModule module,
        DateTimeOffset now,
        Dictionary<(Guid ModuleId, string Key), IntegrationSetting> existingSettings,
        IConfiguration? configuration,
        ISecretProtector secretProtector)
    {
        var definition = ModuleCatalog.Find(module.Key);
        var settings = definition?.Settings ?? [];

        foreach (var settingDefinition in settings)
        {
            var configuredValue = ModuleConfiguration.GetConfiguredValue(configuration, module.Key, settingDefinition.Key);
            var defaultValue = settingDefinition.Key switch
            {
                "Enabled" => module.IsEnabled.ToString().ToLowerInvariant(),
                "UseFallbackMode" => "false",
                "SafeMode" => module.SafeMode.ToString().ToLowerInvariant(),
                "Description" => module.Description,
                _ => settingDefinition.DefaultValue
            };
            var value = configuredValue ?? defaultValue;
            var storedValue = settingDefinition.IsSecret ? secretProtector.Protect(value) : value;

            if (existingSettings.TryGetValue((module.Id, settingDefinition.Key), out var existing))
            {
                existing.IsSecret = settingDefinition.IsSecret;
                existing.IsRequired = settingDefinition.IsRequiredInRealMode;
                existing.ValueType = settingDefinition.ValueType;
                existing.Description = settingDefinition.Description;

                if (configuredValue is not null || settingDefinition.Key is "Enabled" or "UseFallbackMode" or "SafeMode" or "Description")
                {
                    existing.Value = storedValue;
                    existing.UpdatedAt = now;
                }
                else if (existing.IsSecret && !string.IsNullOrWhiteSpace(existing.Value) && !secretProtector.IsProtected(existing.Value))
                {
                    existing.Value = secretProtector.Protect(existing.Value);
                    existing.UpdatedAt = now;
                }

                continue;
            }

            var setting = new IntegrationSetting
            {
                ModuleId = module.Id,
                Key = settingDefinition.Key,
                Value = storedValue,
                IsSecret = settingDefinition.IsSecret,
                IsRequired = settingDefinition.IsRequiredInRealMode,
                ValueType = settingDefinition.ValueType,
                Description = settingDefinition.Description,
                CreatedAt = now,
                UpdatedAt = now
            };
            dbContext.IntegrationSettings.Add(setting);
            existingSettings[(module.Id, settingDefinition.Key)] = setting;
        }
    }

    private static void SeedActions(
        SysAssistDbContext dbContext,
        IntegrationModule module,
        DateTimeOffset now,
        Dictionary<(Guid ModuleId, string Key), IntegrationModuleAction> existingActions,
        HashSet<(Guid ModuleId, string Key)> existingActionKeys)
    {
        foreach (var definition in RemediationActionCatalog.ForModule(module.Key))
        {
            if (existingActions.TryGetValue((module.Id, definition.ActionKey), out var existing))
            {
                existing.Name = definition.Name;
                existing.Description = definition.Description;
                existing.RiskLevel = definition.RiskLevel;
                existing.RequiresApproval = definition.RequiresApproval;
                existing.ParameterSchemaJson = definition.ParameterSchemaJson;
                existing.UpdatedAt = now;
                continue;
            }

            var action = new IntegrationModuleAction
            {
                ModuleId = module.Id,
                ActionKey = definition.ActionKey,
                Name = definition.Name,
                Description = definition.Description,
                RiskLevel = definition.RiskLevel,
                RequiresApproval = definition.RequiresApproval,
                IsEnabled = true,
                ParameterSchemaJson = definition.ParameterSchemaJson,
                CreatedAt = now,
                UpdatedAt = now
            };
            dbContext.IntegrationModuleActions.Add(action);
            existingActions[(module.Id, definition.ActionKey)] = action;
            existingActionKeys.Add((module.Id, definition.ActionKey));
        }

        var catalogKeys = RemediationActionCatalog.ForModule(module.Key)
            .Select(action => action.ActionKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var legacy in existingActions
            .Where(item => item.Key.ModuleId == module.Id && !catalogKeys.Contains(item.Key.Key))
            .Select(item => item.Value))
        {
            legacy.IsEnabled = false;
            legacy.Description = $"{legacy.Description} Legacy action disabled because it is not part of the remediation catalog.";
            legacy.UpdatedAt = now;
        }
    }

    private static async Task CleanupSeedOperationsAsync(SysAssistDbContext dbContext, CancellationToken cancellationToken)
    {
        var seedExternalIds = new[] { "prom-001", "zbx-8102", "gfn-4421", "redis-127", "ngx-204" };
        var seedCorrelations = new[] { "seed-001", "seed-002", "demo-corr-001", "demo-corr-002", "demo-corr-003", "demo-corr-004", "demo-corr-005" };
        var seedEvents = await dbContext.IncidentEvents
            .Where(incident => seedExternalIds.Contains(incident.ExternalEventId!))
            .ToArrayAsync(cancellationToken);
        var seedEventIds = seedEvents.Select(incident => incident.Id).ToArray();

        if (seedEventIds.Length > 0)
        {
            dbContext.ApprovalRequests.RemoveRange(await dbContext.ApprovalRequests.Where(item => seedEventIds.Contains(item.EventId)).ToArrayAsync(cancellationToken));
            dbContext.EventRecommendations.RemoveRange(await dbContext.EventRecommendations.Where(item => seedEventIds.Contains(item.EventId)).ToArrayAsync(cancellationToken));
            dbContext.NotificationMessages.RemoveRange(await dbContext.NotificationMessages.Where(item => item.RelatedEventId != null && seedEventIds.Contains(item.RelatedEventId.Value)).ToArrayAsync(cancellationToken));
            dbContext.IncidentEvents.RemoveRange(seedEvents);
        }

        dbContext.NotificationMessages.RemoveRange(await dbContext.NotificationMessages
            .Where(item =>
                item.Subject == "Approval required" && item.Body.Contains("Critical CPU saturation")
                || item.Subject == "Live configuration required"
                || item.Subject == "Action blocked" && item.RelatedEventId == null)
            .ToArrayAsync(cancellationToken));
        dbContext.SystemLogs.RemoveRange(await dbContext.SystemLogs
            .Where(item => seedCorrelations.Contains(item.CorrelationId!) || item.Component == "Seeder" || item.Message.Contains("Critical event routed to approval queue"))
            .ToArrayAsync(cancellationToken));
        dbContext.AuditEntries.RemoveRange(await dbContext.AuditEntries
            .Where(item => seedCorrelations.Contains(item.CorrelationId!) || item.Action == "seed.database" || item.Action == "integration.enable")
            .ToArrayAsync(cancellationToken));
        dbContext.SupportBundleExports.RemoveRange(await dbContext.SupportBundleExports
            .Where(item => item.FileName == "sysassist-support-bundle.zip")
            .ToArrayAsync(cancellationToken));
    }

    private static string? BootstrapAdminPassword(IConfiguration? configuration)
    {
        var password = configuration?["SysAssist:BootstrapAdminPassword"]
            ?? configuration?["SYSASSIST_BOOTSTRAP_ADMIN_PASSWORD"];
        return string.IsNullOrWhiteSpace(password) ? null : password;
    }

    private static void EnsureStrongBootstrapAdminPassword(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("SysAssist:BootstrapAdminPassword is required to create the initial admin user.");
        }

        if (!IsStrongBootstrapAdminPassword(password) || LooksLikePlaceholder(password))
        {
            throw new InvalidOperationException("SysAssist:BootstrapAdminPassword must be a real strong secret with at least 12 characters, uppercase, lowercase, digit, and symbol.");
        }
    }

    private static bool IsStrongBootstrapAdminPassword(string password) =>
        password.Length >= 12
        && password.Any(char.IsUpper)
        && password.Any(char.IsLower)
        && password.Any(char.IsDigit)
        && password.Any(ch => !char.IsLetterOrDigit(ch));

    private static bool LooksLikePlaceholder(string value) =>
        value.Contains("replace", StringComparison.OrdinalIgnoreCase)
        || value.Contains("change-me", StringComparison.OrdinalIgnoreCase)
        || value.Contains("changeme", StringComparison.OrdinalIgnoreCase);

    private sealed class StringTupleComparer : IEqualityComparer<(Guid ModuleId, string Key)>
    {
        public static readonly StringTupleComparer OrdinalIgnoreCase = new();

        public bool Equals((Guid ModuleId, string Key) x, (Guid ModuleId, string Key) y) =>
            x.ModuleId == y.ModuleId && string.Equals(x.Key, y.Key, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((Guid ModuleId, string Key) obj) =>
            HashCode.Combine(obj.ModuleId, StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Key));
    }
}
