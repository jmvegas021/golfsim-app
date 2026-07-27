using GsproLighting.Core.Config;

namespace GsproLighting.Ui.Controls;

public sealed class ColorSwatchButton : Button
{
    private RgbColor _color = RgbColor.FromRgb(128, 128, 128);

    public ColorSwatchButton()
    {
        FlatStyle = FlatStyle.Flat;
        Width = 88;
        Height = 28;
        Cursor = Cursors.Hand;
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
