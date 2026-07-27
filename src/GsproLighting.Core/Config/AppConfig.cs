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
}

public sealed class EffectConfig
{
    public RgbColor Idle { get; set; } = RgbColor.FromRgb(20, 80, 40);
    public RgbColor PureStrike { get; set; } = RgbColor.FromRgb(0, 220, 80);
    public RgbColor Mishit { get; set; } = RgbColor.FromRgb(180, 40, 30);
    public RgbColor Putt { get; set; } = RgbColor.FromRgb(80, 140, 220);
    public RgbColor Celebrate { get; set; } = RgbColor.FromRgb(255, 210, 40);
    public RgbColor Hazard { get; set; } = RgbColor.FromRgb(220, 20, 20);
    public RgbColor Player { get; set; } = RgbColor.FromRgb(40, 160, 255);

    public double PuttMaxBallSpeedMph { get; set; } = 20;
    public double PureMinSmashFactor { get; set; } = 1.45;
    public double MishitMaxSmashFactor { get; set; } = 1.25;
}

public sealed class RgbColor
{
    public byte R { get; set; }
    public byte G { get; set; }
    public byte B { get; set; }

    public static RgbColor FromRgb(byte r, byte g, byte b) => new() { R = r, G = g, B = b };

    public override string ToString() => $"#{R:X2}{G:X2}{B:X2}";
}

public sealed class LoggingConfig
{
    public string RawLogDirectory { get; set; } = "logs";
    public bool LogHeartbeats { get; set; }
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
