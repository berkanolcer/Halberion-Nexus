namespace Halberion.SystemControl.Core.Models;

public sealed record HardwareSummary(
    string ComputerName,
    string OperatingSystem,
    string Processor,
    string GraphicsAdapter,
    int LogicalProcessorCount);
