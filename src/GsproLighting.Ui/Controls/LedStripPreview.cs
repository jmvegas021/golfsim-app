using GsproLighting.Core.Config;
using GsproLighting.Ui.Theme;

namespace GsproLighting.Ui.Controls;

public sealed class LedStripPreview : Control
{
    private const int PixelCount = 24;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 70 };
    private RgbColor _color = RgbColor.FromRgb(61, 220, 132);
    private string _animation = EffectAnimations.Solid;
    private int _frame;

    public LedStripPreview()
    {
        Height = 62;
        Dock = DockStyle.Top;
        BackColor = UiTheme.Console;
        DoubleBuffered = true;
        _timer.Tick += (_, _) =>
        {
            _frame++;
            Invalidate();
            if (_frame > 34)
                _timer.Stop();
        };
    }

    public void Play(EffectSlot slot)
    {
        _color = slot.Color;
        _animation = slot.Mode == EffectMode.WledPreset
            ? EffectAnimations.Flash
            : slot.Animation;
        _frame = 0;
        _timer.Start();
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _timer.Dispose();
        base.Dispose(disposing);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var gap = 4;
        var availableWidth = Math.Max(1, Width - 28 - (PixelCount - 1) * gap);
        var pixelWidth = Math.Max(4, availableWidth / PixelCount);
        var stripWidth = PixelCount * pixelWidth + (PixelCount - 1) * gap;
        var startX = Math.Max(14, (Width - stripWidth) / 2);

        for (var index = 0; index < PixelCount; index++)
        {
            var intensity = GetIntensity(index);
            var pixelColor = Color.FromArgb(
                Scale(_color.R, intensity),
                Scale(_color.G, intensity),
                Scale(_color.B, intensity));
            using var brush = new SolidBrush(pixelColor);
            e.Graphics.FillRectangle(brush, startX + index * (pixelWidth + gap), 21, pixelWidth, 20);
        }
    }

    private double GetIntensity(int index)
    {
        if (!_timer.Enabled)
            return 0.12;

        var progress = (_frame % 18) / 17d;
        return _animation switch
        {
            EffectAnimations.OutsideToCenter => IsOutsideToCenterLit(index, progress) ? 1 : 0.1,
            EffectAnimations.CenterToOutside => IsCenterToOutsideLit(index, progress) ? 1 : 0.1,
            EffectAnimations.MarkerLeft => MarkerIntensity(index, 4),
            EffectAnimations.MarkerRight => MarkerIntensity(index, PixelCount - 5),
            EffectAnimations.MarkerCenter or EffectAnimations.DirectionAuto =>
                MarkerIntensity(index, PixelCount / 2),
            EffectAnimations.Sweep => MarkerIntensity(index, (int)(progress * (PixelCount - 1))),
            EffectAnimations.Pulse => 0.18 + 0.82 * Math.Abs(Math.Sin(_frame * 0.24)),
            EffectAnimations.Flash => _frame % 6 < 3 ? 1 : 0.06,
            _ => 0.9
        };
    }

    private static bool IsOutsideToCenterLit(int index, double progress)
    {
        var distanceFromEdge = Math.Min(index, PixelCount - 1 - index);
        return distanceFromEdge <= progress * (PixelCount / 2);
    }

    private static bool IsCenterToOutsideLit(int index, double progress)
    {
        var distanceFromCenter = Math.Abs(index - (PixelCount - 1) / 2d);
        return distanceFromCenter <= progress * (PixelCount / 2);
    }

    private static double MarkerIntensity(int index, int center)
    {
        var distance = Math.Abs(index - center);
        return distance switch
        {
            0 => 1,
            1 => 0.65,
            2 => 0.28,
            _ => 0.08
        };
    }

    private static int Scale(byte value, double intensity) =>
        Math.Clamp((int)(value * intensity), 0, 255);
}
