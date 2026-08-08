using GsproLighting.Ui.Theme;

namespace GsproLighting.Ui.Controls;

/// <summary>Inset well empty-state banner for first-run / waiting copy.</summary>
public sealed class EmptyStateBanner : Control
{
    private string _title = string.Empty;
    private string _body = string.Empty;
    private bool _accentWaiting;

    public EmptyStateBanner()
    {
        DoubleBuffered = true;
        TabStop = false;
        Height = 88;
        MinimumSize = new Size(240, 72);
        AccessibleRole = AccessibleRole.StaticText;
        Visible = false;
    }

    public void ShowMessage(string title, string body, bool waitingAccent = false)
    {
        _title = title;
        _body = body;
        _accentWaiting = waitingAccent;
        AccessibleName = $"{title}. {body}";
        Visible = true;
        RecalcHeight();
        Invalidate();
    }

    public void HideMessage()
    {
        Visible = false;
        _title = string.Empty;
        _body = string.Empty;
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        RecalcHeight();
    }

    private void RecalcHeight()
    {
        if (string.IsNullOrEmpty(_body))
            return;

        var width = Math.Max(160, Width - 28);
        using var bodyFont = UiTheme.BodyFont(9f);
        var measured = TextRenderer.MeasureText(
            _body,
            bodyFont,
            new Size(width, int.MaxValue),
            TextFormatFlags.WordBreak);
        var next = Math.Max(72, 48 + measured.Height + 12);
        if (Height != next)
            Height = next;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        UiTheme.FillInsetWell(g, ClientRectangle);

        var accent = _accentWaiting ? UiTheme.Waiting : UiTheme.Accent;
        using var bar = new SolidBrush(accent);
        g.FillRectangle(bar, 0, 0, 4, Height);

        using var titleFont = UiTheme.BodyFont(10.5f, FontStyle.Bold);
        using var bodyFont = UiTheme.BodyFont(9f);
        using var titleBrush = new SolidBrush(UiTheme.Text);
        using var bodyBrush = new SolidBrush(UiTheme.Muted);

        var titleBounds = new Rectangle(16, 12, Width - 28, 22);
        var bodyBounds = new Rectangle(16, 36, Width - 28, Math.Max(24, Height - 48));
        TextRenderer.DrawText(g, _title, titleFont, titleBounds, titleBrush.Color, TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(
            g,
            _body,
            bodyFont,
            bodyBounds,
            bodyBrush.Color,
            TextFormatFlags.WordBreak);
    }
}
