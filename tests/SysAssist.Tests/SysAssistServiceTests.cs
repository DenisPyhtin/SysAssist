using System.Text.Json;
using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SysAssist.Application.Api;
using SysAssist.Application.Auth;
using SysAssist.Application.Integrations;
using SysAssist.Contracts.Api;
using SysAssist.Domain.Enums;
using SysAssist.Infrastructure;
using SysAssist.Infrastructure.Integrations;

namespace SysAssist.Tests;

public sealed class SysAssistServiceTests
{
    [Fact]
    public void PasswordHashing_VerifiesCorrectPassword_AndRejectsWrongPassword()
    {
        var hasher = new SysAssist.Infrastructure.Data.PasswordHasher();
        var hash = hasher.Hash("CorrectHorseBatteryStaple!");

        Assert.True(hasher.Verify("CorrectHorseBatteryStaple!", hash));
        Assert.False(hasher.Verify("wrong-password", hash));
        Assert.DoesNotContain("CorrectHorseBatteryStaple", hash, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ModuleEnableDisable_ChangesModuleState()
    {
        var service = CreateService();
        var module = await ModuleByKey(service, "nginx");

        var disable = await service.SetModuleEnabledAsync(module.Id, false, "test", "test-disable", CancellationToken.None);
        var disabled = await service.GetModuleAsync(module.Id, CancellationToken.None);
        var enable = await service.SetModuleEnabledAsync(module.Id, true, "test", "test-enable", CancellationToken.None);
        var enabled = await service.GetModuleAsync(module.Id, CancellationToken.None);

        Assert.True(disable.Success);
        Assert.False(disabled!.IsEnabled);
        Assert.Equal("Disabled", disabled.HealthStatus);
        Assert.True(enable.Success);
        Assert.True(enabled!.IsEnabled);
    }

    [Fact]
    public async Task RequiredModuleSettingsValidation_FailsWhenRealModeMissingRequiredValues()
    {
        var service = CreateService();
        var module = await ModuleByKey(service, "http-endpoint");

        var result = await service.UpdateModuleSettingsAsync(
            module.Id,
            new UpdateModuleSettingsRequest(
            [
                new("UseFallbackMode", "false"),
                new("EndpointUrl", "")
            ]),
            "test",
            "test-validation",
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Required settings missing", result.Message);
    }

    [Fact]
    public async Task ModuleRegistry_ContainsAllThirteenModules_WithSettingsAndSafeModeDefaults()
    {
        var service = CreateService();
        var modules = await service.ListModulesAsync(CancellationToken.None);
        var expectedKeys = new[]
        {
            "zabbix",
            "grafana",
            "prometheus-alertmanager",
            "postgresql",
            "redis",
            "docker",
            "nginx",
            "linux-host",
            "http-endpoint",
            "file-system",
            "smtp-email",
            "telegram-bot",
            "local-rule-advisor"
        };

        Assert.Equal(13, modules.Count);
        Assert.All(expectedKeys, key => Assert.Contains(modules, module => module.Key == key));
        Assert.All(modules, module =>
        {
            Assert.True(module.SafeMode);
            Assert.False(module.UseFallbackMode);
        });

        foreach (var module in modules)
        {
            var settings = await service.GetModuleSettingsAsync(module.Id, CancellationToken.None);
            Assert.NotEmpty(settings);
            Assert.Contains(settings, setting => setting.Key == "SafeMode" && setting.Value == "true");
            Assert.Contains(settings, setting => setting.Key == "UseFallbackMode" && setting.Value == "false");
        }
    }

    [Fact]
    public async Task RemediationCatalog_ContainsAtLeastFortyEightHealingActionsAcrossAllModules()
    {
        var service = CreateService();
        var modules = await service.ListModulesAsync(CancellationToken.None);
        var actions = await service.ListActionsAsync(CancellationToken.None);
        var actionsByModule = actions.GroupBy(action => action.ModuleId).ToDictionary(group => group.Key, group => group.ToArray());
        var expectedActionKeys = new[]
        {
            "terminate_idle_in_transaction",
            "purge_expired_memory",
            "restart_container",
            "reload_nginx",
            "restart_systemd_service",
            "clean_old_files",
            "create_alert_silence",
            "call_recovery_webhook"
        };

        Assert.True(actions.Count >= 48, $"Expected at least 48 remediation actions, got {actions.Count}.");
        Assert.All(modules, module =>
        {
            Assert.True(module.SupportsActions, $"{module.Key} must expose remediation actions.");
            Assert.True(actionsByModule.TryGetValue(module.Id, out var moduleActions), $"{module.Key} has no actions.");
            Assert.True(moduleActions!.Length >= 5, $"{module.Key} must expose at least 5 actions.");
            Assert.Contains(moduleActions, action => action.ActionKey == "collect_diagnostics");
        });
        Assert.All(expectedActionKeys, key => Assert.Contains(actions, action => action.ActionKey == key));
        Assert.Contains(actions, action => action.RequiresApproval && action.RiskLevel is "High" or "Critical");
    }

    [Fact]
    public async Task Diagnostics_ReportRealHealthEvidenceAndFreshnessFields()
    {
        var service = CreateService();
        var diagnostics = await service.GetDiagnosticsAsync(CancellationToken.None);

        Assert.Contains(diagnostics.Components, component => component == "diagnosticsEvidence=real-adapter-health");
        Assert.Contains(diagnostics.Components, component => component.StartsWith("diagnosticsFreshness=", StringComparison.Ordinal));
        Assert.Contains(diagnostics.Components, component => component.StartsWith("lastDiagnosticsRunAt=", StringComparison.Ordinal));
        Assert.Contains(diagnostics.Components, component => component.StartsWith("healthStaleAfterMinutes=", StringComparison.Ordinal));
        Assert.Contains(diagnostics.Components, component => component.StartsWith("staleHealthChecks=", StringComparison.Ordinal));
        Assert.Contains(diagnostics.Components, component => component.StartsWith("remediationActions=", StringComparison.Ordinal));
        Assert.Contains(diagnostics.Components, component => component.StartsWith("modulesWithRemediationActions=", StringComparison.Ordinal));
        Assert.Contains(diagnostics.Components, component => component.StartsWith("diagnosticRemediation=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SecretSettings_AreMaskedInApiResponse()
    {
        var service = CreateService();
        var module = await ModuleByKey(service, "zabbix");
        var replacement = await service.ReplaceModuleSecretAsync(module.Id, "ApiToken", new ReplaceSecretRequest("test-secret-value"), "test", "test-secret", CancellationToken.None);

        var settings = await service.GetModuleSettingsAsync(module.Id, CancellationToken.None);
        var secret = settings.Single(item => item.Key == "ApiToken");

        Assert.True(replacement.Success);
        Assert.True(secret.IsSecret);
        Assert.True(secret.HasValue);
        Assert.Equal("********", secret.Value);
        Assert.DoesNotContain("test-secret-value", string.Join("|", settings.Select(item => item.Value)), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RealFetch_DoesNotCreateSyntheticEvents()
    {
        var service = CreateService();
        var module = await ModuleByKey(service, "prometheus-alertmanager");
        await service.SetModuleEnabledAsync(module.Id, true, "test", "test-fetch-enable", CancellationToken.None);

        var beforeEvents = await service.ListEventsAsync(CancellationToken.None);
        var beforeApprovals = await service.ListApprovalsAsync(CancellationToken.None);
        var result = await service.FetchModuleEventsAsync(module.Id, "test", "test-fetch", CancellationToken.None);
        var afterEvents = await service.ListEventsAsync(CancellationToken.None);
        var afterApprovals = await service.ListApprovalsAsync(CancellationToken.None);
        var bundle = await service.GetSupportBundleFileAsync("test", "test-fetch-bundle", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(beforeEvents.Count, afterEvents.Count);
        Assert.Equal(beforeApprovals.Count, afterApprovals.Count);
        Assert.Contains("recommendations", bundle.ContentJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Approve_InSafeMode_BlocksExecution_AndWritesAuditEntry()
    {
        var service = CreateService();
        var approval = await CreatePendingApproval(service, "approve");

        var result = await service.DecideApprovalAsync(approval.Id, true, "senior-admin", Guid.CreateVersion7(), "approved by test", "test-approve", CancellationToken.None);
        var decided = await service.GetApprovalAsync(approval.Id, CancellationToken.None);
        var audit = await service.ListAuditAsync(CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Failed", decided!.Status);
        Assert.Contains(audit, item => item.Action == "ACTION_APPROVED" && item.Resource == approval.Id.ToString());
        Assert.Contains(audit, item => item.Action == "ACTION_BLOCKED");
    }

    [Fact]
    public async Task Reject_ChangesStatus_AndWritesAuditEntry()
    {
        var service = CreateService();
        var approval = await CreatePendingApproval(service, "reject");

        var result = await service.DecideApprovalAsync(approval.Id, false, "senior-admin", Guid.CreateVersion7(), "reject reason", "test-reject", CancellationToken.None);
        var decided = await service.GetApprovalAsync(approval.Id, CancellationToken.None);
        var audit = await service.ListAuditAsync(CancellationToken.None);
        var incident = await service.GetEventAsync(approval.EventId, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Rejected", decided!.Status);
        Assert.Equal("Rejected", incident!.Status);
        Assert.Contains(audit, item => item.Action == "ACTION_REJECTED" && item.Resource == approval.Id.ToString());
    }

    [Fact]
    public async Task SupportBundle_ExcludesSecretsAndPasswordHashes()
    {
        var service = CreateService();
        var bundle = await service.GetSupportBundleFileAsync("auditor", "test-bundle", CancellationToken.None);
        using var document = JsonDocument.Parse(bundle.ContentJson);

        Assert.EndsWith(".json", bundle.FileName);
        Assert.Equal("SysAssist", document.RootElement.GetProperty("product").GetString());
        Assert.DoesNotContain("PasswordHash", bundle.ContentJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("demo-secret", bundle.ContentJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ApiToken\":\"", bundle.ContentJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UserAndRoleDtos_DoNotExposePasswordHash()
    {
        var service = CreateService();
        var users = await service.ListUsersAsync(CancellationToken.None);
        var serialized = JsonSerializer.Serialize(users);

        Assert.NotEmpty(users);
        Assert.DoesNotContain("PasswordHash", serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAction_BlocksUntilModuleHasHealthyRealConnection()
    {
        var service = CreateService();
        var action = (await service.ListActionsAsync(CancellationToken.None)).First(item => item.IsEnabled);

        var result = await service.ExecuteActionAsync(action.Id, new SysAssist.Contracts.Api.ExecuteActionRequest(null, "{}"), "test", "test-action-not-connected", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("not connected", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SmtpAction_DoesNotReportSuccessWithoutRealRecipient()
    {
        var adapter = new SmtpEmailAdapter(new StaticModuleSettingsReader("smtp-email", new Dictionary<string, string?>
        {
            ["SmtpHost"] = "127.0.0.1",
            ["SmtpPort"] = "25",
            ["FromEmail"] = "sysassist@example.test"
        }));

        var result = await adapter.ExecuteActionAsync(new ActionExecutionRequest("send_notification", null, """{"body":"hello"}"""), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("recipient", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TelegramAction_CallsSendMessageAndFailsWhenTelegramRejects()
    {
        var requestedUrls = new List<string>();
        var adapter = new TelegramBotAdapter(
            new StaticModuleSettingsReader("telegram-bot", new Dictionary<string, string?>
            {
                ["BotToken"] = "test-token",
                ["DefaultChatId"] = "12345",
                ["ParseMode"] = ""
            }),
            new StubHttpClientFactory(new DelegateHandler((request, _) =>
            {
                requestedUrls.Add(request.RequestUri?.AbsoluteUri ?? string.Empty);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"ok":false,"description":"chat not found"}""", Encoding.UTF8, "application/json")
                });
            })));

        var result = await adapter.ExecuteActionAsync(new ActionExecutionRequest("send_notification", null, """{"text":"hello"}"""), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("ok=false", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(requestedUrls, url => url.Contains("/sendMessage", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task LinuxHostNonAgentMode_DoesNotReturnHealthyOutsideLinuxRuntime()
    {
        if (OperatingSystem.IsLinux())
        {
            return;
        }

        var adapter = new LinuxHostIntegrationAdapter(
            new StaticModuleSettingsReader("linux-host", new Dictionary<string, string?>
            {
                ["AgentMode"] = "false",
                ["Hostname"] = "localhost",
                ["CheckMountPath"] = "/"
            }),
            new StubHttpClientFactory(new DelegateHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)))));

        var health = await adapter.CheckHealthAsync(CancellationToken.None);

        Assert.Equal(HealthStatus.NotConfigured, health.Status);
    }

    [Fact]
    public async Task LocalRuleAdvisorHealth_IsHealthyBecauseItIsInProcess()
    {
        var adapter = new LocalRuleAdvisorAdapter(new StaticModuleSettingsReader("local-rule-advisor", new Dictionary<string, string?>()));

        var health = await adapter.CheckHealthAsync(CancellationToken.None);

        Assert.Equal(HealthStatus.Healthy, health.Status);
        Assert.Contains("in-process", health.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static ISysAssistApiService CreateService()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SysAssist:UseDemoData"] = "true",
                ["SysAssist:BootstrapAdminPassword"] = "TestBootstrapAdmin#12345",
                ["Jwt:SigningKey"] = "test-signing-key-that-is-long-enough-32",
                ["Jwt:Issuer"] = "SysAssist",
                ["Jwt:Audience"] = "sysassist-api"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);
        return services.BuildServiceProvider().GetRequiredService<ISysAssistApiService>();
    }

    private static async Task<ModuleDto> ModuleByKey(ISysAssistApiService service, string key)
    {
        var modules = await service.ListModulesAsync(CancellationToken.None);
        return modules.Single(module => module.Key == key);
    }

    private static async Task<ApprovalDto> CreatePendingApproval(ISysAssistApiService service, string suffix)
    {
        var before = await service.ListApprovalsAsync(CancellationToken.None);
        var payload = $$"""{"ruleName":"test {{suffix}}","title":"Critical test event {{suffix}}","state":"alerting","severity":"critical","target":"test-host-{{suffix}}","message":"critical workflow test"}""";
        var webhook = await service.ProcessWebhookAsync("grafana", payload, null, $"test:{suffix}", $"test-{suffix}", CancellationToken.None);
        Assert.True(webhook.Success);

        var after = await service.ListApprovalsAsync(CancellationToken.None);
        return after
            .Where(item => item.Status == "Pending" && before.All(previous => previous.Id != item.Id))
            .OrderByDescending(item => item.RequestedAt)
            .First();
    }

    private sealed class StaticModuleSettingsReader(string moduleKey, IReadOnlyDictionary<string, string?> settings) : IModuleSettingsReader
    {
        public Task<AdapterModuleState> GetStateAsync(string requestedModuleKey, CancellationToken cancellationToken) =>
            Task.FromResult(new AdapterModuleState(moduleKey, IsEnabled: true, UseFallbackMode: false, SafeMode: false, settings));
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class DelegateHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            respond(request, cancellationToken);
    }
}
