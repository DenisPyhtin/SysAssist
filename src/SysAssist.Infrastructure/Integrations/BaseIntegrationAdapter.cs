using System.Diagnostics;
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
}

public sealed class GrafanaIntegrationAdapter(IModuleSettingsReader settingsReader, IHttpClientFactory httpClientFactory) : HttpIntegrationAdapter(settingsReader, httpClientFactory)
{
    public override string ModuleKey => "grafana";
    protected override bool HasRealConfiguration(AdapterModuleState state) => Has(state, "BaseUrl");
    protected override Task<IntegrationHealthResult> CheckRealHealthAsync(AdapterModuleState state, CancellationToken ct) => GetHealthAsync($"{Setting(state, "BaseUrl")!.TrimEnd('/')}/api/health", ct);
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
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        using var writer = new StreamWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
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
}

public sealed class NginxIntegrationAdapter(IModuleSettingsReader settingsReader, IHttpClientFactory httpClientFactory) : HttpIntegrationAdapter(settingsReader, httpClientFactory)
{
    public override string ModuleKey => "nginx";
    protected override bool HasRealConfiguration(AdapterModuleState state) => Has(state, "BaseUrl");
    protected override Task<IntegrationHealthResult> CheckRealHealthAsync(AdapterModuleState state, CancellationToken ct) => GetHealthAsync(Setting(state, "BaseUrl")!, ct);
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
}

public sealed class FileSystemMonitorAdapter(IModuleSettingsReader settingsReader) : BaseIntegrationAdapter(settingsReader)
{
    public override string ModuleKey => "file-system";
    protected override bool HasRealConfiguration(AdapterModuleState state) => Has(state, "Path");
    protected override Task<IntegrationHealthResult> CheckRealHealthAsync(AdapterModuleState state, CancellationToken ct) =>
        Task.FromResult(Directory.Exists(Setting(state, "Path")) ? new IntegrationHealthResult(ModuleKey, HealthStatus.Healthy, "Path exists.") : new IntegrationHealthResult(ModuleKey, HealthStatus.Error, "Path does not exist."));
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
        var recipients = SplitList(JsonParameter(request, "to", "recipient", "recipients") ?? Setting(state, "DefaultRecipients"));
        if (recipients.Length == 0)
        {
            return new ActionExecutionResult(false, "SMTP send was not attempted: recipient is missing.");
        }

        var body = JsonParameter(request, "body", "message", "text");
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
        var chatId = JsonParameter(request, "chatId", "chat_id", "recipient") ?? Setting(state, "DefaultChatId");
        var text = JsonParameter(request, "text", "message", "body");
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

    public ActionExecutionResult Recommend(IncomingEventDto incoming)
    {
        var critical = incoming.Severity == EventSeverity.Critical || incoming.Summary.Contains("saturation", StringComparison.OrdinalIgnoreCase);
        var explanation = critical ? "Critical infrastructure signal matched local high-risk remediation rules." : "Local rules suggest read-only diagnostics first.";
        return new ActionExecutionResult(true, explanation, RequiresApproval: critical, ResultJson: $$"""{"classification":"{{incoming.EventType}}","confidence":{{(critical ? "0.91" : "0.72")}},"probableCause":"{{incoming.Summary}}","nextStep":"{{(critical ? "Open approval for remediation" : "Collect diagnostics")}}"}""");
    }
}
