using GsproLighting.Ui.Theme;

namespace GsproLighting.Ui.Controls;

/// <summary>Hero chrome — GSPro Lighting as the dominant brand signal.</summary>
public sealed class BrandHeader : Control
{
    public BrandHeader()
    {
        Dock = DockStyle.Top;
        Height = 96;
        DoubleBuffered = true;
        TabStop = false;
        AccessibleName = "GSPro Lighting";
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
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

    private void DrawMark(Graphics g)
    {
        var mark = new Rectangle(22, 22, 44, 44);
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
        using var titleFont = UiTheme.HeadingFont(26f, FontStyle.Bold);
        using var subtitleFont = UiTheme.BodyFont(10f);
        TextRenderer.DrawText(
            g,
            "GSPro Lighting",
            titleFont,
            new Rectangle(78, 16, Width - 100, 36),
            UiTheme.Text,
            TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);
        TextRenderer.DrawText(
            g,
            ProductCopy.BrandSubtitle,
            subtitleFont,
            new Rectangle(80, 54, Width - 100, 24),
            UiTheme.Muted,
            TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);
    }

    private void DrawStripe(Graphics g)
    {
        var stripe = new Rectangle(0, Height - 5, Width, 5);
        using var gradient = new System.Drawing.Drawing2D.LinearGradientBrush(
            stripe,
            UiTheme.Accent,
            UiTheme.Ready,
            System.Drawing.Drawing2D.LinearGradientMode.Horizontal);
        g.FillRectangle(gradient, stripe);
    }
}
