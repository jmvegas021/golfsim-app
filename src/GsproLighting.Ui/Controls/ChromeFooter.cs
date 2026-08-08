using GsproLighting.Ui.Theme;
using GsproLighting.Ui.Updates;

namespace GsproLighting.Ui.Controls;

/// <summary>Bottom chrome — tab tip, version, About entry.</summary>
public sealed class ChromeFooter : Control
{
    private readonly NightButton _about = new()
    {
        Text = "About",
        Width = 96,
        IsPrimary = false
    };
    private string _tip = ProductCopy.PreviewHint;

    public ChromeFooter()
    {
        Height = 52;
        Dock = DockStyle.Bottom;
        DoubleBuffered = true;
        TabStop = false;
        _about.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _about.Click += (_, _) => AboutRequested?.Invoke(this, EventArgs.Empty);
        Controls.Add(_about);
        LayoutAbout();
    }

    public event EventHandler? AboutRequested;

    public void SetTip(string tip)
    {
        _tip = tip;
        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutAbout();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        UiTheme.FillPanelSurface(g, ClientRectangle, raised: false);
        using var top = new Pen(UiTheme.Border);
        g.DrawLine(top, 0, 0, Width, 0);

        using var tipFont = UiTheme.BodyFont(8.5f);
        using var versionFont = UiTheme.BodyFont(8.5f, FontStyle.Bold);
        var tipBounds = new Rectangle(16, 8, Math.Max(40, Width - 220), 20);
        TextRenderer.DrawText(
            g,
            _tip,
            tipFont,
            tipBounds,
            UiTheme.Muted,
            TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);

        var versionBounds = new Rectangle(16, 28, Math.Max(40, Width - 220), 18);
        TextRenderer.DrawText(
            g,
            $"GSPro Lighting  v{AppVersionInfo.Current}",
            versionFont,
            versionBounds,
            UiTheme.Accent,
            TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);
    }

    private void LayoutAbout()
    {
        _about.Location = new Point(Math.Max(8, Width - _about.Width - 16), 4);
    }
}
