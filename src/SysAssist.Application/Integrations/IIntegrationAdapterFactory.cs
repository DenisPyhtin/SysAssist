namespace SysAssist.Application.Integrations;

public interface IIntegrationAdapterFactory
{
    IIntegrationAdapter GetAdapter(string moduleKey);
}
