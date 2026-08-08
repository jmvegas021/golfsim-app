using GsproLighting.Ui.Theme;

namespace GsproLighting.Ui.Controls;

/// <summary>Owner-draw button with amber/primary and panel/secondary night-bay chrome.</summary>
public sealed class NightButton : Button
{
    private bool _hovered;
    private bool _pressed;

    public NightButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        Cursor = Cursors.Hand;
        TabStop = true;
        MinimumSize = new Size(108, UiTheme.TouchMin);
        Height = UiTheme.TouchMin;
        Font = UiTheme.BodyFont(9.5f, FontStyle.Bold);
        ForeColor = UiTheme.Text;
        BackColor = UiTheme.Panel;
        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw,
            true);
    }

    public bool IsPrimary { get; set; }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hovered = false;
        _pressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
            _pressed = true;
        Invalidate();
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _pressed = false;
        Invalidate();
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

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
        var bounds = ClientRectangle;
        var fill = ResolveFill();
        using (var brush = new SolidBrush(fill))
            g.FillRectangle(brush, bounds);

        if (!IsPrimary && Enabled)
            UiTheme.DrawRimLight(g, bounds);

        UiTheme.DrawPanelBorder(g, bounds, focused: Focused, hovered: _hovered && !Focused);

        var textColor = IsPrimary
            ? UiTheme.Background
            : (Enabled ? UiTheme.Text : UiTheme.Muted);
        TextRenderer.DrawText(
            g,
            Text,
            Font,
            bounds,
            textColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    private Color ResolveFill()
    {
        if (!Enabled)
            return UiTheme.Console;
        if (IsPrimary)
        {
            if (_pressed)
                return UiTheme.AccentPressed;
            if (_hovered)
                return UiTheme.AccentHover;
            return UiTheme.Accent;
        }

        if (_pressed)
            return UiTheme.Console;
        if (_hovered || Focused)
            return UiTheme.PanelRaised;
        return UiTheme.Panel;
    }
}
