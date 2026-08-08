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
        MinimumSize = new Size(108, UiTheme.TouchComfort);
        if (Height < UiTheme.TouchComfort)
            Height = UiTheme.TouchComfort;
        Padding = new Padding(14, 8, 14, 8);
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

    /// <summary>When true, long labels wrap inside the button instead of clipping.</summary>
    public bool WrapText { get; set; }

    /// <summary>
    /// Builds a button that auto-grows to fit its own text (via <see cref="GetPreferredSize"/>) —
    /// <paramref name="width"/>/<paramref name="height"/> are floors, not caps. Only use this for
    /// buttons living in a FlowLayoutPanel or otherwise-unconstrained cell; a fixed-column
    /// TableLayoutPanel can visually overlap a sibling if the button grows past its cell.
    /// </summary>
    public static NightButton Create(
        string text,
        int width,
        int? height = null,
        bool isPrimary = false,
        bool wrapText = false,
        bool enabled = true) => new()
    {
        Text = text,
        MinimumSize = new Size(width, height ?? UiTheme.TouchComfort),
        IsPrimary = isPrimary,
        WrapText = wrapText,
        Enabled = enabled,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink
    };

    /// <summary>
    /// Text-driven size floor so a caller opting into <see cref="Control.AutoSize"/> never gets a
    /// button narrower/shorter than its own label needs (avoids silent EndEllipsis truncation).
    /// </summary>
    public override Size GetPreferredSize(Size proposedSize)
    {
        if (string.IsNullOrEmpty(Text))
            return base.GetPreferredSize(proposedSize);

        var constraintWidth = WrapText && proposedSize.Width > 0
            ? Math.Max(1, proposedSize.Width - Padding.Horizontal)
            : int.MaxValue;
        var flags = TextFormatFlags.NoPrefix | (WrapText ? TextFormatFlags.WordBreak : TextFormatFlags.SingleLine);
        var textSize = TextRenderer.MeasureText(
            Text,
            Font,
            new Size(constraintWidth, int.MaxValue),
            flags);

        var width = Math.Max(MinimumSize.Width, textSize.Width + Padding.Horizontal);
        var height = Math.Max(MinimumSize.Height, textSize.Height + Padding.Vertical);
        return new Size(width, height);
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
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
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
        var textBounds = new Rectangle(
            bounds.X + Padding.Left,
            bounds.Y + Padding.Top,
            Math.Max(8, bounds.Width - Padding.Horizontal),
            Math.Max(8, bounds.Height - Padding.Vertical));
        var flags = TextFormatFlags.HorizontalCenter
            | TextFormatFlags.VerticalCenter
            | TextFormatFlags.NoPrefix
            | TextFormatFlags.EndEllipsis;
        if (WrapText)
            flags |= TextFormatFlags.WordBreak;
        TextRenderer.DrawText(g, Text, Font, textBounds, textColor, flags);
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
