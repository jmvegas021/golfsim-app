namespace GsproLighting.Ui.Theme;

/// <summary>Short release notes surfaced in About / What’s New.</summary>
public static class WhatsNewNotes
{
    public const string Headline = "What’s new in this build";

    public static readonly string[] Bullets =
    [
        "Lighting commands fully override WLED UI state (presets, playlists, palettes, extra segments).",
        "Ready stays Chase + Aurora at max sx/ix; Not Ready stays red Chase (Default palette).",
        "Solids, tray test, and hit-direction fills post authoritative full-strip /json/state bodies.",
        "Each apply stops presets/playlists and clears leftover multi-segment layouts.",
    ];
}
