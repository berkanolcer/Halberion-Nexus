using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using Halberion.Nexus.Core;
using Halberion.Nexus.Infrastructure;
using Halberion.Nexus.Modules;
using Localization = Halberion.Nexus.Modules.Localization;

namespace Halberion.Nexus.App;

public partial class MainWindow : Window
{
    private readonly NexusServices _services = new();
    private readonly Dictionary<string, (UserControl View, INexusModule Module)> _modules;
    private INexusModule? _active;
    private bool _initializing = true,_updatingLanguageSelector;
    public MainWindow()
    {
        InitializeComponent();
        Localization.SetMode(_services.Settings.Current.Language);
        LanguageBox.SelectedItem = LanguageBox.Items.Cast<ComboBoxItem>().FirstOrDefault(x => Equals(x.Tag?.ToString(), _services.Settings.Current.Language)) ?? LanguageBox.Items[0];
        _modules = new()
        {
            ["dashboard"] = Pair(new DashboardView(_services)), ["hotspot"] = Pair(new HotspotView(_services)), ["system"] = Pair(new SystemView(_services)), ["network"] = Pair(new NetworkView(_services)), ["device"] = Pair(new DeviceView(_services)), ["storage"] = Pair(new StorageView(_services)), ["settings"] = Pair(new SettingsView(_services)), ["logs"] = Pair(new LogsView(_services))
        };
        ((DashboardView)_modules["dashboard"].View).NavigationRequested += (_, id) => _ = NavigateAsync(id);
        _services.Settings.Changed += (_, _) => Dispatcher.Invoke(() =>
        {
            var mode=_services.Settings.Current.Language;Localization.SetMode(mode);var item=LanguageBox.Items.Cast<ComboBoxItem>().FirstOrDefault(x=>Equals(x.Tag?.ToString(),mode));
            if(item is not null&&!ReferenceEquals(LanguageBox.SelectedItem,item)){_updatingLanguageSelector=true;LanguageBox.SelectedItem=item;_updatingLanguageSelector=false;}
            WelcomeText.Text=Localization.T("Nexus, Halberion'a hoş geldin");Localization.Apply(this);var pair=_modules.Values.FirstOrDefault(x=>x.Module==_active);if(pair.View is not null)Localization.Apply(pair.View);if(_active is not null)FooterText.Text=Localization.IsEnglish?$"{Localization.T(_active.Title)} active • {DateTime.Now:HH:mm:ss}":$"{_active.Title} etkin • {DateTime.Now:HH:mm:ss}";
        });
        Loaded += async (_, _) => { _initializing = false; Localization.Apply(this); await NavigateAsync("dashboard"); ShowWelcome(); };
        Closing += (_, _) => { _active?.DeactivateAsync().GetAwaiter().GetResult(); _services.Dispose(); };
    }
    private static (UserControl, INexusModule) Pair<T>(T view) where T : UserControl, INexusModule => (view, view);
    private async void Navigate_Click(object sender, RoutedEventArgs e) { if (sender is Button { Tag: string id }) await NavigateAsync(id); }
    private void Language_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing||_updatingLanguageSelector || LanguageBox.SelectedItem is not ComboBoxItem item || item.Tag is not string mode) return;
        try { _services.Settings.Save(_services.Settings.Current with { Language = mode }); Localization.SetMode(mode); WelcomeText.Text = Localization.T("Nexus, Halberion'a hoş geldin"); Localization.Apply(this); var activePair=_modules.Values.FirstOrDefault(x=>x.Module==_active); if(activePair.View is not null) Localization.Apply(activePair.View);if(_active is not null)FooterText.Text=Localization.IsEnglish?$"{Localization.T(_active.Title)} active • {DateTime.Now:HH:mm:ss}":$"{_active.Title} etkin • {DateTime.Now:HH:mm:ss}"; }
        catch (Exception ex) { _services.Log.Add("Settings", "Hata", "Dil kaydedilemedi: " + ex.Message); }
    }
    private async Task NavigateAsync(string id)
    {
        if (id == "about") { if (_active is not null) await _active.DeactivateAsync(); _active = null; ContentHost.Content = BuildAbout(); PageHeading.Text = Localization.T("Hakkında"); FooterText.Text = "Halberion Nexus • Source-Available License"; Localization.Apply(this); return; }
        if (!_modules.TryGetValue(id, out var item)) return;
        if (_active != item.Module) { if (_active is not null) await _active.DeactivateAsync(); _active = item.Module; ContentHost.Content = item.View; }
        PageHeading.Text = Localization.T(item.Module.Title);
        try { await item.Module.ActivateAsync(CancellationToken.None); Localization.Apply(item.View); FooterText.Text = Localization.IsEnglish ? $"{Localization.T(item.Module.Title)} active • {DateTime.Now:HH:mm:ss}" : $"{item.Module.Title} etkin • {DateTime.Now:HH:mm:ss}"; }
        catch (Exception ex) { _services.Log.Add(item.Module.Title, "Hata", ex.Message); FooterText.Text = item.Module.Title + " açılamadı: " + ex.Message; }
    }
    private void ShowWelcome()
    {
        WelcomeText.Text = Localization.T("Nexus, Halberion'a hoş geldin");
        WelcomeContinueButton.Content = Localization.T("Devam et");
        WelcomeOverlay.Visibility = Visibility.Visible;
        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(360)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        WelcomeOverlay.BeginAnimation(OpacityProperty, fadeIn);
    }
    private void ContinueWelcome_Click(object sender, RoutedEventArgs e)
    {
        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(360)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
        fadeOut.Completed += (_, _) => WelcomeOverlay.Visibility = Visibility.Collapsed;
        WelcomeOverlay.BeginAnimation(OpacityProperty, fadeOut);
    }
    private UIElement BuildAbout()
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = "HALBERION NEXUS", FontSize = 25, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = "Unified System Control • v0.2.5", Foreground = (System.Windows.Media.Brush)FindResource("MutedBrush"), Margin = new Thickness(0, 5, 0, 18) });
        panel.Children.Add(new TextBlock { Text = "Geliştirici: Azat Berkan Ölçer\nYayıncı: Halberion Studio\nKaynak-kullanılabilir lisans: Halberion Nexus Source-Available License", LineHeight = 25, Margin = new Thickness(0, 0, 0, 18) });
        panel.Children.Add(new TextBlock { Text = "Nexus gerçek Windows ve donanım verilerini gösterir. Sensör veya FPS verisi yoksa tahmin etmez; sistem performansı artışı garanti etmez. Kritik değişiklikler açık onay gerektirir. PdaNet bağlantısı otomatik olarak sıfırlanmaz.", TextWrapping = TextWrapping.Wrap, LineHeight = 24, Margin = new Thickness(0, 0, 0, 20) });
        panel.Children.Add(Link("Halberion web sitesi  ·  halberion.com", "https://www.halberion.com"));
        panel.Children.Add(Link("Instagram  ·  @azatberkanolcer", "https://www.instagram.com/azatberkanolcer/"));
        panel.Children.Add(new TextBlock { Text = "Network Monitor app/PID connection data stays local. Nexus uses a custom source-available license; third-party components retain their own licenses.", TextWrapping = TextWrapping.Wrap, Foreground = (System.Windows.Media.Brush)FindResource("MutedBrush"), Margin = new Thickness(0, 20, 0, 0) });
        Localization.Apply(panel); return new Border { Style = (Style)FindResource("CardStyle"), MaxWidth = 820, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Child = panel };
    }
    private static Button Link(string label, string uri) { var button = new Button { Content = label, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 8), Tag = uri }; button.Click += (_, _) => Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true }); return button; }
}
