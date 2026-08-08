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
    public string ControllerIp { get; set; } = "192.168.1.50";
    public int UdpPort { get; set; } = 21324;
    public int LedCount { get; set; } = 60;
    public byte Brightness { get; set; } = 180;
    public string Protocol { get; set; } = "drgb";

    /// <summary>
    /// When true, swap left/right marker mapping (LED 0 treated as golfer's right).
    /// </summary>
    public bool InvertLeftRight { get; set; }
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
}

/// <summary>
/// Auto-discover GSPro Connect logs and R50 network peers (native Connect path).
/// </summary>
public sealed class R50WatchConfig
{
    public bool AutoWatchEnabled { get; set; } = true;
    public int DiscoveryRefreshSeconds { get; set; } = 10;
}
