namespace GsproLighting.Ui.Theme;

/// <summary>Short release notes surfaced in About / What’s New.</summary>
public static class WhatsNewNotes
{
    public const string Headline = "What’s new in this build";

    public static readonly string[] Bullets =
    [
        "Ready uses native WLED Chase + Aurora at max speed/intensity (smooth on-device).",
        "Not Ready stays red Chase at max sx/ix (Default palette — not Aurora).",
        "Ready↔Not Ready full-strip POSTs clear leftover center-band segments.",
        "Hit directions (Left / Center / Right) remain solid half-fills.",
    ];
}
