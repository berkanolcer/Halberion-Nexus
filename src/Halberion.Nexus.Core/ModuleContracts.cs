namespace Halberion.Nexus.Core;

public interface INexusModule
{
    string Id { get; }
    string Title { get; }
    Task ActivateAsync(CancellationToken cancellationToken);
    Task DeactivateAsync();
}

public sealed class OperationGate
{
    private readonly Dictionary<string, SemaphoreSlim> _gates = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _sync = new();
    public async Task<IDisposable> EnterAsync(string key, CancellationToken cancellationToken)
    {
        SemaphoreSlim gate;
        lock (_sync)
        {
            if (!_gates.TryGetValue(key, out gate!)) _gates[key] = gate = new(1, 1);
        }
        await gate.WaitAsync(cancellationToken);
        return new Release(gate);
    }
    private sealed class Release(SemaphoreSlim gate) : IDisposable
    {
        private bool _done;
        public void Dispose() { if (_done) return; _done = true; gate.Release(); }
    }
}

public sealed record NexusLogEntry(DateTimeOffset Time, string Module, string Level, string Message);
public sealed class NexusLog
{
    private readonly List<NexusLogEntry> _entries = [];
    private readonly object _sync = new();
    public event EventHandler? Changed;
    public event Action<NexusLogEntry>? EntryAdded;
    public IReadOnlyList<NexusLogEntry> Snapshot { get { lock (_sync) return _entries.ToArray(); } }
    public void Add(string module, string level, string message)
    {
        var safe = message.Replace("Passphrase", "Credential", StringComparison.OrdinalIgnoreCase);
        var entry = new NexusLogEntry(DateTimeOffset.Now, module, level, safe);
        lock (_sync) { _entries.Add(entry); if (_entries.Count > 2_000) _entries.RemoveRange(0, 500); }
        EntryAdded?.Invoke(entry); Changed?.Invoke(this, EventArgs.Empty);
    }
}
