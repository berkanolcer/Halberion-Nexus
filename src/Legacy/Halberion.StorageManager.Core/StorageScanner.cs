using System.Diagnostics;
using System.ComponentModel;
using System.Security;
using System.Security.Cryptography;

namespace Halberion.StorageManager.Core;

public static class StorageScanner
{
    public static IReadOnlyList<DriveSnapshot> ReadDrives()
    {
        var rows = new List<DriveSnapshot>();
        DriveInfo[] drives;
        try { drives = DriveInfo.GetDrives(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or Win32Exception)
        {
            return rows;
        }

        foreach (var drive in drives)
        {
            try
            {
                if (!drive.IsReady) continue;
                rows.Add(new(drive.Name, drive.VolumeLabel, drive.DriveFormat,
                    drive.DriveType.ToString(), drive.TotalSize, drive.AvailableFreeSpace));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or Win32Exception or NotSupportedException)
            {
                // One disconnected, restricted, or faulty volume must not prevent
                // the rest of the drive inventory from being shown.
            }
        }
        return rows.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static Task<FolderScanResult> ScanAsync(string rootPath, int largestLimit,
        IProgress<ScanProgress>? progress, CancellationToken cancellationToken) =>
        Task.Run(() => Scan(rootPath, largestLimit, progress, cancellationToken), cancellationToken);

    public static FolderScanResult Scan(string rootPath, int largestLimit = 100,
        IProgress<ScanProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rootPath)) throw new ArgumentException("Klasör yolu boş olamaz.", nameof(rootPath));
        if (largestLimit is < 1 or > 10_000) throw new ArgumentOutOfRangeException(nameof(largestLimit));
        var root = Path.GetFullPath(rootPath);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);

        var timer = Stopwatch.StartNew();
        var queue = new PriorityQueue<FileEntry, long>();
        var groups = new Dictionary<string, (long Bytes, long Files)>(StringComparer.OrdinalIgnoreCase);
        var pending = new Stack<string>();
        pending.Push(root);
        long total = 0, files = 0, directories = 0, skipped = 0;

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = pending.Pop();
            FileInfo[] currentFiles;
            DirectoryInfo[] children;
            try
            {
                var info = new DirectoryInfo(current);
                currentFiles = info.GetFiles();
                children = info.GetDirectories();
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                skipped++;
                continue;
            }

            foreach (var file in currentFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var length = Math.Max(0, file.Length);
                    total = SaturatingAdd(total, length);
                    files++;
                    var entry = new FileEntry(file.Name, file.FullName, length, file.LastWriteTimeUtc);
                    queue.Enqueue(entry, length);
                    if (queue.Count > largestLimit) queue.Dequeue();
                    var key = FirstSegment(root, file.FullName);
                    groups.TryGetValue(key, out var value);
                    groups[key] = (SaturatingAdd(value.Bytes, length), value.Files + 1);
                    if (files % 200 == 0) progress?.Report(new(files, total, file.FullName));
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                {
                    skipped++;
                }
            }

            foreach (var child in children)
            {
                try
                {
                    if ((child.Attributes & FileAttributes.ReparsePoint) != 0) { skipped++; continue; }
                    directories++;
                    pending.Push(child.FullName);
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException) { skipped++; }
            }
        }

        timer.Stop();
        var top = groups.Select(x => new DirectoryEntry(x.Key,
                x.Key == "(kök dosyaları)" ? root : Path.Combine(root, x.Key), x.Value.Bytes, x.Value.Files))
            .OrderByDescending(x => x.Bytes).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        var largest = queue.UnorderedItems.Select(x => x.Element)
            .OrderByDescending(x => x.Bytes).ThenBy(x => x.Path, StringComparer.OrdinalIgnoreCase).ToArray();
        var isUnity = Directory.Exists(Path.Combine(root, "Assets")) &&
            File.Exists(Path.Combine(root, "ProjectSettings", "ProjectVersion.txt"));
        var caches = isUnity ? BuildUnityCaches(root, top) : [];
        progress?.Report(new(files, total, root));
        return new(root, total, files, directories, skipped, timer.Elapsed, largest, top, isUnity, caches);
    }

    public static Task<TempCandidateResult> FindTempCandidatesAsync(string rootPath, DateTime cutoffUtc,
        int resultLimit, CancellationToken cancellationToken) =>
        Task.Run(() => FindTempCandidates(rootPath, cutoffUtc, resultLimit, cancellationToken), cancellationToken);

    /// <summary>Read-only duplicate search. Sizes narrow candidates; SHA-256 verifies byte-identical files.</summary>
    public static Task<IReadOnlyList<DuplicateFileGroup>> FindDuplicatesAsync(string rootPath,
        IProgress<ScanProgress>? progress, CancellationToken cancellationToken) =>
        Task.Run(() => FindDuplicates(rootPath, progress, cancellationToken), cancellationToken);

    public static IReadOnlyList<DuplicateFileGroup> FindDuplicates(string rootPath,
        IProgress<ScanProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rootPath)) throw new ArgumentException("Klasör yolu boş olamaz.", nameof(rootPath));
        var root = Path.GetFullPath(rootPath);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
        var bySize = new Dictionary<long, List<FileEntry>>(); var pending = new Stack<string>(); pending.Push(root);
        long count=0, bytes=0;
        while(pending.Count>0)
        {
            cancellationToken.ThrowIfCancellationRequested();var folder=pending.Pop();FileInfo[] files;DirectoryInfo[] children;
            try{var info=new DirectoryInfo(folder);files=info.GetFiles();children=info.GetDirectories();}
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){continue;}
            foreach(var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try{var entry=new FileEntry(file.Name,file.FullName,file.Length,file.LastWriteTimeUtc);if(!bySize.TryGetValue(entry.Bytes,out var sameSize))bySize[entry.Bytes]=sameSize=[];sameSize.Add(entry);count++;bytes=SaturatingAdd(bytes,entry.Bytes);if(count%250==0)progress?.Report(new(count,bytes,entry.Path));}
                catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){}
            }
            foreach(var dir in children)try{if((dir.Attributes&FileAttributes.ReparsePoint)==0)pending.Push(dir.FullName);}catch(Exception ex)when(ex is IOException or UnauthorizedAccessException){}
        }
        var groups=new List<DuplicateFileGroup>();long checkedCount=0;
        foreach(var candidates in bySize.Values.Where(x=>x.Count>1))
        {
            cancellationToken.ThrowIfCancellationRequested();var byHash=new Dictionary<string,List<FileEntry>>(StringComparer.OrdinalIgnoreCase);
            foreach(var entry in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try{using var stream=new FileStream(entry.Path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete,1024*128,FileOptions.SequentialScan);var hash=Convert.ToHexString(SHA256.HashData(stream));if(!byHash.TryGetValue(hash,out var sameHash))byHash[hash]=sameHash=[];sameHash.Add(entry);}
                catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or System.Security.SecurityException){}
                checkedCount++;if(checkedCount%50==0)progress?.Report(new(checkedCount,bytes,entry.Path));
            }
            groups.AddRange(byHash.Where(x=>x.Value.Count>1).Select(x=>new DuplicateFileGroup(x.Key,x.Value[0].Bytes,x.Value.OrderBy(f=>f.Path,StringComparer.OrdinalIgnoreCase).ToArray())));
        }
        progress?.Report(new(count,bytes,root));return groups.OrderByDescending(x=>x.PotentialDuplicateBytes).ToArray();
    }

    public static TempCandidateResult FindTempCandidates(string rootPath, DateTime cutoffUtc,
        int resultLimit = 500, CancellationToken cancellationToken = default)
    {
        if (resultLimit is < 1 or > 10_000) throw new ArgumentOutOfRangeException(nameof(resultLimit));
        var root = Path.GetFullPath(rootPath);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
        var pending = new Stack<string>();
        var results = new List<TempCandidate>();
        pending.Push(root);
        long total = 0, count = 0, skipped = 0;
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = pending.Pop();
            FileInfo[] files;
            DirectoryInfo[] dirs;
            try { var info = new DirectoryInfo(current); files = info.GetFiles(); dirs = info.GetDirectories(); }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException) { skipped++; continue; }
            foreach (var file in files)
            {
                try
                {
                    if (file.LastWriteTimeUtc >= cutoffUtc) continue;
                    var bytes = Math.Max(0, file.Length);
                    total = SaturatingAdd(total, bytes); count++;
                    results.Add(new(file.Name, file.FullName, bytes, file.LastWriteTimeUtc,
                        Math.Max(0, (int)(DateTime.UtcNow - file.LastWriteTimeUtc).TotalDays)));
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException) { skipped++; }
            }
            foreach (var dir in dirs)
            {
                try
                {
                    if ((dir.Attributes & FileAttributes.ReparsePoint) != 0) { skipped++; continue; }
                    pending.Push(dir.FullName);
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException) { skipped++; }
            }
        }
        var limited = results.OrderByDescending(x => x.Bytes).ThenBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
            .Take(resultLimit).ToArray();
        return new(root, cutoffUtc, total, count, skipped, limited);
    }

    private static IReadOnlyList<UnityCacheEntry> BuildUnityCaches(string root, IReadOnlyList<DirectoryEntry> top)
    {
        var notes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Library"] = "Unity tarafından yeniden oluşturulabilir; proje kapalıyken değerlendirin.",
            ["Temp"] = "Geçici derleme verisi; Unity kapalıyken değerlendirin.",
            ["Logs"] = "Tanılama günlükleri; sorun araştırmıyorsanız gözden geçirilebilir.",
            ["obj"] = ".NET ara derleme çıktısı; yeniden oluşturulabilir."
        };
        return notes.Select(x =>
        {
            var row = top.FirstOrDefault(t => string.Equals(t.Name, x.Key, StringComparison.OrdinalIgnoreCase));
            return new UnityCacheEntry(x.Key, Path.Combine(root, x.Key), row?.Bytes ?? 0, x.Value);
        }).Where(x => Directory.Exists(x.Path)).OrderByDescending(x => x.Bytes).ToArray();
    }

    private static string FirstSegment(string root, string filePath)
    {
        var relative = Path.GetRelativePath(root, filePath);
        var index = relative.IndexOf(Path.DirectorySeparatorChar);
        return index < 0 ? "(kök dosyaları)" : relative[..index];
    }

    private static long SaturatingAdd(long left, long right) =>
        right > 0 && left > long.MaxValue - right ? long.MaxValue : left + right;
}
