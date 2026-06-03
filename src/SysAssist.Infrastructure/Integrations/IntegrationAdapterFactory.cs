using SysAssist.Application.Integrations;

namespace SysAssist.Infrastructure.Integrations;

public sealed class IntegrationAdapterFactory(IEnumerable<IIntegrationAdapter> adapters) : IIntegrationAdapterFactory
{
    private readonly Dictionary<string, IIntegrationAdapter> _adapters = adapters
        .ToDictionary(adapter => adapter.ModuleKey, StringComparer.OrdinalIgnoreCase);

    public IIntegrationAdapter GetAdapter(string moduleKey)
    {
        if (_adapters.TryGetValue(moduleKey, out var adapter))
        {
            return adapter;
        }

        throw new InvalidOperationException($"Integration adapter '{moduleKey}' is not registered.");
    }
}
