using SysAssist.Domain.Enums;

namespace SysAssist.Infrastructure.Modules;

internal sealed record ModuleDefinition(
    string Key,
    string Name,
    ModuleType Type,
    string Description,
    bool SupportsPolling,
    bool SupportsWebhooks,
    bool SupportsActions,
    IReadOnlyCollection<SettingDefinition> Settings);

internal sealed record SettingDefinition(
    string Key,
    string ValueType,
    bool IsSecret = false,
    bool IsRequiredInRealMode = false,
    string? DefaultValue = null,
    string? Description = null);

internal static class ModuleCatalog
{
    private static readonly SettingDefinition[] CommonSettings =
    [
        new("Enabled", "bool", DefaultValue: "true", Description: "Controls whether the module is enabled."),
        new("UseFallbackMode", "bool", DefaultValue: "false", Description: "Deprecated. Real connections are required."),
        new("SafeMode", "bool", DefaultValue: "true", Description: "Blocks dangerous real actions unless explicitly disabled."),
        new("PollingEnabled", "bool", DefaultValue: "true", Description: "Allows background polling when implemented."),
        new("PollingIntervalSeconds", "int", DefaultValue: "60", Description: "Default polling interval."),
        new("TimeoutSeconds", "int", DefaultValue: "15", Description: "External request timeout."),
        new("RetryCount", "int", DefaultValue: "3", Description: "External request retry count."),
        new("Description", "string", Description: "Operator-facing module notes."),
        new("Tags", "string", Description: "Comma-separated module tags.")
    ];

    public static readonly IReadOnlyCollection<ModuleDefinition> Modules =
    [
        Module("zabbix", "Zabbix", ModuleType.Monitoring, "Zabbix trigger and host event ingestion.", true, true, true,
            Setting("BaseUrl", "url", "http://34.107.8.65:10051", required: true), Secret("ApiToken"), Setting("Username", "string"), Secret("Password"),
            Setting("UseApiToken", "bool", "true"), Setting("VerifySsl", "bool", "true"), Setting("EventSeverityMin", "string", "Warning"),
            Setting("HostGroupFilter", "string"), Setting("PollingIntervalSeconds", "int", "60")),
        Module("grafana", "Grafana", ModuleType.Alerting, "Grafana alert webhook receiver.", false, true, false,
            Setting("BaseUrl", "url", "http://34.107.8.65:3000", required: true), Secret("ApiToken"), Secret("WebhookSecret"), Setting("OrganizationId", "string"),
            Setting("AlertFolderFilter", "string"), Setting("VerifySsl", "bool", "true")),
        Module("prometheus-alertmanager", "Prometheus Alertmanager", ModuleType.Monitoring, "Prometheus Alertmanager integration.", true, true, false,
            Setting("PrometheusBaseUrl", "url", "http://34.107.8.65:9090"), Setting("AlertmanagerBaseUrl", "url", "http://34.107.8.65:9093", required: true), Secret("WebhookSecret"), Setting("ReceiverName", "string", "sysassist"),
            Setting("SeverityFilter", "string", "warning,critical"), Setting("SilenceAuthor", "string", "SysAssist"), Setting("VerifySsl", "bool", "true")),
        Module("postgresql", "PostgreSQL", ModuleType.Database, "PostgreSQL compatible database checks.", true, false, true,
            Setting("Host", "string", "34.107.8.65", required: true), Setting("Port", "int", "5432"), Setting("DatabaseName", "string", "sysassistant_infra", required: true),
            Setting("Username", "string", "sysadmin", required: true), Secret("Password"), Secret("ConnectionString"), Setting("UseConnectionString", "bool", "true"),
            Setting("MaxConnectionsThreshold", "int", "80"), Setting("LongQueryThresholdSeconds", "int", "30"),
            Setting("ReplicationLagThresholdSeconds", "int", "60"), Setting("VerifySsl", "bool", "true")),
        Module("redis", "Redis", ModuleType.Database, "Redis health and capacity checks.", true, false, true,
            Setting("RedisUrl", "url", "redis://34.107.8.65:6379"), Setting("Host", "string", "34.107.8.65", required: true), Setting("Port", "int", "6379"), Secret("Password"), Setting("DatabaseIndex", "int", "0"),
            Setting("UseTls", "bool", "false"), Setting("MemoryUsageThresholdPercent", "int", "85"), Setting("ConnectedClientsThreshold", "int", "500")),
        Module("docker", "Docker", ModuleType.Infrastructure, "Local or remote Docker runtime checks.", true, false, true,
            Setting("DockerEndpoint", "string"), Setting("UseLocalDockerSocket", "bool", "true"), Setting("DockerSocketPath", "string", "/var/run/docker.sock"),
            Setting("ApiVersion", "string"), Setting("ContainerNameFilter", "string"), Setting("AllowContainerRestart", "bool", "false")),
        Module("nginx", "Nginx", ModuleType.Infrastructure, "Nginx status, log, and config checks.", true, false, true,
            Setting("BaseUrl", "url", "http://34.107.8.65:80"), Setting("StatusUrl", "url", "http://34.107.8.65:80/nginx_status"), Setting("AccessLogPath", "string"), Setting("ErrorLogPath", "string"),
            Setting("ConfigTestCommand", "string", "nginx -t"), Setting("ReloadCommand", "string", "nginx -s reload"), Setting("VerifySsl", "bool", "true")),
        Module("linux-host", "Linux Host", ModuleType.Infrastructure, "Linux host checks via agent or local probes.", true, false, true,
            Setting("Hostname", "string", "34.107.8.65", required: true), Setting("AgentMode", "bool", "true"), Setting("AgentBaseUrl", "url", "http://34.107.8.65:9100/metrics"),
            Setting("CpuThresholdPercent", "int", "85"), Setting("MemoryThresholdPercent", "int", "85"), Setting("DiskThresholdPercent", "int", "90"),
            Setting("CheckMountPath", "string", "/")),
        Module("http-endpoint", "HTTP Endpoint Checker", ModuleType.LocalCheck, "HTTP endpoint availability checks.", true, false, false,
            Setting("EndpointUrl", "url", "http://34.107.8.65:9115/probe", required: true), Setting("Method", "string", "GET"), Setting("ExpectedStatusCode", "int", "200"),
            Setting("TimeoutSeconds", "int", "10"), Setting("ExpectedText", "string"), Setting("HeadersJson", "json", "{}"), Setting("CheckIntervalSeconds", "int", "60")),
        Module("file-system", "File System Monitor", ModuleType.LocalCheck, "File and disk space monitoring.", true, false, false,
            Setting("Path", "string", required: true), Setting("FileMask", "string", "*.*"), Setting("MaxFileSizeMb", "int", "1024"),
            Setting("MinFreeSpaceGb", "int", "5"), Setting("WatchMode", "bool", "false"), Setting("IncludeSubdirectories", "bool", "false")),
        Module("smtp-email", "SMTP Email", ModuleType.Notification, "SMTP notification delivery.", false, false, false,
            Setting("SmtpHost", "string", required: true), Setting("SmtpPort", "int", "587"), Setting("Username", "string"), Secret("Password"),
            Setting("FromEmail", "string", required: true), Setting("FromName", "string", "SysAssist"), Setting("UseTls", "bool", "true"), Setting("DefaultRecipients", "string")),
        Module("telegram-bot", "Telegram Bot", ModuleType.Notification, "Telegram bot notification delivery.", false, true, false,
            Secret("BotToken", required: true), Setting("DefaultChatId", "string", required: true), Setting("ParseMode", "string", "Markdown"),
            Setting("DisableNotification", "bool", "false")),
        Module("local-rule-advisor", "Local Rule Advisor", ModuleType.Advisor, "Local rule-based recommendation engine.", false, false, false,
            Setting("Enabled", "bool", "true"), Setting("ConfidenceThreshold", "decimal", "0.70"), Setting("RulesetVersion", "string", "default-v1"),
            Setting("ExplainMode", "bool", "true"), Setting("UseHistoricalEvents", "bool", "true"))
    ];

    public static ModuleDefinition? Find(string key) =>
        Modules.SingleOrDefault(module => module.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

    private static ModuleDefinition Module(string key, string name, ModuleType type, string description, bool polling, bool webhooks, bool actions, params SettingDefinition[] settings) =>
        new(key, name, type, description, polling, webhooks, actions, CommonSettings.Concat(settings).GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase).Select(group => group.Last()).ToArray());

    private static SettingDefinition Setting(string key, string valueType, string? defaultValue = null, bool required = false) =>
        new(key, valueType, IsRequiredInRealMode: required, DefaultValue: defaultValue);

    private static SettingDefinition Secret(string key, bool required = false, string? DefaultValue = null) =>
        new(key, "password", IsSecret: true, IsRequiredInRealMode: required, DefaultValue: DefaultValue);
}
