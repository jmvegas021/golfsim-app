using GsproLighting.Core.Config;
using GsproLighting.Ui.Theme;

namespace GsproLighting.Ui.Controls;

public sealed class ColorSwatchButton : Button
{
    private RgbColor _color = RgbColor.FromRgb(128, 128, 128);

    public ColorSwatchButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderColor = UiTheme.Border;
        Width = 132;
        Height = 38;
        Font = UiTheme.BodyFont(9f, FontStyle.Bold);
        Cursor = Cursors.Hand;
        AccessibleName = "Effect color";
        Click += (_, _) => PickColor();
    }

    public RgbColor SelectedColor
    {
        get => _color;
        set
        {
            _color = value;
            BackColor = Color.FromArgb(value.R, value.G, value.B);
            ForeColor = value.R + value.G + value.B > 360 ? Color.Black : Color.White;
            Text = value.ToString();
        }
    }

    private void PickColor()
    {
        using var dialog = new ColorDialog
        {
            Color = BackColor,
            FullOpen = true
        };

        if (dialog.ShowDialog() != DialogResult.OK)
            return;

        SelectedColor = RgbColor.FromRgb(dialog.Color.R, dialog.Color.G, dialog.Color.B);
    }
}
