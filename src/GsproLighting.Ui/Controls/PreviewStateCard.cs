using GsproLighting.Core.Config;
using GsproLighting.Core.Preview;
using GsproLighting.Ui.Theme;

namespace GsproLighting.Ui.Controls;

/// <summary>Premium preview state card — display-only color, no editors, no emoji.</summary>
public sealed class PreviewStateCard : Control
{
    private readonly LightingPreviewItem _item;
    private bool _isSelected;
    private bool _hovered;

    public PreviewStateCard(LightingPreviewItem item)
    {
        _item = item ?? throw new ArgumentNullException(nameof(item));
        Height = 80;
        MinimumSize = new Size(200, UiTheme.TouchMin);
        Margin = new Padding(0, 0, 0, 10);
        Cursor = Cursors.Hand;
        TabStop = true;
        DoubleBuffered = true;
        AccessibleName = item.Title;
        AccessibleRole = AccessibleRole.PushButton;
        AccessibleDescription = item.Description;
    }

    public event EventHandler? Selected;

    public LightingPreviewItem Item => _item;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;
            _isSelected = value;
            Invalidate();
        }
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

    protected override void OnClick(EventArgs e)
    {
        RaiseSelected();
        base.OnClick(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Enter or Keys.Space)
        {
            RaiseSelected();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        base.OnKeyDown(e);
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
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var raised = _isSelected || _hovered || Focused;
        UiTheme.FillPanelSurface(g, ClientRectangle, raised);

        var swatch = new Rectangle(16, (Height - 40) / 2, 40, 40);
        using (var colorBrush = new SolidBrush(ToDrawingColor(_item.Slot.Color)))
            g.FillRectangle(colorBrush, swatch);
        using (var glow = new Pen(Color.FromArgb(100, _item.Slot.Color.R, _item.Slot.Color.G, _item.Slot.Color.B), 2))
            g.DrawRectangle(glow, swatch);

        using var titleFont = UiTheme.BodyFont(11f, FontStyle.Bold);
        using var bodyFont = UiTheme.BodyFont(8.5f);
        var textLeft = swatch.Right + 14;
        var textWidth = Width - textLeft - 36;
        TextRenderer.DrawText(
            g,
            _item.Title,
            titleFont,
            new Rectangle(textLeft, 14, textWidth, 26),
            UiTheme.Text,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(
            g,
            _item.Description,
            bodyFont,
            new Rectangle(textLeft, 40, textWidth, 28),
            UiTheme.Muted,
            TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);

        DrawPlayMark(g);

        var borderColor = _isSelected || Focused
            ? UiTheme.Accent
            : (_hovered ? UiTheme.BorderStrong : UiTheme.Border);
        using var border = new Pen(borderColor, _isSelected || Focused ? 2 : 1);
        var edge = ClientRectangle;
        edge.Width--;
        edge.Height--;
        g.DrawRectangle(border, edge);

        if (_isSelected)
        {
            using var accent = new SolidBrush(UiTheme.Accent);
            g.FillRectangle(accent, 0, 10, 3, Height - 20);
        }

        UiTheme.DrawFocusRing(g, ClientRectangle, Focused && !_isSelected);
    }

    private void DrawPlayMark(Graphics g)
    {
        var cx = Width - 22;
        var cy = Height / 2;
        Point[] triangle =
        [
            new(cx - 5, cy - 7),
            new(cx - 5, cy + 7),
            new(cx + 7, cy)
        ];
        var alpha = _isSelected || _hovered ? 220 : 120;
        using var brush = new SolidBrush(Color.FromArgb(alpha, UiTheme.Accent));
        g.FillPolygon(brush, triangle);
    }

    private void RaiseSelected() => Selected?.Invoke(this, EventArgs.Empty);

    private static Color ToDrawingColor(RgbColor color) =>
        Color.FromArgb(color.R, color.G, color.B);
}
