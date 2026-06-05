using SysAssist.Application.Integrations;

namespace SysAssist.Infrastructure.Integrations;

public interface IModuleSettingsReader
{
    Task<AdapterModuleState> GetStateAsync(string moduleKey, CancellationToken cancellationToken);
}
