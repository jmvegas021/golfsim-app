using System.Diagnostics;
using GsproLighting.Ui.Theme;
using GsproLighting.Ui.Wled;
using GsproLighting.Wled.Device;

namespace GsproLighting.Ui.Forms;

public sealed partial class WledTabPanel
{
    private void WireEvents()
    {
        _refresh.Click += async (_, _) => await RefreshAsync();
        _apply.Click += async (_, _) => await ApplyEditorAsync();
        _revert.Click += async (_, _) => await RevertAsync();
        _ambient.Click += async (_, _) => await SyncAmbientAsync();
        _openWled.Click += (_, _) => OpenFullWled();
        _applyPreset.Click += async (_, _) => await ApplySelectedPresetAsync();
        _playlistNext.Click += async (_, _) => await PlaylistNextAsync();
        foreach (var slider in new[] { _brightness, _speed, _intensity })
            slider.ValueChanged += (_, _) => UpdateValueLabels();
        _segments.SelectedIndexChanged += (_, _) =>
        {
            if (_loading || _segments.SelectedItem is not SegmentItem item)
                return;
            _selectedSegmentId = item.Id;
            if (_manager.State is { } state)
            {
                var seg = state.Segments.FirstOrDefault(s => s.Id == item.Id) ?? state.MainSegment;
                BindSegment(seg);
            }
        };
    }

    private async Task RefreshAsync()
    {
        try
        {
            RefreshConnectionStatus(loading: true);
            await _manager.RefreshAsync(_getControllerIp()).ConfigureAwait(true);
            BindFromManager();
            _hasLoaded = true;
            SetEditorEnabled(true);
            _empty.HideMessage();
            RefreshConnectionStatus();
            if (_manager.CatalogWarnings.Count > 0)
                SetStatusWarning("Live from controller — " + string.Join(" ", _manager.CatalogWarnings));
            else
                SetStatusDetail("Live from controller.");
        }
        catch (Exception ex)
        {
            _hasLoaded = false;
            SetEditorEnabled(false);
            ShowEmptyState();
            RefreshConnectionStatus(error: ex.Message);
        }
    }

    private void BindFromManager()
    {
        _loading = true;
        try
        {
            var info = _manager.Info;
            _deviceMeta.Text = info is null ? "" : FormatDeviceMeta(info);

            var state = _manager.State;
            if (state is null)
                return;

            _power.Checked = state.On;
            _brightness.Value = Math.Clamp((int)state.Brightness, 1, 255);
            _playlistLabel.Text = state.PlaylistId >= 0
                ? $"Playlist active: {state.PlaylistId}"
                : "No playlist active";

            _segments.Items.Clear();
            foreach (var seg in state.Segments)
                _segments.Items.Add(new SegmentItem(seg.Id, $"Seg {seg.Id} ({seg.Start}-{seg.Stop})"));
            if (_segments.Items.Count == 0)
                _segments.Items.Add(new SegmentItem(0, "Seg 0"));
            _selectedSegmentId = state.MainSegmentId;
            SelectSegmentCombo(_selectedSegmentId);

            _effects.SetEntries(_manager.Effects, state.MainSegment.FxId);
            _palettes.SetEntries(_manager.Palettes, state.MainSegment.PaletteId);
            BindSegment(state.MainSegment);

            _presets.Items.Clear();
            _presets.Items.Add(new WledPresetListEntry { Id = -1, Name = "(none)" });
            foreach (var preset in _manager.Presets)
                _presets.Items.Add(preset);
            SelectPresetCombo(state.PresetId);

            UpdateValueLabels();
        }
        finally
        {
            _loading = false;
        }
    }

    private void BindSegment(WledSegmentState seg)
    {
        _effects.SelectId(seg.FxId);
        _palettes.SelectId(seg.PaletteId);
        _speed.Value = Math.Clamp(seg.Speed, 0, 255);
        _intensity.Value = Math.Clamp(seg.Intensity, 0, 255);
        _overlay.Checked = seg.Overlay;
        _option2.Checked = seg.Option2;
        _option3.Checked = seg.Option3;
        _colors.SetColors(seg.Primary, seg.Secondary, seg.Tertiary);
        UpdateValueLabels();
    }

    private async Task ApplyEditorAsync()
    {
        if (!_hasLoaded)
        {
            RefreshConnectionStatus(error: "Refresh from the controller before applying.");
            return;
        }

        try
        {
            await _manager.ApplyAsync(_getControllerIp(), BuildPatchFromEditor()).ConfigureAwait(true);
            SetStatusDetail("Applied to WLED.");
        }
        catch (Exception ex)
        {
            RefreshConnectionStatus(error: ex.Message);
        }
    }

    private async Task RevertAsync()
    {
        if (!_hasLoaded)
            return;

        try
        {
            await _manager.RevertAsync(_getControllerIp()).ConfigureAwait(true);
            await RefreshAsync().ConfigureAwait(true);
            SetStatusDetail("Reverted to last refresh snapshot.");
        }
        catch (Exception ex)
        {
            RefreshConnectionStatus(error: ex.Message);
        }
    }

    private async Task SyncAmbientAsync()
    {
        try
        {
            await _manager.ApplyAmbientAsync(
                    _getControllerIp(),
                    ResolveSyncBrightness(),
                    _selectedSegmentId)
                .ConfigureAwait(true);
            await RefreshAsync().ConfigureAwait(true);
            SetStatusDetail("Ambient synced: Ripple · Red Reef · layered · colors max · timing 15%.");
        }
        catch (Exception ex)
        {
            RefreshConnectionStatus(error: ex.Message);
        }
    }

    private byte ResolveSyncBrightness() =>
        _manager.State is not null
            ? (byte)_brightness.Value
            : _getBrightness();

    private async Task ApplySelectedPresetAsync()
    {
        try
        {
            if (_presets.SelectedItem is not WledPresetListEntry preset || preset.Id <= 0)
            {
                SetStatusDetail("Choose a saved WLED preset first.");
                return;
            }

            await _manager.ApplySavedPresetAsync(_getControllerIp(), preset.Id).ConfigureAwait(true);
            await RefreshAsync().ConfigureAwait(true);
            SetStatusDetail($"Preset {preset.Id} applied.");
        }
        catch (Exception ex)
        {
            RefreshConnectionStatus(error: ex.Message);
        }
    }

    private async Task PlaylistNextAsync()
    {
        try
        {
            await _manager.ApplyAsync(
                    _getControllerIp(),
                    new WledStatePatch { NextPlaylist = true })
                .ConfigureAwait(true);
            await RefreshAsync().ConfigureAwait(true);
            SetStatusDetail("Advanced playlist.");
        }
        catch (Exception ex)
        {
            RefreshConnectionStatus(error: ex.Message);
        }
    }

    private void OpenFullWled()
    {
        try
        {
            var url = WledTabManager.BuildOpenWledUrl(_getControllerIp());
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            RefreshConnectionStatus(error: ex.Message);
        }
    }

    private WledStatePatch BuildPatchFromEditor() =>
        new()
        {
            On = _power.Checked,
            Brightness = (byte)_brightness.Value,
            Live = false,
            SegmentId = _selectedSegmentId,
            FxId = _effects.SelectedEntry?.Id,
            PaletteId = _palettes.SelectedEntry?.Id,
            Speed = _speed.Value,
            Intensity = _intensity.Value,
            Overlay = _overlay.Checked,
            Option2 = _option2.Checked,
            Option3 = _option3.Checked,
            Primary = _colors.Primary,
            Secondary = _colors.Secondary,
            Tertiary = _colors.Tertiary
        };

    private void SelectPresetCombo(int presetId)
    {
        _presets.SelectedIndex = 0;
        if (presetId <= 0)
            return;

        for (var i = 0; i < _presets.Items.Count; i++)
        {
            if (_presets.Items[i] is WledPresetListEntry preset && preset.Id == presetId)
            {
                _presets.SelectedIndex = i;
                return;
            }
        }
    }

    private void SelectSegmentCombo(int id)
    {
        for (var i = 0; i < _segments.Items.Count; i++)
        {
            if (_segments.Items[i] is SegmentItem item && item.Id == id)
            {
                _segments.SelectedIndex = i;
                return;
            }
        }

        if (_segments.Items.Count > 0)
            _segments.SelectedIndex = 0;
    }

    private void SetEditorEnabled(bool enabled)
    {
        _editorHost.Visible = enabled;
        _editorHost.Enabled = enabled;
        _apply.Enabled = enabled;
        _revert.Enabled = enabled;
        _applyPreset.Enabled = enabled;
        _playlistNext.Enabled = enabled;
        _power.Enabled = enabled;
        var ipReady = !string.IsNullOrWhiteSpace(_getControllerIp());
        _ambient.Enabled = ipReady;
        _openWled.Enabled = ipReady;
        _refresh.Enabled = true;
        if (enabled)
            _empty.HideMessage();
    }

    private void ShowEmptyState()
    {
        _empty.ShowMessage("Refresh to load WLED", ProductCopy.WledEmptyBeforeRefresh, waitingAccent: true);
        _empty.Height = 96;
        _empty.Margin = new Padding(0, 0, 0, 12);
    }

    private void RefreshConnectionStatus(bool loading = false, string? error = null)
    {
        var ip = (_getControllerIp() ?? string.Empty).Trim();
        if (loading)
        {
            _connectionStatus.Text = string.IsNullOrEmpty(ip)
                ? "Loading…"
                : $"Connecting to {ip}…";
            _connectionStatus.ForeColor = UiTheme.Muted;
            return;
        }

        if (!string.IsNullOrEmpty(error))
        {
            _connectionStatus.Text = error;
            _connectionStatus.ForeColor = UiTheme.NotReady;
            return;
        }

        if (string.IsNullOrEmpty(ip))
        {
            _connectionStatus.Text = "Set WLED IP on the Connection tab";
            _connectionStatus.ForeColor = UiTheme.Waiting;
            _deviceMeta.Text = ProductCopy.WledConnectionHelp;
            return;
        }

        if (_hasLoaded && _manager.Info is { } info)
        {
            _connectionStatus.Text = $"Connected to {ip}";
            _connectionStatus.ForeColor = UiTheme.Ready;
            _deviceMeta.Text = FormatDeviceMeta(info);
            return;
        }

        _connectionStatus.Text = $"Controller IP {ip} — press Refresh";
        _connectionStatus.ForeColor = UiTheme.Accent;
        _deviceMeta.Text = ProductCopy.WledConnectionHelp;
    }

    private void SetStatusDetail(string text)
    {
        _deviceMeta.Text = text;
        _deviceMeta.ForeColor = UiTheme.Muted;
    }

    private void SetStatusWarning(string text)
    {
        _deviceMeta.Text = text;
        _deviceMeta.ForeColor = UiTheme.Waiting;
    }

    private void UpdateValueLabels()
    {
        _brightnessValue.Text = $"{_brightness.Value} ({FormatPercent(_brightness.Value)})";
        _speedValue.Text = FormatPercent(_speed.Value);
        _intensityValue.Text = FormatPercent(_intensity.Value);
    }

    private static string FormatDeviceMeta(WledDeviceInfo info) =>
        $"{info.Name} · v{info.Version} · {info.LedCount} LEDs · fx {info.EffectCount} · pal {info.PaletteCount}";

    private static string FormatPercent(int value) =>
        $"{Math.Round(value * 100.0 / 255)}%";

    private sealed record SegmentItem(int Id, string Label)
    {
        public override string ToString() => Label;
    }
}
