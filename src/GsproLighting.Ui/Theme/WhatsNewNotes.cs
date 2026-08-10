namespace GsproLighting.Ui.Theme;

/// <summary>Short release notes surfaced in About / What’s New.</summary>
public static class WhatsNewNotes
{
    public const string Headline = "What’s new in this build";

    public static readonly string[] Bullets =
    [
        "Fix Preview Ready/Not Ready DRGB: sync Connection IP/port/LED count into UDP before streaming (HTTP solids already worked).",
        "Surface DRGB send failures with host:port:LED count instead of a silent “running” status.",
        "Fix WLED network scan when the PC NIC reports a wide mask (/16 etc.): clamp to the host’s /24 and use a longer probe timeout.",
        "Scan miss was unrelated to the v0.8.22 DRGB move — discovery was not changed in that release.",
    ];
}
