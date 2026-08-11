using GsproLighting.Core.Config;
using GsproLighting.Ui.Theme;

namespace GsproLighting.Ui.Forms;

/// <summary>
/// Compact per-state Speed / Intensity / Layers (and band %) controls for status lights.
/// Values are multipliers as percent (100 = product default).
/// </summary>
public sealed class StatusEffectTuningPanel : UserControl
{
    private readonly StateCard _ready = new("Ready", showBandSize: true);
    private readonly StateCard _notReady = new("Not Ready", showBandSize: false);
    private readonly StateCard _direction = new("Direction (L/C/R)", showBandSize: true);
    private readonly StateCard _waiting = new("Loading / Waiting", showBandSize: false);

    public StatusEffectTuningPanel()
    {
        Dock = DockStyle.Top;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = Color.Transparent;
        Controls.Add(BuildLayout());
    }

    public void LoadConfig(StatusEffectTuning tuning)
    {
        ArgumentNullException.ThrowIfNull(tuning);
        _ready.Load(tuning.Ready);
        _notReady.Load(tuning.NotReady);
        _direction.Load(tuning.Direction);
        _waiting.Load(tuning.Waiting);
    }

    public void ApplyTo(StatusEffectTuning tuning)
    {
        ArgumentNullException.ThrowIfNull(tuning);
        _ready.ApplyTo(tuning.Ready);
        _notReady.ApplyTo(tuning.NotReady);
        _direction.ApplyTo(tuning.Direction);
        _waiting.ApplyTo(tuning.Waiting);
        tuning.ClampAll();
    }

    public StatusEffectTuning Capture()
    {
        var tuning = new StatusEffectTuning();
        ApplyTo(tuning);
        return tuning;
    }

    private Control BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 8)
        };
        root.Controls.Add(UiTheme.CreateSectionLabel("Status light tuning"), 0, 0);
        root.Controls.Add(new Label
        {
            Text = "Moderate Speed / Intensity / Layers (100 = product default). Band % for Ready & Direction only.",
            AutoSize = true,
            MaximumSize = new Size(640, 0),
            ForeColor = UiTheme.Muted,
            Margin = new Padding(0, 0, 0, 8),
            BackColor = Color.Transparent
        }, 0, 1);

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 2,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 4)
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.Controls.Add(_ready, 0, 0);
        grid.Controls.Add(_notReady, 1, 0);
        grid.Controls.Add(_direction, 0, 1);
        grid.Controls.Add(_waiting, 1, 1);
        root.Controls.Add(grid, 0, 2);
        return root;
    }

    private sealed class StateCard : Panel
    {
        private readonly NumericUpDown _speed = PercentBox();
        private readonly NumericUpDown _intensity = PercentBox();
        private readonly NumericUpDown _layers = PercentBox();
        private readonly NumericUpDown? _band;

        public StateCard(string title, bool showBandSize)
        {
            Width = 300;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            BackColor = UiTheme.Panel;
            Padding = new Padding(10, 8, 10, 10);
            Margin = new Padding(0, 0, 10, 10);
            BorderStyle = BorderStyle.FixedSingle;

            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Color.Transparent
            };
            flow.Controls.Add(new Label
            {
                Text = title,
                AutoSize = true,
                ForeColor = UiTheme.Accent,
                Font = UiTheme.BodyFont(9f, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 6),
                BackColor = Color.Transparent
            });
            flow.Controls.Add(Field("Speed %", _speed));
            flow.Controls.Add(Field("Intensity %", _intensity));
            flow.Controls.Add(Field("Layers %", _layers));
            if (showBandSize)
            {
                _band = BandBox();
                flow.Controls.Add(Field("Band size %", _band));
            }

            Controls.Add(flow);
        }

        public void Load(StatusEffectStateTuning tuning)
        {
            _speed.Value = ToPercent(tuning.Speed);
            _intensity.Value = ToPercent(tuning.Intensity);
            _layers.Value = ToPercent(tuning.Layers);
            if (_band is not null)
                _band.Value = (decimal)Math.Clamp(
                    tuning.BandSizePercent,
                    StatusEffectStateTuning.MinBandSizePercent,
                    StatusEffectStateTuning.MaxBandSizePercent);
        }

        public void ApplyTo(StatusEffectStateTuning tuning)
        {
            tuning.Speed = FromPercent(_speed.Value);
            tuning.Intensity = FromPercent(_intensity.Value);
            tuning.Layers = FromPercent(_layers.Value);
            if (_band is not null)
                tuning.BandSizePercent = (double)_band.Value;
        }

        private static Control Field(string label, NumericUpDown input)
        {
            UiTheme.StyleInput(input);
            var row = new TableLayoutPanel
            {
                Width = 270,
                Height = UiTheme.TouchMin,
                ColumnCount = 2,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 0, 4)
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            row.Controls.Add(new Label
            {
                Text = label,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = UiTheme.Text,
                BackColor = Color.Transparent
            }, 0, 0);
            row.Controls.Add(input, 1, 0);
            input.Dock = DockStyle.Fill;
            return row;
        }

        private static NumericUpDown PercentBox() => new()
        {
            Minimum = 25,
            Maximum = 300,
            DecimalPlaces = 0,
            Increment = 5,
            Value = 100,
            Width = 100
        };

        private static NumericUpDown BandBox() => new()
        {
            Minimum = (decimal)StatusEffectStateTuning.MinBandSizePercent,
            Maximum = (decimal)StatusEffectStateTuning.MaxBandSizePercent,
            DecimalPlaces = 0,
            Increment = 1,
            Value = (decimal)StatusEffectStateTuning.DefaultBandSizePercent,
            Width = 100
        };

        private static decimal ToPercent(double multiplier) =>
            (decimal)Math.Clamp(
                Math.Round(multiplier * 100),
                25,
                300);

        private static double FromPercent(decimal percent) =>
            (double)percent / 100.0;
    }
}
