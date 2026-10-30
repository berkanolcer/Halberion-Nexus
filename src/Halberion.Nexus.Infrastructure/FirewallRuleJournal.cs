using System.Text.Json;

namespace Halberion.Nexus.Infrastructure;
public sealed record FirewallRuleEntry(string RuleName, string ProgramPath, DateTimeOffset CreatedAt);
public sealed class FirewallRuleJournal
{
    private readonly string _path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Halberion", "Nexus", "firewall-undo.json");
    private readonly object _sync = new();
    public IReadOnlyList<FirewallRuleEntry> Entries { get { lock (_sync) return Load().ToArray(); } }
    public void Add(FirewallRuleEntry entry) { lock (_sync) { var rows = Load(); rows.Add(entry); Save(rows); } }
    public void Remove(string ruleName) { lock (_sync) { var rows = Load(); rows.RemoveAll(x => string.Equals(x.RuleName, ruleName, StringComparison.Ordinal)); Save(rows); } }
    private List<FirewallRuleEntry> Load() { try { return File.Exists(_path) ? JsonSerializer.Deserialize<List<FirewallRuleEntry>>(File.ReadAllText(_path)) ?? [] : []; } catch { return []; } }
    private void Save(List<FirewallRuleEntry> rows) { Directory.CreateDirectory(Path.GetDirectoryName(_path)!); var temp = _path + ".tmp"; File.WriteAllText(temp, JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true })); File.Move(temp, _path, true); }
}
