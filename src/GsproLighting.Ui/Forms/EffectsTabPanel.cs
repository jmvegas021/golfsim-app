using GsproLighting.Core.Config;
using GsproLighting.Ui.Controls;
using GsproLighting.Ui.Theme;

namespace GsproLighting.Ui.Forms;

public sealed class EffectsTabPanel : UserControl
{
    private readonly LedStripPreview _stripPreview = new();
    private readonly Label _statusChip = new()
    {
        AutoSize = false,
        Width = 104,
        Height = 32,
        TextAlign = ContentAlignment.MiddleCenter
    };
    private readonly Label _watchSummary = new()
    {
        AutoEllipsis = true,
        ForeColor = UiTheme.Muted,
        TextAlign = ContentAlignment.MiddleLeft,
        Dock = DockStyle.Fill
    };
    private readonly Button _save = new() { Text = "Save", Width = 86 };
    private readonly Button _test = new() { Text = "Test lights", Width = 106 };
    private readonly Button _idle = new() { Text = "Idle glow", Width = 96 };
    private readonly Button _proxy = new() { Text = "Start proxy", Width = 112 };
    private readonly FlowLayoutPanel _cards;
    private readonly EffectSlotCard _idleCard = new("Idle / ready", "Ready-state bay glow");
    private readonly EffectSlotCard _notReadyCard = new("Not ready", "Waiting for the next ball");
    private readonly EffectSlotCard _pureCard = new("Pure", "Centered, efficient strike");
    private readonly EffectSlotCard _mishitCard = new("Mishit", "Low-efficiency strike");
    private readonly EffectSlotCard _puttCard = new("Putt", "Low-speed shot");
    private readonly EffectSlotCard _celebrateCard = new("Celebrate", "Preview-only course outcome");
    private readonly EffectSlotCard _hazardCard = new("Hazard", "Preview-only penalty outcome");
    private readonly EffectSlotCard _playerCard = new("Player", "Player and club event");

    public EffectsTabPanel()
    {
        Dock = DockStyle.Fill;
        BackColor = UiTheme.Background;
        Padding = new Padding(18, 14, 18, 16);
        UiTheme.StyleButton(_save, primary: true);
        UiTheme.StyleButton(_test, primary: true);
        UiTheme.StyleButton(_idle);
        UiTheme.StyleButton(_proxy);

        _cards = BuildCards();
        Controls.Add(_cards);
        Controls.Add(BuildTopArea());
        WireEvents();
        Resize += (_, _) => ResizeCards();
    }

    public event EventHandler? SaveRequested;
    public event EventHandler? TestRequested;
    public event EventHandler? IdleRequested;
    public event EventHandler? ProxyToggleRequested;
    public event EventHandler<EffectSlotPreviewEventArgs>? PreviewRequested;

    public void LoadConfig(EffectConfig config)
    {
        _idleCard.SelectedSlot = config.Idle;
        _notReadyCard.SelectedSlot = config.NotReady;
        _pureCard.SelectedSlot = config.PureStrike;
        _mishitCard.SelectedSlot = config.Mishit;
        _puttCard.SelectedSlot = config.Putt;
        _celebrateCard.SelectedSlot = config.Celebrate;
        _hazardCard.SelectedSlot = config.Hazard;
        _playerCard.SelectedSlot = config.Player;
    }

    public void ApplyTo(EffectConfig config)
    {
        config.Idle = _idleCard.SelectedSlot;
        config.NotReady = _notReadyCard.SelectedSlot;
        config.PureStrike = _pureCard.SelectedSlot;
        config.Mishit = _mishitCard.SelectedSlot;
        config.Putt = _puttCard.SelectedSlot;
        config.Celebrate = _celebrateCard.SelectedSlot;
        config.Hazard = _hazardCard.SelectedSlot;
        config.Player = _playerCard.SelectedSlot;
    }

    public void UpdateStatus(string chipText, Color chipColor, string summary, bool proxyRunning)
    {
        _statusChip.Text = chipText;
        _statusChip.BackColor = chipColor;
        _statusChip.ForeColor = UiTheme.Background;
        _watchSummary.Text = summary;
        _proxy.Text = proxyRunning ? "Stop proxy" : "Start proxy";
    }

    public void ShowActionStatus(string message, bool isError = false)
    {
        _watchSummary.Text = message;
        _watchSummary.ForeColor = isError ? UiTheme.NotReady : UiTheme.Muted;
    }

    private Control BuildTopArea()
    {
        var top = new Panel { Dock = DockStyle.Top, Height = 154, Padding = new Padding(0, 0, 0, 12) };
        var status = new TableLayoutPanel { Dock = DockStyle.Top, Height = 40, ColumnCount = 2 };
        status.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));
        status.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        status.Controls.Add(_statusChip, 0, 0);
        status.Controls.Add(_watchSummary, 1, 0);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 42,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        actions.Controls.AddRange([_save, _test, _idle, _proxy]);
        top.Controls.Add(actions);
        top.Controls.Add(_stripPreview);
        top.Controls.Add(status);
        return top;
    }

    private FlowLayoutPanel BuildCards()
    {
        var cards = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };
        cards.Controls.AddRange([
            _idleCard, _notReadyCard, _pureCard, _mishitCard,
            _puttCard, _celebrateCard, _hazardCard, _playerCard
        ]);
        return cards;
    }

    private void WireEvents()
    {
        _save.Click += (_, _) => SaveRequested?.Invoke(this, EventArgs.Empty);
        _test.Click += (_, _) => TestRequested?.Invoke(this, EventArgs.Empty);
        _idle.Click += (_, _) => IdleRequested?.Invoke(this, EventArgs.Empty);
        _proxy.Click += (_, _) => ProxyToggleRequested?.Invoke(this, EventArgs.Empty);
        foreach (var card in _cards.Controls.OfType<EffectSlotCard>())
            card.PreviewRequested += OnCardPreviewRequested;
    }

    private void OnCardPreviewRequested(object? sender, EventArgs _)
    {
        if (sender is not EffectSlotCard card)
            return;

        var slot = card.SelectedSlot;
        _stripPreview.Play(slot);
        PreviewRequested?.Invoke(this, new EffectSlotPreviewEventArgs(slot));
    }

    private void ResizeCards()
    {
        var width = Math.Max(560, _cards.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 4);
        foreach (Control card in _cards.Controls)
            card.Width = width;
    }
}

public sealed class EffectSlotPreviewEventArgs(EffectSlot slot) : EventArgs
{
    public EffectSlot Slot { get; } = slot;
}
