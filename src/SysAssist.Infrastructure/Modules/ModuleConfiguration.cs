using System.Text;
using Microsoft.Extensions.Configuration;

namespace SysAssist.Infrastructure.Modules;

internal static class ModuleConfiguration
{
    public static string? GetConfiguredValue(IConfiguration? configuration, string moduleKey, string settingKey)
    {
        if (configuration is null)
        {
            return null;
        }

        var directKeys = new[]
        {
            $"SysAssist:Modules:{moduleKey}:Settings:{settingKey}",
            $"Modules:{moduleKey}:Settings:{settingKey}",
            $"SysAssist:Modules:{moduleKey}:{settingKey}",
            $"Modules:{moduleKey}:{settingKey}"
        };

        foreach (var key in directKeys)
        {
            var value = configuration[key];
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return configuration[EnvKey(moduleKey, settingKey)];
    }

    private static string EnvKey(string moduleKey, string settingKey) =>
        $"SYSASSIST_MODULE_{Normalize(moduleKey)}_{Normalize(settingKey)}";

    private static string Normalize(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            builder.Append(char.IsLetterOrDigit(ch) ? char.ToUpperInvariant(ch) : '_');
        }

        return builder.ToString();
    }
}
