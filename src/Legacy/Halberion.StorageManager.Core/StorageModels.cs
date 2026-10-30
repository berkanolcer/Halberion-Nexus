namespace Halberion.StorageManager.Core;

public sealed record DriveSnapshot(string Name, string Label, string Format, string Type,
    long TotalBytes, long FreeBytes)
{
    public long UsedBytes => Math.Max(0, TotalBytes - FreeBytes);
    public double UsedPercent => TotalBytes <= 0 ? 0 : UsedBytes * 100d / TotalBytes;
}

public sealed record FileEntry(string Name, string Path, long Bytes, DateTime LastWriteUtc);
public sealed record DirectoryEntry(string Name, string Path, long Bytes, long FileCount);
public sealed record UnityCacheEntry(string Name, string Path, long Bytes, string Note);
public sealed record ScanProgress(long Files, long Bytes, string CurrentPath);

public sealed record FolderScanResult(string RootPath, long TotalBytes, long FileCount,
    long DirectoryCount, long SkippedCount, TimeSpan Duration,
    IReadOnlyList<FileEntry> LargestFiles, IReadOnlyList<DirectoryEntry> TopDirectories,
    bool IsUnityProject, IReadOnlyList<UnityCacheEntry> UnityCaches);

public sealed record TempCandidate(string Name, string Path, long Bytes, DateTime LastWriteUtc,
    int AgeDays);

public sealed record DuplicateFileGroup(string Sha256, long Bytes, IReadOnlyList<FileEntry> Files)
{
    public int Copies => Files.Count;
    public long PotentialDuplicateBytes => Files.Count <= 1 ? 0 : Bytes > long.MaxValue / (Files.Count - 1L) ? long.MaxValue : Bytes * (Files.Count - 1L);
}

public sealed record TempCandidateResult(string RootPath, DateTime CutoffUtc, long TotalBytes,
    long CandidateCount, long SkippedCount, IReadOnlyList<TempCandidate> Candidates);
