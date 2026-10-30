using System.Windows.Controls;
using Halberion.Nexus.Core;
using Halberion.Nexus.Infrastructure;

namespace Halberion.Nexus.Modules;
public partial class LogsView : UserControl, INexusModule
{
    private readonly NexusServices _services; public string Id => "logs"; public string Title => "Logs";
    public LogsView(NexusServices services) { _services = services; InitializeComponent(); _services.Log.Changed += (_, _) => Dispatcher.Invoke(Render); }
    public Task ActivateAsync(CancellationToken cancellationToken) { Render(); return Task.CompletedTask; }
    public Task DeactivateAsync() => Task.CompletedTask;
    private void Render() { LogGrid.ItemsSource = null; LogGrid.ItemsSource = _services.Log.Snapshot.Reverse().ToArray(); }
}
