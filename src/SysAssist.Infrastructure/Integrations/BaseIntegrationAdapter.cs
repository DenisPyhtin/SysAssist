using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Mail;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Npgsql;
using SysAssist.Application.Integrations;
using SysAssist.Domain.Enums;

namespace SysAssist.Infrastructure.Integrations;

public abstract class BaseIntegrationAdapter(IModuleSettingsReader settingsReader) : IIntegrationAdapter
{
    protected IModuleSettingsReader SettingsReader { get; } = settingsReader;
    public abstract string ModuleKey { get; }

    public async Task<IntegrationHealthResult> CheckHealthAsync(CancellationToken ct)
    {
        var state = await SettingsReader.GetStateAsync(ModuleKey, ct);
        if (!state.IsEnabled)
        {
            return new IntegrationHealthResult(ModuleKey, HealthStatus.Disabled, "Module is disabled.");
        }

        if (state.UseFallbackMode)
        {
            return new IntegrationHealthResult(ModuleKey, HealthStatus.NotConfigured, "Fallback mode is not a real connection. Disable fallback mode and configure live settings.");
        }

        if (!HasRealConfiguration(state))
        {
            return new IntegrationHealthResult(ModuleKey, HealthStatus.NotConfigured, "Required real-mode settings are missing.");
        }

        try
        {
            return await CheckRealHealthAsync(state, ct);
        }
        catch (Exception ex)
        {
            return new IntegrationHealthResult(ModuleKey, HealthStatus.Error, ex.Message);
        }
    }

    public async Task<IReadOnlyList<IncomingEventDto>> FetchEventsAsync(CancellationToken ct)
    {
        var state = await SettingsReader.GetStateAsync(ModuleKey, ct);
        if (!state.IsEnabled)
        {
            return [];
        }

        if (state.UseFallbackMode)
        {
            return [NotConfiguredEvent("Fallback mode is not allowed for real event fetch.")];
        }

        if (!HasRealConfiguration(state))
        {
            return [NotConfiguredEvent()];
        }

        try
        {
            return await FetchRealEventsAsync(state, ct);
        }
        catch (Exception ex)
        {
            return [new IncomingEventDto(ModuleKey, $"error-{Guid.CreateVersion7()}", "adapter.error", EventSeverity.Error, ModuleKey, ex.Message, $$"""{"adapter":"{{ModuleKey}}"}""")];
        }
    }

    public async Task<ActionExecutionResult> ExecuteActionAsync(ActionExecutionRequest request, CancellationToken ct)
    {
        var state = await SettingsReader.GetStateAsync(ModuleKey, ct);
        if (!state.IsEnabled)
        {
            return new ActionExecutionResult(false, "Module is disabled.");
        }

        if (IsDestructive(request.ActionKey) && (state.SafeMode || !request.ApprovalGranted))
        {
            return new ActionExecutionResult(false, "Action requires approval and SafeMode=false.", RequiresApproval: true);
        }

        if (state.UseFallbackMode)
        {
            return new ActionExecutionResult(false, "Fallback mode is not a real action executor. Disable fallback mode and configure live settings.");
        }

        if (!HasRealConfiguration(state))
        {
            return new ActionExecutionResult(false, "Module is not configured for real-mode action execution.");
        }

        try
        {
            return await ExecuteRealActionAsync(state, request, ct);
        }
        catch (Exception ex)
        {
            return new ActionExecutionResult(false, ex.Message);
        }
    }

    protected virtual bool HasRealConfiguration(AdapterModuleState state) => true;

    protected virtual Task<IntegrationHealthResult> CheckRealHealthAsync(AdapterModuleState state, CancellationToken ct) =>
        Task.FromResult(new IntegrationHealthResult(ModuleKey, HealthStatus.NotConfigured, "No real health check is implemented for this module."));

    protected virtual Task<IReadOnlyList<IncomingEventDto>> FetchRealEventsAsync(AdapterModuleState state, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<IncomingEventDto>>([]);

    protected virtual Task<ActionExecutionResult> ExecuteRealActionAsync(AdapterModuleState state, ActionExecutionRequest request, CancellationToken ct) =>
        Task.FromResult(new ActionExecutionResult(false, $"No real executor is implemented for action '{request.ActionKey}'."));

    protected virtual bool IsDestructive(string actionKey) =>
        actionKey.Contains("restart", StringComparison.OrdinalIgnoreCase)
        || actionKey.Contains("stop", StringComparison.OrdinalIgnoreCase)
        || actionKey.Contains("terminate", StringComparison.OrdinalIgnoreCase)
        || actionKey.Contains("flush", StringComparison.OrdinalIgnoreCase)
        || actionKey.Contains("reload", StringComparison.OrdinalIgnoreCase)
        || actionKey.Contains("archive", StringComparison.OrdinalIgnoreCase);

    protected static string? Setting(AdapterModuleState state, string key) =>
        state.Settings.TryGetValue(key, out var value) ? value : null;

    protected static bool Has(AdapterModuleState state, params string[] keys) =>
        keys.All(key => !string.IsNullOrWhiteSpace(Setting(state, key)));

    protected static bool BoolSetting(AdapterModuleState state, string key, bool defaultValue = false) =>
        bool.TryParse(Setting(state, key), out var parsed) ? parsed : defaultValue;

    protected static int IntSetting(AdapterModuleState state, string key, int defaultValue) =>
        int.TryParse(Setting(state, key), out var parsed) ? parsed : defaultValue;

    protected static string? JsonParameter(ActionExecutionRequest request, params string[] keys)
    {
        if (string.IsNullOrWhiteSpace(request.ParametersJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(request.ParametersJson);
            if (document.RootElement.ValueKind is not JsonValueKind.Object)
            {
                return null;
            }

            foreach (var key in keys)
            {
                if (document.RootElement.TryGetProperty(key, out var value))
                {
                    return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
                }
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    protected static bool BoolParameter(ActionExecutionRequest request, string key, bool defaultValue = false)
    {
        var value = JsonParameter(request, key);
        return bool.TryParse(value, out var parsed) ? parsed : defaultValue;
    }

    protected static int IntParameter(ActionExecutionRequest request, string key, int defaultValue)
    {
        var value = JsonParameter(request, key);
        return int.TryParse(value, out var parsed) ? parsed : defaultValue;
    }

    protected static string? ActionTarget(ActionExecutionRequest request, AdapterModuleState state, params string[] fallbackSettingKeys)
    {
        var target = request.Target ?? JsonParameter(request, "target", "id", "key", "name", "path", "url", "service", "container", "table", "index", "pid");
        if (!string.IsNullOrWhiteSpace(target))
        {
            return target.Trim();
        }

        foreach (var key in fallbackSettingKeys)
        {
            var value = Setting(state, key);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    protected static string MessageParameter(ActionExecutionRequest request, string fallback) =>
        JsonParameter(request, "message", "body", "text", "reason", "comment") ?? fallback;

    protected static ActionExecutionResult JsonSuccess(string message, params (string Key, object? Value)[] values) =>
        new(true, message, ResultJson: JsonSerializer.Serialize(values.ToDictionary(item => item.Key, item => item.Value)));

    protected static ActionExecutionResult JsonFailure(string message, params (string Key, object? Value)[] values) =>
        new(false, message, ResultJson: values.Length == 0 ? null : JsonSerializer.Serialize(values.ToDictionary(item => item.Key, item => item.Value)));

    protected static async Task<ActionExecutionResult> RunShellCommandAsync(string? command, string actionName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return new ActionExecutionResult(false, $"{actionName} command is not configured.");
        }

        var startInfo = OperatingSystem.IsWindows()
            ? new ProcessStartInfo("cmd.exe", $"/c {command}")
            : new ProcessStartInfo("/bin/sh", $"-lc \"{command.Replace("\"", "\\\"")}\"");
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return new ActionExecutionResult(false, $"{actionName} command could not be started.");
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        var stdout = Shorten(await stdoutTask);
        var stderr = Shorten(await stderrTask);
        return process.ExitCode == 0
            ? JsonSuccess($"{actionName} completed.", ("exitCode", process.ExitCode), ("stdout", stdout))
            : JsonFailure($"{actionName} failed with exit code {process.ExitCode}.", ("exitCode", process.ExitCode), ("stdout", stdout), ("stderr", stderr));
    }

    protected static string Shorten(string? value, int maxLength = 1600)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : $"{trimmed[..maxLength]}...";
    }

    protected static string[] SplitList(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    protected async Task<IntegrationHealthResult> TcpHealthAsync(string host, int port, string service, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        using var client = new TcpClient();
        await client.ConnectAsync(host, port, ct);
        sw.Stop();
        return new IntegrationHealthResult(ModuleKey, HealthStatus.Healthy, $"{service} TCP {host}:{port} reachable.", (int)sw.ElapsedMilliseconds);
    }

    private IncomingEventDto NotConfiguredEvent(string? message = null) =>
        new(ModuleKey, $"not-configured-{ModuleKey}", "adapter.not_configured", EventSeverity.Warning, ModuleKey, message ?? $"{ModuleKey} is not configured for real mode.");
}

public abstract class HttpIntegrationAdapter(IModuleSettingsReader settingsReader, IHttpClientFactory httpClientFactory) : BaseIntegrationAdapter(settingsReader)
{
    protected HttpClient HttpClient { get; } = httpClientFactory.CreateClient("SysAssistIntegrations");

    protected async Task<IntegrationHealthResult> GetHealthAsync(string url, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        using var response = await HttpClient.GetAsync(url, ct);
        sw.Stop();
        return new IntegrationHealthResult(ModuleKey, response.IsSuccessStatusCode ? HealthStatus.Healthy : HealthStatus.Warning, $"HTTP {(int)response.StatusCode} {response.StatusCode}", (int)sw.ElapsedMilliseconds);
    }

    protected override Task<ActionExecutionResult> ExecuteRealActionAsync(AdapterModuleState state, ActionExecutionRequest request, CancellationToken ct) =>
        ExecuteWebhookActionAsync(state, request, ct);

    protected async Task<ActionExecutionResult> ExecuteWebhookActionAsync(AdapterModuleState state, ActionExecutionRequest request, CancellationToken ct)
    {
        var webhookUrl = Setting(state, "RemediationWebhookUrl");
        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            return new ActionExecutionResult(false, $"No direct executor or RemediationWebhookUrl is configured for action '{request.ActionKey}'.");
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, webhookUrl)
        {
            Content = JsonContent.Create(new
            {
                moduleKey = ModuleKey,
                actionKey = request.ActionKey,
                target = request.Target,
                parametersJson = request.ParametersJson,
                requestedAt = DateTimeOffset.UtcNow
            })
        };
        var token = Setting(state, "RemediationWebhookToken");
        if (!string.IsNullOrWhiteSpace(token))
        {
            message.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        using var response = await HttpClient.SendAsync(message, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        return response.IsSuccessStatusCode
            ? JsonSuccess("Remediation webhook accepted the action.", ("httpStatus", (int)response.StatusCode), ("body", Shorten(body)))
            : JsonFailure($"Remediation webhook rejected the action: HTTP {(int)response.StatusCode} {response.StatusCode}.", ("body", Shorten(body)));
    }
}

public sealed class ZabbixIntegrationAdapter(IModuleSettingsReader settingsReader, IHttpClientFactory httpClientFactory) : HttpIntegrationAdapter(settingsReader, httpClientFactory)
{
    public override string ModuleKey => "zabbix";
    protected override bool HasRealConfiguration(AdapterModuleState state)
    {
        var baseUrl = Setting(state, "BaseUrl");
        return Has(state, "BaseUrl")
            && ((Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) && uri.Port == 10051) || Has(state, "ApiToken") || Has(state, "Username", "Password"));
    }
    protected override Task<IntegrationHealthResult> CheckRealHealthAsync(AdapterModuleState state, CancellationToken ct)
    {
        var baseUrl = Setting(state, "BaseUrl")!;
        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) && uri.Port == 10051)
        {
            return TcpHealthAsync(uri.Host, uri.Port, "Zabbix server", ct);
        }

        return GetHealthAsync($"{baseUrl.TrimEnd('/')}/api_jsonrpc.php", ct);
    }

    protected override async Task<ActionExecutionResult> ExecuteRealActionAsync(AdapterModuleState state, ActionExecutionRequest request, CancellationToken ct)
    {
        return request.ActionKey switch
        {
            "collect_diagnostics" => await CollectZabbixDiagnosticsAsync(state, ct),
            "acknowledge_problem" => await AcknowledgeEventAsync(state, request, actionMask: 6, "Zabbix problem acknowledged.", ct),
            "suppress_flapping_trigger" => await AcknowledgeEventAsync(state, request, actionMask: 6, "Zabbix flapping trigger suppressed by acknowledgement.", ct),
            "close_resolved_problem" => await AcknowledgeEventAsync(state, request, actionMask: 1, "Zabbix problem close request submitted.", ct),
            "create_maintenance_window" => await CreateMaintenanceWindowAsync(state, request, ct),
            _ => await base.ExecuteRealActionAsync(state, request, ct)
        };
    }

    private async Task<ActionExecutionResult> AcknowledgeEventAsync(AdapterModuleState state, ActionExecutionRequest request, int actionMask, string successMessage, CancellationToken ct)
    {
        var eventId = ActionTarget(request, state);
        if (string.IsNullOrWhiteSpace(eventId))
        {
            return new ActionExecutionResult(false, "Zabbix event id target is required.");
        }

        var message = MessageParameter(request, $"SysAssist remediation action: {request.ActionKey}");
        return await ZabbixApiActionAsync(state, "event.acknowledge", new { eventids = new[] { eventId }, action = actionMask, message }, successMessage, ct);
    }

    private async Task<ActionExecutionResult> CreateMaintenanceWindowAsync(AdapterModuleState state, ActionExecutionRequest request, CancellationToken ct)
    {
        var hostId = ActionTarget(request, state);
        if (string.IsNullOrWhiteSpace(hostId))
        {
            return new ActionExecutionResult(false, "Zabbix host id target is required for maintenance creation.");
        }

        var durationSeconds = Math.Max(300, IntParameter(request, "durationSeconds", 1800));
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var name = JsonParameter(request, "name") ?? $"SysAssist remediation {DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
        return await ZabbixApiActionAsync(
            state,
            "maintenance.create",
            new
            {
                name,
                active_since = now,
                active_till = now + durationSeconds,
                hostids = new[] { hostId },
                timeperiods = new[] { new { timeperiod_type = 0, start_date = now, period = durationSeconds } },
                description = MessageParameter(request, "Temporary maintenance created by SysAssist remediation.")
            },
            "Zabbix maintenance window created.",
            ct);
    }

    private async Task<ActionExecutionResult> CollectZabbixDiagnosticsAsync(AdapterModuleState state, CancellationToken ct)
    {
        if (ZabbixApiUrl(state) is not null)
        {
            return await ZabbixApiActionAsync(state, "apiinfo.version", new { }, "Zabbix diagnostics collected.", ct, requiresAuth: false);
        }

        var baseUrl = Setting(state, "BaseUrl");
        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) && uri.Port == 10051)
        {
            var health = await TcpHealthAsync(uri.Host, uri.Port, "Zabbix server", ct);
            return health.Status is HealthStatus.Healthy or HealthStatus.Warning
                ? JsonSuccess("Zabbix diagnostics collected.", ("transport", "tcp"), ("status", health.Status.ToString()), ("message", health.Message), ("latencyMs", health.LatencyMs))
                : JsonFailure("Zabbix diagnostics failed.", ("transport", "tcp"), ("status", health.Status.ToString()), ("message", health.Message));
        }

        return new ActionExecutionResult(false, "Zabbix BaseUrl must be either API web endpoint or TCP server endpoint.");
    }

    private async Task<ActionExecutionResult> ZabbixApiActionAsync(AdapterModuleState state, string method, object parameters, string successMessage, CancellationToken ct, bool requiresAuth = true)
    {
        var apiUrl = ZabbixApiUrl(state);
        if (apiUrl is null)
        {
            return new ActionExecutionResult(false, "Zabbix API URL is required for this action. BaseUrl must point to the web/API endpoint, not only TCP 10051.");
        }

        var auth = requiresAuth ? await ZabbixAuthAsync(state, apiUrl, ct) : null;
        if (requiresAuth && string.IsNullOrWhiteSpace(auth))
        {
            return new ActionExecutionResult(false, "Zabbix ApiToken or Username/Password is required for this action.");
        }

        using var response = await HttpClient.PostAsJsonAsync(apiUrl, new
        {
            jsonrpc = "2.0",
            method,
            @params = parameters,
            auth,
            id = 1
        }, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode || body.Contains("\"error\"", StringComparison.OrdinalIgnoreCase))
        {
            return JsonFailure($"Zabbix API action {method} failed.", ("httpStatus", (int)response.StatusCode), ("body", Shorten(body)));
        }

        return JsonSuccess(successMessage, ("method", method), ("body", Shorten(body)));
    }

    private async Task<string?> ZabbixAuthAsync(AdapterModuleState state, string apiUrl, CancellationToken ct)
    {
        var token = Setting(state, "ApiToken");
        if (!string.IsNullOrWhiteSpace(token))
        {
            return token;
        }

        if (!Has(state, "Username", "Password"))
        {
            return null;
        }

        using var response = await HttpClient.PostAsJsonAsync(apiUrl, new
        {
            jsonrpc = "2.0",
            method = "user.login",
            @params = new { username = Setting(state, "Username"), password = Setting(state, "Password") },
            id = 1
        }, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        using var document = JsonDocument.Parse(body);
        return document.RootElement.TryGetProperty("result", out var result) ? result.GetString() : null;
    }

    private static string? ZabbixApiUrl(AdapterModuleState state)
    {
        var baseUrl = Setting(state, "BaseUrl");
        if (string.IsNullOrWhiteSpace(baseUrl) || !Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) || uri.Port == 10051)
        {
            return null;
        }

        return baseUrl.EndsWith("api_jsonrpc.php", StringComparison.OrdinalIgnoreCase)
            ? baseUrl
            : $"{baseUrl.TrimEnd('/')}/api_jsonrpc.php";
    }
}

public sealed class GrafanaIntegrationAdapter(IModuleSettingsReader settingsReader, IHttpClientFactory httpClientFactory) : HttpIntegrationAdapter(settingsReader, httpClientFactory)
{
    public override string ModuleKey => "grafana";
    protected override bool HasRealConfiguration(AdapterModuleState state) => Has(state, "BaseUrl");
    protected override Task<IntegrationHealthResult> CheckRealHealthAsync(AdapterModuleState state, CancellationToken ct) => GetHealthAsync($"{Setting(state, "BaseUrl")!.TrimEnd('/')}/api/health", ct);

    protected override async Task<ActionExecutionResult> ExecuteRealActionAsync(AdapterModuleState state, ActionExecutionRequest request, CancellationToken ct)
    {
        if (request.ActionKey == "collect_diagnostics")
        {
            var health = await CheckRealHealthAsync(state, ct);
            return JsonSuccess("Grafana diagnostics collected.", ("status", health.Status.ToString()), ("message", health.Message));
        }

        if (request.ActionKey == "add_incident_annotation")
        {
            var baseUrl = Setting(state, "BaseUrl")!.TrimEnd('/');
            using var message = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/annotations")
            {
                Content = JsonContent.Create(new
                {
                    time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    tags = new[] { "sysassist", request.ActionKey },
                    text = MessageParameter(request, $"SysAssist remediation for {request.Target ?? ModuleKey}")
                })
            };
            AddBearer(message, Setting(state, "ApiToken"));
            using var response = await HttpClient.SendAsync(message, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            return response.IsSuccessStatusCode
                ? JsonSuccess("Grafana incident annotation created.", ("httpStatus", (int)response.StatusCode), ("body", Shorten(body)))
                : JsonFailure($"Grafana annotation failed: HTTP {(int)response.StatusCode} {response.StatusCode}.", ("body", Shorten(body)));
        }

        return await ExecuteWebhookActionAsync(state, request, ct);
    }

    private static void AddBearer(HttpRequestMessage message, string? token)
    {
        if (!string.IsNullOrWhiteSpace(token))
        {
            message.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }
    }
}

public sealed class AlertmanagerIntegrationAdapter(IModuleSettingsReader settingsReader, IHttpClientFactory httpClientFactory) : HttpIntegrationAdapter(settingsReader, httpClientFactory)
{
    public override string ModuleKey => "prometheus-alertmanager";
    protected override bool HasRealConfiguration(AdapterModuleState state) => Has(state, "AlertmanagerBaseUrl");
    protected override async Task<IntegrationHealthResult> CheckRealHealthAsync(AdapterModuleState state, CancellationToken ct)
    {
        var alertmanager = await GetHealthAsync($"{Setting(state, "AlertmanagerBaseUrl")!.TrimEnd('/')}/-/ready", ct);
        var prometheusBaseUrl = NormalizePrometheusBaseUrl(Setting(state, "PrometheusBaseUrl"));
        if (string.IsNullOrWhiteSpace(prometheusBaseUrl))
        {
            return alertmanager;
        }

        var prometheus = await GetHealthAsync($"{prometheusBaseUrl}/-/ready", ct);
        var healthy = alertmanager.Status == HealthStatus.Healthy && prometheus.Status == HealthStatus.Healthy;
        var status = healthy ? HealthStatus.Healthy : HealthStatus.Warning;
        return new IntegrationHealthResult(
            ModuleKey,
            status,
            $"Alertmanager: {alertmanager.Message}; Prometheus: {prometheus.Message}",
            new[] { alertmanager.LatencyMs, prometheus.LatencyMs }.Where(item => item.HasValue).Sum(),
            $$"""{"alertmanager":"{{alertmanager.Status}}","prometheus":"{{prometheus.Status}}"}""");
    }

    private static string? NormalizePrometheusBaseUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim().TrimEnd('/');
        var apiIndex = trimmed.IndexOf("/api/v1", StringComparison.OrdinalIgnoreCase);
        return apiIndex >= 0 ? trimmed[..apiIndex] : trimmed;
    }

    protected override async Task<ActionExecutionResult> ExecuteRealActionAsync(AdapterModuleState state, ActionExecutionRequest request, CancellationToken ct)
    {
        return request.ActionKey switch
        {
            "collect_diagnostics" or "verify_alert_route" => await VerifyAlertmanagerAsync(state, ct),
            "create_alert_silence" => await CreateSilenceAsync(state, request, ct),
            "expire_alert_silence" => await ExpireSilenceAsync(state, request, ct),
            "reload_prometheus_config" => await ReloadPrometheusAsync(state, ct),
            _ => await base.ExecuteRealActionAsync(state, request, ct)
        };
    }

    private async Task<ActionExecutionResult> VerifyAlertmanagerAsync(AdapterModuleState state, CancellationToken ct)
    {
        using var response = await HttpClient.GetAsync($"{Setting(state, "AlertmanagerBaseUrl")!.TrimEnd('/')}/api/v2/status", ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        return response.IsSuccessStatusCode
            ? JsonSuccess("Alertmanager route/status verified.", ("httpStatus", (int)response.StatusCode), ("body", Shorten(body)))
            : JsonFailure($"Alertmanager status failed: HTTP {(int)response.StatusCode} {response.StatusCode}.", ("body", Shorten(body)));
    }

    private async Task<ActionExecutionResult> CreateSilenceAsync(AdapterModuleState state, ActionExecutionRequest request, CancellationToken ct)
    {
        var target = ActionTarget(request, state, "ReceiverName") ?? "sysassist";
        var durationMinutes = Math.Clamp(IntParameter(request, "durationMinutes", 30), 5, 1440);
        var now = DateTimeOffset.UtcNow;
        var payload = new
        {
            matchers = new[] { new { name = JsonParameter(request, "label") ?? "instance", value = target, isRegex = false } },
            startsAt = now,
            endsAt = now.AddMinutes(durationMinutes),
            createdBy = Setting(state, "SilenceAuthor") ?? "SysAssist",
            comment = MessageParameter(request, $"SysAssist silence for {target}")
        };
        using var response = await HttpClient.PostAsJsonAsync($"{Setting(state, "AlertmanagerBaseUrl")!.TrimEnd('/')}/api/v2/silences", payload, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        return response.IsSuccessStatusCode
            ? JsonSuccess("Alertmanager silence created.", ("target", target), ("durationMinutes", durationMinutes), ("body", Shorten(body)))
            : JsonFailure($"Alertmanager silence failed: HTTP {(int)response.StatusCode} {response.StatusCode}.", ("body", Shorten(body)));
    }

    private async Task<ActionExecutionResult> ExpireSilenceAsync(AdapterModuleState state, ActionExecutionRequest request, CancellationToken ct)
    {
        var silenceId = ActionTarget(request, state);
        if (string.IsNullOrWhiteSpace(silenceId))
        {
            return new ActionExecutionResult(false, "Alertmanager silence id target is required.");
        }

        using var response = await HttpClient.DeleteAsync($"{Setting(state, "AlertmanagerBaseUrl")!.TrimEnd('/')}/api/v2/silence/{Uri.EscapeDataString(silenceId)}", ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        return response.IsSuccessStatusCode
            ? JsonSuccess("Alertmanager silence expired.", ("silenceId", silenceId))
            : JsonFailure($"Alertmanager expire silence failed: HTTP {(int)response.StatusCode} {response.StatusCode}.", ("body", Shorten(body)));
    }

    private async Task<ActionExecutionResult> ReloadPrometheusAsync(AdapterModuleState state, CancellationToken ct)
    {
        var prometheusBaseUrl = NormalizePrometheusBaseUrl(Setting(state, "PrometheusBaseUrl"));
        if (string.IsNullOrWhiteSpace(prometheusBaseUrl))
        {
            return new ActionExecutionResult(false, "PrometheusBaseUrl is required for reload_prometheus_config.");
        }

        using var response = await HttpClient.PostAsync($"{prometheusBaseUrl}/-/reload", content: null, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        return response.IsSuccessStatusCode
            ? JsonSuccess("Prometheus reload requested.", ("httpStatus", (int)response.StatusCode))
            : JsonFailure($"Prometheus reload failed: HTTP {(int)response.StatusCode} {response.StatusCode}.", ("body", Shorten(body)));
    }
}

public sealed class PostgreSqlIntegrationAdapter(IModuleSettingsReader settingsReader) : BaseIntegrationAdapter(settingsReader)
{
    public override string ModuleKey => "postgresql";
    protected override bool HasRealConfiguration(AdapterModuleState state) => Has(state, "ConnectionString") || Has(state, "Host", "DatabaseName", "Username");
    protected override async Task<IntegrationHealthResult> CheckRealHealthAsync(AdapterModuleState state, CancellationToken ct)
    {
        var connectionString = NormalizePostgresConnectionString(Setting(state, "ConnectionString") ?? $"Host={Setting(state, "Host")};Port={Setting(state, "Port") ?? "5432"};Database={Setting(state, "DatabaseName")};Username={Setting(state, "Username")};Password={Setting(state, "Password")}");
        await using var connection = new NpgsqlConnection(connectionString);
        var sw = Stopwatch.StartNew();
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand("SELECT 1", connection);
        await command.ExecuteScalarAsync(ct);
        sw.Stop();
        return new IntegrationHealthResult(ModuleKey, HealthStatus.Healthy, "SELECT 1 succeeded.", (int)sw.ElapsedMilliseconds);
    }

    protected override async Task<ActionExecutionResult> ExecuteRealActionAsync(AdapterModuleState state, ActionExecutionRequest request, CancellationToken ct)
    {
        return request.ActionKey switch
        {
            "collect_diagnostics" => await CollectPostgresDiagnosticsAsync(state, ct),
            "terminate_idle_in_transaction" => await TerminateIdleTransactionsAsync(state, request, ct),
            "cancel_long_running_query" => await CancelLongRunningQueryAsync(state, request, ct),
            "vacuum_analyze_table" => await RunTableMaintenanceAsync(state, request, "VACUUM ANALYZE", "PostgreSQL VACUUM ANALYZE completed.", ct),
            "reindex_index_concurrently" => await RunTableMaintenanceAsync(state, request, "REINDEX INDEX CONCURRENTLY", "PostgreSQL REINDEX CONCURRENTLY completed.", ct),
            _ => await base.ExecuteRealActionAsync(state, request, ct)
        };
    }

    private async Task<ActionExecutionResult> CollectPostgresDiagnosticsAsync(AdapterModuleState state, CancellationToken ct)
    {
        await using var connection = await OpenPostgresConnectionAsync(state, ct);
        var version = await ScalarAsync<string>(connection, "SELECT version()", ct);
        var isCockroach = version.Contains("CockroachDB", StringComparison.OrdinalIgnoreCase);
        var active = await ScalarAsync<long>(connection, "SELECT count(*) FROM pg_stat_activity WHERE state = 'active'", ct);
        var idleInTx = await ScalarAsync<long>(connection, "SELECT count(*) FROM pg_stat_activity WHERE state = 'idle in transaction'", ct);
        object blockers = isCockroach
            ? "not_supported_by_cockroach"
            : await ScalarAsync<long>(connection, "SELECT count(DISTINCT unnest(pg_blocking_pids(pid))) FROM pg_stat_activity WHERE cardinality(pg_blocking_pids(pid)) > 0", ct);

        return JsonSuccess("PostgreSQL diagnostics collected.", ("dialect", isCockroach ? "cockroachdb" : "postgresql"), ("activeSessions", active), ("idleInTransaction", idleInTx), ("blockingBackends", blockers));
    }

    private async Task<ActionExecutionResult> TerminateIdleTransactionsAsync(AdapterModuleState state, ActionExecutionRequest request, CancellationToken ct)
    {
        await using var connection = await OpenPostgresConnectionAsync(state, ct);
        var pid = ActionTarget(request, state);
        string sql;
        if (int.TryParse(pid, out var targetPid))
        {
            sql = $"SELECT count(*) FROM pg_stat_activity WHERE pid = {targetPid} AND pg_terminate_backend(pid)";
        }
        else
        {
            var olderThanSeconds = Math.Max(60, IntParameter(request, "olderThanSeconds", 900));
            sql = $"SELECT count(*) FROM pg_stat_activity WHERE state = 'idle in transaction' AND now() - COALESCE(xact_start, now()) > interval '{olderThanSeconds} seconds' AND pid <> pg_backend_pid() AND pg_terminate_backend(pid)";
        }

        var terminated = await ScalarAsync<long>(connection, sql, ct);
        return JsonSuccess("Idle PostgreSQL transactions terminated.", ("terminated", terminated));
    }

    private async Task<ActionExecutionResult> CancelLongRunningQueryAsync(AdapterModuleState state, ActionExecutionRequest request, CancellationToken ct)
    {
        await using var connection = await OpenPostgresConnectionAsync(state, ct);
        var pid = ActionTarget(request, state);
        string sql;
        if (int.TryParse(pid, out var targetPid))
        {
            sql = $"SELECT count(*) FROM pg_stat_activity WHERE pid = {targetPid} AND pg_cancel_backend(pid)";
        }
        else
        {
            var thresholdSeconds = Math.Max(30, IntParameter(request, "thresholdSeconds", IntSetting(state, "LongQueryThresholdSeconds", 30)));
            sql = $"SELECT count(*) FROM pg_stat_activity WHERE state = 'active' AND now() - query_start > interval '{thresholdSeconds} seconds' AND pid <> pg_backend_pid() AND pg_cancel_backend(pid)";
        }

        var canceled = await ScalarAsync<long>(connection, sql, ct);
        return JsonSuccess("Long PostgreSQL queries cancelled.", ("cancelled", canceled));
    }

    private async Task<ActionExecutionResult> RunTableMaintenanceAsync(AdapterModuleState state, ActionExecutionRequest request, string command, string successMessage, CancellationToken ct)
    {
        var target = ActionTarget(request, state);
        if (string.IsNullOrWhiteSpace(target))
        {
            return new ActionExecutionResult(false, $"{command} requires a target table or index.");
        }

        await using var connection = await OpenPostgresConnectionAsync(state, ct);
        await using var sql = new NpgsqlCommand($"{command} {QuoteQualifiedIdentifier(target)}", connection);
        await sql.ExecuteNonQueryAsync(ct);
        return JsonSuccess(successMessage, ("target", target));
    }

    private async Task<NpgsqlConnection> OpenPostgresConnectionAsync(AdapterModuleState state, CancellationToken ct)
    {
        var connectionString = NormalizePostgresConnectionString(Setting(state, "ConnectionString") ?? $"Host={Setting(state, "Host")};Port={Setting(state, "Port") ?? "5432"};Database={Setting(state, "DatabaseName")};Username={Setting(state, "Username")};Password={Setting(state, "Password")}");
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        return connection;
    }

    private static async Task<T> ScalarAsync<T>(NpgsqlConnection connection, string sql, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        var result = await command.ExecuteScalarAsync(ct);
        return (T)Convert.ChangeType(result ?? 0, typeof(T));
    }

    private static string QuoteQualifiedIdentifier(string value)
    {
        var parts = value.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0 || parts.Any(part => part.Any(ch => !(char.IsLetterOrDigit(ch) || ch == '_'))))
        {
            throw new InvalidOperationException("Target identifier must contain only letters, digits, underscores, and dots.");
        }

        return string.Join('.', parts.Select(part => "\"" + part.Replace("\"", "\"\"") + "\""));
    }

    private static string NormalizePostgresConnectionString(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || !uri.Scheme.StartsWith("postgres", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        var userInfo = uri.UserInfo.Split(':', 2);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = uri.AbsolutePath.TrimStart('/'),
            Username = Uri.UnescapeDataString(userInfo.ElementAtOrDefault(0) ?? string.Empty),
            Password = Uri.UnescapeDataString(userInfo.ElementAtOrDefault(1) ?? string.Empty)
        };
        return builder.ConnectionString;
    }
}

public sealed class RedisIntegrationAdapter(IModuleSettingsReader settingsReader) : BaseIntegrationAdapter(settingsReader)
{
    private static readonly Encoding RedisProtocolEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    public override string ModuleKey => "redis";
    protected override bool HasRealConfiguration(AdapterModuleState state) => Has(state, "RedisUrl") || Has(state, "Host");
    protected override async Task<IntegrationHealthResult> CheckRealHealthAsync(AdapterModuleState state, CancellationToken ct)
    {
        var redisUrl = Setting(state, "RedisUrl");
        var host = Setting(state, "Host") ?? "localhost";
        var port = int.TryParse(Setting(state, "Port"), out var parsedPort) ? parsedPort : 6379;
        var password = Setting(state, "Password");
        if (Uri.TryCreate(redisUrl, UriKind.Absolute, out var uri))
        {
            host = uri.Host;
            port = uri.Port > 0 ? uri.Port : 6379;
            password = string.IsNullOrWhiteSpace(uri.UserInfo) ? password : uri.UserInfo.Split(':', 2).ElementAtOrDefault(1);
        }

        var sw = Stopwatch.StartNew();
        using var client = new TcpClient();
        await client.ConnectAsync(host, port, ct);
        using var stream = client.GetStream();
        using var reader = new StreamReader(stream, RedisProtocolEncoding, leaveOpen: true);
        using var writer = new StreamWriter(stream, RedisProtocolEncoding, leaveOpen: true) { AutoFlush = true };
        if (!string.IsNullOrWhiteSpace(password))
        {
            await writer.WriteAsync($"*2\r\n$4\r\nAUTH\r\n${password.Length}\r\n{password}\r\n".AsMemory(), ct);
            await reader.ReadLineAsync(ct);
        }

        await writer.WriteAsync("*1\r\n$4\r\nPING\r\n".AsMemory(), ct);
        var response = await reader.ReadLineAsync(ct) ?? string.Empty;
        sw.Stop();
        return response.StartsWith("+PONG", StringComparison.OrdinalIgnoreCase)
            ? new IntegrationHealthResult(ModuleKey, HealthStatus.Healthy, "Redis PING returned PONG.", (int)sw.ElapsedMilliseconds)
            : new IntegrationHealthResult(ModuleKey, HealthStatus.Warning, $"Redis returned: {response.Trim()}", (int)sw.ElapsedMilliseconds);
    }

    protected override async Task<ActionExecutionResult> ExecuteRealActionAsync(AdapterModuleState state, ActionExecutionRequest request, CancellationToken ct)
    {
        return request.ActionKey switch
        {
            "collect_diagnostics" => await RedisCommandActionAsync(state, "Redis diagnostics collected.", ct, "INFO", "memory"),
            "purge_expired_memory" => await RedisCommandActionAsync(state, "Redis MEMORY PURGE completed.", ct, "MEMORY", "PURGE"),
            "delete_cache_key" => await DeleteRedisKeyAsync(state, request, ct),
            "set_key_ttl" => await SetRedisKeyTtlAsync(state, request, ct),
            "kill_idle_client" => await KillRedisClientAsync(state, request, ct),
            _ => await base.ExecuteRealActionAsync(state, request, ct)
        };
    }

    private async Task<ActionExecutionResult> DeleteRedisKeyAsync(AdapterModuleState state, ActionExecutionRequest request, CancellationToken ct)
    {
        var key = ActionTarget(request, state);
        return string.IsNullOrWhiteSpace(key)
            ? new ActionExecutionResult(false, "Redis key target is required.")
            : await RedisCommandActionAsync(state, "Redis key deleted.", ct, "DEL", key);
    }

    private async Task<ActionExecutionResult> SetRedisKeyTtlAsync(AdapterModuleState state, ActionExecutionRequest request, CancellationToken ct)
    {
        var key = ActionTarget(request, state);
        if (string.IsNullOrWhiteSpace(key))
        {
            return new ActionExecutionResult(false, "Redis key target is required.");
        }

        var ttlSeconds = Math.Max(1, IntParameter(request, "ttlSeconds", 3600));
        return await RedisCommandActionAsync(state, "Redis key TTL applied.", ct, "EXPIRE", key, ttlSeconds.ToString());
    }

    private async Task<ActionExecutionResult> KillRedisClientAsync(AdapterModuleState state, ActionExecutionRequest request, CancellationToken ct)
    {
        var clientId = ActionTarget(request, state);
        return string.IsNullOrWhiteSpace(clientId)
            ? new ActionExecutionResult(false, "Redis client id target is required.")
            : await RedisCommandActionAsync(state, "Redis client killed.", ct, "CLIENT", "KILL", "ID", clientId);
    }

    private async Task<ActionExecutionResult> RedisCommandActionAsync(AdapterModuleState state, string successMessage, CancellationToken ct, params string[] command)
    {
        var response = await SendRedisCommandAsync(state, ct, command);
        return response.StartsWith("-", StringComparison.Ordinal)
            ? JsonFailure($"Redis command failed: {response}", ("command", string.Join(' ', command)))
            : JsonSuccess(successMessage, ("command", string.Join(' ', command)), ("response", Shorten(response)));
    }

    private async Task<string> SendRedisCommandAsync(AdapterModuleState state, CancellationToken ct, params string[] command)
    {
        var redisUrl = Setting(state, "RedisUrl");
        var host = Setting(state, "Host") ?? "localhost";
        var port = int.TryParse(Setting(state, "Port"), out var parsedPort) ? parsedPort : 6379;
        var password = Setting(state, "Password");
        if (Uri.TryCreate(redisUrl, UriKind.Absolute, out var uri))
        {
            host = uri.Host;
            port = uri.Port > 0 ? uri.Port : 6379;
            password = string.IsNullOrWhiteSpace(uri.UserInfo) ? password : uri.UserInfo.Split(':', 2).ElementAtOrDefault(1);
        }

        using var client = new TcpClient();
        await client.ConnectAsync(host, port, ct);
        using var stream = client.GetStream();
        using var reader = new StreamReader(stream, RedisProtocolEncoding, leaveOpen: true);
        using var writer = new StreamWriter(stream, RedisProtocolEncoding, leaveOpen: true) { AutoFlush = true };
        if (!string.IsNullOrWhiteSpace(password))
        {
            await WriteRedisCommandAsync(writer, ct, "AUTH", password);
            _ = await reader.ReadLineAsync(ct);
        }

        await WriteRedisCommandAsync(writer, ct, command);
        return await reader.ReadLineAsync(ct) ?? string.Empty;
    }

    private static async Task WriteRedisCommandAsync(StreamWriter writer, CancellationToken ct, params string[] command)
    {
        await writer.WriteAsync($"*{command.Length}\r\n".AsMemory(), ct);
        foreach (var part in command)
        {
            await writer.WriteAsync($"${Encoding.UTF8.GetByteCount(part)}\r\n{part}\r\n".AsMemory(), ct);
        }
    }
}

public sealed class DockerIntegrationAdapter(IModuleSettingsReader settingsReader, IHttpClientFactory httpClientFactory) : HttpIntegrationAdapter(settingsReader, httpClientFactory)
{
    public override string ModuleKey => "docker";
    protected override bool HasRealConfiguration(AdapterModuleState state) =>
        Has(state, "DockerEndpoint")
        || (BoolSetting(state, "UseLocalDockerSocket") && OperatingSystem.IsLinux() && Has(state, "DockerSocketPath"));

    protected override Task<IntegrationHealthResult> CheckRealHealthAsync(AdapterModuleState state, CancellationToken ct)
    {
        var endpoint = Setting(state, "DockerEndpoint");
        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            return GetHealthAsync(DockerPingUrl(endpoint), ct);
        }

        if (BoolSetting(state, "UseLocalDockerSocket") && OperatingSystem.IsLinux())
        {
            return CheckUnixSocketAsync(Setting(state, "DockerSocketPath") ?? "/var/run/docker.sock", ct);
        }

        return Task.FromResult(new IntegrationHealthResult(ModuleKey, HealthStatus.NotConfigured, "Docker API endpoint or Linux socket bridge is required for a real check."));
    }

    private static string DockerPingUrl(string endpoint)
    {
        var normalized = endpoint.Trim();
        if (normalized.StartsWith("tcp://", StringComparison.OrdinalIgnoreCase))
        {
            normalized = $"http://{normalized[6..]}";
        }

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out _))
        {
            normalized = $"http://{normalized}";
        }

        return $"{normalized.TrimEnd('/')}/_ping";
    }

    private async Task<IntegrationHealthResult> CheckUnixSocketAsync(string socketPath, CancellationToken ct)
    {
        if (!File.Exists(socketPath))
        {
            return new IntegrationHealthResult(ModuleKey, HealthStatus.Error, $"Docker socket not found: {socketPath}");
        }

        var sw = Stopwatch.StartNew();
        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), ct);
        var request = Encoding.ASCII.GetBytes("GET /_ping HTTP/1.1\r\nHost: docker\r\nConnection: close\r\n\r\n");
        await socket.SendAsync(request, SocketFlags.None, ct);
        var buffer = new byte[2048];
        var received = await socket.ReceiveAsync(buffer, SocketFlags.None, ct);
        sw.Stop();
        var response = Encoding.ASCII.GetString(buffer, 0, received);
        return response.Contains("200 OK", StringComparison.OrdinalIgnoreCase) && response.Contains("OK", StringComparison.OrdinalIgnoreCase)
            ? new IntegrationHealthResult(ModuleKey, HealthStatus.Healthy, "Docker socket /_ping returned OK.", (int)sw.ElapsedMilliseconds)
            : new IntegrationHealthResult(ModuleKey, HealthStatus.Warning, "Docker socket responded but /_ping was not healthy.", (int)sw.ElapsedMilliseconds);
    }

    protected override async Task<ActionExecutionResult> ExecuteRealActionAsync(AdapterModuleState state, ActionExecutionRequest request, CancellationToken ct)
    {
        return request.ActionKey switch
        {
            "collect_diagnostics" => await DockerApiActionAsync(state, HttpMethod.Get, "/version", "Docker diagnostics collected.", ct),
            "restart_container" => await RestartContainerAsync(state, request, ct),
            "prune_exited_containers" => await DockerApiActionAsync(state, HttpMethod.Post, "/containers/prune", "Docker exited containers pruned.", ct),
            "prune_unused_images" => await DockerApiActionAsync(state, HttpMethod.Post, "/images/prune", "Docker unused images pruned.", ct),
            "pull_image" => await PullImageAsync(state, request, ct),
            _ => await base.ExecuteRealActionAsync(state, request, ct)
        };
    }

    private async Task<ActionExecutionResult> RestartContainerAsync(AdapterModuleState state, ActionExecutionRequest request, CancellationToken ct)
    {
        var container = ActionTarget(request, state, "ContainerNameFilter");
        if (string.IsNullOrWhiteSpace(container))
        {
            return new ActionExecutionResult(false, "Docker container id or name target is required.");
        }

        return await DockerApiActionAsync(state, HttpMethod.Post, $"/containers/{Uri.EscapeDataString(container)}/restart", "Docker container restart requested.", ct);
    }

    private async Task<ActionExecutionResult> PullImageAsync(AdapterModuleState state, ActionExecutionRequest request, CancellationToken ct)
    {
        var image = ActionTarget(request, state);
        if (string.IsNullOrWhiteSpace(image))
        {
            return new ActionExecutionResult(false, "Docker image target is required.");
        }

        return await DockerApiActionAsync(state, HttpMethod.Post, $"/images/create?fromImage={Uri.EscapeDataString(image)}", "Docker image pull requested.", ct);
    }

    private async Task<ActionExecutionResult> DockerApiActionAsync(AdapterModuleState state, HttpMethod method, string path, string successMessage, CancellationToken ct)
    {
        var baseUrl = DockerHttpBaseUrl(Setting(state, "DockerEndpoint"));
        if (baseUrl is not null)
        {
            using var request = new HttpRequestMessage(method, $"{baseUrl.TrimEnd('/')}{path}");
            using var response = await HttpClient.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            return response.IsSuccessStatusCode
                ? JsonSuccess(successMessage, ("transport", "http"), ("httpStatus", (int)response.StatusCode), ("body", Shorten(body)))
                : JsonFailure($"Docker API action failed: HTTP {(int)response.StatusCode} {response.StatusCode}.", ("transport", "http"), ("body", Shorten(body)));
        }

        if (BoolSetting(state, "UseLocalDockerSocket") && OperatingSystem.IsLinux())
        {
            return await DockerSocketActionAsync(Setting(state, "DockerSocketPath") ?? "/var/run/docker.sock", method, path, successMessage, ct);
        }

        return new ActionExecutionResult(false, "DockerEndpoint HTTP/TCP API or Linux Docker socket is required for remediation actions.");
    }

    private static async Task<ActionExecutionResult> DockerSocketActionAsync(string socketPath, HttpMethod method, string path, string successMessage, CancellationToken ct)
    {
        if (!File.Exists(socketPath))
        {
            return new ActionExecutionResult(false, $"Docker socket not found: {socketPath}");
        }

        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), ct);
        var request = Encoding.ASCII.GetBytes($"{method.Method} {path} HTTP/1.1\r\nHost: docker\r\nConnection: close\r\nContent-Length: 0\r\n\r\n");
        await socket.SendAsync(request, SocketFlags.None, ct);

        using var responseBytes = new MemoryStream();
        var buffer = new byte[8192];
        int received;
        while ((received = await socket.ReceiveAsync(buffer, SocketFlags.None, ct)) > 0)
        {
            responseBytes.Write(buffer, 0, received);
        }

        var response = Encoding.UTF8.GetString(responseBytes.ToArray());
        var statusLineEnd = response.IndexOf("\r\n", StringComparison.Ordinal);
        var statusLine = statusLineEnd > 0 ? response[..statusLineEnd] : response;
        var bodyStart = response.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        var body = bodyStart >= 0 ? response[(bodyStart + 4)..] : string.Empty;
        var ok = statusLine.Contains(" 2", StringComparison.Ordinal);
        return ok
            ? JsonSuccess(successMessage, ("transport", "unix-socket"), ("statusLine", statusLine), ("body", Shorten(body)))
            : JsonFailure("Docker socket action failed.", ("transport", "unix-socket"), ("statusLine", statusLine), ("body", Shorten(body)));
    }

    private static string? DockerHttpBaseUrl(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return null;
        }

        var normalized = endpoint.Trim();
        if (normalized.StartsWith("tcp://", StringComparison.OrdinalIgnoreCase))
        {
            normalized = $"http://{normalized[6..]}";
        }

        if (!normalized.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            normalized = $"http://{normalized}";
        }

        return normalized;
    }
}

public sealed class NginxIntegrationAdapter(IModuleSettingsReader settingsReader, IHttpClientFactory httpClientFactory) : HttpIntegrationAdapter(settingsReader, httpClientFactory)
{
    public override string ModuleKey => "nginx";
    protected override bool HasRealConfiguration(AdapterModuleState state) => Has(state, "BaseUrl");
    protected override Task<IntegrationHealthResult> CheckRealHealthAsync(AdapterModuleState state, CancellationToken ct) => GetHealthAsync(Setting(state, "BaseUrl")!, ct);

    protected override async Task<ActionExecutionResult> ExecuteRealActionAsync(AdapterModuleState state, ActionExecutionRequest request, CancellationToken ct)
    {
        return request.ActionKey switch
        {
            "collect_diagnostics" => await NginxDiagnosticsAsync(state, ct),
            "test_nginx_config" => await RunShellCommandAsync(Setting(state, "ConfigTestCommand"), "Nginx config test", ct),
            "reload_nginx" => await ReloadNginxAsync(state, ct),
            "rotate_nginx_logs" => await RunShellCommandAsync(Setting(state, "LogRotateCommand") ?? "nginx -s reopen", "Nginx log rotation", ct),
            "purge_nginx_cache_path" => await PurgeNginxCachePathAsync(state, request, ct),
            _ => await base.ExecuteRealActionAsync(state, request, ct)
        };
    }

    private async Task<ActionExecutionResult> NginxDiagnosticsAsync(AdapterModuleState state, CancellationToken ct)
    {
        var health = await CheckRealHealthAsync(state, ct);
        var statusUrl = Setting(state, "StatusUrl");
        string? statusBody = null;
        if (!string.IsNullOrWhiteSpace(statusUrl))
        {
            using var response = await HttpClient.GetAsync(statusUrl, ct);
            statusBody = await response.Content.ReadAsStringAsync(ct);
        }

        return JsonSuccess("Nginx diagnostics collected.", ("status", health.Status.ToString()), ("message", health.Message), ("statusBody", Shorten(statusBody)));
    }

    private async Task<ActionExecutionResult> ReloadNginxAsync(AdapterModuleState state, CancellationToken ct)
    {
        var test = await RunShellCommandAsync(Setting(state, "ConfigTestCommand"), "Nginx config test", ct);
        if (!test.Success)
        {
            return test;
        }

        return await RunShellCommandAsync(Setting(state, "ReloadCommand"), "Nginx reload", ct);
    }

    private static Task<ActionExecutionResult> PurgeNginxCachePathAsync(AdapterModuleState state, ActionExecutionRequest request, CancellationToken ct)
    {
        var path = ActionTarget(request, state, "CachePath");
        if (string.IsNullOrWhiteSpace(path))
        {
            return Task.FromResult(new ActionExecutionResult(false, "Nginx cache path target or CachePath setting is required."));
        }

        if (File.Exists(path))
        {
            File.Delete(path);
            return Task.FromResult(JsonSuccess("Nginx cache file purged.", ("path", path)));
        }

        if (Directory.Exists(path))
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();
                File.Delete(file);
            }

            return Task.FromResult(JsonSuccess("Nginx cache directory purged.", ("path", path)));
        }

        return Task.FromResult(new ActionExecutionResult(false, $"Nginx cache path does not exist: {path}"));
    }
}

public sealed class LinuxHostIntegrationAdapter(IModuleSettingsReader settingsReader, IHttpClientFactory httpClientFactory) : HttpIntegrationAdapter(settingsReader, httpClientFactory)
{
    public override string ModuleKey => "linux-host";
    protected override bool HasRealConfiguration(AdapterModuleState state) =>
        BoolSetting(state, "AgentMode", true)
            ? Has(state, "AgentBaseUrl")
            : OperatingSystem.IsLinux() && Has(state, "CheckMountPath");

    protected override Task<IntegrationHealthResult> CheckRealHealthAsync(AdapterModuleState state, CancellationToken ct)
    {
        if (BoolSetting(state, "AgentMode", true) && Has(state, "AgentBaseUrl"))
        {
            return GetHealthAsync(Setting(state, "AgentBaseUrl")!, ct);
        }

        if (!OperatingSystem.IsLinux())
        {
            return Task.FromResult(new IntegrationHealthResult(ModuleKey, HealthStatus.NotConfigured, "Local Linux checks require AgentMode=true or a Linux runtime."));
        }

        var mountPath = Setting(state, "CheckMountPath") ?? "/";
        return Task.FromResult(Directory.Exists(mountPath)
            ? new IntegrationHealthResult(ModuleKey, HealthStatus.Healthy, $"Local Linux mount path exists: {mountPath}.", DetailsJson: $$"""{"os":"{{Environment.OSVersion}}","mountPath":"{{mountPath}}"}""")
            : new IntegrationHealthResult(ModuleKey, HealthStatus.Error, $"Local Linux mount path does not exist: {mountPath}.", DetailsJson: $$"""{"os":"{{Environment.OSVersion}}","mountPath":"{{mountPath}}"}"""));
    }

    protected override async Task<ActionExecutionResult> ExecuteRealActionAsync(AdapterModuleState state, ActionExecutionRequest request, CancellationToken ct)
    {
        if (BoolSetting(state, "AgentMode", true))
        {
            if (request.ActionKey == "collect_diagnostics" && Has(state, "AgentBaseUrl"))
            {
                return await CollectLinuxAgentDiagnosticsAsync(state, ct);
            }

            return await ExecuteWebhookActionAsync(state, request, ct);
        }

        if (!OperatingSystem.IsLinux())
        {
            return new ActionExecutionResult(false, "Local Linux remediation requires a Linux runtime or AgentMode=true with RemediationWebhookUrl.");
        }

        return request.ActionKey switch
        {
            "collect_diagnostics" => await CollectLinuxDiagnosticsAsync(state, ct),
            "restart_systemd_service" => await RestartSystemdServiceAsync(request, ct),
            "rotate_system_logs" => await RunShellCommandAsync(JsonParameter(request, "command") ?? "logrotate -f /etc/logrotate.conf", "Linux log rotation", ct),
            "clean_temp_files" => await CleanTempFilesAsync(request, ct),
            "vacuum_journal" => await VacuumJournalAsync(request, ct),
            _ => await base.ExecuteRealActionAsync(state, request, ct)
        };
    }

    private async Task<ActionExecutionResult> CollectLinuxAgentDiagnosticsAsync(AdapterModuleState state, CancellationToken ct)
    {
        var agentBaseUrl = Setting(state, "AgentBaseUrl")!;
        var health = await GetHealthAsync(agentBaseUrl, ct);
        using var response = await HttpClient.GetAsync(agentBaseUrl, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        var summary = SummarizeNodeExporterMetrics(body);
        return response.IsSuccessStatusCode
            ? JsonSuccess("Linux host diagnostics collected.", ("transport", "agent-http"), ("status", health.Status.ToString()), ("message", health.Message), ("latencyMs", health.LatencyMs), ("metrics", summary))
            : JsonFailure("Linux host diagnostics failed.", ("transport", "agent-http"), ("httpStatus", (int)response.StatusCode), ("body", Shorten(body)));
    }

    private static string SummarizeNodeExporterMetrics(string body)
    {
        var prefixes = new[]
        {
            "node_load1",
            "node_memory_MemAvailable_bytes",
            "node_memory_MemTotal_bytes",
            "node_filesystem_avail_bytes",
            "node_filesystem_size_bytes",
            "node_uname_info"
        };
        var lines = body
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !line.StartsWith('#') && prefixes.Any(prefix => line.StartsWith(prefix, StringComparison.Ordinal)))
            .Take(20);
        return Shorten(string.Join('\n', lines));
    }

    private static Task<ActionExecutionResult> CollectLinuxDiagnosticsAsync(AdapterModuleState state, CancellationToken ct)
    {
        var mountPath = Setting(state, "CheckMountPath") ?? "/";
        var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(mountPath)) ?? "/");
        return Task.FromResult(JsonSuccess("Linux host diagnostics collected.", ("os", Environment.OSVersion.ToString()), ("mountPath", mountPath), ("freeBytes", drive.AvailableFreeSpace), ("totalBytes", drive.TotalSize)));
    }

    private static Task<ActionExecutionResult> RestartSystemdServiceAsync(ActionExecutionRequest request, CancellationToken ct)
    {
        var service = ActionTarget(request, new AdapterModuleState("linux-host", true, false, false, new Dictionary<string, string?>()));
        if (string.IsNullOrWhiteSpace(service) || service.Any(ch => !(char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.' or '@')))
        {
            return Task.FromResult(new ActionExecutionResult(false, "Safe systemd service target is required."));
        }

        return RunShellCommandAsync($"systemctl restart {service}", "systemd service restart", ct);
    }

    private static Task<ActionExecutionResult> CleanTempFilesAsync(ActionExecutionRequest request, CancellationToken ct)
    {
        var path = JsonParameter(request, "path") ?? "/tmp";
        var olderThanDays = Math.Max(1, IntParameter(request, "olderThanDays", 7));
        if (!Directory.Exists(path))
        {
            return Task.FromResult(new ActionExecutionResult(false, $"Temp path does not exist: {path}"));
        }

        var cutoff = DateTimeOffset.UtcNow.AddDays(-olderThanDays);
        var deleted = 0;
        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.TopDirectoryOnly))
        {
            ct.ThrowIfCancellationRequested();
            var info = new FileInfo(file);
            if (info.LastWriteTimeUtc < cutoff.UtcDateTime)
            {
                info.Delete();
                deleted++;
            }
        }

        return Task.FromResult(JsonSuccess("Old temp files removed.", ("path", path), ("deleted", deleted)));
    }

    private static Task<ActionExecutionResult> VacuumJournalAsync(ActionExecutionRequest request, CancellationToken ct)
    {
        var size = JsonParameter(request, "size") ?? "1G";
        return RunShellCommandAsync($"journalctl --vacuum-size={size}", "systemd journal vacuum", ct);
    }
}

public sealed class HttpEndpointCheckerAdapter(IModuleSettingsReader settingsReader, IHttpClientFactory httpClientFactory) : HttpIntegrationAdapter(settingsReader, httpClientFactory)
{
    public override string ModuleKey => "http-endpoint";
    protected override bool HasRealConfiguration(AdapterModuleState state) => Has(state, "EndpointUrl");
    protected override Task<IntegrationHealthResult> CheckRealHealthAsync(AdapterModuleState state, CancellationToken ct) => GetHealthAsync(Setting(state, "EndpointUrl")!, ct);
    protected override async Task<IReadOnlyList<IncomingEventDto>> FetchRealEventsAsync(AdapterModuleState state, CancellationToken ct)
    {
        var response = await HttpClient.GetAsync(Setting(state, "EndpointUrl"), ct);
        var expected = int.TryParse(Setting(state, "ExpectedStatusCode"), out var parsed) ? parsed : 200;
        return (int)response.StatusCode == expected ? [] : [new IncomingEventDto(ModuleKey, $"http-{Guid.CreateVersion7()}", "http.status_mismatch", EventSeverity.Error, Setting(state, "EndpointUrl")!, $"HTTP status {(int)response.StatusCode}, expected {expected}")];
    }

    protected override async Task<ActionExecutionResult> ExecuteRealActionAsync(AdapterModuleState state, ActionExecutionRequest request, CancellationToken ct)
    {
        return request.ActionKey switch
        {
            "collect_diagnostics" or "retry_endpoint_probe" => await ProbeEndpointAsync(state, ct),
            "warm_endpoint_cache" => await CallEndpointActionAsync(state, request, "WarmupUrl", "Endpoint warm-up completed.", ct),
            "call_recovery_webhook" => await ExecuteWebhookActionAsync(state, request, ct),
            "invalidate_endpoint_cache" => await CallEndpointActionAsync(state, request, "InvalidateCacheUrl", "Endpoint cache invalidation requested.", ct),
            _ => await base.ExecuteRealActionAsync(state, request, ct)
        };
    }

    private async Task<ActionExecutionResult> ProbeEndpointAsync(AdapterModuleState state, CancellationToken ct)
    {
        var health = await CheckRealHealthAsync(state, ct);
        return health.Status is HealthStatus.Healthy or HealthStatus.Warning
            ? JsonSuccess("HTTP endpoint probe completed.", ("status", health.Status.ToString()), ("message", health.Message), ("latencyMs", health.LatencyMs))
            : JsonFailure("HTTP endpoint probe failed.", ("status", health.Status.ToString()), ("message", health.Message));
    }

    private async Task<ActionExecutionResult> CallEndpointActionAsync(AdapterModuleState state, ActionExecutionRequest request, string settingKey, string successMessage, CancellationToken ct)
    {
        var url = JsonParameter(request, "url") ?? Setting(state, settingKey) ?? request.Target;
        if (string.IsNullOrWhiteSpace(url))
        {
            return new ActionExecutionResult(false, $"{settingKey} or url parameter is required.");
        }

        var method = JsonParameter(request, "method") ?? "POST";
        using var httpRequest = new HttpRequestMessage(new HttpMethod(method), url);
        using var response = await HttpClient.SendAsync(httpRequest, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        return response.IsSuccessStatusCode
            ? JsonSuccess(successMessage, ("httpStatus", (int)response.StatusCode), ("body", Shorten(body)))
            : JsonFailure($"Endpoint action failed: HTTP {(int)response.StatusCode} {response.StatusCode}.", ("body", Shorten(body)));
    }
}

public sealed class FileSystemMonitorAdapter(IModuleSettingsReader settingsReader) : BaseIntegrationAdapter(settingsReader)
{
    public override string ModuleKey => "file-system";
    protected override bool HasRealConfiguration(AdapterModuleState state) => Has(state, "Path");
    protected override Task<IntegrationHealthResult> CheckRealHealthAsync(AdapterModuleState state, CancellationToken ct) =>
        Task.FromResult(Directory.Exists(Setting(state, "Path")) ? new IntegrationHealthResult(ModuleKey, HealthStatus.Healthy, "Path exists.") : new IntegrationHealthResult(ModuleKey, HealthStatus.Error, "Path does not exist."));

    protected override Task<ActionExecutionResult> ExecuteRealActionAsync(AdapterModuleState state, ActionExecutionRequest request, CancellationToken ct)
    {
        return request.ActionKey switch
        {
            "collect_diagnostics" => FileSystemDiagnosticsAsync(state),
            "create_missing_directory" => CreateMissingDirectoryAsync(state, request),
            "clean_old_files" => CleanOldFilesAsync(state, request, ct),
            "compress_large_logs" => CompressLargeLogsAsync(state, request, ct),
            "remove_zero_byte_files" => RemoveZeroByteFilesAsync(state, request, ct),
            _ => base.ExecuteRealActionAsync(state, request, ct)
        };
    }

    private static Task<ActionExecutionResult> FileSystemDiagnosticsAsync(AdapterModuleState state)
    {
        var path = Setting(state, "Path")!;
        if (!Directory.Exists(path))
        {
            return Task.FromResult(new ActionExecutionResult(false, $"Path does not exist: {path}"));
        }

        var files = Directory.EnumerateFiles(path, Setting(state, "FileMask") ?? "*.*", SearchOption.TopDirectoryOnly).Take(5000).Select(item => new FileInfo(item)).ToArray();
        return Task.FromResult(JsonSuccess("File-system diagnostics collected.", ("path", path), ("files", files.Length), ("bytes", files.Sum(file => file.Length))));
    }

    private static Task<ActionExecutionResult> CreateMissingDirectoryAsync(AdapterModuleState state, ActionExecutionRequest request)
    {
        var path = ActionTarget(request, state, "Path");
        if (string.IsNullOrWhiteSpace(path))
        {
            return Task.FromResult(new ActionExecutionResult(false, "Directory target or Path setting is required."));
        }

        Directory.CreateDirectory(path);
        return Task.FromResult(JsonSuccess("Directory exists after remediation.", ("path", path)));
    }

    private static Task<ActionExecutionResult> CleanOldFilesAsync(AdapterModuleState state, ActionExecutionRequest request, CancellationToken ct)
    {
        var path = Setting(state, "Path")!;
        if (!Directory.Exists(path))
        {
            return Task.FromResult(new ActionExecutionResult(false, $"Path does not exist: {path}"));
        }

        var olderThanDays = Math.Max(1, IntParameter(request, "olderThanDays", 14));
        var includeSubdirectories = BoolSetting(state, "IncludeSubdirectories");
        var cutoff = DateTimeOffset.UtcNow.AddDays(-olderThanDays).UtcDateTime;
        var deleted = 0;
        foreach (var file in Directory.EnumerateFiles(path, Setting(state, "FileMask") ?? "*.*", includeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
        {
            ct.ThrowIfCancellationRequested();
            var info = new FileInfo(file);
            if (info.LastWriteTimeUtc < cutoff)
            {
                info.Delete();
                deleted++;
            }
        }

        return Task.FromResult(JsonSuccess("Old files cleaned.", ("path", path), ("deleted", deleted)));
    }

    private static Task<ActionExecutionResult> CompressLargeLogsAsync(AdapterModuleState state, ActionExecutionRequest request, CancellationToken ct)
    {
        var path = Setting(state, "Path")!;
        if (!Directory.Exists(path))
        {
            return Task.FromResult(new ActionExecutionResult(false, $"Path does not exist: {path}"));
        }

        var minSizeMb = Math.Max(1, IntParameter(request, "minSizeMb", 50));
        var compressed = 0;
        foreach (var file in Directory.EnumerateFiles(path, Setting(state, "FileMask") ?? "*.log", SearchOption.TopDirectoryOnly))
        {
            ct.ThrowIfCancellationRequested();
            var info = new FileInfo(file);
            if (info.Length < minSizeMb * 1024L * 1024L || file.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            using var input = File.OpenRead(file);
            using var output = File.Create($"{file}.gz");
            using var gzip = new GZipStream(output, CompressionLevel.SmallestSize);
            input.CopyTo(gzip);
            compressed++;
        }

        return Task.FromResult(JsonSuccess("Large log files compressed.", ("path", path), ("compressed", compressed)));
    }

    private static Task<ActionExecutionResult> RemoveZeroByteFilesAsync(AdapterModuleState state, ActionExecutionRequest request, CancellationToken ct)
    {
        var path = Setting(state, "Path")!;
        if (!Directory.Exists(path))
        {
            return Task.FromResult(new ActionExecutionResult(false, $"Path does not exist: {path}"));
        }

        var removed = 0;
        foreach (var file in Directory.EnumerateFiles(path, Setting(state, "FileMask") ?? "*.*", SearchOption.TopDirectoryOnly))
        {
            ct.ThrowIfCancellationRequested();
            var info = new FileInfo(file);
            if (info.Length == 0)
            {
                info.Delete();
                removed++;
            }
        }

        return Task.FromResult(JsonSuccess("Zero-byte files removed.", ("path", path), ("removed", removed)));
    }
}

public sealed class SmtpEmailAdapter(IModuleSettingsReader settingsReader) : BaseIntegrationAdapter(settingsReader)
{
    public override string ModuleKey => "smtp-email";
    protected override bool HasRealConfiguration(AdapterModuleState state) => Has(state, "SmtpHost", "FromEmail");
    protected override async Task<IntegrationHealthResult> CheckRealHealthAsync(AdapterModuleState state, CancellationToken ct)
    {
        var host = Setting(state, "SmtpHost")!;
        var port = IntSetting(state, "SmtpPort", 587);
        var sw = Stopwatch.StartNew();
        using var client = new TcpClient();
        await client.ConnectAsync(host, port, ct);
        using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
        using var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true) { AutoFlush = true, NewLine = "\r\n" };
        var banner = await ReadSmtpResponseAsync(reader, ct);
        await writer.WriteAsync($"EHLO sysassist.local\r\n".AsMemory(), ct);
        var ehlo = await ReadSmtpResponseAsync(reader, ct);
        sw.Stop();
        var healthy = banner.StartsWith("220", StringComparison.Ordinal) && ehlo.StartsWith("250", StringComparison.Ordinal);
        return new IntegrationHealthResult(ModuleKey, healthy ? HealthStatus.Healthy : HealthStatus.Warning, healthy ? "SMTP banner and EHLO succeeded." : $"SMTP handshake returned: {TrimSmtpMessage(ehlo)}", (int)sw.ElapsedMilliseconds);
    }

    protected override async Task<ActionExecutionResult> ExecuteRealActionAsync(AdapterModuleState state, ActionExecutionRequest request, CancellationToken ct)
    {
        if (request.ActionKey == "collect_diagnostics")
        {
            var health = await CheckRealHealthAsync(state, ct);
            return JsonSuccess("SMTP diagnostics collected.", ("status", health.Status.ToString()), ("message", health.Message), ("latencyMs", health.LatencyMs));
        }

        var recipients = SplitList(JsonParameter(request, "to", "recipient", "recipients") ?? Setting(state, "DefaultRecipients"));
        if (recipients.Length == 0)
        {
            return new ActionExecutionResult(false, "SMTP send was not attempted: recipient is missing.");
        }

        var body = JsonParameter(request, "body", "message", "text") ?? DefaultNotificationBody(request);
        if (string.IsNullOrWhiteSpace(body))
        {
            return new ActionExecutionResult(false, "SMTP send was not attempted: message body is missing.");
        }

        using var message = new MailMessage
        {
            From = new MailAddress(Setting(state, "FromEmail")!, Setting(state, "FromName") ?? "SysAssist"),
            Subject = JsonParameter(request, "subject") ?? $"SysAssist: {request.ActionKey}",
            Body = body,
            IsBodyHtml = false
        };
        foreach (var recipient in recipients)
        {
            message.To.Add(recipient);
        }

        using var client = new SmtpClient(Setting(state, "SmtpHost")!, IntSetting(state, "SmtpPort", 587))
        {
            EnableSsl = BoolSetting(state, "UseTls", true),
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = IntSetting(state, "TimeoutSeconds", 15) * 1000
        };
        var username = Setting(state, "Username");
        if (!string.IsNullOrWhiteSpace(username))
        {
            client.Credentials = new NetworkCredential(username, Setting(state, "Password"));
        }

        await client.SendMailAsync(message, ct);
        return new ActionExecutionResult(true, "SMTP message was sent by the configured server.", ResultJson: $$"""{"channel":"smtp","recipients":{{recipients.Length}}}""");
    }

    private static string DefaultNotificationBody(ActionExecutionRequest request) =>
        request.ActionKey switch
        {
            "send_recovery_notice" => $"SysAssist detected recovery for {request.Target ?? "the monitored target"}.",
            "send_escalation_digest" => $"SysAssist escalation digest for repeated events on {request.Target ?? "the monitored target"}.",
            "send_postmortem_summary" => $"SysAssist postmortem summary requested for {request.Target ?? "the incident"}.",
            _ => $"SysAssist notification for {request.Target ?? "the monitored target"}."
        };

    private static async Task<string> ReadSmtpResponseAsync(StreamReader reader, CancellationToken ct)
    {
        var lines = new List<string>();
        while (true)
        {
            var line = await reader.ReadLineAsync(ct) ?? throw new IOException("SMTP server closed the connection.");
            lines.Add(line);
            if (line.Length < 4 || line[3] == ' ')
            {
                return string.Join(" | ", lines);
            }
        }
    }

    private static string TrimSmtpMessage(string message) =>
        message.Length > 180 ? $"{message[..180]}..." : message;
}

public sealed class TelegramBotAdapter(IModuleSettingsReader settingsReader, IHttpClientFactory httpClientFactory) : HttpIntegrationAdapter(settingsReader, httpClientFactory)
{
    public override string ModuleKey => "telegram-bot";
    protected override bool HasRealConfiguration(AdapterModuleState state) => Has(state, "BotToken", "DefaultChatId");
    protected override async Task<IntegrationHealthResult> CheckRealHealthAsync(AdapterModuleState state, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        using var response = await HttpClient.GetAsync($"https://api.telegram.org/bot{Setting(state, "BotToken")}/getMe", ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        sw.Stop();
        if (!response.IsSuccessStatusCode)
        {
            return new IntegrationHealthResult(ModuleKey, HealthStatus.Warning, $"Telegram getMe HTTP {(int)response.StatusCode} {response.StatusCode}.", (int)sw.ElapsedMilliseconds);
        }

        return TelegramOk(body)
            ? new IntegrationHealthResult(ModuleKey, HealthStatus.Healthy, "Telegram getMe succeeded.", (int)sw.ElapsedMilliseconds)
            : new IntegrationHealthResult(ModuleKey, HealthStatus.Warning, "Telegram getMe returned ok=false.", (int)sw.ElapsedMilliseconds);
    }

    protected override async Task<ActionExecutionResult> ExecuteRealActionAsync(AdapterModuleState state, ActionExecutionRequest request, CancellationToken ct)
    {
        if (request.ActionKey == "collect_diagnostics")
        {
            var health = await CheckRealHealthAsync(state, ct);
            return JsonSuccess("Telegram diagnostics collected.", ("status", health.Status.ToString()), ("message", health.Message), ("latencyMs", health.LatencyMs));
        }

        var chatId = JsonParameter(request, "chatId", "chat_id", "recipient") ?? Setting(state, "DefaultChatId");
        var text = JsonParameter(request, "text", "message", "body") ?? DefaultTelegramText(request);
        if (string.IsNullOrWhiteSpace(chatId))
        {
            return new ActionExecutionResult(false, "Telegram send was not attempted: chat id is missing.");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return new ActionExecutionResult(false, "Telegram send was not attempted: message text is missing.");
        }

        var payload = new Dictionary<string, object?>
        {
            ["chat_id"] = chatId,
            ["text"] = text,
            ["disable_notification"] = BoolSetting(state, "DisableNotification")
        };
        var parseMode = Setting(state, "ParseMode");
        if (!string.IsNullOrWhiteSpace(parseMode))
        {
            payload["parse_mode"] = parseMode;
        }

        using var response = await HttpClient.PostAsJsonAsync($"https://api.telegram.org/bot{Setting(state, "BotToken")}/sendMessage", payload, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            return new ActionExecutionResult(false, $"Telegram sendMessage HTTP {(int)response.StatusCode} {response.StatusCode}.");
        }

        return TelegramOk(body)
            ? new ActionExecutionResult(true, "Telegram message was sent through sendMessage.", ResultJson: """{"channel":"telegram"}""")
            : new ActionExecutionResult(false, "Telegram sendMessage returned ok=false.");
    }

    private static string DefaultTelegramText(ActionExecutionRequest request) =>
        request.ActionKey switch
        {
            "send_recovery_notice" => $"SysAssist recovery notice: {request.Target ?? "target"} is stable.",
            "send_escalation_digest" => $"SysAssist escalation digest: repeated events require attention for {request.Target ?? "target"}.",
            "send_oncall_page" => $"SysAssist on-call page: critical remediation required for {request.Target ?? "target"}.",
            _ => $"SysAssist notification for {request.Target ?? "target"}."
        };

    private static bool TelegramOk(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.True;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

public sealed class LocalRuleAdvisorAdapter(IModuleSettingsReader settingsReader) : BaseIntegrationAdapter(settingsReader)
{
    public override string ModuleKey => "local-rule-advisor";

    protected override Task<IntegrationHealthResult> CheckRealHealthAsync(AdapterModuleState state, CancellationToken ct) =>
        Task.FromResult(new IntegrationHealthResult(ModuleKey, HealthStatus.Healthy, "Local rule engine is available in-process.", DetailsJson: """{"rules":"local","externalDependency":false}"""));

    protected override Task<ActionExecutionResult> ExecuteRealActionAsync(AdapterModuleState state, ActionExecutionRequest request, CancellationToken ct)
    {
        var target = request.Target ?? JsonParameter(request, "target") ?? "local-rule-advisor";
        var result = request.ActionKey switch
        {
            "collect_diagnostics" => JsonSuccess("Advisor diagnostics collected.", ("ruleset", Setting(state, "RulesetVersion") ?? "default-v1"), ("explainMode", BoolSetting(state, "ExplainMode", true))),
            "generate_runbook" => JsonSuccess("Advisor runbook generated.", ("target", target), ("steps", new[] { "collect diagnostics", "verify module health", "choose least-risk remediation", "request approval for unsafe changes", "validate recovery" })),
            "open_manual_review" => JsonSuccess("Manual review opened locally.", ("target", target), ("confidenceThreshold", Setting(state, "ConfidenceThreshold") ?? "0.70")),
            "raise_confidence_threshold" => JsonSuccess("Advisor confidence threshold raise recorded.", ("target", target), ("newThreshold", JsonParameter(request, "confidenceThreshold", "threshold") ?? "0.85")),
            "suppress_duplicate_recommendations" => JsonSuccess("Duplicate recommendation suppression recorded.", ("target", target), ("windowMinutes", IntParameter(request, "windowMinutes", 30))),
            _ => new ActionExecutionResult(false, $"Advisor action '{request.ActionKey}' is not implemented.")
        };
        return Task.FromResult(result);
    }

    public ActionExecutionResult Recommend(IncomingEventDto incoming)
    {
        var critical = incoming.Severity == EventSeverity.Critical || incoming.Summary.Contains("saturation", StringComparison.OrdinalIgnoreCase);
        var explanation = critical ? "Critical infrastructure signal matched local high-risk remediation rules." : "Local rules suggest read-only diagnostics first.";
        return new ActionExecutionResult(true, explanation, RequiresApproval: critical, ResultJson: $$"""{"classification":"{{incoming.EventType}}","confidence":{{(critical ? "0.91" : "0.72")}},"probableCause":"{{incoming.Summary}}","nextStep":"{{(critical ? "Open approval for remediation" : "Collect diagnostics")}}"}""");
    }
}
