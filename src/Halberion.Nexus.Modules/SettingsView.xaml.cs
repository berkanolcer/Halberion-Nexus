using System.Windows;
using System.Windows.Controls;
using Halberion.Nexus.Core;
using Halberion.Nexus.Infrastructure;

namespace Halberion.Nexus.Modules;
public partial class SettingsView : UserControl, INexusModule
{
    private readonly NexusServices _services; public string Id => "settings"; public string Title => "Settings";
    public SettingsView(NexusServices services) { _services = services; InitializeComponent(); }
    public Task ActivateAsync(CancellationToken cancellationToken) { var value = _services.Settings.Current; Select(LanguageBox,value.Language); Select(RefreshBox, value.RefreshSeconds); Select(TempAgeBox, value.TemporaryFileAgeDays); AlertsCheck.IsChecked = value.NetworkAlertsEnabled; Localization.Apply(this); return Task.CompletedTask; }
    public Task DeactivateAsync() => Task.CompletedTask;
    private void Save_Click(object sender, RoutedEventArgs e) { try { var language=((ComboBoxItem)LanguageBox.SelectedItem).Tag?.ToString()??"auto";_services.Settings.Save(_services.Settings.Current with{RefreshSeconds=Read(RefreshBox),NetworkAlertsEnabled=AlertsCheck.IsChecked==true,TemporaryFileAgeDays=Read(TempAgeBox),Language=language});Localization.SetMode(language);StatusText.Text = Localization.IsEnglish?"Settings saved.":"Ayarlar kaydedildi."; Localization.Apply(this); } catch (Exception ex) { StatusText.Text = "Kaydedilemedi: " + ex.Message; } }
    private static int Read(ComboBox box) => int.Parse(((ComboBoxItem)box.SelectedItem).Tag!.ToString()!);
    private static void Select(ComboBox box, int value) { box.SelectedItem = box.Items.Cast<ComboBoxItem>().FirstOrDefault(x => x.Tag?.ToString() == value.ToString()) ?? box.Items[0]; }
    private static void Select(ComboBox box,string value){box.SelectedItem=box.Items.Cast<ComboBoxItem>().FirstOrDefault(x=>x.Tag?.ToString()==value)??box.Items[0];}
}
