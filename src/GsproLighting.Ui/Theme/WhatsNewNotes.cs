namespace GsproLighting.Ui.Theme;

/// <summary>Short release notes surfaced in About / What’s New.</summary>
public static class WhatsNewNotes
{
    public const string Headline = "What’s new in this build";

    public static readonly string[] Bullets =
    [
        "Ready: edges-in intro, then full-strip Chase + Aurora at max speed/width (tt:0) — matches WLED UI.",
        "Not Ready: center-out expand, then full-strip red Chase + Red Reef at max sx/ix (tt:0).",
        "Lighting commands still fully override WLED UI state (presets, playlists, extra segments).",
        "Preview Ready / Not Ready buttons match the live BallReady / BallNotReady sequences.",
    ];
}
