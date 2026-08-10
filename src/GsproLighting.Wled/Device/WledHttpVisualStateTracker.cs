using GsproLighting.Core.Config;

namespace GsproLighting.Wled.Device;

/// <summary>Remembers the last solid color/brightness so Ready/Not Ready can morph in-place.</summary>
public sealed class WledHttpVisualStateTracker
{
    private readonly object _gate = new();
    private RgbColor? _color;
    private byte _brightness;
    private bool _hasSolid;

    public bool TryGetSolid(out RgbColor color, out byte brightness)
    {
        lock (_gate)
        {
            if (!_hasSolid || _color is null)
            {
                color = RgbColor.FromRgb(0, 0, 0);
                brightness = 0;
                return false;
            }

            color = RgbColor.FromRgb(_color.R, _color.G, _color.B);
            brightness = _brightness;
            return true;
        }
    }

    public void RememberSolid(RgbColor color, byte brightness)
    {
        lock (_gate)
        {
            _color = RgbColor.FromRgb(color.R, color.G, color.B);
            _brightness = brightness;
            _hasSolid = true;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _hasSolid = false;
            _color = null;
            _brightness = 0;
        }
    }
}
