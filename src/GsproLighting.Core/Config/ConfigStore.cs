using System.Text.Json;
using System.Text.Json.Serialization;

namespace GsproLighting.Core.Config;

public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = null,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _path;

    public ConfigStore(string? path = null)
    {
        _path = path ?? ResolveDefaultPath();
    }

    public string Path => _path;

    public AppConfig Load()
    {
        if (!File.Exists(_path))
            return new AppConfig();

        var json = File.ReadAllText(_path);
        return JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
    }

    public void Save(AppConfig config)
    {
        var directory = System.IO.Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(_path, json);
    }

    private static string ResolveDefaultPath()
    {
        var candidates = new[]
        {
            System.IO.Path.Combine(Directory.GetCurrentDirectory(), "config", "appsettings.json"),
            System.IO.Path.Combine(AppContext.BaseDirectory, "config", "appsettings.json"),
            System.IO.Path.Combine(AppContext.BaseDirectory, "appsettings.json")
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        return candidates[0];
    }
}
