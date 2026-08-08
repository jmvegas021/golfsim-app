using GsproLighting.Ui.Theme;

namespace GsproLighting.Ui.Controls;

/// <summary>Hero chrome — GSPro Lighting as the dominant brand signal.</summary>
public sealed class BrandHeader : Control
{
    public BrandHeader()
    {
        Dock = DockStyle.Top;
        Height = 118;
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
        var mark = new Rectangle(22, 28, 44, 44);
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
        // Bahnschrift needs generous vertical room — tight rects clip ascenders on Ally/150% DPI.
        using var titleFont = UiTheme.HeadingFont(24f, FontStyle.Bold);
        using var subtitleFont = UiTheme.BodyFont(10f);
        TextRenderer.DrawText(
            g,
            "GSPro Lighting",
            titleFont,
            new Rectangle(78, 22, Math.Max(40, Width - 100), 44),
            UiTheme.Text,
            TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        TextRenderer.DrawText(
            g,
            ProductCopy.BrandSubtitle,
            subtitleFont,
            new Rectangle(80, 68, Math.Max(40, Width - 100), 28),
            UiTheme.Muted,
            TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
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
