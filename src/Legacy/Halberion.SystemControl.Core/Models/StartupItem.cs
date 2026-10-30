namespace Halberion.SystemControl.Core.Models;

public sealed record StartupItem(
    string Name,
    string Command,
    string Scope,
    string Source)
{
    // Presence in Run or a startup folder does not prove Windows will enable the entry.
    public string StatusText => "Windows’ta doğrulayın";
}
