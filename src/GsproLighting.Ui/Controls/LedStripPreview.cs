using GsproLighting.Core.Config;
using GsproLighting.Ui.Theme;
using GsproLighting.Wled.Animations;

namespace GsproLighting.Ui.Controls;

/// <summary>On-screen LED strip with animation + hold-after-play for Preview/Effects.</summary>
public sealed class LedStripPreview : Control
{
    private const int PixelCount = 24;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 60 };
    private RgbColor _color = RgbColor.FromRgb(61, 220, 132);
    private string _animation = EffectAnimations.Solid;
    private AnimationDirection _direction = AnimationDirection.Center;
    private int _frame;
    private int _animationFrames = 18;
    private bool _holdAfter;
    private bool _isHolding;
    private double _holdIntensity = 0.9;
    private string _status = "Idle preview · choose a state";

    public LedStripPreview()
    {
        Height = 108;
        MinimumSize = new Size(200, 96);
        Dock = DockStyle.Top;
        DoubleBuffered = true;
        TabStop = false;
        AccessibleName = "LED strip preview";
        _timer.Tick += (_, _) => OnTimerTick();
    }

    public void Play(
        EffectSlot slot,
        AnimationDirection direction = AnimationDirection.Center,
        bool holdAfter = false,
        double holdIntensity = 0.9)
    {
        ArgumentNullException.ThrowIfNull(slot);
        _color = slot.Color;
        _animation = slot.Mode == EffectMode.WledPreset
            ? EffectAnimations.Flash
            : slot.Animation;
        _direction = direction;
        _holdAfter = holdAfter;
        _holdIntensity = Math.Clamp(holdIntensity, 0.05, 1);
        _isHolding = false;
        _animationFrames = ResolveAnimationFrames(_animation);
        _frame = 0;
        _status = "Previewing animation…";
        _timer.Start();
        Invalidate();
    }

    public void HoldSolid(RgbColor color, double intensity = 0.9, string? status = null)
    {
        _timer.Stop();
        _color = color;
        _animation = EffectAnimations.Solid;
        _holdAfter = true;
        _isHolding = true;
        _holdIntensity = Math.Clamp(intensity, 0.05, 1);
        _status = status ?? "Holding end color";
        Invalidate();
    }

    public void ClearToIdle(RgbColor idleColor) =>
        HoldSolid(idleColor, intensity: 0.9, status: "Stopped · holding ready / idle green");

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _timer.Dispose();
        base.Dispose(disposing);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using (var well = new SolidBrush(UiTheme.Console))
            g.FillRectangle(well, ClientRectangle);
        using (var border = new Pen(UiTheme.Border))
        {
            var edge = ClientRectangle;
            edge.Width--;
            edge.Height--;
            g.DrawRectangle(border, edge);
        }

        DrawHeader(g);
        DrawPixels(g);
        DrawLegend(g);
    }

    private void OnTimerTick()
    {
        _frame++;
        if (_frame >= _animationFrames)
        {
            _timer.Stop();
            if (_holdAfter)
            {
                _isHolding = true;
                _status = "Holding end color";
            }
            else
            {
                _isHolding = false;
                _status = "Idle preview · choose a state";
            }
        }

        Invalidate();
    }

    private void DrawHeader(Graphics graphics)
    {
        using var titleFont = UiTheme.BodyFont(9f, FontStyle.Bold);
        TextRenderer.DrawText(
            graphics,
            "STRIP PREVIEW",
            titleFont,
            new Rectangle(14, 8, Width - 28, 18),
            UiTheme.Accent,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
    }

    private void DrawPixels(Graphics graphics)
    {
        var gap = 3;
        var availableWidth = Math.Max(1, Width - 28 - (PixelCount - 1) * gap);
        var pixelWidth = Math.Max(4, availableWidth / PixelCount);
        var stripWidth = PixelCount * pixelWidth + (PixelCount - 1) * gap;
        var startX = Math.Max(14, (Width - stripWidth) / 2);
        const int y = 34;
        const int h = 22;

        for (var index = 0; index < PixelCount; index++)
        {
            var intensity = GetIntensity(index);
            var pixelColor = Color.FromArgb(
                Scale(_color.R, intensity),
                Scale(_color.G, intensity),
                Scale(_color.B, intensity));
            var rect = new Rectangle(startX + index * (pixelWidth + gap), y, pixelWidth, h);
            using var brush = new SolidBrush(pixelColor);
            graphics.FillRectangle(brush, rect);
            if (intensity > 0.4)
            {
                using var glow = new Pen(Color.FromArgb(70, pixelColor), 1);
                graphics.DrawRectangle(glow, rect);
            }
        }
    }

    private void DrawLegend(Graphics graphics)
    {
        var legendBounds = new Rectangle(14, 64, Width - 28, 16);
        using var legendFont = UiTheme.BodyFont(8f);
        TextRenderer.DrawText(graphics, "Left", legendFont, legendBounds, UiTheme.Muted,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        TextRenderer.DrawText(graphics, "Center", legendFont, legendBounds, UiTheme.Muted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        TextRenderer.DrawText(graphics, "Right", legendFont, legendBounds, UiTheme.Muted,
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
        TextRenderer.DrawText(
            graphics,
            _status,
            legendFont,
            new Rectangle(14, 84, Width - 28, 16),
            UiTheme.Muted,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
    }

    private double GetIntensity(int index)
    {
        if (_isHolding)
            return ResolveHoldIntensity(index);
        if (!_timer.Enabled)
            return 0.12;

        var progress = Math.Min(1, _frame / (double)Math.Max(1, _animationFrames - 1));
        return _animation switch
        {
            EffectAnimations.OutsideToCenter => IsOutsideToCenterLit(index, progress) ? 1 : 0.1,
            EffectAnimations.CenterToOutside => IsCenterToOutsideLit(index, progress) ? 1 : 0.1,
            EffectAnimations.MarkerLeft => MarkerIntensity(index, MarkerCenter(AnimationDirection.Left)),
            EffectAnimations.MarkerRight => MarkerIntensity(index, MarkerCenter(AnimationDirection.Right)),
            EffectAnimations.MarkerCenter => MarkerIntensity(index, MarkerCenter(AnimationDirection.Center)),
            EffectAnimations.DirectionAuto => MarkerIntensity(index, MarkerCenter(_direction)),
            EffectAnimations.Sweep => MarkerIntensity(index, (int)(progress * (PixelCount - 1))),
            EffectAnimations.Pulse => 0.18 + 0.82 * Math.Abs(Math.Sin(_frame * 0.24)),
            EffectAnimations.Flash => _frame % 6 < 3 ? 1 : 0.06,
            _ => 0.9
        };
    }

    private double ResolveHoldIntensity(int index)
    {
        if (_animation is EffectAnimations.MarkerLeft
            or EffectAnimations.MarkerRight
            or EffectAnimations.MarkerCenter
            or EffectAnimations.DirectionAuto)
        {
            var direction = _animation == EffectAnimations.DirectionAuto
                ? _direction
                : ParseMarkerDirection(_animation);
            return MarkerIntensity(index, MarkerCenter(direction)) * Math.Max(0.35, _holdIntensity);
        }

        return _holdIntensity;
    }

    private static AnimationDirection ParseMarkerDirection(string animation) =>
        animation switch
        {
            EffectAnimations.MarkerLeft => AnimationDirection.Left,
            EffectAnimations.MarkerRight => AnimationDirection.Right,
            _ => AnimationDirection.Center
        };

    private static int MarkerCenter(AnimationDirection direction) =>
        direction switch
        {
            AnimationDirection.Left => 4,
            AnimationDirection.Right => PixelCount - 5,
            _ => PixelCount / 2
        };

    private static int ResolveAnimationFrames(string animation) =>
        animation switch
        {
            EffectAnimations.OutsideToCenter or EffectAnimations.CenterToOutside => 18,
            EffectAnimations.Pulse => 20,
            EffectAnimations.Flash => 12,
            EffectAnimations.Sweep => 24,
            EffectAnimations.Solid => 4,
            _ => 10
        };

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

    private static double MarkerIntensity(int index, int center) =>
        Math.Abs(index - center) switch
        {
            0 => 1,
            1 => 0.65,
            2 => 0.28,
            _ => 0.08
        };

    private static int Scale(byte value, double intensity) =>
        Math.Clamp((int)(value * intensity), 0, 255);
}
