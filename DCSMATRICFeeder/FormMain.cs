using DcsBios.Communicator.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Text.Json.Nodes;

namespace EXM.DBMM;

public partial class FormMain : Form {
    private MiddlewareSession? _session;
    private bool _transitioning;
    private bool _closing;
    private bool _allowClose;
    private string _operation = "";
    private Task _transitionTask = Task.CompletedTask;
    private readonly Label _biosHealth = new() { AutoSize = true, Text = "DCS-BIOS: stopped" };
    private readonly Label _matricHealth = new() { AutoSize = true, Text = "MATRIC: stopped" };
    private readonly Label _aircraft = new() { AutoSize = true, Text = "Aircraft: —" };
    private readonly System.Windows.Forms.Timer _healthTimer;

    public FormMain() {
        InitializeComponent();
        txtBIOSListenPort.Minimum = 1;
        txtDCSBIOSImportPort.Minimum = 1;
        ClientSize = new Size(ClientSize.Width, 580);
        groupBox1.Size = new Size(651, 185);
        groupBox1.Controls.Add(pbBuffer);
        groupBox1.Controls.Add(pbSentVars);
        pbBuffer.Location = new Point(62, 31);
        pbSentVars.Location = new Point(62, 64);
        var health = new FlowLayoutPanel {
            Location = new Point(12, 100), Size = new Size(627, 80), FlowDirection = FlowDirection.TopDown, WrapContents = false
        };
        health.Controls.AddRange(new Control[] { _biosHealth, _matricHealth, _aircraft });
        groupBox1.Controls.Add(health);
        var logs = new ToolStripMenuItem("Open logs folder");
        logs.Click += (_, _) => OpenLogs();
        helpToolStripMenuItem.DropDownItems.Add(logs);
        components ??= new System.ComponentModel.Container();
        _healthTimer = new System.Windows.Forms.Timer(components) { Interval = 1000 };
        _healthTimer.Tick += (_, _) => RefreshHealth();
        _healthTimer.Start();
        FormClosing += OnFormClosing;
    }

    private void btnBrowse_Click(object sender, EventArgs e) {
        dlgFindDCS.InitialDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Saved Games");
        if (dlgFindDCS.ShowDialog(this) == DialogResult.OK) txtDCSBiosInstancePath.Text = dlgFindDCS.SelectedPath;
    }

    private void frmMain_Load(object sender, EventArgs e) {
        try { LoadSettings(); }
        catch (Exception ex) { ShowError("Could not load settings", ex); }
        var paths = GetDCSBiosPaths(GetDCSUserPaths());
        txtDCSBiosInstancePath.Items.AddRange(paths.ToArray());
        if (!Directory.Exists(txtDCSBiosInstancePath.Text) && paths.Count > 0) txtDCSBiosInstancePath.Text = paths[0];
    }

    private static List<string> GetDCSUserPaths() {
        var savedGames = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Saved Games");
        return new[] { "DCS", "DCS.openbeta" }.Select(d => Path.Combine(savedGames, d)).Where(Directory.Exists).ToList();
    }
    private static List<string> GetDCSBiosPaths(List<string> paths) => paths
        .Select(p => Path.Combine(p, "Scripts", "DCS-BIOS", "doc", "json")).Where(Directory.Exists).ToList();

    private static (bool enabled, int port) GetMatricConfig() {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MATRIC", "config", "config.json");
        var config = JsonNode.Parse(File.ReadAllText(path)) ?? throw new InvalidDataException("MATRIC configuration is empty.");
        var enabled = config["EnableIntegrationAPI"]?.GetValue<bool>() ?? false;
        var port = config["NET_IntegrationAPIUDPPort"]?.GetValue<int>() ?? throw new InvalidDataException("MATRIC integration port is missing.");
        if (port is < 1 or > 65535) throw new InvalidDataException("MATRIC integration port must be between 1 and 65535.");
        return (enabled, port);
    }

    private void LoadSettings() {
        var saved = Properties.Settings.Default;
        var settings = new MiddlewareSettings {
            ListenAddress = saved.ListenAddress, ListenPort = saved.ListenPort, ImportPort = saved.DCSBIOSImportPort,
            DCSBIOSJsonPath = saved.DCSBIOSJsonPath, UpdateFrequency = saved.UpdateFrequency
        };
        try { settings.AircraftVariables = MiddlewareSettings.ParseSelections(saved.AircraftVariables); }
        catch (Exception ex) {
            Program.logger.LogWarning(ex, "Invalid saved variable selections; using defaults");
            MessageBox.Show(this, "Saved variable selections could not be loaded. See Help → Open logs folder for details.", "Settings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        try { settings.Validate(); }
        catch (ArgumentException ex) {
            Program.logger.LogWarning(ex, "Invalid saved network settings; using defaults");
            var defaults = new MiddlewareSettings();
            settings.ListenAddress = defaults.ListenAddress;
            settings.ListenPort = defaults.ListenPort;
            settings.ImportPort = defaults.ImportPort;
            settings.UpdateFrequency = defaults.UpdateFrequency;
        }
        Program.mwSettings = settings;
        txtBIOSListenPort.Value = settings.ListenPort;
        txtDCSBIOSImportPort.Value = settings.ImportPort;
        txtListenIp.Text = settings.ListenAddress;
        txtDCSBiosInstancePath.Text = settings.DCSBIOSJsonPath;
        tbUpdateFrequency.Value = settings.UpdateFrequency;
        lblUpdateFrequency.Text = settings.UpdateFrequency.ToString();
    }

    private static void SaveSettings(MiddlewareSettings? settings = null) {
        settings ??= Program.mwSettings;
        var saved = Properties.Settings.Default;
        saved.ListenAddress = settings.ListenAddress;
        saved.ListenPort = settings.ListenPort;
        saved.DCSBIOSImportPort = settings.ImportPort;
        saved.DCSBIOSJsonPath = settings.DCSBIOSJsonPath;
        saved.UpdateFrequency = settings.UpdateFrequency;
        saved.AircraftVariables = JsonConvert.SerializeObject(settings.AircraftVariables);
        saved.Save();
    }

    private async Task<AircraftBiosConfiguration[]> LoadDCSBiosConfig(string path) {
        if (!Directory.Exists(path)) throw new DirectoryNotFoundException($"DCS-BIOS JSON folder does not exist: {path}");
        var configurations = await Task.Run(() => AircraftBiosConfiguration.AllConfigurations("AircraftAliases.json",
            Program.loggerFactory.CreateLogger<AircraftBiosConfiguration>(), path));
        if (configurations.Length == 0) throw new InvalidDataException("No usable DCS-BIOS modules were found. Choose the doc/json folder. See Help → Open logs folder for details.");
        Program.aircraftBiosConfigurations = configurations.ToDictionary(c => c.AircraftName);
        return configurations;
    }

    private async void btnStartStop_Click(object sender, EventArgs e) {
        await ToggleProcessingAsync();
    }

    internal Task ToggleProcessingAsync() {
        if (_transitioning || _closing) return _transitionTask;
        _transitionTask = ChangeProcessingAsync();
        return _transitionTask;
    }

    internal MiddlewareSettings ReadSettingsFromControls() => new() {
        DCSBIOSJsonPath = txtDCSBiosInstancePath.Text.Trim(),
        ListenAddress = txtListenIp.Text.Trim(),
        ListenPort = (int)txtBIOSListenPort.Value,
        ImportPort = (int)txtDCSBIOSImportPort.Value,
        UpdateFrequency = tbUpdateFrequency.Value,
        AircraftVariables = Program.mwSettings.AircraftVariables.ToDictionary(p => p.Key, p => p.Value.ToList())
    };

    private async Task ChangeProcessingAsync() {
        _transitioning = true;
        _operation = _session == null ? "STARTING…" : "STOPPING…";
        SetControlState();
        try {
            if (_session != null) await StopSessionAsync();
            else {
                var settings = ReadSettingsFromControls();
                settings.Validate();
                var (enabled, port) = GetMatricConfig();
                if (!enabled) throw new InvalidOperationException("Enable the integration API in MATRIC settings and restart MATRIC Server.");
                var warnings = Program.fileLogger.WarningCount;
                var configurations = await LoadDCSBiosConfig(settings.DCSBIOSJsonPath);
                if (_closing) return;
                SaveSettings(settings);
                _session = await Task.Run(() => MiddlewareSession.StartAsync(settings, port, configurations, Program.loggerFactory));
                Program.mwSettings = settings;
                if (_closing) { await StopSessionAsync(); return; }
                if (Program.fileLogger.WarningCount > warnings)
                    MessageBox.Show(this, "Some DCS-BIOS files or outputs were skipped. See Help → Open logs folder for the affected controls and reasons.", "Configuration warnings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex) {
            await StopSessionAsync();
            if (!_closing) ShowError("Could not change middleware state", ex);
        }
        finally { _transitioning = false; SetControlState(); RefreshHealth(); }
    }

    private async Task StopSessionAsync() {
        var session = _session;
        _session = null;
        if (session != null) await session.DisposeAsync();
    }

    private void SetControlState() {
        var editable = _session == null && !_transitioning && !_closing;
        txtDCSBiosInstancePath.Enabled = btnBrowse.Enabled = txtListenIp.Enabled = txtBIOSListenPort.Enabled = txtDCSBIOSImportPort.Enabled = editable;
        btnStartStop.Enabled = !_transitioning && !_closing;
        btnFilterDialog.Enabled = importVariablesListToolStripMenuItem.Enabled = exportVariablesListToolStripMenuItem.Enabled = !_transitioning && !_closing;
        tbUpdateFrequency.Enabled = !_transitioning && !_closing;
        btnStartStop.Text = _transitioning ? _operation : _session == null ? "START" : "STOP";
    }

    private void RefreshHealth() {
        if (_session == null) {
            _biosHealth.Text = _transitioning ? "DCS-BIOS: changing state…" : "DCS-BIOS: stopped";
            _matricHealth.Text = "MATRIC: stopped";
            _biosHealth.ForeColor = _matricHealth.ForeColor = SystemColors.ControlText;
            _aircraft.Text = "Aircraft: —";
            pbBuffer.Value = pbSentVars.Value = 0;
            return;
        }
        var listener = _session.Listener;
        var translator = _session.Translator;
        _biosHealth.Text = "DCS-BIOS: " + (listener.LastError != null ? "error — stop and restart; see logs" : listener.IsReceiving ? "receiving" : "waiting for stream");
        _biosHealth.ForeColor = listener.LastError != null ? Color.Firebrick : listener.IsReceiving ? Color.DarkGreen : Color.DarkGoldenrod;
        _matricHealth.Text = "MATRIC: " + (translator.LastError != null ? "send error; see logs" : translator.IsMatricConnected ? "responding" : "waiting / not responding");
        _matricHealth.ForeColor = translator.IsMatricConnected && translator.LastError == null ? Color.DarkGreen : Color.DarkGoldenrod;
        _aircraft.Text = "Aircraft: " + (string.IsNullOrEmpty(listener.ActiveAircraft) ? "waiting for detection" : listener.ActiveAircraft);
        pbBuffer.Value = Math.Min(translator.PendingCount, pbBuffer.Maximum);
        pbSentVars.Value = Math.Min(translator.LastSentCount, pbSentVars.Maximum);
    }

    private async void OnFormClosing(object? sender, FormClosingEventArgs e) {
        if (_allowClose) return;
        if (_session == null && !_transitioning) return;
        e.Cancel = true;
        if (_closing) return;
        _closing = true;
        SetControlState();
        try { await _transitionTask; await StopSessionAsync(); }
        catch (Exception ex) { Program.logger.LogError(ex, "Error shutting down middleware"); }
        finally { _allowClose = true; Close(); }
    }

    private async void btnFilterDialog_Click(object sender, EventArgs e) {
        if (_transitioning || _closing) return;
        _transitionTask = ConfigureVariablesAsync();
        await _transitionTask;
    }

    private async Task ConfigureVariablesAsync() {
        _transitioning = true;
        _operation = "CONFIGURING…";
        SetControlState();
        try {
            if (_session == null) await LoadDCSBiosConfig(txtDCSBiosInstancePath.Text);
            if (_closing) return;
            using var form = new FormVariablesConfiguration();
            if (form.ShowDialog(this) == DialogResult.OK) _session?.Translator.UpdateConfiguration(Program.mwSettings.AircraftVariables);
        }
        catch (Exception ex) { if (!_closing) ShowError("Could not configure variables", ex); }
        finally { _transitioning = false; SetControlState(); }
    }

    private void tbUpdateFrequency_Scroll(object sender, EventArgs e) {
        Program.mwSettings.UpdateFrequency = tbUpdateFrequency.Value;
        lblUpdateFrequency.Text = tbUpdateFrequency.Value.ToString();
        _session?.Translator.SetUpdateFrequency(tbUpdateFrequency.Value);
        try { SaveSettings(); } catch (Exception ex) { ShowError("Could not save update frequency", ex); }
    }

    private void OpenLogs() {
        try { Directory.CreateDirectory(FileLoggerProvider.LogDirectory); Open(FileLoggerProvider.LogDirectory); }
        catch (Exception ex) { ShowError("Could not open logs", ex); }
    }
    private static void Open(string path) => Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    private void ShowError(string title, Exception ex) {
        Program.logger.LogError(ex, "{Operation}", title);
        MessageBox.Show(this, ex.Message + Environment.NewLine + "Details: Help → Open logs folder.", title, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
    private void manualToolStripMenuItem_Click(object sender, EventArgs e) {
        try { Open(Path.Combine(AppContext.BaseDirectory, "DCSMATRICManual.pdf")); } catch (Exception ex) { ShowError("Could not open manual", ex); }
    }
    private void projectWebToolStripMenuItem_Click(object sender, EventArgs e) => Open("https://github.com/tgudelj/DCS-BIOS-MATRIC-Middleware");
    private void checkForUpdateToolStripMenuItem_Click(object sender, EventArgs e) => Open("https://github.com/tgudelj/DCS-BIOS-MATRIC-Middleware/releases");
    private void lblBiosListenIP_Click(object sender, EventArgs e) => txtListenIp.Focus();
    private void lblBiosListenPort_Click(object sender, EventArgs e) => txtBIOSListenPort.Focus();
    private void lblBiosInstance_Click(object sender, EventArgs e) => txtDCSBiosInstancePath.Focus();
    private void lblUpdatesLabel_Click(object sender, EventArgs e) => tbUpdateFrequency.Focus();

    private void exportVariablesListToolStripMenuItem_Click(object sender, EventArgs e) {
        using var dialog = new SaveFileDialog { Title = "Export variables configuration", Filter = "JSON files|*.json", DefaultExt = "json", AddExtension = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try { File.WriteAllText(dialog.FileName, JsonConvert.SerializeObject(Program.mwSettings.AircraftVariables, Formatting.Indented)); }
        catch (Exception ex) { ShowError("Export failed", ex); }
    }
    private async void importVariablesListToolStripMenuItem_Click(object sender, EventArgs e) {
        if (_transitioning || _closing) return;
        using var dialog = new OpenFileDialog { Title = "Import variables configuration", Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*", CheckFileExists = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _transitionTask = ImportVariablesAsync(dialog.FileName);
        await _transitionTask;
    }

    private async Task ImportVariablesAsync(string path) {
        _transitioning = true;
        _operation = "IMPORTING…";
        SetControlState();
        try {
            var imported = MiddlewareSettings.ParseSelections(await File.ReadAllTextAsync(path));
            if (_closing) return;
            var merged = Program.mwSettings.AircraftVariables.ToDictionary(p => p.Key, p => p.Value.ToList());
            foreach (var (module, variables) in imported)
                merged[module] = merged.GetValueOrDefault(module, new()).Concat(variables).Distinct(StringComparer.Ordinal).ToList();
            Properties.Settings.Default.AircraftVariables = JsonConvert.SerializeObject(merged);
            Properties.Settings.Default.Save();
            Program.mwSettings.AircraftVariables = merged;
            _session?.Translator.UpdateConfiguration(merged);
        }
        catch (Exception ex) { if (!_closing) ShowError("Import failed", ex); }
        finally { _transitioning = false; SetControlState(); }
    }
}
