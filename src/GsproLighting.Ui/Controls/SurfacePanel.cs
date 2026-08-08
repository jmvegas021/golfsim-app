using GsproLighting.Ui.Theme;

namespace GsproLighting.Ui.Controls;

/// <summary>Gradient night-bay surface used as a card/section shell.</summary>
public sealed class SurfacePanel : Panel
{
    private bool _raised;
    private bool _hovered;

    public SurfacePanel()
    {
        DoubleBuffered = true;
        BackColor = UiTheme.Panel;
        Padding = new Padding(UiTheme.SpacingLg);
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
    }

    public bool Raised
    {
        get => _raised;
        set
        {
            _raised = value;
            Invalidate();
        }
    }

    public bool HighlightOnHover { get; set; }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hovered = true;
        if (HighlightOnHover)
            Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hovered = false;
        if (HighlightOnHover)
            Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        UiTheme.FillPanelSurface(e.Graphics, ClientRectangle, _raised || _hovered);
        UiTheme.DrawPanelBorder(e.Graphics, ClientRectangle, focused: false, hovered: _hovered && HighlightOnHover);
    }
}
