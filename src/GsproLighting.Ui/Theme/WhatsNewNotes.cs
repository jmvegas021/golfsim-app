namespace GsproLighting.Ui.Theme;

/// <summary>Short release notes surfaced in About / What’s New.</summary>
public static class WhatsNewNotes
{
    public const string Headline = "What’s new in this build";

    public static readonly string[] Bullets =
    [
        "Ready: one-shot edges-in → concentrate, then on-device Chase + Aurora on the center band.",
        "Not Ready: one-shot center-out expand, then on-device red Chase + Red Reef (no breathe loop).",
        "Lighting commands still fully override WLED UI state (presets, playlists, extra segments).",
        "Preview Ready / Not Ready buttons match the live BallReady / BallNotReady sequences.",
    ];
}
