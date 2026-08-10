namespace GsproLighting.Core.Config;

public sealed class AppConfig
{
    public GsproConfig Gspro { get; set; } = new();
    public WledConfig Wled { get; set; } = new();
    public EffectConfig Effects { get; set; } = new();
    public LoggingConfig Logging { get; set; } = new();
    public UiConfig Ui { get; set; } = new();
    public R50WatchConfig R50Watch { get; set; } = new();
}

public sealed class GsproConfig
{
    public string ListenHost { get; set; } = "127.0.0.1";
    public int ListenPort { get; set; } = 1921;
    public string UpstreamHost { get; set; } = "127.0.0.1";
    public int UpstreamPort { get; set; } = 921;
}

public sealed class WledConfig
{
    /// <summary>
    /// Legacy shipped placeholder — treated as "not configured" so we never keep POSTing to a
    /// dead/wrong host after an upgrade. Prefer an empty IP until Connection sets a real one.
    /// </summary>
    public const string DefaultControllerIp = "192.168.1.50";

    public string ControllerIp { get; set; } = string.Empty;

    /// <summary>Realtime pixel stream UDP port. DDP default is 4048 (WLED).</summary>
    public int UdpPort { get; set; } = 4048;

    public int LedCount { get; set; } = 60;
    public byte Brightness { get; set; } = 180;

    /// <summary>Realtime transport id — <c>ddp</c> (UDP :4048). Legacy <c>drgb</c> is unused.</summary>
    public string Protocol { get; set; } = "ddp";

    /// <summary>
    /// When true, swap left/right marker mapping (LED 0 treated as golfer's right).
    /// </summary>
    public bool InvertLeftRight { get; set; }

    /// <summary>
    /// False until the user (or scan) sets a real controller address — also false for the
    /// legacy placeholder <see cref="DefaultControllerIp"/>.
    /// </summary>
    public bool HasConfiguredController => IsConfiguredController(ControllerIp);

    public static bool IsConfiguredController(string? controllerIp)
    {
        var host = controllerIp?.Trim();
        if (string.IsNullOrEmpty(host) || host is "0.0.0.0")
            return false;
        return !string.Equals(host, DefaultControllerIp, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class LoggingConfig
{
    public string RawLogDirectory { get; set; } = "logs";
    public bool LogHeartbeats { get; set; }

    /// <summary>
    /// Days of log files to include when exporting a zip (default: today only).
    /// </summary>
    public int ExportIncludeDays { get; set; } = 1;
}

public sealed class UiConfig
{
    public bool StartMinimizedToTray { get; set; }
    public bool StartProxyOnLaunch { get; set; } = true;

    /// <summary>
    /// Registers/unregisters a per-user Windows Run-key autostart entry. Applied as an OS-level
    /// side effect on save, not just persisted — see WindowsStartupManager.
    /// </summary>
    public bool StartWithWindows { get; set; }
}

/// <summary>
/// Auto-discover GSPro Connect logs and R50 network peers (native Connect path).
/// </summary>
public sealed class R50WatchConfig
{
    public bool AutoWatchEnabled { get; set; } = true;
    public int DiscoveryRefreshSeconds { get; set; } = 10;
}
