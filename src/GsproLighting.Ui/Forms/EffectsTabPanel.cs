using GsproLighting.Core.Config;
using GsproLighting.Ui.Controls;
using GsproLighting.Ui.Theme;

namespace GsproLighting.Ui.Forms;

/// <summary>
/// Live bay status + runtime actions. Lighting colors are product-authored (read-only legend).
/// </summary>
public sealed class EffectsTabPanel : UserControl
{
    private readonly EffectConfig _productSlots = new();
    private readonly LedStripPreview _stripPreview = new();
    private readonly StatusChip _readyChip = new() { Width = 118, Height = 32 };
    private readonly StatusChip _serviceChip = new() { Width = 132, Height = 32 };
    private readonly Label _watchSummary = new()
    {
        AutoEllipsis = true,
        ForeColor = UiTheme.Muted,
        TextAlign = ContentAlignment.MiddleLeft,
        Dock = DockStyle.Fill
    };
    private readonly NightButton _save = NightButton.Create("Save settings", 140, isPrimary: true);
    private readonly NightButton _test = NightButton.Create("Test lights", 118);
    private readonly NightButton _idle = NightButton.Create("Idle glow", 110);
    private readonly NightButton _proxy = NightButton.Create("Start proxy", 124);
    private readonly EffectStateLegend _legend = new();
    private string _lastReadyText = string.Empty;
    private bool _stripOwnedByAction;

    public EffectsTabPanel()
    {
        Dock = DockStyle.Fill;
        BackColor = UiTheme.Background;
        Padding = new Padding(18, 12, 18, 14);
        Controls.Add(BuildRootLayout());
        WireEvents();
        SyncStripToReadyState("WAITING");
    }

    public event EventHandler? SaveRequested;
    public event EventHandler? TestRequested;
    public event EventHandler? IdleRequested;
    public event EventHandler? ProxyToggleRequested;

    /// <summary>Kept for SettingsForm wiring; Effects no longer raises per-slot previews.</summary>
#pragma warning disable CS0067 // Retained for SettingsForm API compatibility.
    public event EventHandler<EffectSlotPreviewEventArgs>? PreviewRequested;
#pragma warning restore CS0067

    public EffectSlot IdleSlot => _productSlots.Idle.Clone();
    public EffectSlot PureSlot => _productSlots.PureStrike.Clone();

    public void LoadConfig(EffectConfig config)
    {
        // Lighting slots are product-authored; thresholds live on Connection.
        ArgumentNullException.ThrowIfNull(config);
    }

    public void ApplyTo(EffectConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        config.ResetLightingSlotsToProductDefaults();
    }

    public void UpdateStatus(
        string readyText,
        Color readyColor,
        string serviceText,
        Color serviceColor,
        string summary,
        bool proxyRunning)
    {
        _readyChip.SetStatus(readyText, readyColor);
        _serviceChip.SetStatus(serviceText, serviceColor);
        _watchSummary.Text = summary;
        _watchSummary.ForeColor = UiTheme.Muted;
        _proxy.Text = proxyRunning ? "Stop proxy" : "Start proxy";

        if (!_stripOwnedByAction &&
            !string.Equals(readyText, _lastReadyText, StringComparison.Ordinal))
        {
            _lastReadyText = readyText;
            SyncStripToReadyState(readyText);
        }
    }

    public void ShowActionStatus(string message, bool isError = false)
    {
        _watchSummary.Text = message;
        _watchSummary.ForeColor = isError ? UiTheme.NotReady : UiTheme.Muted;
        if (message.EndsWith("sent.", StringComparison.Ordinal) ||
            message.Contains("WLED error", StringComparison.Ordinal))
            _stripOwnedByAction = false;
    }

    protected override void OnPaintBackground(PaintEventArgs e) =>
        UiTheme.FillNightBackground(e.Graphics, ClientRectangle);

    private Control BuildRootLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            BackColor = Color.Transparent
        };
        for (var row = 0; row < 5; row++)
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(BuildHeading(), 0, 0);
        root.Controls.Add(BuildStatus(), 0, 1);
        root.Controls.Add(_stripPreview, 0, 2);
        root.Controls.Add(BuildRuntimeActions(), 0, 3);
        root.Controls.Add(BuildLegendHeading(), 0, 4);
        root.Controls.Add(_legend, 0, 5);
        return root;
    }

    private Control BuildHeading()
    {
        var heading = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Margin = new Padding(0, 0, 0, 8),
            BackColor = Color.Transparent
        };
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        heading.Controls.Add(new TabSectionHeading
        {
            Dock = DockStyle.Top,
            Title = "Bay lighting",
            Subtitle = "Live status and runtime controls. Colors are product-authored."
        }, 0, 0);
        heading.Controls.Add(_save, 1, 0);
        _save.Anchor = AnchorStyles.Right | AnchorStyles.Top;
        _save.Margin = new Padding(8, 10, 0, 0);
        return heading;
    }

    private Control BuildStatus()
    {
        var status = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 44,
            ColumnCount = 3,
            Margin = new Padding(0, 0, 0, 10),
            BackColor = Color.Transparent
        };
        status.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 126));
        status.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        status.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        status.Controls.Add(_readyChip, 0, 0);
        status.Controls.Add(_serviceChip, 1, 0);
        status.Controls.Add(_watchSummary, 2, 0);
        return status;
    }

    private Control BuildRuntimeActions()
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 64,
            ColumnCount = 2,
            Margin = new Padding(0, 12, 0, 0),
            BackColor = Color.Transparent
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.Controls.Add(new Label
        {
            Text = "RUNTIME",
            Dock = DockStyle.Fill,
            ForeColor = UiTheme.Accent,
            Font = UiTheme.BodyFont(8.5f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Color.Transparent
        }, 0, 0);
        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent
        };
        actions.Controls.AddRange([_test, _idle, _proxy]);
        row.Controls.Add(actions, 1, 0);
        return row;
    }

    private static Control BuildLegendHeading()
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = UiTheme.SectionTitleRow,
            ColumnCount = 2,
            Margin = new Padding(0),
            BackColor = Color.Transparent
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        row.Controls.Add(new Label
        {
            Text = "STATE LEGEND",
            Dock = DockStyle.Fill,
            ForeColor = UiTheme.Accent,
            Font = UiTheme.BodyFont(8.5f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(0, 8, 0, 4),
            BackColor = Color.Transparent
        }, 0, 0);
        row.Controls.Add(new Label
        {
            Text = "Authored defaults · not editable",
            Dock = DockStyle.Fill,
            ForeColor = UiTheme.Muted,
            TextAlign = ContentAlignment.MiddleRight,
            BackColor = Color.Transparent
        }, 1, 0);
        return row;
    }

    private void WireEvents()
    {
        _save.Click += (_, _) => SaveRequested?.Invoke(this, EventArgs.Empty);
        _test.Click += (_, _) =>
        {
            _stripOwnedByAction = true;
            _stripPreview.Play(PureSlot);
            TestRequested?.Invoke(this, EventArgs.Empty);
        };
        _idle.Click += (_, _) =>
        {
            _stripOwnedByAction = true;
            _stripPreview.Play(IdleSlot, holdAfter: true);
            IdleRequested?.Invoke(this, EventArgs.Empty);
        };
        _proxy.Click += (_, _) => ProxyToggleRequested?.Invoke(this, EventArgs.Empty);
    }

    private void SyncStripToReadyState(string readyText)
    {
        switch (readyText.ToUpperInvariant())
        {
            case "READY":
                _stripPreview.HoldSolid(_productSlots.Idle.Color, intensity: 0.9, status: "Live · ready / idle");
                break;
            case "NOT READY":
                _stripPreview.HoldSolid(_productSlots.NotReady.Color, intensity: 0.33, status: "Live · not ready (dim)");
                break;
            default:
                _stripPreview.HoldSolid(_productSlots.Waiting.Color, intensity: 0.4, status: "Live · waiting");
                break;
        }
    }
}

public sealed class EffectSlotPreviewEventArgs(EffectSlot slot) : EventArgs
{
    public EffectSlot Slot { get; } = slot;
}
