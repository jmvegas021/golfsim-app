namespace GsproLighting.Wled.Device;

/// <summary>Parsed WLED <c>/json/state</c> snapshot used by the control surface.</summary>
public sealed class WledDeviceState
{
    public bool On { get; init; } = true;
    public byte Brightness { get; init; } = 128;
    public int PresetId { get; init; } = -1;
    public int PlaylistId { get; init; } = -1;
    public bool Live { get; init; }
    public int MainSegmentId { get; init; }
    public IReadOnlyList<WledSegmentState> Segments { get; init; } = [];

    public WledSegmentState MainSegment =>
        Segments.FirstOrDefault(s => s.Id == MainSegmentId)
        ?? Segments.FirstOrDefault()
        ?? new WledSegmentState();

    public WledDeviceState Clone() => new()
    {
        On = On,
        Brightness = Brightness,
        PresetId = PresetId,
        PlaylistId = PlaylistId,
        Live = Live,
        MainSegmentId = MainSegmentId,
        Segments = Segments.Select(s => s.Clone()).ToArray()
    };
}
