namespace GsproLighting.Ui.Theme;

/// <summary>Short release notes surfaced in About / What’s New.</summary>
public static class WhatsNewNotes
{
    public const string Headline = "What’s new in this build";

    public static readonly string[] Bullets =
    [
        "Ready: sides-to-center fill, then retract to a solid green top/center band (FX 0 hold — not Chase).",
        "Not Ready: center-out expand, then full-strip red Chase + Red Reef at max sx/ix (tt:0).",
        "Lighting commands still fully override WLED UI state (presets, playlists, extra segments).",
        "Preview Ready / Not Ready buttons match the live BallReady / BallNotReady sequences.",
    ];
}
