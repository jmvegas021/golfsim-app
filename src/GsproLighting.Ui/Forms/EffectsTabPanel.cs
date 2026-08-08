using GsproLighting.Core.Config;
using GsproLighting.Ui.Controls;
using GsproLighting.Ui.Theme;

namespace GsproLighting.Ui.Forms;

public sealed class EffectsTabPanel : UserControl
{
    private readonly LedStripPreview _stripPreview = new();
    private readonly Label _readyChip = CreateChip(112);
    private readonly Label _serviceChip = CreateChip(126);
    private readonly Label _watchSummary = new()
    {
        AutoEllipsis = true,
        ForeColor = UiTheme.Muted,
        TextAlign = ContentAlignment.MiddleLeft,
        Dock = DockStyle.Fill
    };
    private readonly Button _save = new() { Text = "Save settings", Width = 122 };
    private readonly Button _test = new() { Text = "Test lights", Width = 106 };
    private readonly Button _idle = new() { Text = "Idle glow", Width = 96 };
    private readonly Button _proxy = new() { Text = "Start proxy", Width = 112 };
    private readonly FlowLayoutPanel _cards;
    private readonly EffectSlotCard _idleCard = new("Idle / ready", "Ready-state bay glow");
    private readonly EffectSlotCard _notReadyCard = new("Not ready", "Waiting for the next ball");
    private readonly EffectSlotCard _pureCard = new("Pure", "Centered, efficient strike");
    private readonly EffectSlotCard _mishitCard = new("Mishit", "Low-efficiency strike");
    private readonly EffectSlotCard _puttCard = new("Putt", "Low-speed shot");
    private readonly EffectSlotCard _celebrateCard = new("Celebrate", "Course outcome", supportsWledPreset: true);
    private readonly EffectSlotCard _hazardCard = new("Hazard", "Penalty outcome", supportsWledPreset: true);
    private readonly EffectSlotCard _playerCard = new("Player", "Player and club event");

    public EffectsTabPanel()
    {
        Dock = DockStyle.Fill;
        BackColor = UiTheme.Background;
        Padding = new Padding(18, 12, 18, 14);
        UiTheme.StyleButton(_save, primary: true);
        UiTheme.StyleButton(_test);
        UiTheme.StyleButton(_idle);
        UiTheme.StyleButton(_proxy);

        _cards = BuildCards();
        Controls.Add(BuildRootLayout());
        WireEvents();
        _cards.ClientSizeChanged += (_, _) => ResizeCards();
    }

    public event EventHandler? SaveRequested;
    public event EventHandler? TestRequested;
    public event EventHandler? IdleRequested;
    public event EventHandler? ProxyToggleRequested;
    public event EventHandler<EffectSlotPreviewEventArgs>? PreviewRequested;

    public EffectSlot IdleSlot => _idleCard.SelectedSlot;
    public EffectSlot PureSlot => _pureCard.SelectedSlot;

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

    public void UpdateStatus(
        string readyText,
        Color readyColor,
        string serviceText,
        Color serviceColor,
        string summary,
        bool proxyRunning)
    {
        StyleChip(_readyChip, readyText, readyColor);
        StyleChip(_serviceChip, serviceText, serviceColor);
        _watchSummary.Text = summary;
        _watchSummary.ForeColor = UiTheme.Muted;
        _proxy.Text = proxyRunning ? "Stop proxy" : "Start proxy";
    }

    public void ShowActionStatus(string message, bool isError = false)
    {
        _watchSummary.Text = message;
        _watchSummary.ForeColor = isError ? UiTheme.NotReady : UiTheme.Muted;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        BeginInvoke(() =>
        {
            PerformLayout();
            ResizeCards();
        });
    }

    private Control BuildRootLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            BackColor = UiTheme.Background
        };
        for (var row = 0; row < 5; row++)
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(BuildHeading(), 0, 0);
        root.Controls.Add(BuildStatus(), 0, 1);
        root.Controls.Add(_stripPreview, 0, 2);
        root.Controls.Add(BuildRuntimeActions(), 0, 3);
        root.Controls.Add(BuildCardsHeading(), 0, 4);
        root.Controls.Add(_cards, 0, 5);
        return root;
    }

    private Control BuildHeading()
    {
        var heading = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 58,
            ColumnCount = 2,
            Margin = new Padding(0)
        };
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 134));
        heading.Controls.Add(new Label
        {
            Text = "Lighting effects\nChoose a color and animation for each simulator event.",
            Dock = DockStyle.Fill,
            ForeColor = UiTheme.Text,
            Font = UiTheme.BodyFont(11f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        heading.Controls.Add(_save, 1, 0);
        _save.Anchor = AnchorStyles.Right;
        return heading;
    }

    private Control BuildStatus()
    {
        var status = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 42,
            ColumnCount = 3,
            Margin = new Padding(0, 0, 0, 8)
        };
        status.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        status.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 134));
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
            Height = 52,
            ColumnCount = 2,
            Margin = new Padding(0, 8, 0, 0)
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.Controls.Add(new Label
        {
            Text = "RUNTIME",
            Dock = DockStyle.Fill,
            ForeColor = UiTheme.Muted,
            Font = UiTheme.BodyFont(8.5f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        actions.Controls.AddRange([_test, _idle, _proxy]);
        row.Controls.Add(actions, 1, 0);
        return row;
    }

    private static Control BuildCardsHeading()
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 36,
            ColumnCount = 2,
            Margin = new Padding(0)
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        row.Controls.Add(new Label
        {
            Text = "EVENT EFFECTS",
            Dock = DockStyle.Fill,
            ForeColor = UiTheme.Text,
            Font = UiTheme.BodyFont(9f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        row.Controls.Add(new Label
        {
            Text = "Scroll for more effects ↓",
            Dock = DockStyle.Fill,
            ForeColor = UiTheme.Accent,
            TextAlign = ContentAlignment.MiddleRight
        }, 1, 0);
        return row;
    }

    private FlowLayoutPanel BuildCards()
    {
        var cards = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = UiTheme.Background,
            Margin = new Padding(0)
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
        var width = Math.Max(1, _cards.ClientSize.Width - 2);
        foreach (Control card in _cards.Controls)
            card.Width = width;
    }

    private static Label CreateChip(int width) => new()
    {
        AutoSize = false,
        Width = width,
        Height = 32,
        Margin = new Padding(0, 4, 8, 4),
        TextAlign = ContentAlignment.MiddleCenter,
        Font = UiTheme.BodyFont(8.5f, FontStyle.Bold)
    };

    private static void StyleChip(Label chip, string text, Color color)
    {
        chip.Text = text;
        chip.BackColor = color;
        chip.ForeColor = UiTheme.Background;
    }
}

public sealed class EffectSlotPreviewEventArgs(EffectSlot slot) : EventArgs
{
    public EffectSlot Slot { get; } = slot;
}
