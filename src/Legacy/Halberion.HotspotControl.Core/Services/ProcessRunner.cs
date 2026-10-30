using System.Diagnostics;
using System.Text;

namespace Halberion.HotspotControl.Services;

public static class ProcessRunner
{
    public static async Task<string> RunReadOnlyAsync(string fileName, string arguments, CancellationToken cancellationToken = default)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            }
        };

        process.Start();
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var output = await outputTask;
        var error = await errorTask;
        return string.IsNullOrWhiteSpace(error) ? output.Trim() : $"{output.Trim()}\n{error.Trim()}".Trim();
    }
}
