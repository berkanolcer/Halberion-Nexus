using System.Text.Json;

namespace Halberion.Nexus.Infrastructure;

public sealed record NexusSettings(int RefreshSeconds = 2, bool NetworkAlertsEnabled = true, int TemporaryFileAgeDays = 7,
    string Language = "auto", string? NetworkAdapterId = null, string? PresentMonPath = null);

public sealed class NexusSettingsService
{
    private readonly string _path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Halberion", "Nexus", "settings.json");
    public NexusSettings Current { get; private set; }
    public event EventHandler? Changed;
    public NexusSettingsService() { Current = Load(); }
    public void Save(NexusSettings settings)
    {
        if (settings.RefreshSeconds is < 1 or > 30 || settings.TemporaryFileAgeDays is < 1 or > 365 || settings.Language is not ("auto" or "tr" or "en")) throw new ArgumentOutOfRangeException(nameof(settings));
        var folder = Path.GetDirectoryName(_path)!; Directory.CreateDirectory(folder); var temporary = _path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true })); File.Move(temporary, _path, true); Current = settings; Changed?.Invoke(this, EventArgs.Empty);
    }
    private NexusSettings Load()
    {
        try { if (!File.Exists(_path)) return new(); var value = JsonSerializer.Deserialize<NexusSettings>(File.ReadAllText(_path)); return value is not null && value.RefreshSeconds is >= 1 and <= 30 && value.TemporaryFileAgeDays is >= 1 and <= 365 && value.Language is "auto" or "tr" or "en" ? value : new(); }
        catch { return new(); }
    }
}
