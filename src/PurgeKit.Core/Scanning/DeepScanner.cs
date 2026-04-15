using PurgeKit.Core.Models;
using PurgeKit.Core.Services;

namespace PurgeKit.Core.Scanning;

public class DeepScanner
{
    private readonly AppSettings _settings;

    private static readonly HashSet<string> ProtectedPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        @"C:\Windows\System32",
        @"C:\Windows\SysWOW64",
        @"C:\Windows\WinSxS",
        @"C:\Program Files",
        @"C:\Program Files (x86)"
    };

    private static readonly HashSet<string> ProtectedFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "pagefile.sys", "swapfile.sys", "hiberfil.sys"
    };

    public record ScanProgress(string CurrentPath, long FilesScanned);

    public DeepScanner(AppSettings settings)
    {
        _settings = settings;
    }

    public async Task<List<DeepScanItem>> ScanAsync(
        IProgress<ScanProgress>? progress = null,
        CancellationToken ct = default)
    {
        var results = new List<DeepScanItem>();

        await Task.Run(() =>
        {
            LogService.Log("DeepScan: Starting drive-wide file scan...");
            var largeFiles = ScanLargeFiles(@"C:\", progress, ct);
            results.AddRange(largeFiles);
            LogService.Log($"DeepScan: Found {largeFiles.Count} large files.");

            LogService.Log("DeepScan: Starting directory size scan...");
            var largeDirs = ScanLargeDirectories(@"C:\", progress, ct);
            results.AddRange(largeDirs);
            LogService.Log($"DeepScan: Found {largeDirs.Count} large directories.");

            LogService.Log("DeepScan: Starting AppData spotlight...");
            var appDataItems = ScanAppData(progress, ct);
            results.AddRange(appDataItems);
            LogService.Log($"DeepScan: Found {appDataItems.Count} AppData items.");

        }, ct);

        // Deduplicate by path
        return results
            .GroupBy(r => r.FullPath, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderByDescending(r => r.SizeBytes)
            .ToList();
    }

    private List<DeepScanItem> ScanLargeFiles(
        string root,
        IProgress<ScanProgress>? progress,
        CancellationToken ct)
    {
        var topFiles = new SortedList<long, DeepScanItem>();
        long filesScanned = 0;
        const int maxResults = 200;

        var options = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.System
        };

        foreach (var filePath in Directory.EnumerateFiles(root, "*", options))
        {
            ct.ThrowIfCancellationRequested();
            filesScanned++;

            if (filesScanned % 5000 == 0)
                progress?.Report(new ScanProgress(filePath, filesScanned));

            if (IsProtectedPath(filePath)) continue;
            if (IsExcludedPath(filePath)) continue;

            try
            {
                var fi = new FileInfo(filePath);
                if (ProtectedFiles.Contains(fi.Name)) continue;
                if (fi.Length < _settings.MinFileSizeBytes) continue;

                // Use negative size as key for descending sort
                var key = -fi.Length;
                while (topFiles.ContainsKey(key)) key--; // handle dups

                if (topFiles.Count < maxResults)
                {
                    topFiles.Add(key, CreateFileItem(fi));
                }
                else if (fi.Length > -topFiles.Keys[topFiles.Count - 1])
                {
                    topFiles.RemoveAt(topFiles.Count - 1);
                    topFiles.Add(key, CreateFileItem(fi));
                }
            }
            catch { }
        }

        return topFiles.Values.ToList();
    }

    private List<DeepScanItem> ScanLargeDirectories(
        string root,
        IProgress<ScanProgress>? progress,
        CancellationToken ct)
    {
        var dirSizes = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        var options = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = false
        };

        // Get top-level directories on C:
        foreach (var dir in Directory.EnumerateDirectories(root, "*", options))
        {
            ct.ThrowIfCancellationRequested();
            if (IsProtectedPath(dir)) continue;
            if (IsExcludedPath(dir)) continue;

            progress?.Report(new ScanProgress(dir, 0));

            // Get immediate subdirectories and aggregate
            try
            {
                foreach (var subDir in Directory.EnumerateDirectories(dir, "*", options))
                {
                    ct.ThrowIfCancellationRequested();
                    if (IsProtectedPath(subDir)) continue;

                    var size = GetDirectorySizeSafe(subDir, ct);
                    if (size >= _settings.MinFileSizeBytes)
                        dirSizes[subDir] = size;
                }

                // Also measure the dir itself if it's not just a container
                var dirSize = GetDirectorySizeSafe(dir, ct);
                if (dirSize >= _settings.MinFileSizeBytes)
                    dirSizes[dir] = dirSize;
            }
            catch { }
        }

        return dirSizes
            .OrderByDescending(kv => kv.Value)
            .Take(50)
            .Select(kv =>
            {
                DateTime? lastMod = null;
                try { lastMod = new DirectoryInfo(kv.Key).LastWriteTime; } catch { }

                return new DeepScanItem
                {
                    Name = Path.GetFileName(kv.Key),
                    FullPath = kv.Key,
                    SizeBytes = kv.Value,
                    LastModified = lastMod,
                    ItemType = DeepScanItemType.Directory,
                    IsSymlink = IsSymlink(kv.Key)
                };
            })
            .ToList();
    }

    private List<DeepScanItem> ScanAppData(
        IProgress<ScanProgress>? progress,
        CancellationToken ct)
    {
        var results = new List<DeepScanItem>();
        var appDataPaths = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages")
        };

        var options = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = false
        };

        foreach (var appDataPath in appDataPaths)
        {
            if (!Directory.Exists(appDataPath)) continue;
            ct.ThrowIfCancellationRequested();

            foreach (var dir in Directory.EnumerateDirectories(appDataPath, "*", options))
            {
                ct.ThrowIfCancellationRequested();
                progress?.Report(new ScanProgress(dir, 0));

                var size = GetDirectorySizeSafe(dir, ct);
                DateTime? lastMod = null;
                try
                {
                    lastMod = GetMostRecentModification(dir);
                }
                catch { }

                bool isLargeEnough = size >= 100L * 1024 * 1024; // > 100 MB
                bool isStaleAndLarge = size >= 50L * 1024 * 1024
                    && lastMod.HasValue
                    && lastMod.Value < DateTime.Now.AddDays(-_settings.MinFileAgeDays);

                if (isLargeEnough || isStaleAndLarge)
                {
                    results.Add(new DeepScanItem
                    {
                        Name = Path.GetFileName(dir),
                        FullPath = dir,
                        SizeBytes = size,
                        LastModified = lastMod,
                        ItemType = DeepScanItemType.Directory,
                        IsSymlink = IsSymlink(dir)
                    });
                }
            }
        }

        return results;
    }

    private static DeepScanItem CreateFileItem(FileInfo fi)
    {
        return new DeepScanItem
        {
            Name = fi.Name,
            FullPath = fi.FullName,
            SizeBytes = fi.Length,
            LastModified = fi.LastWriteTime,
            ItemType = DeepScanItemType.File,
            IsSymlink = fi.Attributes.HasFlag(FileAttributes.ReparsePoint)
        };
    }

    private bool IsProtectedPath(string path)
    {
        foreach (var pp in ProtectedPaths)
        {
            if (path.StartsWith(pp, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private bool IsExcludedPath(string path)
    {
        foreach (var ep in _settings.ExcludedPaths)
        {
            if (path.StartsWith(ep, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static long GetDirectorySizeSafe(string path, CancellationToken ct)
    {
        long total = 0;
        var options = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.System
        };

        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", options))
            {
                ct.ThrowIfCancellationRequested();
                try { total += new FileInfo(file).Length; } catch { }
            }
        }
        catch { }

        return total;
    }

    private static DateTime? GetMostRecentModification(string path)
    {
        var options = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.System
        };

        DateTime? most = null;
        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", options))
            {
                try
                {
                    var mod = File.GetLastWriteTime(file);
                    if (!most.HasValue || mod > most.Value)
                        most = mod;
                }
                catch { }
            }
        }
        catch { }

        return most;
    }

    private static bool IsSymlink(string path)
    {
        try
        {
            var attrs = File.GetAttributes(path);
            return attrs.HasFlag(FileAttributes.ReparsePoint);
        }
        catch { return false; }
    }
}
