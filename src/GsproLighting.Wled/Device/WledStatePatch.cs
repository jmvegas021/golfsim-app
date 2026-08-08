using GsproLighting.Core.Config;

namespace GsproLighting.Wled.Device;

/// <summary>
/// Partial WLED state write from the control surface.
/// Null members are omitted from the JSON payload.
/// </summary>
public sealed class WledStatePatch
{
    public bool? On { get; init; }
    public byte? Brightness { get; init; }
    public int? PresetId { get; init; }
    public int? PlaylistId { get; init; }
    public bool? NextPlaylist { get; init; }
    public bool? Live { get; init; }
    public int? SegmentId { get; init; }
    public int? FxId { get; init; }
    public int? Speed { get; init; }
    public int? Intensity { get; init; }
    public int? PaletteId { get; init; }
    public bool? Overlay { get; init; }
    public bool? Option2 { get; init; }
    public bool? Option3 { get; init; }
    public RgbColor? Primary { get; init; }
    public RgbColor? Secondary { get; init; }
    public RgbColor? Tertiary { get; init; }
}
