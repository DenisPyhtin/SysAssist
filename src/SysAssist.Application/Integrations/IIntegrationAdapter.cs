namespace SysAssist.Application.Integrations;

public interface IIntegrationAdapter
{
    string ModuleKey { get; }
    Task<IntegrationHealthResult> CheckHealthAsync(CancellationToken ct);
    Task<IReadOnlyList<IncomingEventDto>> FetchEventsAsync(CancellationToken ct);
    Task<ActionExecutionResult> ExecuteActionAsync(ActionExecutionRequest request, CancellationToken ct);
}
