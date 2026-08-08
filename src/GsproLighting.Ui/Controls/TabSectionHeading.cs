using GsproLighting.Ui.Theme;

namespace GsproLighting.Ui.Controls;

/// <summary>
/// Title + optional subtitle with enough vertical room for Bahnschrift at 100–150% DPI.
/// </summary>
public sealed class TabSectionHeading : Control
{
    private string _title = string.Empty;
    private string _subtitle = string.Empty;

    public TabSectionHeading()
    {
        DoubleBuffered = true;
        TabStop = false;
        MinimumSize = new Size(120, 56);
        Margin = new Padding(0, 0, 0, UiTheme.SpacingMd);
        SetStyle(
            ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.ResizeRedraw
            | ControlStyles.SupportsTransparentBackColor,
            true);
        BackColor = Color.Transparent;
    }

    public string Title
    {
        get => _title;
        set
        {
            _title = value ?? string.Empty;
            AccessibleName = _title;
            RecalcHeight();
            Invalidate();
        }
    }

    public string Subtitle
    {
        get => _subtitle;
        set
        {
            _subtitle = value ?? string.Empty;
            RecalcHeight();
            Invalidate();
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        RecalcHeight();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        using var titleFont = UiTheme.HeadingFont(15f, FontStyle.Bold);
        using var bodyFont = UiTheme.BodyFont(9.5f);
        var titleBounds = new Rectangle(0, 6, Math.Max(1, Width), 34);
        TextRenderer.DrawText(
            g,
            _title,
            titleFont,
            titleBounds,
            UiTheme.Text,
            TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

        if (string.IsNullOrWhiteSpace(_subtitle))
            return;

        var subtitleBounds = new Rectangle(0, 40, Math.Max(1, Width), Math.Max(24, Height - 44));
        TextRenderer.DrawText(
            g,
            _subtitle,
            bodyFont,
            subtitleBounds,
            UiTheme.Muted,
            TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }

    private void RecalcHeight()
    {
        var width = Math.Max(120, Width);
        using var bodyFont = UiTheme.BodyFont(9.5f);
        var subtitleHeight = 0;
        if (!string.IsNullOrWhiteSpace(_subtitle))
        {
            var measured = TextRenderer.MeasureText(
                _subtitle,
                bodyFont,
                new Size(width, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            subtitleHeight = Math.Max(24, measured.Height + 4);
        }

        var next = 46 + subtitleHeight;
        if (Height != next)
            Height = next;
    }
}
