namespace GsproLighting.Ui.Theme;

/// <summary>Short release notes surfaced in About / What’s New.</summary>
public static class WhatsNewNotes
{
    public const string Headline = "What’s new in this build";

    public static readonly string[] Bullets =
    [
        "Live bay ready/idle/not-ready holds refresh over DRGB so WLED won’t drop to the playlist after ~5s.",
        "Lighting colors are product defaults — Load/Save normalize slots; thresholds and WLED stay yours.",
        "Preview lab tests sequences on-screen and WLED; end colors hold (Stop = ready green).",
        "No per-phase color editors — tune connection and thresholds, not the palette.",
    ];
}
