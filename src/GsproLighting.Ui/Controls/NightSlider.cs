using GsproLighting.Ui.Theme;

namespace GsproLighting.Ui.Controls;

/// <summary>Night-bay owner-draw slider — no stock white TrackBar chrome.</summary>
public sealed class NightSlider : Control
{
    private int _minimum;
    private int _maximum = 255;
    private int _value = 128;
    private bool _dragging;
    private bool _hovered;

    public NightSlider()
    {
        Height = UiTheme.TouchMin;
        MinimumSize = new Size(120, UiTheme.TouchMin);
        Cursor = Cursors.Hand;
        TabStop = true;
        DoubleBuffered = true;
        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw
            | ControlStyles.Selectable,
            true);
        AccessibleRole = AccessibleRole.Slider;
    }

    public int Minimum
    {
        get => _minimum;
        set
        {
            _minimum = value;
            if (_maximum < _minimum)
                _maximum = _minimum;
            Value = _value;
        }
    }

    public int Maximum
    {
        get => _maximum;
        set
        {
            _maximum = Math.Max(value, _minimum);
            Value = _value;
        }
    }

    public int Value
    {
        get => _value;
        set
        {
            var next = Math.Clamp(value, _minimum, _maximum);
            if (next == _value)
                return;
            _value = next;
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? ValueChanged;

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var track = TrackBounds;
        using (var well = new SolidBrush(UiTheme.Console))
            g.FillRectangle(well, track);
        using (var edge = new Pen(UiTheme.Border))
            g.DrawRectangle(edge, track.X, track.Y, track.Width - 1, track.Height - 1);

        var fillWidth = ThumbCenterX - track.Left;
        if (fillWidth > 0)
        {
            var fill = new Rectangle(track.Left, track.Top, fillWidth, track.Height);
            using var accent = new SolidBrush(
                Enabled ? UiTheme.Accent : UiTheme.BorderStrong);
            g.FillRectangle(accent, fill);
        }

        var thumb = ThumbBounds;
        using (var thumbFill = new SolidBrush(ResolveThumbColor()))
            g.FillRectangle(thumbFill, thumb);
        UiTheme.DrawPanelBorder(g, thumb, focused: Focused, hovered: _hovered && !Focused);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hovered = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && Enabled)
        {
            Focus();
            _dragging = true;
            SetValueFromX(e.X);
            Capture = true;
        }

        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_dragging)
            SetValueFromX(e.X);
        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (_dragging)
        {
            _dragging = false;
            Capture = false;
            Invalidate();
        }

        base.OnMouseUp(e);
    }

    protected override void OnGotFocus(EventArgs e)
    {
        Invalidate();
        base.OnGotFocus(e);
    }

    protected override void OnLostFocus(EventArgs e)
    {
        Invalidate();
        base.OnLostFocus(e);
    }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Home or Keys.End
        || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!Enabled)
        {
            base.OnKeyDown(e);
            return;
        }

        var step = Math.Max(1, (_maximum - _minimum) / 40);
        switch (e.KeyCode)
        {
            case Keys.Left:
            case Keys.Down:
                Value -= step;
                e.Handled = true;
                break;
            case Keys.Right:
            case Keys.Up:
                Value += step;
                e.Handled = true;
                break;
            case Keys.Home:
                Value = _minimum;
                e.Handled = true;
                break;
            case Keys.End:
                Value = _maximum;
                e.Handled = true;
                break;
        }

        base.OnKeyDown(e);
    }

    private Color ResolveThumbColor()
    {
        if (!Enabled)
            return UiTheme.Muted;
        if (_dragging)
            return UiTheme.AccentPressed;
        if (_hovered || Focused)
            return UiTheme.AccentHover;
        return UiTheme.Text;
    }

    private Rectangle TrackBounds
    {
        get
        {
            var y = Math.Max(0, (Height - 10) / 2);
            return new Rectangle(10, y, Math.Max(20, Width - 20), 10);
        }
    }

    private int ThumbCenterX
    {
        get
        {
            var track = TrackBounds;
            var span = _maximum - _minimum;
            if (span <= 0)
                return track.Left;
            var ratio = (_value - _minimum) / (float)span;
            return track.Left + (int)Math.Round(ratio * track.Width);
        }
    }

    private Rectangle ThumbBounds
    {
        get
        {
            const int size = 22;
            var x = Math.Clamp(ThumbCenterX - size / 2, 2, Math.Max(2, Width - size - 2));
            var y = Math.Max(0, (Height - size) / 2);
            return new Rectangle(x, y, size, size);
        }
    }

    private void SetValueFromX(int x)
    {
        var track = TrackBounds;
        if (track.Width <= 0)
            return;
        var ratio = Math.Clamp((x - track.Left) / (float)track.Width, 0f, 1f);
        Value = _minimum + (int)Math.Round(ratio * (_maximum - _minimum));
    }
}
