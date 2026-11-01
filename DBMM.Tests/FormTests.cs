using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using EXM.DBMM;
using Xunit;

namespace DBMM.Tests;

public class FormTests {
    private static void Invoke(object instance, string method, params object[] args) =>
        instance.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(instance, args);

    [Fact]
    public async Task InitialAircraftShowsSavedSelectionsAndFilteredEditPreservesHiddenItems() {
        var config = await TestSupport.Config("A", "NEW_VAR");
        await TestSupport.Sta(() => {
            var originalConfigs = Program.aircraftBiosConfigurations;
            var originalSettings = Program.mwSettings;
            try {
                Program.aircraftBiosConfigurations = new() { ["A"] = config };
                Program.mwSettings = new() { AircraftVariables = new() { ["A"] = new() { "A_VAR", "B_VAR" } } };
                using var form = new FormVariablesConfiguration();
                _ = form.Handle;
                Invoke(form, "FormVariablesFilter_Load", form, EventArgs.Empty);
                var configured = (ListBox)form.Controls.Find("lstConfiguredVariables", true).Single();
                Assert.Equal(new[] { "A_VAR", "B_VAR" }, configured.Items.Cast<string>());
                ((TextBox)form.Controls.Find("txtConfiguredFilter", true).Single()).Text = "A_";
                Invoke(form, "btnAddAll_Click", form, EventArgs.Empty);
                ((TextBox)form.Controls.Find("txtConfiguredFilter", true).Single()).Text = "";
                Assert.Equal(new[] { "A_VAR", "B_VAR", "NEW_VAR" }, configured.Items.Cast<string>());
                Assert.Equal(2, Program.mwSettings.AircraftVariables["A"].Count);
                Render(form, "variables.png");
            }
            finally { Program.aircraftBiosConfigurations = originalConfigs; Program.mwSettings = originalSettings; }
        });
    }

    [Fact]
    public async Task ImportPortLoadsAndEditedAddressIsIncludedInStartupSettings() {
        await TestSupport.Sta(() => {
            var saved = EXM.DBMM.Properties.Settings.Default;
            var original = (saved.ListenAddress, saved.ListenPort, saved.DCSBIOSImportPort, saved.UpdateFrequency, saved.AircraftVariables);
            var originalSettings = Program.mwSettings;
            try {
                saved.ListenAddress = "239.255.50.11";
                saved.ListenPort = 5011;
                saved.DCSBIOSImportPort = 8888;
                saved.UpdateFrequency = 25;
                saved.AircraftVariables = "{}";
                using var form = new FormMain();
                var moduleLabel = (Label)form.Controls.Find("lblActiveModule", true).Single();
                Assert.Equal("Module: —", moduleLabel.Text);
                Invoke(form, "RefreshHealth");
                Assert.Equal("Module: —", moduleLabel.Text);
                Invoke(form, "LoadSettings");
                var settings = form.ReadSettingsFromControls();
                Assert.Equal(8888, settings.ImportPort);
                Assert.Equal(5011, settings.ListenPort);
                Assert.Equal(25, settings.UpdateFrequency);
                ((ComboBox)form.Controls.Find("txtListenIp", true).Single()).Text = "239.255.50.12";
                Assert.Equal("239.255.50.12", form.ReadSettingsFromControls().ListenAddress);
                Render(form, "main.png");
            }
            finally {
                (saved.ListenAddress, saved.ListenPort, saved.DCSBIOSImportPort, saved.UpdateFrequency, saved.AircraftVariables) = original;
                Program.mwSettings = originalSettings;
            }
        });
    }

    private static void Render(Form form, string name) {
        var directory = Environment.GetEnvironmentVariable("DBMM_REVIEW_SCREENSHOTS");
        if (directory == null) return;
        Directory.CreateDirectory(directory);
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(-32000, -32000);
        form.Show();
        form.Update();
        using var image = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size));
        image.Save(Path.Combine(directory, name));
        form.Hide();
    }
}
