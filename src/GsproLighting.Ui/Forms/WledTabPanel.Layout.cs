using GsproLighting.Ui.Controls;
using GsproLighting.Ui.Theme;

namespace GsproLighting.Ui.Forms;

public sealed partial class WledTabPanel
{
    private void BuildLayout()
    {
        StyleChecks();

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var scroll = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 0, 8, 0)
        };
        var stack = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 0, 4, 12)
        };
        scroll.Resize += (_, _) =>
        {
            var width = Math.Max(320, scroll.ClientSize.Width - 4);
            if (stack.Width != width)
                stack.Width = width;
        };

        stack.Controls.Add(new TabSectionHeading
        {
            Dock = DockStyle.Top,
            Title = "WLED",
            Subtitle = ProductCopy.WledTabIntro
        });
        stack.Controls.Add(BuildConnectionSection());
        stack.Controls.Add(BuildQuickActionsSection());
        stack.Controls.Add(_empty);

        _editorHost.Dock = DockStyle.Top;
        _editorHost.Controls.Add(BuildEditorStack());
        stack.Controls.Add(_editorHost);
        stack.Controls.Add(BuildFooterNote());

        scroll.Controls.Add(stack);
        root.Controls.Add(scroll, 0, 0);
        root.Controls.Add(BuildStickyActions(), 0, 1);
        Controls.Add(root);
    }

    private Control BuildEditorStack()
    {
        var stack = VerticalStack(DockStyle.Top);
        stack.Controls.Add(BuildLookSection());
        stack.Controls.Add(BuildMotionSection());
        stack.Controls.Add(BuildColorsSection());
        stack.Controls.Add(BuildOptionsSection());
        stack.Controls.Add(BuildBrightnessSection());
        stack.Controls.Add(BuildPresetsSection());
        return stack;
    }

    private Control BuildConnectionSection()
    {
        var body = VerticalStack(DockStyle.Top);

        _connectionStatus.AutoSize = true;
        _connectionStatus.ForeColor = UiTheme.Text;
        _connectionStatus.Font = UiTheme.BodyFont(10f, FontStyle.Bold);
        _connectionStatus.Margin = new Padding(0, 0, 0, 4);
        _connectionStatus.Padding = new Padding(0, 4, 0, 4);

        _deviceMeta.AutoSize = true;
        _deviceMeta.ForeColor = UiTheme.Muted;
        _deviceMeta.Margin = new Padding(0, 0, 0, 8);

        var row = WrapFlow(new Padding(0, 0, 0, 4));
        _refresh.Margin = new Padding(0, 0, 8, 4);
        row.Controls.Add(_refresh);

        body.Controls.Add(_connectionStatus);
        body.Controls.Add(_deviceMeta);
        body.Controls.Add(row);
        return Section("Connection", ProductCopy.WledConnectionHelp, body);
    }

    private Control BuildQuickActionsSection()
    {
        var row = WrapFlow();
        _ambient.Margin = new Padding(0, 0, 8, 6);
        _openWled.Margin = new Padding(0, 0, 8, 6);
        row.Controls.Add(_ambient);
        row.Controls.Add(_openWled);
        return Section("Quick actions", ProductCopy.WledQuickActionsHelp, row);
    }

    private Control BuildLookSection()
    {
        var row = new TableLayoutPanel
        {
            ColumnCount = 2,
            AutoSize = true,
            Dock = DockStyle.Top,
            BackColor = Color.Transparent
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        row.Controls.Add(CatalogBlock("Effect", _effects), 0, 0);
        row.Controls.Add(CatalogBlock("Palette", _palettes), 1, 0);
        return Section("Look", ProductCopy.WledLookHelp, row);
    }

    private Control BuildMotionSection()
    {
        var stack = VerticalStack();
        stack.Controls.Add(SliderRow("Speed", _speed, _speedValue));
        stack.Controls.Add(SliderRow("Intensity", _intensity, _intensityValue));
        return Section("Motion", ProductCopy.WledMotionHelp, stack);
    }

    private Control BuildColorsSection()
    {
        _colors.Height = UiTheme.TouchComfort + 8;
        _colors.Dock = DockStyle.Top;
        return Section("Colors", ProductCopy.WledColorsHelp, _colors);
    }

    private Control BuildOptionsSection()
    {
        var row = WrapFlow();
        foreach (var box in new[] { _overlay, _option2, _option3 })
        {
            box.Margin = new Padding(0, 0, 16, 4);
            row.Controls.Add(box);
        }

        var optionTip = "Meaning depends on the selected effect — check WLED's own effect " +
                         "description for what this toggle does.";
        _optionsToolTip.SetToolTip(_option2, optionTip);
        _optionsToolTip.SetToolTip(_option3, optionTip);

        return Section("Options", ProductCopy.WledOptionsHelp, row);
    }

    private Control BuildBrightnessSection()
    {
        var stack = VerticalStack();
        _power.Margin = new Padding(0, 0, 0, 8);
        stack.Controls.Add(_power);
        stack.Controls.Add(SliderRow("Brightness", _brightness, _brightnessValue));

        var segRow = WrapFlow(new Padding(0, 8, 0, 0));
        segRow.Controls.Add(FieldLabel("Segment", width: 90, height: UiTheme.TouchComfort));
        _segments.Height = UiTheme.TouchComfort;
        segRow.Controls.Add(_segments);
        stack.Controls.Add(segRow);
        return Section("Power, brightness & segment", ProductCopy.WledBrightnessHelp, stack);
    }

    private Control BuildPresetsSection()
    {
        var row = WrapFlow();
        _presets.Height = UiTheme.TouchComfort;
        _presets.Margin = new Padding(0, 0, 8, 6);
        _applyPreset.Margin = new Padding(0, 0, 8, 6);
        _playlistNext.Margin = new Padding(0, 0, 8, 6);
        _playlistLabel.AutoSize = true;
        _playlistLabel.ForeColor = UiTheme.Muted;
        _playlistLabel.Margin = new Padding(4, 14, 0, 0);
        row.Controls.Add(_presets);
        row.Controls.Add(_applyPreset);
        row.Controls.Add(_playlistNext);
        row.Controls.Add(_playlistLabel);
        return Section("Presets", ProductCopy.WledPresetsHelp, row);
    }

    private Control BuildStickyActions()
    {
        var bar = new Panel
        {
            Dock = DockStyle.Fill,
            Height = 64,
            Padding = new Padding(0, 8, 0, 0),
            BackColor = Color.Transparent
        };
        bar.Paint += (_, e) =>
        {
            using var pen = new Pen(UiTheme.Border);
            e.Graphics.DrawLine(pen, 0, 0, bar.Width, 0);
        };

        var row = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 4, 0, 0)
        };
        _apply.Margin = new Padding(0, 0, 8, 0);
        _revert.Margin = new Padding(0, 0, 8, 0);
        row.Controls.Add(_apply);
        row.Controls.Add(_revert);
        bar.Controls.Add(row);
        return bar;
    }

    private static Control BuildFooterNote() =>
        UiTheme.CreateHelpLabel(ProductCopy.WledDrgbNote);

    private static Control Section(string title, string help, Control body)
    {
        var shell = new SurfacePanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 0, 0, UiTheme.SpacingLg),
            Padding = new Padding(14, 12, 14, 14)
        };
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            BackColor = Color.Transparent
        };
        panel.Controls.Add(UiTheme.CreateSectionLabel(title));
        panel.Controls.Add(UiTheme.CreateHelpLabel(help, maxWidth: 780));
        body.Dock = DockStyle.Top;
        panel.Controls.Add(body);
        shell.Controls.Add(panel);
        return shell;
    }

    private static Control CatalogBlock(string title, WledCatalogPicker picker)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 1,
            Margin = new Padding(0, 0, 8, 0),
            BackColor = Color.Transparent
        };
        panel.Controls.Add(FieldLabel(title, height: 28, dock: DockStyle.Top, bottomPadding: 2));
        picker.Dock = DockStyle.Top;
        picker.Height = 220;
        panel.Controls.Add(picker);
        return panel;
    }

    private static Control SliderRow(string title, NightSlider slider, Label value)
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            Margin = new Padding(0, 0, 0, 8),
            BackColor = Color.Transparent
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));

        row.Controls.Add(FieldLabel(title, dock: DockStyle.Fill), 0, 0);
        slider.Dock = DockStyle.Fill;
        slider.Height = UiTheme.TouchComfort;
        row.Controls.Add(slider, 1, 0);
        value.Dock = DockStyle.Fill;
        value.ForeColor = UiTheme.Muted;
        value.TextAlign = ContentAlignment.MiddleRight;
        value.Padding = new Padding(0, 4, 0, 4);
        row.Controls.Add(value, 2, 0);
        return row;
    }

    private void StyleChecks()
    {
        foreach (var box in new[] { _power, _overlay, _option2, _option3 })
        {
            UiTheme.StyleCheckBox(box);
            box.AutoSize = true;
        }
    }

    private static TableLayoutPanel VerticalStack(DockStyle dock = DockStyle.None) =>
        new()
        {
            Dock = dock,
            AutoSize = true,
            ColumnCount = 1,
            BackColor = Color.Transparent
        };

    private static FlowLayoutPanel WrapFlow(Padding? margin = null)
    {
        var row = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = true,
            BackColor = Color.Transparent
        };
        if (margin.HasValue)
            row.Margin = margin.Value;
        return row;
    }

    private static Label FieldLabel(
        string text,
        int? width = null,
        int? height = null,
        DockStyle dock = DockStyle.None,
        int bottomPadding = 4)
    {
        var label = new Label
        {
            Text = text,
            Dock = dock,
            ForeColor = UiTheme.Text,
            Font = UiTheme.BodyFont(9.5f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(0, 4, 0, bottomPadding)
        };
        if (width.HasValue || height.HasValue)
        {
            label.AutoSize = false;
            if (width.HasValue)
                label.Width = width.Value;
            if (height.HasValue)
                label.Height = height.Value;
        }

        return label;
    }
}
