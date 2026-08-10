namespace GsproLighting.Ui.Theme;

/// <summary>Short release notes surfaced in About / What’s New.</summary>
public static class WhatsNewNotes
{
    public const string Headline = "What’s new in this build";

    public static readonly string[] Bullets =
    [
        "Ready is a one-shot edges-in → center concentrate → full-strip solid green (no Chase loop).",
        "Not Ready morphs to red, expands center-out, then breathes full-strip red.",
        "Lighting commands still fully override WLED UI state (presets, playlists, extra segments).",
        "Preview Ready / Not Ready buttons match the live BallReady / BallNotReady sequences.",
    ];
}
