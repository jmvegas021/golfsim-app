using System.Diagnostics;
using GsproLighting.Ui.Wled;
using GsproLighting.Wled.Device;

namespace GsproLighting.Ui.Forms;

public sealed partial class WledTabPanel
{
    private async Task RefreshAsync()
    {
        try
        {
            SetStatus("Loading from controller…");
            await _manager.RefreshAsync(_getControllerIp()).ConfigureAwait(true);
            BindFromManager();
            SetStatus("Live from controller.");
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message);
        }
    }

    private void BindFromManager()
    {
        _loading = true;
        try
        {
            var info = _manager.Info;
            _deviceMeta.Text = info is null
                ? ""
                : $"{info.Name} · v{info.Version} · {info.LedCount} LEDs · fx {info.EffectCount} · pal {info.PaletteCount}";

            var state = _manager.State;
            if (state is null)
                return;

            _power.Checked = state.On;
            _brightness.Value = Math.Clamp((int)state.Brightness, 1, 255);
            _brightnessValue.Text = _brightness.Value.ToString();
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
            _presets.SelectedIndex = 0;
            if (state.PresetId > 0)
            {
                for (var i = 0; i < _presets.Items.Count; i++)
                {
                    if (_presets.Items[i] is WledPresetListEntry p && p.Id == state.PresetId)
                    {
                        _presets.SelectedIndex = i;
                        break;
                    }
                }
            }
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
        _speedValue.Text = FormatPercent(_speed.Value);
        _intensityValue.Text = FormatPercent(_intensity.Value);
        _overlay.Checked = seg.Overlay;
        _option2.Checked = seg.Option2;
        _option3.Checked = seg.Option3;
        _colors.SetColors(seg.Primary, seg.Secondary, seg.Tertiary);
    }

    private async Task ApplyEditorAsync()
    {
        try
        {
            await _manager.ApplyAsync(_getControllerIp(), BuildPatchFromEditor()).ConfigureAwait(true);
            SetStatus("Applied to WLED.");
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message);
        }
    }

    private async Task RevertAsync()
    {
        try
        {
            await _manager.RevertAsync(_getControllerIp()).ConfigureAwait(true);
            await RefreshAsync().ConfigureAwait(true);
            SetStatus("Reverted to last refresh snapshot.");
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message);
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
            SetStatus("Ambient synced: Ripple · Red Reef · layered · colors max · timing 15%.");
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message);
        }
    }

    /// <summary>
    /// Prefer the WLED tab brightness slider once the editor has loaded; otherwise Connection.
    /// </summary>
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
                SetStatus("Choose a saved WLED preset first.");
                return;
            }

            await _manager.ApplySavedPresetAsync(_getControllerIp(), preset.Id).ConfigureAwait(true);
            await RefreshAsync().ConfigureAwait(true);
            SetStatus($"Preset {preset.Id} applied.");
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message);
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
            SetStatus("Advanced playlist.");
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message);
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
            SetStatus(ex.Message);
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

    private void SetStatus(string text) => _status.Text = text;

    private static string FormatPercent(int value) =>
        $"{value} ({Math.Round(value * 100.0 / 255)}%)";

    private sealed record SegmentItem(int Id, string Label)
    {
        public override string ToString() => Label;
    }
}
