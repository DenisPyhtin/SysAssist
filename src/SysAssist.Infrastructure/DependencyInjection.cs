using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SysAssist.Application.Api;
using SysAssist.Application.Auth;
using SysAssist.Application.Integrations;
using SysAssist.Application.Incidents;
using SysAssist.Application.Security;
using SysAssist.Infrastructure.Auth;
using SysAssist.Infrastructure.Data;
using SysAssist.Infrastructure.Integrations;
using SysAssist.Infrastructure.Repositories;
using SysAssist.Infrastructure.Security;
using SysAssist.Infrastructure.Services;

namespace SysAssist.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var useDemoData = configuration.GetValue("SysAssist:UseDemoData", false);
        var connectionString = configuration.GetConnectionString("SysAssistDb")
            ?? configuration.GetConnectionString("DefaultConnection");
        var secretProtection = SecretProtectionOptions.FromConfiguration(configuration);

        services.AddSingleton(configuration);
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ILicenseService, LicenseService>();
        services.AddSingleton<ISecretProtector>(_ =>
        {
            if (!string.IsNullOrWhiteSpace(secretProtection.Key))
            {
                return new AesGcmSecretProtector(secretProtection.Key);
            }

            if (!useDemoData && secretProtection.RequireEncryptionInRealMode)
            {
                throw new InvalidOperationException("Real mode requires Security:SecretEncryptionKey or SYSASSIST_SECRET_ENCRYPTION_KEY for encrypted module secrets.");
            }

            return NoOpSecretProtector.Instance;
        });
        services.AddHttpClient();
        services.AddHttpClient("SysAssistIntegrations", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        });
        services.AddScoped<IIntegrationAdapterFactory, IntegrationAdapterFactory>();
        services.AddScoped<IIntegrationAdapter, ZabbixIntegrationAdapter>();
        services.AddScoped<IIntegrationAdapter, GrafanaIntegrationAdapter>();
        services.AddScoped<IIntegrationAdapter, AlertmanagerIntegrationAdapter>();
        services.AddScoped<IIntegrationAdapter, PostgreSqlIntegrationAdapter>();
        services.AddScoped<IIntegrationAdapter, RedisIntegrationAdapter>();
        services.AddScoped<IIntegrationAdapter, DockerIntegrationAdapter>();
        services.AddScoped<IIntegrationAdapter, NginxIntegrationAdapter>();
        services.AddScoped<IIntegrationAdapter, LinuxHostIntegrationAdapter>();
        services.AddScoped<IIntegrationAdapter, HttpEndpointCheckerAdapter>();
        services.AddScoped<IIntegrationAdapter, FileSystemMonitorAdapter>();
        services.AddScoped<IIntegrationAdapter, SmtpEmailAdapter>();
        services.AddScoped<IIntegrationAdapter, TelegramBotAdapter>();
        services.AddScoped<IIntegrationAdapter, LocalRuleAdvisorAdapter>();

        if (!useDemoData && string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Real mode requires ConnectionStrings:SysAssistDb or ConnectionStrings:DefaultConnection. Set SysAssist:UseDemoData=true only for explicit test fixtures.");
        }

        if (!useDemoData)
        {
            services.AddDbContext<SysAssistDbContext>(options =>
                options.UseNpgsql(connectionString, npgsql =>
                {
                    npgsql.CommandTimeout(45);
                    npgsql.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorCodesToAdd: ["40001", "CR000"]);
                }));
            services.AddScoped<IIncidentRepository, EfIncidentRepository>();
            services.AddScoped<IAuthService>(provider => new AuthService(
                provider.GetRequiredService<IConfiguration>(),
                provider.GetRequiredService<IPasswordHasher>(),
                provider.GetRequiredService<SysAssistDbContext>()));
            services.AddScoped<IModuleSettingsReader>(provider => new ModuleSettingsReader(
                provider.GetRequiredService<SysAssistDbContext>(),
                provider.GetRequiredService<ISecretProtector>()));
            services.AddScoped<ISysAssistApiService>(provider => new SysAssistApiService(
                provider.GetRequiredService<IPasswordHasher>(),
                provider.GetRequiredService<IIntegrationAdapterFactory>(),
                provider.GetRequiredService<SysAssistDbContext>(),
                provider.GetRequiredService<ISecretProtector>(),
                provider.GetRequiredService<ILicenseService>(),
                provider.GetRequiredService<IConfiguration>()));
        }
        else
        {
            services.AddSingleton<IIncidentRepository, DemoIncidentRepository>();
            services.AddScoped<IAuthService>(provider => new AuthService(
                provider.GetRequiredService<IConfiguration>(),
                provider.GetRequiredService<IPasswordHasher>()));
            services.AddScoped<IModuleSettingsReader>(provider => new ModuleSettingsReader(
                secretProtector: provider.GetRequiredService<ISecretProtector>()));
            services.AddScoped<ISysAssistApiService>(provider => new SysAssistApiService(
                provider.GetRequiredService<IPasswordHasher>(),
                provider.GetRequiredService<IIntegrationAdapterFactory>(),
                secretProtector: provider.GetRequiredService<ISecretProtector>(),
                licenseService: provider.GetRequiredService<ILicenseService>(),
                configuration: provider.GetRequiredService<IConfiguration>()));
        }

        return services;
    }
}
