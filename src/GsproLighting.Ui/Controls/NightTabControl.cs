using GsproLighting.Ui.Theme;

namespace GsproLighting.Ui.Controls;

/// <summary>Owner-draw tabs — 44px Ally targets, amber selection underline.</summary>
public sealed class NightTabControl : TabControl
{
    private int _hotIndex = -1;

    public NightTabControl()
    {
        Dock = DockStyle.Fill;
        DrawMode = TabDrawMode.OwnerDrawFixed;
        ItemSize = new Size(128, UiTheme.TouchMin);
        SizeMode = TabSizeMode.Fixed;
        // Wrap to a second row instead of running tabs off the edge of the window on
        // narrower/smaller-DPI displays — 7 tabs at 128px fixed width (896px) can exceed the
        // available width on compact bay-mounted screens.
        Multiline = true;
        Padding = new Point(14, 8);
        BackColor = UiTheme.Background;
        ForeColor = UiTheme.Text;
        Font = UiTheme.BodyFont(10f, FontStyle.Bold);
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, false);
        MouseMove += OnTabMouseMove;
        MouseLeave += (_, _) =>
        {
            if (_hotIndex < 0)
                return;
            _hotIndex = -1;
            Invalidate();
        };
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= TabCount)
            return;

        var isSelected = e.Index == SelectedIndex;
        var isHot = e.Index == _hotIndex;
        var bounds = GetTabRect(e.Index);
        var g = e.Graphics;

        if (isSelected)
            UiTheme.FillPanelSurface(g, bounds, raised: true);
        else
        {
            using var background = new SolidBrush(isHot ? UiTheme.Panel : UiTheme.Background);
            g.FillRectangle(background, bounds);
        }

        if (isSelected)
        {
            using var topGlow = new SolidBrush(Color.FromArgb(36, 212, 160, 23));
            g.FillRectangle(topGlow, bounds.Left, bounds.Top, bounds.Width, 3);
            UiTheme.DrawRimLight(g, bounds);
        }
        else if (isHot)
        {
            UiTheme.DrawHoverAmberRing(g, bounds, active: true);
        }

        using var font = UiTheme.BodyFont(10f, isSelected ? FontStyle.Bold : FontStyle.Regular);
        TextRenderer.DrawText(
            g,
            TabPages[e.Index].Text,
            font,
            bounds,
            isSelected ? UiTheme.Text : (isHot ? UiTheme.Text : UiTheme.Muted),
            TextFormatFlags.HorizontalCenter
                | TextFormatFlags.VerticalCenter
                | TextFormatFlags.EndEllipsis
                | TextFormatFlags.NoPrefix);

        if (isSelected)
        {
            using var accent = new SolidBrush(UiTheme.Accent);
            g.FillRectangle(accent, bounds.Left + 10, bounds.Bottom - 3, bounds.Width - 20, 3);
        }
        else
        {
            using var hairline = new Pen(UiTheme.Border);
            g.DrawLine(hairline, bounds.Left, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1);
        }
    }

    private void OnTabMouseMove(object? sender, MouseEventArgs e)
    {
        var next = -1;
        for (var i = 0; i < TabCount; i++)
        {
            if (!GetTabRect(i).Contains(e.Location))
                continue;
            next = i;
            break;
        }

        if (next == _hotIndex)
            return;
        _hotIndex = next;
        Invalidate();
    }
}
