using Microsoft.Win32;
using Halberion.SystemControl.Core.Models;

namespace Halberion.SystemControl.Core.Services;

public sealed class StartupItemScanner
{
    public const string CoverageNotice = "Run kayıtları ve başlangıç klasörleri listelenir. Zamanlanmış görevler, servisler ve paketli uygulamalar bu envantere dahil değildir. Etkinlik durumunu Windows ayarlarında doğrulayın.";

    public IReadOnlyList<string> LastWarnings { get; private set; } = [];

    public IReadOnlyList<StartupItem> Scan()
    {
        var items = new List<StartupItem>();
        var warnings = new List<string>();
        var nativeView = Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Registry32;
        // HKCU\Software\...\Run is shared between registry views. Read it once.
        ReadRegistryRunKey(items, RegistryHive.CurrentUser, nativeView, "Kullanıcı", warnings);
        ReadRegistryRunKey(items, RegistryHive.LocalMachine, nativeView, "Tüm kullanıcılar", warnings);
        if (Environment.Is64BitOperatingSystem)
        {
            ReadRegistryRunKey(items, RegistryHive.LocalMachine, RegistryView.Registry32, "Tüm kullanıcılar (32 bit)", warnings);
        }

        ReadStartupFolder(items, Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Kullanıcı", warnings);
        ReadStartupFolder(items, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), "Tüm kullanıcılar", warnings);
        LastWarnings = warnings.ToArray();

        return items
            .DistinctBy(item => $"{item.Name}\0{item.Command}\0{item.Scope}", StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private static void ReadRegistryRunKey(
        ICollection<StartupItem> items,
        RegistryHive hive,
        RegistryView view,
        string scope,
        ICollection<string> warnings)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var key = baseKey.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: false);
            if (key is null)
            {
                return;
            }

            foreach (var name in key.GetValueNames())
            {
                var command = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
                if (!string.IsNullOrWhiteSpace(command))
                {
                    items.Add(new StartupItem(string.IsNullOrWhiteSpace(name) ? "(Adsız kayıt)" : name,
                        command, scope, $"Kayıt defteri • {view}"));
                }
            }
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            warnings.Add($"{scope} Run kayıtları tamamen okunamadı ({exception.GetType().Name}).");
        }
    }

    private static void ReadStartupFolder(ICollection<StartupItem> items, string folder, string scope,
        ICollection<string> warnings)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                warnings.Add($"{scope} başlangıç klasörü yolu bulunamadı.");
                return;
            }

            foreach (var file in Directory.EnumerateFiles(folder))
            {
                if (string.Equals(Path.GetFileName(file), "desktop.ini", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                items.Add(new StartupItem(Path.GetFileNameWithoutExtension(file), file, scope, "Başlangıç klasörü"));
            }
        }
        catch (DirectoryNotFoundException)
        {
            // A missing optional startup folder is an empty source, not a permission error.
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            warnings.Add($"{scope} başlangıç klasörü tamamen okunamadı ({exception.GetType().Name}).");
        }
    }
}
