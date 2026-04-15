using PurgeKit.Core.Models;
using PurgeKit.Core.Services;

namespace PurgeKit.Core.Scanning;

public class QuickWinScanner
{
    private static readonly List<CleanupTarget> Targets = new()
    {
        new()
        {
            Name = "Windows Temp",
            Description = "Temporary files created by Windows and applications. Safe to delete — they are recreated as needed.",
            Paths = new() { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp") },
            Safety = SafetyLevel.Safe
        },
        new()
        {
            Name = "User Temp",
            Description = "Temporary files in your user profile. Applications create these for short-term use.",
            Paths = new() { Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar) },
            Safety = SafetyLevel.Safe
        },
        new()
        {
            Name = "Windows Update Cache",
            Description = "Downloaded Windows Update files. Safe to delete — Windows will re-download if needed.",
            Paths = new() { @"C:\Windows\SoftwareDistribution\Download" },
            Safety = SafetyLevel.Safe
        },
        new()
        {
            Name = "Prefetch Files",
            Description = "Application launch optimization cache. Safe to delete — Windows rebuilds it automatically. May cause a minor slowdown on next boot.",
            Paths = new() { @"C:\Windows\Prefetch" },
            Safety = SafetyLevel.Safe
        },
        new()
        {
            Name = "Windows Error Reports",
            Description = "Crash reports and diagnostic logs. Removing these does not affect system stability.",
            Paths = new()
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "WER")
            },
            Safety = SafetyLevel.Safe
        },
        new()
        {
            Name = "Recycle Bin",
            Description = "Files you have previously deleted. They remain here until the Recycle Bin is emptied.",
            Paths = new() { @"C:\$Recycle.Bin" },
            Safety = SafetyLevel.Safe
        },
        new()
        {
            Name = "Chrome Cache",
            Description = "Google Chrome's cached web content. Safe to delete — pages will load from the web instead.",
            Paths = new()
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome", "User Data", "Default", "Cache"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome", "User Data", "Default", "Code Cache"),
            },
            Safety = SafetyLevel.Safe
        },
        new()
        {
            Name = "Edge Cache",
            Description = "Microsoft Edge's cached web content. Safe to delete.",
            Paths = new()
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Edge", "User Data", "Default", "Cache"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Edge", "User Data", "Default", "Code Cache"),
            },
            Safety = SafetyLevel.Safe
        },
        new()
        {
            Name = "Firefox Cache",
            Description = "Mozilla Firefox's cached web content. Safe to delete.",
            Paths = new()
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mozilla", "Firefox", "Profiles")
            },
            FilePattern = "cache2",
            Safety = SafetyLevel.Safe
        },
        new()
        {
            Name = "Thumbnail Cache",
            Description = "Cached thumbnail images shown in File Explorer. Windows regenerates these automatically.",
            Paths = new()
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Explorer")
            },
            FilePattern = "thumbcache_*.db",
            Safety = SafetyLevel.Safe
        },
        new()
        {
            Name = "Recent Files Shortcuts",
            Description = "Shortcuts to recently opened files. Removing these clears the recent files list but does not delete the actual files.",
            Paths = new()
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Windows", "Recent")
            },
            Safety = SafetyLevel.Safe
        },
        new()
        {
            Name = "Downloads Folder",
            Description = "Your Downloads folder. Review the contents before deleting — these are files you downloaded intentionally.",
            Paths = new()
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")
            },
            Safety = SafetyLevel.Caution,
            RequiresConfirmation = true
        },
        new()
        {
            Name = "Old Log Files",
            Description = "Application log files older than 30 days. Usually safe to remove, but some apps may reference them.",
            Paths = new()
            {
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            },
            FilePattern = "*.log",
            MaxAgeDays = 30,
            Safety = SafetyLevel.Caution
        }
    };

    public async Task<List<ScanResult>> ScanAsync(
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var results = new System.Collections.Concurrent.ConcurrentBag<ScanResult>();

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = 4,
            CancellationToken = ct
        };

        await Parallel.ForEachAsync(Targets, parallelOptions, async (target, token) =>
        {
            await Task.CompletedTask; // satisfy async signature
            progress?.Report($"Scanning {target.Name}...");

                var result = new ScanResult
                {
                    Name = target.Name,
                    Description = target.Description,
                    Safety = target.Safety,
                    Path = target.Paths.FirstOrDefault() ?? ""
                };

                long totalSize = 0;
                int fileCount = 0;

                foreach (var basePath in target.Paths)
                {
                    if (!Directory.Exists(basePath)) continue;

                    try
                    {
                        IEnumerable<string> files;

                        if (target.Name == "Recycle Bin")
                        {
                            // Recycle bin needs special handling
                            files = EnumerateRecycleBin();
                        }
                        else if (target.FilePattern == "cache2")
                        {
                            // Firefox profile-based cache
                            files = EnumerateFirefoxCache(basePath);
                        }
                        else if (target.FilePattern != null && target.FilePattern.Contains('*'))
                        {
                            files = SafeEnumerateFiles(basePath, target.FilePattern, SearchOption.AllDirectories);
                        }
                        else
                        {
                            files = SafeEnumerateFiles(basePath, "*", SearchOption.AllDirectories);
                        }

                        foreach (var file in files)
                        {
                            ct.ThrowIfCancellationRequested();
                            try
                            {
                                var fi = new FileInfo(file);
                                if (target.MaxAgeDays.HasValue && fi.LastWriteTime > DateTime.Now.AddDays(-target.MaxAgeDays.Value))
                                    continue;

                                totalSize += fi.Length;
                                fileCount++;
                            }
                            catch { }
                        }
                    }
                    catch (UnauthorizedAccessException) { }
                    catch (IOException) { }
                }

                result.SizeBytes = totalSize;
                result.FileCount = fileCount;

                LogService.Log($"QuickWin [{target.Name}]: {fileCount} files, {ScanResult.FormatSize(totalSize)}");

                if (fileCount > 0 || target.Safety == SafetyLevel.Caution)
                    results.Add(result);
        });

        return results.ToList();
    }

    private static IEnumerable<string> EnumerateRecycleBin()
    {
        var drives = DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed);
        foreach (var drive in drives)
        {
            var recyclePath = Path.Combine(drive.RootDirectory.FullName, "$Recycle.Bin");
            if (Directory.Exists(recyclePath))
            {
                foreach (var file in SafeEnumerateFiles(recyclePath, "*", SearchOption.AllDirectories))
                    yield return file;
            }
        }
    }

    private static IEnumerable<string> EnumerateFirefoxCache(string profilesPath)
    {
        if (!Directory.Exists(profilesPath)) yield break;

        foreach (var profileDir in SafeEnumerateDirectories(profilesPath))
        {
            var cachePath = Path.Combine(profileDir, "cache2");
            if (Directory.Exists(cachePath))
            {
                foreach (var file in SafeEnumerateFiles(cachePath, "*", SearchOption.AllDirectories))
                    yield return file;
            }
        }
    }

    private static IEnumerable<string> SafeEnumerateFiles(string path, string pattern, SearchOption option)
    {
        var options = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = option == SearchOption.AllDirectories,
            AttributesToSkip = FileAttributes.System
        };

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(path, pattern, options);
        }
        catch
        {
            yield break;
        }

        foreach (var file in files)
        {
            yield return file;
        }
    }

    private static IEnumerable<string> SafeEnumerateDirectories(string path)
    {
        var options = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = false
        };

        IEnumerable<string> dirs;
        try
        {
            dirs = Directory.EnumerateDirectories(path, "*", options);
        }
        catch
        {
            yield break;
        }

        foreach (var dir in dirs)
        {
            yield return dir;
        }
    }
}
