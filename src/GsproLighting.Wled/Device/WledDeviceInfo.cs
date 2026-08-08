namespace GsproLighting.Wled.Device;

/// <summary>Parsed WLED <c>/json/info</c> snapshot.</summary>
public sealed class WledDeviceInfo
{
    public string Name { get; init; } = "WLED";
    public string Version { get; init; } = "";
    public int LedCount { get; init; }
    public int EffectCount { get; init; }
    public int PaletteCount { get; init; }
    public int MaxSegments { get; init; } = 1;
    public string Mac { get; init; } = "";
}
