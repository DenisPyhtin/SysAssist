namespace SysAssist.Api;

internal static class DotEnv
{
    public static void LoadFromNearestFile(params string[] fileNames)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                foreach (var fileName in fileNames)
                {
                    var path = Path.Combine(directory.FullName, fileName);
                    if (visited.Add(path) && File.Exists(path))
                    {
                        Load(path);
                    }
                }

                directory = directory.Parent;
            }
        }
    }

    private static void Load(string path)
    {
        foreach (var line in File.ReadAllLines(path))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            var separator = trimmed.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var key = trimmed[..separator].Trim();
            var value = trimmed[(separator + 1)..].Trim();
            if ((value.StartsWith('"') && value.EndsWith('"')) || (value.StartsWith('\'') && value.EndsWith('\'')))
            {
                value = value[1..^1];
            }

            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key)))
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}
