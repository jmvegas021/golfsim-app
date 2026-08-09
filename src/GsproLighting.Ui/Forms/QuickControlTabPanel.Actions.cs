using GsproLighting.Core.Preview;
using GsproLighting.Ui.Controls;
using GsproLighting.Ui.Theme;
using GsproLighting.Wled.Animations;
using GsproLighting.Wled.Device;

namespace GsproLighting.Ui.Forms;

public sealed partial class QuickControlTabPanel
{
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
            var button = NightButton.Create(
                isPreviewOnly ? $"{item.Title} · preview only" : item.Title,
                200);
            button.Margin = new Padding(0, 0, 10, 10);
            button.Tag = item;
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
            LogWledFailure($"Preview {item.Title}: {ex.Message}");
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
        catch (Exception ex)
        {
            LogWledFailure($"Refresh presets/power: {ex.Message}");
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
            var button = NightButton.Create(preset.Name, 200);
            button.Margin = new Padding(0, 0, 10, 10);
            button.Tag = preset;
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
            LogWledFailure($"Preset {preset.Name}: {ex.Message}");
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
            LogWledFailure($"Power toggle: {ex.Message}");
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
            LogWledFailure($"Brightness apply: {ex.Message}");
            SetStatus($"Brightness apply failed: {ex.Message}", generation, isError: true);
        }
    }

    private void LogWledFailure(string message) =>
        _logWledFailure?.Invoke("quick-control", message);

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
}
