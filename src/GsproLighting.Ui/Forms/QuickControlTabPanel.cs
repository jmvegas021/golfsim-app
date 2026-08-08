using GsproLighting.Core.Config;
using GsproLighting.Core.Preview;
using GsproLighting.Ui.Controls;
using GsproLighting.Ui.Preview;
using GsproLighting.Ui.Theme;
using GsproLighting.Wled;
using GsproLighting.Wled.Animations;
using GsproLighting.Wled.Device;

namespace GsproLighting.Ui.Forms;

/// <summary>
/// One-tap "home screen" in the style of the WLED app — quick-switch buttons for the 11 bay
/// lighting states, a tap-grid of the connected device's own saved presets, and prominent
/// power/brightness. Purely a UI composition over existing preview playback
/// (<see cref="PreviewPlaybackCoordinator"/>) and device-client (<see cref="WledDeviceClient"/>)
/// code — no new WLED communication logic.
/// </summary>
public sealed class QuickControlTabPanel : UserControl
{
    private static readonly HashSet<string> PreviewOnlyIds =
    [
        LightingPreviewIds.Celebrate,
        LightingPreviewIds.Hazard,
        LightingPreviewIds.Water,
        LightingPreviewIds.OutOfBounds
    ];

    private readonly Func<EffectConfig> _resolveEffects;
    private readonly Func<WledConfig> _resolveWled;
    private readonly Func<string> _getControllerIp;
    private readonly LedStripPreview _strip = new() { Height = 64 };
    private readonly LightingPreviewCatalog _catalog = new();
    private readonly PreviewPlaybackCoordinator _coordinator;
    private readonly WledDeviceClient _deviceClient = new();
    private readonly FlowLayoutPanel _stateGrid = WrapFlow();
    private readonly FlowLayoutPanel _presetGrid = WrapFlow();
    private readonly EmptyStateBanner _presetsEmpty = new() { Width = 640, Margin = new Padding(0, 4, 0, 4) };
    private readonly Label _status = new();
    private readonly CheckBox _power = new() { Text = "Power on" };
    private readonly NightSlider _brightness = new() { Minimum = 1, Maximum = 255, Width = 260 };
    private readonly Label _brightnessValue = new();
    private readonly List<NightButton> _stateButtons = [];
    private int _statusGeneration;
    private CancellationTokenSource? _previewCts;
    private bool _loadingPower;

    public QuickControlTabPanel(
        Func<EffectConfig> resolveEffects,
        Func<WledConfig> resolveWled,
        Func<string> getControllerIp,
        WledPreviewPlayer player)
    {
        _resolveEffects = resolveEffects;
        _resolveWled = resolveWled;
        _getControllerIp = getControllerIp;
        _coordinator = new PreviewPlaybackCoordinator(player, _strip);

        Dock = DockStyle.Fill;
        BackColor = UiTheme.Background;
        Padding = new Padding(18, 14, 18, 14);
        Font = UiTheme.BodyFont();

        BuildLayout();
        ReloadStateButtons();
        WireEvents();
    }

    public void RefreshFromEffects() => ReloadStateButtons();

    protected override void OnPaintBackground(PaintEventArgs e) =>
        UiTheme.FillNightBackground(e.Graphics, ClientRectangle);

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (Visible)
            _ = RefreshPresetsAndPowerAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _deviceClient.Dispose();
        base.Dispose(disposing);
    }

    private void BuildLayout()
    {
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
            Title = "Quick control",
            Subtitle = "One-tap toggles — like the WLED app's home screen. Full detail lives in " +
                       "Preview and WLED tabs."
        });

        _strip.Dock = DockStyle.Top;
        stack.Controls.Add(_strip);

        _status.Dock = DockStyle.Top;
        _status.Height = UiTheme.TouchMin - 4;
        _status.ForeColor = UiTheme.Muted;
        _status.Font = UiTheme.BodyFont(9.5f);
        _status.TextAlign = ContentAlignment.MiddleLeft;
        _status.Margin = new Padding(0, 6, 0, 0);
        _status.Text = "Tap a state to preview it on the real strip.";
        stack.Controls.Add(_status);

        stack.Controls.Add(Section("States", "Preview-only states never fire from live GSPro data.", _stateGrid));
        stack.Controls.Add(Section("WLED presets", "Your saved presets on this controller.", BuildPresetsBody()));
        stack.Controls.Add(Section("Power", "Applies immediately to the connected controller.", BuildPowerBody()));

        scroll.Controls.Add(stack);
        Controls.Add(scroll);
    }

    private Control BuildPresetsBody()
    {
        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            BackColor = Color.Transparent
        };
        body.Controls.Add(_presetsEmpty);
        body.Controls.Add(_presetGrid);
        _presetsEmpty.ShowMessage("No presets yet", "Refresh once the controller is connected, or save presets in WLED.");
        return body;
    }

    private Control BuildPowerBody()
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            BackColor = Color.Transparent
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60));

        UiTheme.StyleCheckBox(_power);
        _power.Margin = new Padding(0, 6, 0, 0);
        _brightnessValue.AutoSize = true;
        _brightnessValue.ForeColor = UiTheme.Muted;
        _brightnessValue.TextAlign = ContentAlignment.MiddleLeft;
        _brightnessValue.Margin = new Padding(8, 12, 0, 0);

        row.Controls.Add(_power, 0, 0);
        row.Controls.Add(_brightness, 1, 0);
        row.Controls.Add(_brightnessValue, 2, 0);
        return row;
    }

    private void WireEvents()
    {
        _power.CheckedChanged += async (_, _) => await ApplyPowerAsync();
        _brightness.ValueChanged += (_, _) => UpdateBrightnessLabel();
        _brightness.MouseUp += async (_, _) => await ApplyBrightnessAsync();
    }

    private void ReloadStateButtons()
    {
        foreach (var button in _stateButtons)
            button.Click -= OnStateButtonClick;
        _stateGrid.Controls.Clear();
        _stateButtons.Clear();

        foreach (var item in _catalog.Create(_resolveEffects()))
        {
            if (item.Id == LightingPreviewIds.TestSweep)
                continue;

            var isPreviewOnly = PreviewOnlyIds.Contains(item.Id);
            var button = new NightButton
            {
                Text = isPreviewOnly ? $"{item.Title} · preview only" : item.Title,
                Width = 200,
                Margin = new Padding(0, 0, 10, 10),
                Tag = item
            };
            button.Click += OnStateButtonClick;
            _stateButtons.Add(button);
            _stateGrid.Controls.Add(button);
        }
    }

    private async void OnStateButtonClick(object? sender, EventArgs e)
    {
        if (sender is not NightButton { Tag: LightingPreviewItem item })
            return;

        var generation = BeginStatusGeneration();
        _previewCts?.Cancel();
        _previewCts?.Dispose();
        _previewCts = new CancellationTokenSource();
        var token = _previewCts.Token;

        SetStatus($"Previewing {item.Title}…", generation);
        try
        {
            await _coordinator.PreviewAsync(
                item,
                _resolveEffects(),
                _resolveWled(),
                AnimationDirection.Center,
                token,
                onHoldStarted: () => SetStatus($"Holding {item.Title}", generation));
            SetStatus($"Holding {item.Title}", generation);
        }
        catch (OperationCanceledException)
        {
            // Superseded by another tap.
        }
        catch (Exception ex)
        {
            SetStatus($"On-screen holding · WLED: {ex.Message}", generation, isError: true);
        }
    }

    private async Task RefreshPresetsAndPowerAsync()
    {
        var ip = (_getControllerIp() ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(ip) || ip is "0.0.0.0")
        {
            _presetsEmpty.ShowMessage("Set a WLED IP", "Configure the controller on the Connection tab first.");
            _presetGrid.Controls.Clear();
            return;
        }

        try
        {
            var presetsTask = _deviceClient.GetPresetsAsync(ip);
            var stateTask = _deviceClient.GetStateAsync(ip);
            await Task.WhenAll(presetsTask, stateTask).ConfigureAwait(true);

            BindPresets(await presetsTask.ConfigureAwait(true), ip);

            var state = await stateTask.ConfigureAwait(true);
            _loadingPower = true;
            try
            {
                _power.Checked = state.On;
                _brightness.Value = Math.Clamp((int)state.Brightness, 1, 255);
                UpdateBrightnessLabel();
            }
            finally
            {
                _loadingPower = false;
            }
        }
        catch (Exception)
        {
            _presetsEmpty.ShowMessage("Couldn't reach controller", "Check the IP on the Connection tab and try again.");
            _presetGrid.Controls.Clear();
        }
    }

    private void BindPresets(IReadOnlyList<WledPresetListEntry> presets, string ip)
    {
        _presetGrid.Controls.Clear();
        if (presets.Count == 0)
        {
            _presetsEmpty.ShowMessage("No presets yet", "Save presets in WLED, then reopen this tab.");
            return;
        }

        _presetsEmpty.HideMessage();
        foreach (var preset in presets)
        {
            var button = new NightButton
            {
                Text = preset.Name,
                Width = 200,
                Margin = new Padding(0, 0, 10, 10),
                Tag = preset
            };
            button.Click += async (_, _) => await ApplyPresetAsync(preset, ip);
            _presetGrid.Controls.Add(button);
        }
    }

    private async Task ApplyPresetAsync(WledPresetListEntry preset, string ip)
    {
        var generation = BeginStatusGeneration();
        SetStatus($"Applying preset {preset.Name}…", generation);
        try
        {
            await _deviceClient.ApplySavedPresetAsync(ip, preset.Id).ConfigureAwait(true);
            SetStatus($"Preset {preset.Name} applied.", generation);
        }
        catch (Exception ex)
        {
            SetStatus($"Preset apply failed: {ex.Message}", generation, isError: true);
        }
    }

    private async Task ApplyPowerAsync()
    {
        if (_loadingPower)
            return;

        var ip = (_getControllerIp() ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(ip) || ip is "0.0.0.0")
            return;

        var generation = BeginStatusGeneration();
        try
        {
            await _deviceClient.ApplyStateAsync(
                    ip,
                    new WledStatePatch { On = _power.Checked, Live = false })
                .ConfigureAwait(true);
            SetStatus(_power.Checked ? "Power on." : "Power off.", generation);
        }
        catch (Exception ex)
        {
            SetStatus($"Power toggle failed: {ex.Message}", generation, isError: true);
        }
    }

    private async Task ApplyBrightnessAsync()
    {
        if (_loadingPower)
            return;

        var ip = (_getControllerIp() ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(ip) || ip is "0.0.0.0")
            return;

        var generation = BeginStatusGeneration();
        try
        {
            await _deviceClient.ApplyStateAsync(
                    ip,
                    new WledStatePatch { Brightness = (byte)_brightness.Value, Live = false })
                .ConfigureAwait(true);
            SetStatus("Brightness applied.", generation);
        }
        catch (Exception ex)
        {
            SetStatus($"Brightness apply failed: {ex.Message}", generation, isError: true);
        }
    }

    private void UpdateBrightnessLabel() => _brightnessValue.Text = _brightness.Value.ToString();

    private int BeginStatusGeneration() => Interlocked.Increment(ref _statusGeneration);

    private void SetStatus(string message, int generation, bool isError = false)
    {
        if (generation != Volatile.Read(ref _statusGeneration))
            return;

        if (IsHandleCreated && InvokeRequired)
        {
            BeginInvoke(() => SetStatus(message, generation, isError));
            return;
        }

        _status.Text = message;
        _status.ForeColor = isError ? UiTheme.NotReady : UiTheme.Muted;
    }

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

    private static FlowLayoutPanel WrapFlow() => new()
    {
        AutoSize = true,
        WrapContents = true,
        BackColor = Color.Transparent
    };
}
