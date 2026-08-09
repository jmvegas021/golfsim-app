using GsproLighting.Ui.Theme;

namespace GsproLighting.Ui.Controls;

/// <summary>Hero chrome — GSPro Lighting as the dominant brand signal.</summary>
public sealed class BrandHeader : Control
{
    private const int TopPadding = 20;
    private const int BottomPadding = 18;
    private const int StripeHeight = 5;
    private const int MarkSize = 44;
    private const int ContentLeft = 22;
    private const int TextLeft = 78;
    private const int TitleSubtitleGap = 4;

    private int _titleHeight;
    private int _subtitleHeight;

    public BrandHeader()
    {
        Dock = DockStyle.Top;
        DoubleBuffered = true;
        TabStop = false;
        AccessibleName = "GSPro Lighting";
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
        RecalcHeight();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        UiTheme.FillNightBackground(g, ClientRectangle);
        DrawMark(g);
        DrawCopy(g);
        DrawStripe(g);
    }

    /// <summary>
    /// Measures the actual title/subtitle font metrics instead of guessing fixed pixel rect
    /// heights — a hardcoded 44px rect for 24pt bold Bahnschrift clipped descenders on some
    /// DPI/font-rendering combinations even with generous-looking numbers.
    /// </summary>
    private void RecalcHeight()
    {
        using var titleFont = UiTheme.HeadingFont(24f, FontStyle.Bold);
        using var subtitleFont = UiTheme.BodyFont(10f);
        _titleHeight = MeasureLineHeight("GSPro Lighting", titleFont);
        _subtitleHeight = MeasureLineHeight(ProductCopy.BrandSubtitle, subtitleFont);

        var textBlockHeight = _titleHeight + TitleSubtitleGap + _subtitleHeight;
        var contentHeight = Math.Max(MarkSize, textBlockHeight);
        var next = TopPadding + contentHeight + BottomPadding + StripeHeight;
        if (Height != next)
            Height = next;
    }

    private static int MeasureLineHeight(string text, Font font) =>
        TextRenderer.MeasureText(
            text,
            font,
            new Size(int.MaxValue, int.MaxValue),
            TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine).Height;

    private void DrawMark(Graphics g)
    {
        var textBlockHeight = _titleHeight + TitleSubtitleGap + _subtitleHeight;
        var markY = TopPadding + Math.Max(0, (textBlockHeight - MarkSize) / 2);
        var mark = new Rectangle(ContentLeft, markY, MarkSize, MarkSize);
        using var fill = new System.Drawing.Drawing2D.LinearGradientBrush(
            mark,
            UiTheme.Accent,
            UiTheme.Ready,
            System.Drawing.Drawing2D.LinearGradientMode.ForwardDiagonal);
        g.FillRectangle(fill, mark);
        UiTheme.DrawRimLight(g, mark);
        using var edge = new Pen(UiTheme.BorderStrong);
        g.DrawRectangle(edge, mark.X, mark.Y, mark.Width - 1, mark.Height - 1);
        using var led = new SolidBrush(UiTheme.Background);
        for (var i = 0; i < 4; i++)
            g.FillRectangle(led, mark.X + 7 + i * 8, mark.Y + 18, 5, 8);
    }

    private void DrawCopy(Graphics g)
    {
        using var titleFont = UiTheme.HeadingFont(24f, FontStyle.Bold);
        using var subtitleFont = UiTheme.BodyFont(10f);
        var textWidth = Math.Max(40, Width - TextLeft - 20);

        // NoClipping is defense-in-depth: even measured heights can be off by a hair across
        // DPI/font-rendering combinations, and GDI hard-clips to the rect by default otherwise.
        var flags = TextFormatFlags.Left
            | TextFormatFlags.Top
            | TextFormatFlags.EndEllipsis
            | TextFormatFlags.NoPrefix
            | TextFormatFlags.NoClipping;

        TextRenderer.DrawText(
            g,
            "GSPro Lighting",
            titleFont,
            new Rectangle(TextLeft, TopPadding, textWidth, _titleHeight),
            UiTheme.Text,
            flags);
        TextRenderer.DrawText(
            g,
            ProductCopy.BrandSubtitle,
            subtitleFont,
            new Rectangle(TextLeft, TopPadding + _titleHeight + TitleSubtitleGap, textWidth, _subtitleHeight),
            UiTheme.Muted,
            flags);
    }

    private void DrawStripe(Graphics g)
    {
        var stripe = new Rectangle(0, Height - StripeHeight, Width, StripeHeight);
        using var gradient = new System.Drawing.Drawing2D.LinearGradientBrush(
            stripe,
            UiTheme.Accent,
            UiTheme.Ready,
            System.Drawing.Drawing2D.LinearGradientMode.Horizontal);
        g.FillRectangle(gradient, stripe);
    }
}
