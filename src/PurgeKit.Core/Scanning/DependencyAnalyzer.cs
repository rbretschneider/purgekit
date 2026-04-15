using PurgeKit.Core.Models;
using Microsoft.Win32;

namespace PurgeKit.Core.Scanning;

public class DependencyAnalyzer
{
    private static readonly HashSet<string> KnownSafeCachePaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "npm", "npm-cache", "_cacache",
        "pip", "pip-cache", "__pycache__",
        "nuget", "NuGet",
        "gradle", ".gradle",
        "maven", ".m2",
        "cargo", "registry",
        "yarn", "berry",
        "pnpm", "store",
        "composer",
        "Cache", "CachedData", "CachedExtensions", "CachedExtensionVSIXs",
        "Code Cache", "GPUCache", "GrShaderCache", "ShaderCache",
        "DawnCache", "DawnWebGPUCache",
        "CrashDump", "Crashpad",
        "logs", "log", "Logs",
        "temp", "tmp", "Temp", "Tmp",
        "Thumbnails", "thumbnails",
        "blob_storage"
    };

    private static readonly HashSet<string> CacheIndicatorNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "cache", "caches", "cached", "temp", "tmp", "logs", "log",
        "data", "storage", "backup", "backups", "old", "archive"
    };

    private static readonly HashSet<string> BinaryExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".sys", ".ocx", ".drv"
    };

    public void Classify(DeepScanItem item)
    {
        // Check protected paths first
        if (IsProtectedSystemPath(item.FullPath))
        {
            item.Safety = SafetyLevel.DoNotDelete;
            item.SafetyExplanation = "This is a protected system path. Deleting it could break Windows.";
            return;
        }

        // Check if it's a known safe cache path
        if (IsKnownSafeCachePath(item))
        {
            item.Safety = SafetyLevel.Safe;
            item.SafetyExplanation = "This is a known application cache that can be safely deleted. The application will recreate it as needed.";
            return;
        }

        // Check if it's tied to an installed application
        var linkedApp = FindLinkedApplication(item.FullPath);
        if (linkedApp != null)
        {
            item.LinkedApplication = linkedApp;

            if (IsCacheOrDataFolder(item))
            {
                item.Safety = SafetyLevel.Caution;
                item.SafetyExplanation = $"This folder belongs to {linkedApp}. It appears to contain cache or data files. Deleting it may cause {linkedApp} to lose settings or cached data.";
            }
            else if (ContainsBinaries(item.FullPath))
            {
                item.Safety = SafetyLevel.DoNotDelete;
                item.SafetyExplanation = $"This folder belongs to {linkedApp} and contains application binaries. Deleting it would break the application. Use the uninstaller instead.";
            }
            else
            {
                item.Safety = SafetyLevel.Caution;
                item.SafetyExplanation = $"This folder is associated with {linkedApp}. Deleting it may affect the application.";
            }
            return;
        }

        // Check if it looks like a cache/temp folder by name
        if (IsCacheOrDataFolder(item) && !ContainsBinaries(item.FullPath))
        {
            item.Safety = SafetyLevel.Safe;
            item.SafetyExplanation = "This appears to be a cache or temporary data folder with no executable dependencies.";
            return;
        }

        // Symlink check
        if (item.IsSymlink)
        {
            item.Safety = SafetyLevel.Caution;
            item.SafetyExplanation = "This is a symbolic link or junction point. It may point to data on another drive. Deleting the link will not delete the target data.";
            return;
        }

        // Unknown
        item.Safety = SafetyLevel.Unknown;
        item.SafetyExplanation = "PurgeKit cannot determine whether this is safe to delete. Manual review recommended.";
    }

    public void ClassifyAll(IEnumerable<DeepScanItem> items)
    {
        foreach (var item in items)
            Classify(item);
    }

    private static bool IsProtectedSystemPath(string path)
    {
        var protectedPrefixes = new[]
        {
            @"C:\Windows\System32",
            @"C:\Windows\SysWOW64",
            @"C:\Windows\WinSxS",
            @"C:\Program Files\",
            @"C:\Program Files (x86)\"
        };

        return protectedPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsKnownSafeCachePath(DeepScanItem item)
    {
        var folderName = Path.GetFileName(item.FullPath);
        return KnownSafeCachePaths.Contains(folderName);
    }

    private static bool IsCacheOrDataFolder(DeepScanItem item)
    {
        var folderName = Path.GetFileName(item.FullPath).ToLowerInvariant();
        return CacheIndicatorNames.Any(indicator =>
            folderName.Contains(indicator, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsBinaries(string path)
    {
        if (!Directory.Exists(path)) return false;

        try
        {
            var options = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = false
            };

            return Directory.EnumerateFiles(path, "*", options)
                .Any(f => BinaryExtensions.Contains(Path.GetExtension(f)));
        }
        catch { return false; }
    }

    private static string? FindLinkedApplication(string path)
    {
        var pathParts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        var registryPaths = new[]
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
        };

        var hives = new[] { Registry.LocalMachine, Registry.CurrentUser };

        foreach (var hive in hives)
        {
            foreach (var regPath in registryPaths)
            {
                try
                {
                    using var key = hive.OpenSubKey(regPath);
                    if (key == null) continue;

                    foreach (var subKeyName in key.GetSubKeyNames())
                    {
                        try
                        {
                            using var subKey = key.OpenSubKey(subKeyName);
                            if (subKey == null) continue;

                            var displayName = subKey.GetValue("DisplayName") as string;
                            var publisher = subKey.GetValue("Publisher") as string;
                            if (string.IsNullOrEmpty(displayName)) continue;

                            // Check if any path component matches the app or publisher name
                            foreach (var part in pathParts)
                            {
                                if (string.IsNullOrEmpty(part) || part.Length < 3) continue;

                                if (displayName.Contains(part, StringComparison.OrdinalIgnoreCase) ||
                                    part.Contains(displayName.Split(' ')[0], StringComparison.OrdinalIgnoreCase))
                                {
                                    return displayName;
                                }

                                if (!string.IsNullOrEmpty(publisher) &&
                                    (publisher.Contains(part, StringComparison.OrdinalIgnoreCase) ||
                                     part.Contains(publisher.Split(' ')[0], StringComparison.OrdinalIgnoreCase)))
                                {
                                    return displayName;
                                }
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }

        return null;
    }
}
