using GsproLighting.Ui.Theme;

namespace GsproLighting.Ui.Controls;

public sealed class BrandHeader : Control
{
    public BrandHeader()
    {
        Dock = DockStyle.Top;
        Height = 82;
        BackColor = UiTheme.Background;
        DoubleBuffered = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        using var titleFont = UiTheme.BodyFont(18f, FontStyle.Bold);
        using var subtitleFont = UiTheme.BodyFont(9.5f);
        using var titleBrush = new SolidBrush(UiTheme.Text);
        using var subtitleBrush = new SolidBrush(UiTheme.Muted);
        e.Graphics.DrawString("GSPro Lighting", titleFont, titleBrush, 22, 13);
        e.Graphics.DrawString("WLED bay lights", subtitleFont, subtitleBrush, 24, 47);

        var stripe = new Rectangle(0, Height - 4, Width, 4);
        using var gradient = new System.Drawing.Drawing2D.LinearGradientBrush(
            stripe,
            UiTheme.Accent,
            UiTheme.Ready,
            System.Drawing.Drawing2D.LinearGradientMode.Horizontal);
        e.Graphics.FillRectangle(gradient, stripe);
    }
}
