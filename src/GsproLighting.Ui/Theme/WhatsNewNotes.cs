namespace GsproLighting.Ui.Theme;

/// <summary>Short release notes surfaced in About / What’s New.</summary>
public static class WhatsNewNotes
{
    public const string Headline = "What’s new in this build";

    public static readonly string[] Bullets =
    [
        "Ready: DRGB sides→center fill, retract to a solid green top/center band, then DRGB keepalive hold.",
        "Not Ready: DRGB morph/expand to full red, then brightness breathe on DRGB (no HTTP chase fight).",
        "Hit directions stay HTTP; starting one cancels DRGB first so live mode hands back cleanly.",
        "Preview Ready / Not Ready match live BallReady / BallNotReady DRGB sequences.",
    ];
}
