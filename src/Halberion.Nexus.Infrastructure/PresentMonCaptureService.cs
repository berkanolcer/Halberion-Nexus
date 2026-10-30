using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Halberion.Nexus.Infrastructure;

public sealed record FpsProcessReading(int ProcessId,string Application,double Fps,double AverageFrameTimeMs,double? GpuBusyMs,double? CpuBusyMs)
{
    public string FpsText=>$"{Fps:0.0}";
    public string FrameTimeText=>$"{AverageFrameTimeMs:0.00} ms";
    public string GpuText=>GpuBusyMs is double n?$"{n:0.00} ms":"—";
    public string CpuText=>CpuBusyMs is double n?$"{n:0.00} ms":"—";
}

/// <summary>Reads real presented-frame telemetry from the official optional PresentMon console tool.</summary>
public sealed class PresentMonCaptureService : IDisposable
{
    private Process? _process;
    private readonly ConcurrentDictionary<int,Queue<(double Frame,double? Gpu,double? Cpu,string Name)>> _frames=new();
    private int _frameColumn=-1,_pidColumn=-1,_appColumn=-1,_gpuColumn=-1,_cpuColumn=-1;
    private readonly object _queueLock=new();
    public event EventHandler<IReadOnlyList<FpsProcessReading>>? Updated;
    public event EventHandler<string>? StatusChanged;
    public bool IsRunning=>_process is {HasExited:false};
    public static bool IsPresentMonPath(string? path)=>!string.IsNullOrWhiteSpace(path)&&File.Exists(path)&&Path.GetFileName(path).StartsWith("PresentMon",StringComparison.OrdinalIgnoreCase)&&Path.GetExtension(path).Equals(".exe",StringComparison.OrdinalIgnoreCase);
    public bool TryParseCsvRow(string line, out int pid,out string app,out double frameMs,out double? gpuMs,out double? cpuMs)
    {
        pid=0;app="";frameMs=0;gpuMs=cpuMs=null;
        if(string.IsNullOrWhiteSpace(line))return false;
        var cells=ParseCsv(line);
        if(_frameColumn<0)
        {
            var frame=cells.FindIndex(x=>x.Equals("MsBetweenPresents",StringComparison.OrdinalIgnoreCase));
            if(frame>=0){_frameColumn=frame;_pidColumn=cells.FindIndex(x=>x.Equals("ProcessID",StringComparison.OrdinalIgnoreCase));_appColumn=cells.FindIndex(x=>x.Equals("Application",StringComparison.OrdinalIgnoreCase));_gpuColumn=cells.FindIndex(x=>x.Equals("MsGPUBusy",StringComparison.OrdinalIgnoreCase));_cpuColumn=cells.FindIndex(x=>x.Equals("MsCPUBusy",StringComparison.OrdinalIgnoreCase));}
            return false;
        }
        if(cells.Count<=Math.Max(_frameColumn,Math.Max(_pidColumn,_appColumn))||_pidColumn<0||!int.TryParse(cells[_pidColumn],out pid)||!double.TryParse(cells[_frameColumn],NumberStyles.Float,CultureInfo.InvariantCulture,out frameMs)||frameMs<=0||frameMs>1000)return false;
        app=_appColumn>=0?cells[_appColumn]:"Unknown process";
        if(_gpuColumn>=0&&cells.Count>_gpuColumn&&double.TryParse(cells[_gpuColumn],NumberStyles.Float,CultureInfo.InvariantCulture,out var gpuValue)&&double.IsFinite(gpuValue))gpuMs=gpuValue;
        if(_cpuColumn>=0&&cells.Count>_cpuColumn&&double.TryParse(cells[_cpuColumn],NumberStyles.Float,CultureInfo.InvariantCulture,out var cpuValue)&&double.IsFinite(cpuValue))cpuMs=cpuValue;
        return true;
    }
    private static List<string> ParseCsv(string line)
    {
        var result=new List<string>();var value=new StringBuilder();var quoted=false;
        for(var i=0;i<line.Length;i++){var c=line[i];if(c=='"'){if(quoted&&i+1<line.Length&&line[i+1]=='"'){value.Append('"');i++;}else quoted=!quoted;}else if(c==','&&!quoted){result.Add(value.ToString());value.Clear();}else value.Append(c);}result.Add(value.ToString());return result;
    }
    public void Start(string executable)
    {
        if(!IsPresentMonPath(executable))throw new FileNotFoundException("PresentMon yürütülebilir dosyası bulunamadı veya adı doğrulanamadı.",executable);
        Stop();lock(_queueLock){_frames.Clear();_frameColumn=_pidColumn=_appColumn=_gpuColumn=_cpuColumn=-1;}
        var info=new ProcessStartInfo(executable){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8};
        info.ArgumentList.Add("--output_stdout");info.ArgumentList.Add("--v2_metrics");info.ArgumentList.Add("--no_console_stats");info.ArgumentList.Add("--session_name");info.ArgumentList.Add("HalberionNexusFPS");
        _process=new Process{StartInfo=info,EnableRaisingEvents=true};
        _process.OutputDataReceived+=(_,e)=>{if(e.Data is null)return;lock(_queueLock){if(TryParseCsvRow(e.Data,out var pid,out var app,out var frame,out var gpu,out var cpu)){var queue=_frames.GetOrAdd(pid,_=>new Queue<(double,double?,double?,string)>());queue.Enqueue((frame,gpu,cpu,app));while(queue.Count>120)queue.Dequeue();var snapshot=_frames.Select(x=>{var rows=x.Value.ToArray();var avg=rows.Average(y=>y.Frame);return new FpsProcessReading(x.Key,rows[^1].Name,1000/avg,avg,AverageOrNull(rows.Select(y=>y.Gpu)),AverageOrNull(rows.Select(y=>y.Cpu)));}).OrderByDescending(x=>x.Fps).ToArray();Updated?.Invoke(this,snapshot);}}};
        _process.ErrorDataReceived+=(_,e)=>{if(!string.IsNullOrWhiteSpace(e.Data))StatusChanged?.Invoke(this,e.Data);};
        _process.Exited+=(_,_)=>{try{StatusChanged?.Invoke(this,_process?.ExitCode==0?"FPS capture ended":"PresentMon exited. It may require Windows administrator permission or a compatible driver.");}catch{}};
        if(!_process.Start())throw new InvalidOperationException("PresentMon başlatılamadı.");_process.BeginOutputReadLine();_process.BeginErrorReadLine();StatusChanged?.Invoke(this,"FPS capture active • Real PresentMon frame events");
    }
    private static double? AverageOrNull(IEnumerable<double?> values){var valid=values.Where(x=>x is double n&&double.IsFinite(n)).Select(x=>x!.Value).ToArray();return valid.Length>0?valid.Average():null;}
    public void Stop(){try{if(_process is {HasExited:false})_process.Kill(true);}catch{}try{_process?.Dispose();}catch{} _process=null;StatusChanged?.Invoke(this,"FPS capture stopped");}
    public void Dispose()=>Stop();
}
