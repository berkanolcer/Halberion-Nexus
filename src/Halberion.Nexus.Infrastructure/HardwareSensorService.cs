using LibreHardwareMonitor.Hardware;

namespace Halberion.Nexus.Infrastructure;

public sealed record HardwareSensorReading(string Hardware, string Type, string Sensor, float Value,
    float Minimum, float Maximum, string Unit)
{
    public string CurrentText => $"{Value:0.##} {Unit}";
    public string MinimumText => $"{Minimum:0.##} {Unit}";
    public string MaximumText => $"{Maximum:0.##} {Unit}";
}

public sealed class HardwareSensorService : IDisposable
{
    private readonly object _sync = new(); private readonly Dictionary<string, (float Min, float Max)> _ranges = new();
    private Computer? _computer; public string? LastError { get; private set; }
    public IReadOnlyList<HardwareSensorReading> Read()
    {
        lock (_sync)
        {
            try
            {
                _computer ??= OpenComputer(); var visitor = new UpdateVisitor(); _computer.Accept(visitor); var result = new List<HardwareSensorReading>();
                foreach (var hardware in _computer.Hardware) Collect(hardware, result);
                LastError = null; return result.OrderBy(x => x.Hardware).ThenBy(x => x.Type).ThenBy(x => x.Sensor).ToArray();
            }
            catch (Exception ex) { LastError = ex.Message; return []; }
        }
    }
    private static Computer OpenComputer() { var computer = new Computer { IsCpuEnabled = true, IsGpuEnabled = true, IsMemoryEnabled = true, IsMotherboardEnabled = true, IsStorageEnabled = true, IsNetworkEnabled = true, IsBatteryEnabled = true, IsControllerEnabled = true }; computer.Open(); return computer; }
    private void Collect(IHardware hardware, List<HardwareSensorReading> output)
    {
        foreach (var sensor in hardware.Sensors)
        {
            if (sensor.Value is not float value || !float.IsFinite(value)) continue; var key = hardware.Identifier + "/" + sensor.Identifier; var range = _ranges.TryGetValue(key, out var old) ? (Math.Min(old.Min, value), Math.Max(old.Max, value)) : (value, value); _ranges[key] = range;
            output.Add(new(hardware.Name, sensor.SensorType.ToString(), sensor.Name, value, range.Item1, range.Item2, Unit(sensor.SensorType)));
        }
        foreach (var sub in hardware.SubHardware) Collect(sub, output);
    }
    private static string Unit(SensorType type) => type switch { SensorType.Temperature => "°C", SensorType.Load or SensorType.Level or SensorType.Control => "%", SensorType.Clock => "MHz", SensorType.Power => "W", SensorType.Fan => "RPM", SensorType.Voltage => "V", SensorType.Data => "GiB", SensorType.SmallData => "MiB", SensorType.Throughput => "B/s", SensorType.Frequency => "Hz", _ => "" };
    public void Dispose() { lock (_sync) { _computer?.Close(); _computer = null; } }
    private sealed class UpdateVisitor : IVisitor
    {
        public void VisitComputer(IComputer computer) => computer.Traverse(this);
        public void VisitHardware(IHardware hardware) { hardware.Update(); foreach (var sub in hardware.SubHardware) sub.Accept(this); }
        public void VisitSensor(ISensor sensor) { }
        public void VisitParameter(IParameter parameter) { }
    }
}
