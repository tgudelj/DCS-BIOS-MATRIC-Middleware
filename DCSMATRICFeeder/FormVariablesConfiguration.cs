using Newtonsoft.Json;
using Microsoft.Extensions.Logging;

namespace EXM.DBMM;

public partial class FormVariablesConfiguration : Form {
    private const string AllItems = "-- ALL --";
    private VariableSelectionEditor _editor = new(new());
    private string? _aircraft;
    private bool _loading;

    public FormVariablesConfiguration() {
        InitializeComponent();
    }

    private void SearchFilters_TextChanged(object? sender, EventArgs e) => RefreshLists();

    public void LoadTempSettings() => _editor = new(Program.mwSettings.AircraftVariables);

    private void FormVariablesFilter_Load(object sender, EventArgs e) {
        LoadTempSettings();
        ddAircraft.DataSource = Program.aircraftBiosConfigurations.Keys.OrderBy(v => v, StringComparer.Ordinal).ToList();
        SelectAircraft();
    }

    private void ddAircraft_SelectedIndexChanged(object sender, EventArgs e) => SelectAircraft();
    private void SelectAircraft() {
        _aircraft = ddAircraft.SelectedItem as string;
        if (_aircraft == null || !Program.aircraftBiosConfigurations.TryGetValue(_aircraft, out var config)) return;
        _loading = true;
        try {
            txtAvailableFilter.Clear();
            txtConfiguredFilter.Clear();
            ddCategory.DataSource = new[] { AllItems }.Concat(config.Keys.OrderBy(v => v, StringComparer.Ordinal)).ToList();
            ddCategory.SelectedIndex = 0;
        }
        finally { _loading = false; }
        RefreshLists();
    }

    private void RefreshLists() {
        if (_loading || _aircraft == null || !Program.aircraftBiosConfigurations.TryGetValue(_aircraft, out var config)) return;
        var category = ddCategory.SelectedItem as string;
        var source = category != null && config.TryGetValue(category, out var controls)
            ? controls.Keys.AsEnumerable() : config.Values.SelectMany(c => c.Keys);
        lstAvailableVariables.DataSource = _editor.Available(_aircraft, source, txtAvailableFilter.Text);
        lstConfiguredVariables.DataSource = _editor.Configured(_aircraft, txtConfiguredFilter.Text);
        lstAvailableVariables.ClearSelected();
        lstConfiguredVariables.ClearSelected();
    }

    private void ddCategory_SelectedIndexChanged(object sender, EventArgs e) => RefreshLists();
    private void btnAddSelected_Click(object sender, EventArgs e) {
        if (_aircraft == null) return;
        _editor.Add(_aircraft, lstAvailableVariables.SelectedItems.Cast<string>().ToArray());
        RefreshLists();
    }
    private void btnAddAll_Click(object sender, EventArgs e) {
        if (_aircraft == null) return;
        _editor.Add(_aircraft, lstAvailableVariables.Items.Cast<string>().ToArray());
        RefreshLists();
    }
    private void btnRemoveSelected_Click(object sender, EventArgs e) {
        if (_aircraft == null) return;
        _editor.Remove(_aircraft, lstConfiguredVariables.SelectedItems.Cast<string>().ToArray());
        RefreshLists();
    }
    private void btnRemoveAll_Click(object sender, EventArgs e) {
        if (_aircraft == null) return;
        _editor.Clear(_aircraft);
        RefreshLists();
    }
    private void btnClearAvailableFilter_Click(object sender, EventArgs e) => txtAvailableFilter.Clear();
    private void btnClearConfiguredFilter_Click(object sender, EventArgs e) => txtConfiguredFilter.Clear();
    private void btnCancel_Click(object sender, EventArgs e) { DialogResult = DialogResult.Cancel; Close(); }
    private void btnSave_Click(object sender, EventArgs e) {
        try {
            var selections = _editor.Snapshot();
            Properties.Settings.Default.AircraftVariables = JsonConvert.SerializeObject(selections);
            Properties.Settings.Default.Save();
            Program.mwSettings.AircraftVariables = selections;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex) {
            Program.logger.LogError(ex, "Could not save variable selections");
            MessageBox.Show(this, ex.Message, "Could not save selections", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
    private void lblAircraft_Click(object sender, EventArgs e) => ddAircraft.Focus();
    private void lblCategory_Click(object sender, EventArgs e) => ddCategory.Focus();
}
