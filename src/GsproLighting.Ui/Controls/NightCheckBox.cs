using GsproLighting.Ui.Theme;

namespace GsproLighting.Ui.Controls;

/// <summary>
/// Owner-draw checkbox with night-bay chrome. Stock FlatStyle checkmarks vanish on dark
/// Transparent backgrounds — this paints a bordered box and high-contrast tick instead.
/// </summary>
public sealed class NightCheckBox : CheckBox
{
    private const int IndicatorSize = 18;
    private const int IndicatorTextGap = 10;
    private bool _hovered;

    public NightCheckBox()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        BackColor = Color.Transparent;
        ForeColor = UiTheme.Text;
        Cursor = Cursors.Hand;
        Font = UiTheme.BodyFont();
        MinimumSize = new Size(0, UiTheme.TouchMin);
        Height = Math.Max(Height, UiTheme.TouchMin);
        AutoSize = false;
        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw
            | ControlStyles.SupportsTransparentBackColor,
            true);
        AccessibleRole = AccessibleRole.CheckButton;
    }

    public static NightCheckBox Create(string text, int width = 560) => new()
    {
        Text = text,
        Width = width,
        Height = UiTheme.TouchMin,
        Margin = new Padding(0, 2, 0, 2)
    };

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // Let WinForms composite through Transparent onto the night-bay parent chrome.
        base.OnPaintBackground(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var indicator = IndicatorBounds;
        DrawIndicator(g, indicator);
        DrawLabel(g, indicator);
        if (Focused)
            UiTheme.DrawFocusRing(g, ClientRectangle, focused: true);
    }

    protected override void OnCheckedChanged(EventArgs e)
    {
        Invalidate();
        base.OnCheckedChanged(e);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        Invalidate();
        base.OnEnabledChanged(e);
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

    private Rectangle IndicatorBounds
    {
        get
        {
            var y = Math.Max(0, (ClientSize.Height - IndicatorSize) / 2);
            return new Rectangle(Padding.Left, y, IndicatorSize, IndicatorSize);
        }
    }

    private void DrawIndicator(Graphics g, Rectangle box)
    {
        var fill = ResolveBoxFill();
        using (var brush = new SolidBrush(fill))
            g.FillRectangle(brush, box);

        var border = Focused
            ? UiTheme.FocusRing
            : (Checked && Enabled ? UiTheme.Accent : (_hovered ? UiTheme.BorderStrong : UiTheme.Border));
        using (var pen = new Pen(border))
        {
            var edge = box;
            edge.Width--;
            edge.Height--;
            g.DrawRectangle(pen, edge);
        }

        if (!Checked)
            return;

        using var tick = new Pen(Enabled ? UiTheme.Background : UiTheme.Muted, 2.2f)
        {
            StartCap = System.Drawing.Drawing2D.LineCap.Round,
            EndCap = System.Drawing.Drawing2D.LineCap.Round,
            LineJoin = System.Drawing.Drawing2D.LineJoin.Round
        };
        var x1 = box.Left + 4;
        var y1 = box.Top + box.Height / 2;
        var x2 = box.Left + box.Width / 2 - 1;
        var y2 = box.Bottom - 5;
        var x3 = box.Right - 4;
        var y3 = box.Top + 4;
        Point[] tickPath = [new Point(x1, y1), new Point(x2, y2), new Point(x3, y3)];
        g.DrawLines(tick, tickPath);
    }

    private void DrawLabel(Graphics g, Rectangle indicator)
    {
        if (string.IsNullOrEmpty(Text))
            return;

        var textBounds = new Rectangle(
            indicator.Right + IndicatorTextGap,
            0,
            Math.Max(8, ClientSize.Width - indicator.Right - IndicatorTextGap - Padding.Right),
            ClientSize.Height);
        var color = Enabled ? UiTheme.Text : UiTheme.Muted;
        TextRenderer.DrawText(
            g,
            Text,
            Font,
            textBounds,
            color,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }

    private Color ResolveBoxFill()
    {
        if (!Enabled)
            return UiTheme.Console;
        if (Checked)
            return UiTheme.Accent;
        return _hovered ? UiTheme.PanelRaised : UiTheme.Panel;
    }
}
