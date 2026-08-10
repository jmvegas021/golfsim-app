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
        AppConfig config;
        if (!File.Exists(_path))
            config = new AppConfig();
        else
        {
            var json = File.ReadAllText(_path);
            config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
        }

        Normalize(config);
        return config;
    }

    public void Save(AppConfig config)
    {
        Normalize(config);
        var directory = System.IO.Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(_path, json);
    }

    /// <summary>
    /// Paths + product lighting. Legacy/custom EffectSlot RGB from disk is accepted on
    /// deserialize, then rewritten to authored defaults. Thresholds, WLED, and connection
    /// settings are kept. Save persists the same normalized lighting.
    /// </summary>
    private static void Normalize(AppConfig config)
    {
        NormalizePaths(config);
        MigrateRealtimeTransportToDdp(config.Wled);
        config.Effects.ResetLightingSlotsToProductDefaults();
    }

    /// <summary>
    /// Ready/Not Ready now stream over DDP :4048. Upgrade leftover DRGB :21324 installs
    /// so Connection does not keep sending to the wrong realtime port.
    /// </summary>
    private static void MigrateRealtimeTransportToDdp(WledConfig wled)
    {
        var protocol = wled.Protocol?.Trim() ?? string.Empty;
        var isLegacyDrgb = protocol.Length == 0 ||
            string.Equals(protocol, "drgb", StringComparison.OrdinalIgnoreCase);

        if (!isLegacyDrgb)
            return;

        wled.Protocol = "ddp";
        if (wled.UdpPort is 21324 or 0)
            wled.UdpPort = 4048;
    }

    private static void NormalizePaths(AppConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.Logging.RawLogDirectory) ||
            !System.IO.Path.IsPathRooted(config.Logging.RawLogDirectory))
        {
            config.Logging.RawLogDirectory = AppPaths.LogsDirectory;
        }
    }

    private static string ResolveDefaultPath()
    {
        var preferred = AppPaths.ConfigFilePath;
        if (File.Exists(preferred))
            return preferred;

        var candidates = new[]
        {
            preferred,
            System.IO.Path.Combine(Directory.GetCurrentDirectory(), "config", "appsettings.json"),
            System.IO.Path.Combine(AppContext.BaseDirectory, "config", "appsettings.json"),
            System.IO.Path.Combine(AppContext.BaseDirectory, "appsettings.json")
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        return preferred;
    }
}
