using SysAssist.Domain.Enums;

namespace SysAssist.Infrastructure.Modules;

internal sealed record RemediationActionDefinition(
    string ModuleKey,
    string ActionKey,
    string Name,
    string Description,
    RiskLevel RiskLevel,
    bool RequiresApproval,
    string ParameterSchemaJson,
    IReadOnlyCollection<string> Symptoms);

internal static class RemediationActionCatalog
{
    private const string StandardSchema = """
        {"type":"object","properties":{"target":{"type":"string"},"reason":{"type":"string"},"dryRun":{"type":"boolean"}}}
        """;
    private const string MessageSchema = """
        {"type":"object","properties":{"target":{"type":"string"},"subject":{"type":"string"},"message":{"type":"string"},"body":{"type":"string"},"recipients":{"type":"string"},"dryRun":{"type":"boolean"}}}
        """;

    public static readonly IReadOnlyCollection<RemediationActionDefinition> Actions =
    [
        Action("zabbix", "collect_diagnostics", "Collect Zabbix diagnostics", "Read current API version, target context, and module connectivity evidence before remediation.", RiskLevel.Low, false, ["diagnostic", "unknown", "not_configured"]),
        Action("zabbix", "acknowledge_problem", "Acknowledge Zabbix problem", "Acknowledge a target Zabbix event and attach the SysAssist remediation reason.", RiskLevel.Medium, true, ["problem", "trigger", "warning", "error"]),
        Action("zabbix", "create_maintenance_window", "Create host maintenance window", "Open a temporary maintenance window for a noisy host while the operator fixes the root cause.", RiskLevel.High, true, ["flapping", "maintenance", "host"]),
        Action("zabbix", "suppress_flapping_trigger", "Suppress flapping trigger", "Suppress repeated trigger noise by acknowledging the event with a deduplication message.", RiskLevel.High, true, ["flapping", "repeat", "noise"]),
        Action("zabbix", "close_resolved_problem", "Close resolved Zabbix problem", "Close a target event after external checks show recovery.", RiskLevel.Critical, true, ["resolved", "close", "recovery"]),

        Action("grafana", "collect_diagnostics", "Collect Grafana diagnostics", "Read Grafana health and alerting context before remediation.", RiskLevel.Low, false, ["diagnostic", "unknown"]),
        Action("grafana", "add_incident_annotation", "Add incident annotation", "Write a Grafana annotation for the affected target so dashboards show the remediation window.", RiskLevel.Low, false, ["annotation", "dashboard", "incident"]),
        Action("grafana", "pause_alert_rule", "Pause alert rule", "Pause a noisy alert rule through the configured Grafana remediation endpoint.", RiskLevel.High, true, ["flapping", "rule", "alert"]),
        Action("grafana", "mute_contact_point", "Mute contact point", "Mute a contact point or notification route during an approved noisy incident.", RiskLevel.High, true, ["notification", "contact", "noise"]),
        Action("grafana", "restore_alert_rule", "Restore alert rule", "Restore a paused alert rule after the incident is fixed.", RiskLevel.High, true, ["restore", "recovery", "rule"]),

        Action("prometheus-alertmanager", "collect_diagnostics", "Collect Alertmanager diagnostics", "Read Alertmanager and Prometheus readiness before remediation.", RiskLevel.Low, false, ["diagnostic", "unknown"]),
        Action("prometheus-alertmanager", "create_alert_silence", "Create alert silence", "Create a bounded Alertmanager silence for the target labels while recovery is in progress.", RiskLevel.Medium, true, ["silence", "flapping", "warning", "error"]),
        Action("prometheus-alertmanager", "expire_alert_silence", "Expire alert silence", "Expire an existing silence when the signal is stable again.", RiskLevel.Medium, true, ["restore", "silence", "recovery"]),
        Action("prometheus-alertmanager", "reload_prometheus_config", "Reload Prometheus config", "Call the Prometheus lifecycle reload endpoint after a fixed rule or target configuration is deployed.", RiskLevel.High, true, ["config", "reload", "prometheus"]),
        Action("prometheus-alertmanager", "verify_alert_route", "Verify alert route", "Verify Alertmanager route health and receiver readiness after remediation.", RiskLevel.Low, false, ["route", "receiver", "notification"]),

        Action("postgresql", "collect_diagnostics", "Collect PostgreSQL diagnostics", "Run read-only database checks for active sessions, blockers, and connection pressure.", RiskLevel.Low, false, ["diagnostic", "database", "sql"]),
        Action("postgresql", "terminate_idle_in_transaction", "Terminate idle transactions", "Terminate idle-in-transaction sessions that block vacuum or schema changes.", RiskLevel.High, true, ["idle", "transaction", "lock", "blocker"]),
        Action("postgresql", "cancel_long_running_query", "Cancel long query", "Cancel a long-running backend by PID or target query threshold.", RiskLevel.High, true, ["long query", "timeout", "latency"]),
        Action("postgresql", "vacuum_analyze_table", "Vacuum analyze table", "Run VACUUM ANALYZE for a target table after bloat or stale statistics warnings.", RiskLevel.Medium, true, ["bloat", "vacuum", "statistics"]),
        Action("postgresql", "reindex_index_concurrently", "Reindex concurrently", "Run REINDEX INDEX CONCURRENTLY for a target index after corruption or bloat warnings.", RiskLevel.Critical, true, ["index", "bloat", "corruption"]),

        Action("redis", "collect_diagnostics", "Collect Redis diagnostics", "Read Redis PING/INFO evidence before cache remediation.", RiskLevel.Low, false, ["diagnostic", "cache", "redis"]),
        Action("redis", "purge_expired_memory", "Purge Redis allocator memory", "Run MEMORY PURGE to return unused allocator memory after memory-pressure warnings.", RiskLevel.Medium, true, ["memory", "pressure", "allocator"]),
        Action("redis", "delete_cache_key", "Delete cache key", "Delete a poisoned or oversized cache key identified by diagnostics.", RiskLevel.High, true, ["key", "cache", "poison"]),
        Action("redis", "set_key_ttl", "Set key TTL", "Apply a TTL to a target key that is causing unbounded memory growth.", RiskLevel.Medium, true, ["ttl", "memory", "growth"]),
        Action("redis", "kill_idle_client", "Kill idle client", "Kill a selected idle client when connection saturation is detected.", RiskLevel.High, true, ["client", "connection", "saturation"]),

        Action("docker", "collect_diagnostics", "Collect Docker diagnostics", "Read Docker runtime availability and target container context.", RiskLevel.Low, false, ["diagnostic", "container", "docker"]),
        Action("docker", "restart_container", "Restart container", "Restart a failed or unhealthy container by id or name.", RiskLevel.High, true, ["container", "unhealthy", "restart"]),
        Action("docker", "prune_exited_containers", "Prune exited containers", "Remove stopped containers to recover disk space and reduce noise.", RiskLevel.Medium, true, ["disk", "exited", "container"]),
        Action("docker", "prune_unused_images", "Prune unused images", "Remove dangling images when Docker disk usage crosses warning thresholds.", RiskLevel.High, true, ["disk", "image", "prune"]),
        Action("docker", "pull_image", "Pull container image", "Pull a target image before a controlled restart or recovery rollout.", RiskLevel.Medium, true, ["image", "deploy", "pull"]),

        Action("nginx", "collect_diagnostics", "Collect Nginx diagnostics", "Run Nginx health and configured status checks before remediation.", RiskLevel.Low, false, ["diagnostic", "nginx", "http"]),
        Action("nginx", "test_nginx_config", "Test Nginx config", "Run the configured nginx -t command before reload or rollback.", RiskLevel.Low, false, ["config", "syntax", "test"]),
        Action("nginx", "reload_nginx", "Reload Nginx", "Reload Nginx after a successful config test to restore service without dropping active connections.", RiskLevel.High, true, ["reload", "config", "gateway"]),
        Action("nginx", "rotate_nginx_logs", "Rotate Nginx logs", "Signal Nginx log reopening or run the configured log-rotation command after disk warnings.", RiskLevel.Medium, true, ["logs", "disk", "rotate"]),
        Action("nginx", "purge_nginx_cache_path", "Purge Nginx cache path", "Purge a configured cache path or target file when stale cached content causes errors.", RiskLevel.High, true, ["cache", "stale", "purge"]),

        Action("linux-host", "collect_diagnostics", "Collect Linux host diagnostics", "Collect host-level CPU, memory, disk, and service context.", RiskLevel.Low, false, ["diagnostic", "host", "linux"]),
        Action("linux-host", "restart_systemd_service", "Restart systemd service", "Restart a target service after a confirmed failed-unit or health-check error.", RiskLevel.High, true, ["service", "systemd", "failed"]),
        Action("linux-host", "rotate_system_logs", "Rotate system logs", "Run log rotation to recover disk space after log growth warnings.", RiskLevel.Medium, true, ["logs", "disk", "rotate"]),
        Action("linux-host", "clean_temp_files", "Clean temp files", "Remove old temporary files from a configured path when disk space is low.", RiskLevel.Medium, true, ["tmp", "temp", "disk"]),
        Action("linux-host", "vacuum_journal", "Vacuum journal", "Vacuum systemd journal to a configured size or age after disk pressure warnings.", RiskLevel.Medium, true, ["journal", "disk", "logs"]),

        Action("http-endpoint", "collect_diagnostics", "Collect endpoint diagnostics", "Run an HTTP probe and capture status/latency evidence before remediation.", RiskLevel.Low, false, ["diagnostic", "http", "endpoint"]),
        Action("http-endpoint", "retry_endpoint_probe", "Retry endpoint probe", "Run an immediate probe to confirm whether the warning is transient.", RiskLevel.Low, false, ["retry", "timeout", "probe"]),
        Action("http-endpoint", "warm_endpoint_cache", "Warm endpoint cache", "Call the configured warm-up endpoint to restore cold route or cache performance.", RiskLevel.Medium, true, ["latency", "cache", "cold"]),
        Action("http-endpoint", "call_recovery_webhook", "Call recovery webhook", "Call a configured recovery webhook for endpoint-specific self-healing.", RiskLevel.High, true, ["recovery", "webhook", "restart"]),
        Action("http-endpoint", "invalidate_endpoint_cache", "Invalidate endpoint cache", "Call an invalidation endpoint when stale cached responses create user-visible errors.", RiskLevel.High, true, ["cache", "stale", "invalidate"]),

        Action("file-system", "collect_diagnostics", "Collect file-system diagnostics", "Read path, free-space, and file-mask evidence before remediation.", RiskLevel.Low, false, ["diagnostic", "filesystem", "disk"]),
        Action("file-system", "create_missing_directory", "Create missing directory", "Create a required monitored directory when the path is absent.", RiskLevel.Medium, true, ["missing", "directory", "path"]),
        Action("file-system", "clean_old_files", "Clean old files", "Delete files older than the configured retention period to recover disk space.", RiskLevel.High, true, ["old files", "disk", "cleanup"]),
        Action("file-system", "compress_large_logs", "Compress large logs", "Compress large log files matching the module mask to reduce disk pressure.", RiskLevel.Medium, true, ["large", "logs", "compress"]),
        Action("file-system", "remove_zero_byte_files", "Remove zero-byte files", "Remove empty marker files that break ingestion or polling workflows.", RiskLevel.Medium, true, ["zero", "empty", "files"]),

        Action("smtp-email", "collect_diagnostics", "Collect SMTP diagnostics", "Run SMTP banner and EHLO checks before notification remediation.", RiskLevel.Low, false, ["diagnostic", "smtp", "mail"]),
        Action("smtp-email", "send_notification", "Send notification", "Send a direct SMTP notification to the configured recipients.", RiskLevel.Low, false, ["notify", "mail", "warning"]),
        Action("smtp-email", "send_recovery_notice", "Send recovery notice", "Send a recovery notice when SysAssist detects the signal has stabilized.", RiskLevel.Low, false, ["recovery", "mail", "notice"]),
        Action("smtp-email", "send_escalation_digest", "Send escalation digest", "Send a concise escalation digest for repeated warning or error events.", RiskLevel.Medium, false, ["escalation", "digest", "repeat"]),
        Action("smtp-email", "send_postmortem_summary", "Send postmortem summary", "Send a post-incident summary to the configured distribution list.", RiskLevel.Low, false, ["postmortem", "summary", "mail"]),

        Action("telegram-bot", "collect_diagnostics", "Collect Telegram diagnostics", "Run Telegram getMe and chat-target checks before notification remediation.", RiskLevel.Low, false, ["diagnostic", "telegram", "chat"]),
        Action("telegram-bot", "send_notification", "Send Telegram notification", "Send a direct Telegram notification to the configured chat.", RiskLevel.Low, false, ["notify", "telegram", "warning"]),
        Action("telegram-bot", "send_recovery_notice", "Send Telegram recovery", "Send a recovery notice to the Telegram on-call chat.", RiskLevel.Low, false, ["recovery", "telegram", "notice"]),
        Action("telegram-bot", "send_escalation_digest", "Send Telegram escalation", "Send a concise repeated-event escalation digest to Telegram.", RiskLevel.Medium, false, ["escalation", "telegram", "repeat"]),
        Action("telegram-bot", "send_oncall_page", "Send on-call page", "Page the on-call chat with a high-priority Telegram message.", RiskLevel.Medium, false, ["oncall", "page", "critical"]),

        Action("local-rule-advisor", "collect_diagnostics", "Collect advisor diagnostics", "Collect local rule-engine version and recommendation context.", RiskLevel.Low, false, ["diagnostic", "advisor", "rule"]),
        Action("local-rule-advisor", "generate_runbook", "Generate runbook", "Generate a deterministic remediation runbook for the event class.", RiskLevel.Low, false, ["runbook", "manual", "guide"]),
        Action("local-rule-advisor", "open_manual_review", "Open manual review", "Create a local manual-review decision point when confidence is below threshold.", RiskLevel.Low, false, ["review", "confidence", "manual"]),
        Action("local-rule-advisor", "raise_confidence_threshold", "Raise confidence threshold", "Raise the local recommendation threshold during noisy warning storms.", RiskLevel.Medium, true, ["confidence", "threshold", "noise"]),
        Action("local-rule-advisor", "suppress_duplicate_recommendations", "Suppress duplicate recommendations", "Suppress duplicate local recommendations for repeated warning storms.", RiskLevel.Medium, true, ["duplicate", "storm", "noise"])
    ];

    public static IReadOnlyCollection<RemediationActionDefinition> ForModule(string moduleKey) =>
        Actions.Where(action => action.ModuleKey.Equals(moduleKey, StringComparison.OrdinalIgnoreCase)).ToArray();

    public static RemediationActionDefinition? Find(string moduleKey, string actionKey) =>
        Actions.SingleOrDefault(action =>
            action.ModuleKey.Equals(moduleKey, StringComparison.OrdinalIgnoreCase)
            && action.ActionKey.Equals(actionKey, StringComparison.OrdinalIgnoreCase));

    private static RemediationActionDefinition Action(
        string moduleKey,
        string actionKey,
        string name,
        string description,
        RiskLevel riskLevel,
        bool requiresApproval,
        string[] symptoms,
        string? parameterSchemaJson = null) =>
        new(moduleKey, actionKey, name, description, riskLevel, requiresApproval, parameterSchemaJson ?? SchemaFor(moduleKey), symptoms);

    private static string SchemaFor(string moduleKey) =>
        moduleKey is "smtp-email" or "telegram-bot" ? MessageSchema : StandardSchema;
}
