namespace GsproLighting.Ui.Theme;

/// <summary>Short release notes surfaced in About / What’s New.</summary>
public static class WhatsNewNotes
{
    public const string Headline = "What’s new in this build";

    public static readonly string[] Bullets =
    [
        "New WLED tab: live effects, palettes, colors, speed/intensity, presets, and Sync ambient.",
        "Basic idle/waiting ambient is Ripple + Red Reef (layered, colors max, timing 15%).",
        "Shot/ready/not-ready flashes still play over DRGB, then return to Ripple ambient via HTTP.",
        "Open full WLED from the tab for matrix/audio/Wi‑Fi and other controller-only settings.",
    ];
}
