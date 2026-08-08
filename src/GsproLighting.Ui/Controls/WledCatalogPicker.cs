using GsproLighting.Ui.Theme;
using GsproLighting.Wled.Device;

namespace GsproLighting.Ui.Controls;

/// <summary>Searchable night-bay list for WLED effects or palettes.</summary>
public sealed class WledCatalogPicker : UserControl
{
    private readonly TextBox _filter = new();
    private readonly ListBox _list = new();
    private IReadOnlyList<WledNamedEntry> _all = [];
    private bool _suppressSelection;

    public WledCatalogPicker()
    {
        Height = 240;
        MinimumSize = new Size(200, 200);
        BackColor = Color.Transparent;

        UiTheme.StyleInput(_filter);
        _filter.PlaceholderText = "Search…";
        _filter.Dock = DockStyle.Top;
        _filter.Height = UiTheme.TouchComfort - 4;
        _filter.Margin = new Padding(0, 0, 0, 6);
        _filter.TextChanged += (_, _) => ApplyFilter();

        _list.Dock = DockStyle.Fill;
        _list.BackColor = UiTheme.Console;
        _list.ForeColor = UiTheme.Text;
        _list.BorderStyle = BorderStyle.FixedSingle;
        _list.IntegralHeight = false;
        _list.Font = UiTheme.BodyFont();
        _list.ItemHeight = 30;
        _list.SelectedIndexChanged += (_, _) =>
        {
            if (!_suppressSelection)
                SelectionChanged?.Invoke(this, EventArgs.Empty);
        };

        Controls.Add(_list);
        Controls.Add(_filter);
    }

    public event EventHandler? SelectionChanged;

    public WledNamedEntry? SelectedEntry => _list.SelectedItem as WledNamedEntry;

    public void SetEntries(IReadOnlyList<WledNamedEntry> entries, int? selectId = null)
    {
        _all = entries ?? [];
        ApplyFilter(selectId);
    }

    public void SelectId(int id)
    {
        for (var i = 0; i < _list.Items.Count; i++)
        {
            if (_list.Items[i] is WledNamedEntry entry && entry.Id == id)
            {
                _suppressSelection = true;
                _list.SelectedIndex = i;
                _suppressSelection = false;
                return;
            }
        }
    }

    private void ApplyFilter(int? selectId = null)
    {
        var query = _filter.Text.Trim();
        var filtered = string.IsNullOrEmpty(query)
            ? _all
            : _all.Where(e =>
                    e.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    e.Id.ToString().Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToArray();

        var preferred = selectId ?? SelectedEntry?.Id;
        _suppressSelection = true;
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var entry in filtered)
            _list.Items.Add(entry);
        _list.EndUpdate();
        _suppressSelection = false;

        if (preferred is int id)
            SelectId(id);
        else if (_list.Items.Count > 0)
            _list.SelectedIndex = 0;
    }
}
