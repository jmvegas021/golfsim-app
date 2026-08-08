using GsproLighting.Ui.Theme;

namespace GsproLighting.Ui.Controls;

/// <summary>High-contrast status pill for ready/service indicators.</summary>
public sealed class StatusChip : Control
{
    private string _label = "—";
    private Color _fill = UiTheme.Muted;

    public StatusChip()
    {
        DoubleBuffered = true;
        Size = new Size(120, 32);
        MinimumSize = new Size(88, 28);
        TabStop = false;
        AccessibleRole = AccessibleRole.StaticText;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
    }

    public void SetStatus(string label, Color fill)
    {
        _label = label;
        _fill = fill;
        AccessibleName = label;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
        using var fill = new SolidBrush(_fill);
        g.FillRectangle(fill, bounds);
        using var edge = new Pen(Color.FromArgb(70, 0, 0, 0));
        g.DrawRectangle(edge, bounds);
        using var highlight = new Pen(Color.FromArgb(50, 255, 255, 255));
        g.DrawLine(highlight, 1, 1, Width - 3, 1);
        using var font = UiTheme.BodyFont(8.5f, FontStyle.Bold);
        TextRenderer.DrawText(
            g,
            _label,
            font,
            ClientRectangle,
            UiTheme.Background,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}
