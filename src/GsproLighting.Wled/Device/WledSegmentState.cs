using GsproLighting.Core.Config;

namespace GsproLighting.Wled.Device;

/// <summary>Selected segment fields from <c>/json/state</c>.</summary>
public sealed class WledSegmentState
{
    public int Id { get; init; }
    public int Start { get; init; }
    public int Stop { get; init; }
    public int FxId { get; init; }
    public int Speed { get; init; } = 128;
    public int Intensity { get; init; } = 128;
    public int PaletteId { get; init; }
    public bool Overlay { get; init; }
    public bool Option2 { get; init; }
    public bool Option3 { get; init; }
    public bool On { get; init; } = true;
    public byte Brightness { get; init; } = 255;
    public RgbColor Primary { get; init; } = RgbColor.FromRgb(255, 255, 255);
    public RgbColor Secondary { get; init; } = RgbColor.FromRgb(0, 0, 0);
    public RgbColor Tertiary { get; init; } = RgbColor.FromRgb(0, 0, 0);

    public WledSegmentState Clone() => new()
    {
        Id = Id,
        Start = Start,
        Stop = Stop,
        FxId = FxId,
        Speed = Speed,
        Intensity = Intensity,
        PaletteId = PaletteId,
        Overlay = Overlay,
        Option2 = Option2,
        Option3 = Option3,
        On = On,
        Brightness = Brightness,
        Primary = RgbColor.FromRgb(Primary.R, Primary.G, Primary.B),
        Secondary = RgbColor.FromRgb(Secondary.R, Secondary.G, Secondary.B),
        Tertiary = RgbColor.FromRgb(Tertiary.R, Tertiary.G, Tertiary.B)
    };
}
