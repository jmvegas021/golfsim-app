using GsproLighting.Ui.Theme;

namespace GsproLighting.Ui.Controls;

public sealed class NightTabControl : TabControl
{
    public NightTabControl()
    {
        Dock = DockStyle.Fill;
        DrawMode = TabDrawMode.OwnerDrawFixed;
        ItemSize = new Size(130, 42);
        SizeMode = TabSizeMode.Fixed;
        Padding = new Point(18, 6);
        BackColor = UiTheme.Background;
        ForeColor = UiTheme.Text;
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        var isSelected = e.Index == SelectedIndex;
        var bounds = GetTabRect(e.Index);
        using var background = new SolidBrush(UiTheme.Background);
        using var textBrush = new SolidBrush(isSelected ? UiTheme.Text : UiTheme.Muted);
        using var font = UiTheme.BodyFont(10f, isSelected ? FontStyle.Bold : FontStyle.Regular);
        e.Graphics.FillRectangle(background, bounds);

        TextRenderer.DrawText(
            e.Graphics,
            TabPages[e.Index].Text,
            font,
            bounds,
            textBrush.Color,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

        if (isSelected)
        {
            using var accent = new SolidBrush(UiTheme.Accent);
            e.Graphics.FillRectangle(accent, bounds.Left + 12, bounds.Bottom - 3, bounds.Width - 24, 3);
        }
    }
}
