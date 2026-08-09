using GsproLighting.Core.Config;
using GsproLighting.Core.Preview;
using GsproLighting.Ui.Controls;
using GsproLighting.Ui.Preview;
using GsproLighting.Ui.Theme;
using GsproLighting.Wled;
using GsproLighting.Wled.Animations;

namespace GsproLighting.Ui.Forms;

/// <summary>
/// Preview tab — test each lighting state with hold-after-animation. Never saves config.
/// Stop returns to held ready/idle green.
/// </summary>
public sealed class PreviewTabPanel : UserControl
{
    private readonly Func<EffectConfig> _resolveEffects;
    private readonly Func<WledConfig> _resolveWled;
    private readonly Action<string, string>? _logWledFailure;
    private readonly LedStripPreview _strip = new();
    private readonly LightingPreviewCatalog _catalog = new();
    private readonly PreviewPlaybackCoordinator _coordinator;
    private readonly FlowLayoutPanel _cards = new();
    private readonly Label _stateLabel = new();
    private readonly NightComboBox _direction = new();
    private readonly NightButton _stop = NightButton.Create("Stop / hold idle", 150);
    private readonly NightButton _playAll = NightButton.Create("Play all", 120, isPrimary: true);
    private readonly NightButton _skip = NightButton.Create("Skip", 88);
    private readonly List<PreviewStateCard> _stateCards = [];
    private int _statusGeneration;
    private CancellationTokenSource? _previewCts;
    private bool _playAllActive;

    public PreviewTabPanel(
        Func<EffectConfig> resolveEffects,
        Func<WledConfig> resolveWled,
        WledPreviewPlayer player,
        Action<string, string>? logWledFailure = null)
    {
        _resolveEffects = resolveEffects;
        _resolveWled = resolveWled;
        _logWledFailure = logWledFailure;
        _coordinator = new PreviewPlaybackCoordinator(player, _strip);

        Dock = DockStyle.Fill;
        BackColor = UiTheme.Background;
        Padding = new Padding(18, 14, 18, 14);
        Font = UiTheme.BodyFont();

        ConfigureChrome();
        Controls.Add(BuildRoot());
        ReloadCards();
        _cards.ClientSizeChanged += (_, _) => ResizeCards();
        _stop.Click += async (_, _) => await StopAsync();
        _playAll.Click += async (_, _) => await PlayAllAsync();
        _skip.Click += (_, _) => _coordinator.SkipCurrent();
        UpdateToolbarEnabled();
    }

    public void RefreshFromEffects() => ReloadCards();

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        BeginInvoke(() =>
        {
            PerformLayout();
            ResizeCards();
        });
    }

    protected override void OnPaintBackground(PaintEventArgs e) =>
        UiTheme.FillNightBackground(e.Graphics, ClientRectangle);

    private void ConfigureChrome()
    {
        _strip.Height = 108;
        _direction.Items.AddRange(["Center", "Left", "Right"]);
        _direction.SelectedIndex = 0;
        _direction.Width = 120;
        _direction.AccessibleName = "Shot direction for Pure and Mishit";

        _stateLabel.AutoSize = true;
        _stateLabel.MaximumSize = new Size(360, 0);
        _stateLabel.ForeColor = UiTheme.Muted;
        _stateLabel.Font = UiTheme.BodyFont(9.5f);
        _stateLabel.TextAlign = ContentAlignment.MiddleLeft;
        _stateLabel.Margin = new Padding(8, 14, 0, 0);
        _stateLabel.Text = "Select a state · colors hold after animation · Stop holds ready green";
        _skip.Enabled = false;
    }

    private Control BuildRoot()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            BackColor = Color.Transparent
        };
        for (var i = 0; i < 4; i++)
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        root.Controls.Add(BuildHeading(), 0, 0);
        root.Controls.Add(_strip, 0, 1);
        root.Controls.Add(BuildToolbar(), 0, 2);
        root.Controls.Add(BuildStatesHeading(), 0, 3);

        _cards.Dock = DockStyle.Fill;
        _cards.AutoScroll = true;
        _cards.FlowDirection = FlowDirection.TopDown;
        _cards.WrapContents = false;
        _cards.BackColor = Color.Transparent;
        _cards.Margin = new Padding(0, 4, 0, 0);
        root.Controls.Add(_cards, 0, 4);
        return root;
    }

    private static Control BuildHeading() =>
        new TabSectionHeading
        {
            Dock = DockStyle.Top,
            Title = "Preview lighting states",
            Subtitle = "Test each bay reaction. Playback holds the end color until you pick another state or Stop."
        };

    private Control BuildToolbar()
    {
        var row = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 12, 0, 6)
        };

        var dirField = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            RowCount = 2,
            Margin = new Padding(0, 0, 12, 0)
        };
        dirField.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        dirField.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        dirField.Controls.Add(new Label
        {
            Text = "DIRECTION",
            AutoSize = true,
            ForeColor = UiTheme.Accent,
            Font = UiTheme.BodyFont(7.5f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(0, 2, 0, 2)
        }, 0, 0);
        _direction.Margin = new Padding(0);
        dirField.Controls.Add(_direction, 0, 1);

        _stop.Margin = new Padding(0, 0, 8, 0);
        _playAll.Margin = new Padding(0, 0, 8, 0);
        _skip.Margin = new Padding(0, 0, 8, 0);

        row.Controls.Add(dirField);
        row.Controls.Add(_stop);
        row.Controls.Add(_playAll);
        row.Controls.Add(_skip);
        row.Controls.Add(_stateLabel);
        return row;
    }

    private static Control BuildStatesHeading() =>
        new Label
        {
            Text = "STATES",
            Dock = DockStyle.Top,
            Height = UiTheme.SectionTitleRow,
            ForeColor = UiTheme.Accent,
            Font = UiTheme.BodyFont(8.5f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(0, 8, 0, 4),
            Margin = new Padding(0, 8, 0, 0)
        };

    private void ReloadCards()
    {
        foreach (var card in _stateCards)
            card.Selected -= OnCardSelected;
        _cards.Controls.Clear();
        _stateCards.Clear();

        foreach (var item in _catalog.Create(_resolveEffects()))
        {
            var card = new PreviewStateCard(item) { Width = Math.Max(280, _cards.ClientSize.Width - 2) };
            card.Selected += OnCardSelected;
            _stateCards.Add(card);
            _cards.Controls.Add(card);
        }
    }

    private async void OnCardSelected(object? sender, EventArgs _)
    {
        if (sender is not PreviewStateCard card)
            return;

        var generation = BeginStatusGeneration();
        CancelActivePreviewToken();
        _previewCts = new CancellationTokenSource();
        var token = _previewCts.Token;

        SelectOnly(card);
        _playAllActive = false;
        UpdateToolbarEnabled();
        SetState($"Previewing {card.Item.Title}…", generation);

        try
        {
            await _coordinator.PreviewAsync(
                card.Item,
                _resolveEffects(),
                _resolveWled(),
                SelectedDirection(),
                token,
                onHoldStarted: () => SetState($"Holding {card.Item.Title}", generation));
            SetState($"Holding {card.Item.Title}", generation);
        }
        catch (OperationCanceledException)
        {
            // Superseded by another card, Play all, or Stop — generation guards status.
        }
        catch (Exception ex)
        {
            LogWledFailure($"Preview {card.Item.Title}: {ex.Message}");
            SetState($"On-screen holding · WLED: {ex.Message}", generation, isError: true);
        }
        finally
        {
            UpdateToolbarEnabled();
        }
    }

    private async Task PlayAllAsync()
    {
        var generation = BeginStatusGeneration();
        CancelActivePreviewToken();
        _previewCts = new CancellationTokenSource();
        var token = _previewCts.Token;
        _playAllActive = true;
        UpdateToolbarEnabled();

        var progress = new Progress<PreviewSequenceProgress>(report =>
        {
            if (!report.IsComplete)
                SelectByTitle(report.StateTitle);
            SetState(report.FormatLabel(), generation);
        });

        try
        {
            await _coordinator.PlayAllAsync(
                _resolveEffects(),
                _resolveWled(),
                SelectedDirection(),
                progress,
                token);
            SetState("Play all complete · last state holding", generation);
        }
        catch (OperationCanceledException)
        {
            SetState("Play all stopped", generation);
        }
        catch (Exception ex)
        {
            LogWledFailure($"Play all: {ex.Message}");
            SetState($"Play all · WLED: {ex.Message}", generation, isError: true);
        }
        finally
        {
            _playAllActive = false;
            UpdateToolbarEnabled();
        }
    }

    private async Task StopAsync()
    {
        var generation = BeginStatusGeneration();
        CancelActivePreviewToken();
        _coordinator.CancelSequence();
        _playAllActive = false;
        UpdateToolbarEnabled();
        SetState("Stopping…", generation);

        try
        {
            await _coordinator.StopAsync(_resolveEffects(), _resolveWled());
            foreach (var card in _stateCards)
                card.IsSelected = false;
            SetState("Stopped · holding ready / idle green", generation);
        }
        catch (Exception ex)
        {
            LogWledFailure($"Stop: {ex.Message}");
            SetState($"Stop failed: {ex.Message}", generation, isError: true);
        }
        finally
        {
            UpdateToolbarEnabled();
        }
    }

    private void LogWledFailure(string message) =>
        _logWledFailure?.Invoke("preview", message);

    private int BeginStatusGeneration() => Interlocked.Increment(ref _statusGeneration);

    private void CancelActivePreviewToken()
    {
        _previewCts?.Cancel();
        _previewCts?.Dispose();
        _previewCts = null;
        _coordinator.CancelSequence();
        // Player cancel happens inside PreviewAsync/Stop via BeginPreview supersede.
    }

    private void SelectOnly(PreviewStateCard card)
    {
        foreach (var other in _stateCards)
            other.IsSelected = ReferenceEquals(other, card);
    }

    private void SelectByTitle(string title)
    {
        foreach (var card in _stateCards)
            card.IsSelected = string.Equals(card.Item.Title, title, StringComparison.Ordinal);
    }

    private void UpdateToolbarEnabled()
    {
        _playAll.Enabled = !_playAllActive;
        _skip.Enabled = _playAllActive || _coordinator.IsPlayAllRunning;
        _stop.Enabled = true;
    }

    private AnimationDirection SelectedDirection() =>
        _direction.SelectedItem?.ToString() switch
        {
            "Left" => AnimationDirection.Left,
            "Right" => AnimationDirection.Right,
            _ => AnimationDirection.Center
        };

    private void SetState(string message, int generation, bool isError = false)
    {
        if (generation != Volatile.Read(ref _statusGeneration))
            return;

        if (IsHandleCreated && InvokeRequired)
        {
            BeginInvoke(() => SetState(message, generation, isError));
            return;
        }

        _stateLabel.Text = message;
        _stateLabel.ForeColor = isError ? UiTheme.NotReady : UiTheme.Muted;
    }

    private void ResizeCards()
    {
        var width = Math.Max(1, _cards.ClientSize.Width - 2);
        foreach (Control card in _cards.Controls)
            card.Width = width;
    }
}
