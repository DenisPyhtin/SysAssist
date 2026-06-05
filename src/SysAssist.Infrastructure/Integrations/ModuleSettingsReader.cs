using Microsoft.EntityFrameworkCore;
using SysAssist.Application.Security;
using SysAssist.Application.Integrations;
using SysAssist.Infrastructure.Data;
using SysAssist.Infrastructure.Modules;
using SysAssist.Infrastructure.Security;
using SysAssist.Infrastructure.Services;

namespace SysAssist.Infrastructure.Integrations;

public sealed class ModuleSettingsReader(SysAssistDbContext? dbContext = null, ISecretProtector? secretProtector = null) : IModuleSettingsReader
{
    private readonly ISecretProtector _secretProtector = secretProtector ?? NoOpSecretProtector.Instance;

    public async Task<AdapterModuleState> GetStateAsync(string moduleKey, CancellationToken cancellationToken)
    {
        if (dbContext is not null)
        {
            var module = await dbContext.IntegrationModules
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Key == moduleKey, cancellationToken);
            var settingRows = await dbContext.IntegrationSettings
                .AsNoTracking()
                .Where(item => item.Module != null && item.Module.Key == moduleKey)
                .ToArrayAsync(cancellationToken);
            var settings = settingRows.ToDictionary(
                item => item.Key,
                item => item.IsSecret ? _secretProtector.Unprotect(item.Value) : item.Value,
                StringComparer.OrdinalIgnoreCase);

            return module is null
                ? MissingState(moduleKey)
                : new AdapterModuleState(module.Key, module.IsEnabled, module.UseFallbackMode, module.SafeMode, settings);
        }

        var demoModule = DemoStore.Modules.SingleOrDefault(item => item.Key.Equals(moduleKey, StringComparison.OrdinalIgnoreCase));
        if (demoModule is null)
        {
            return MissingState(moduleKey);
        }

        var demoSettings = DemoStore.Settings
            .Where(item => item.ModuleId == demoModule.Id)
            .ToDictionary(
                item => item.Key,
                item => item.IsSecret ? _secretProtector.Unprotect(item.Value) : item.Value,
                StringComparer.OrdinalIgnoreCase);
        return new AdapterModuleState(demoModule.Key, demoModule.IsEnabled, demoModule.UseFallbackMode, demoModule.SafeMode, demoSettings);
    }

    private static AdapterModuleState MissingState(string moduleKey)
    {
        var definition = ModuleCatalog.Find(moduleKey);
        var settings = definition?.Settings.ToDictionary(item => item.Key, item => item.DefaultValue, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        return new AdapterModuleState(moduleKey, false, false, true, settings);
    }
}
