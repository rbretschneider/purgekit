using System.Globalization;
using PurgeKit.Core.Models;
using Microsoft.Win32;

namespace PurgeKit.Core.Scanning;

public class InstalledProgramsScanner
{
    private static readonly string[] UninstallRegistryPaths =
    {
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
    };

    public async Task<List<InstalledProgram>> ScanAsync(CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var programs = new Dictionary<string, InstalledProgram>(StringComparer.OrdinalIgnoreCase);

            // Scan HKLM and HKCU
            foreach (var regPath in UninstallRegistryPaths)
            {
                ScanRegistryHive(Registry.LocalMachine, regPath, programs, ct);
                ScanRegistryHive(Registry.CurrentUser, regPath, programs, ct);
            }

            // Determine last-used dates in parallel (CPU + light I/O per program)
            Parallel.ForEach(programs.Values, new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = ct },
                program => DetermineLastUsed(program));

            return programs.Values
                .Where(p => !string.IsNullOrWhiteSpace(p.DisplayName))
                .OrderBy(p => p.LastUsed ?? DateTime.MinValue)
                .ToList();
        }, ct);
    }

    private static void ScanRegistryHive(
        RegistryKey hive, string path,
        Dictionary<string, InstalledProgram> programs,
        CancellationToken ct)
    {
        try
        {
            using var key = hive.OpenSubKey(path);
            if (key == null) return;

            foreach (var subKeyName in key.GetSubKeyNames())
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    using var subKey = key.OpenSubKey(subKeyName);
                    if (subKey == null) continue;

                    var displayName = subKey.GetValue("DisplayName") as string;
                    if (string.IsNullOrWhiteSpace(displayName)) continue;

                    // Skip system components and updates
                    var systemComponent = subKey.GetValue("SystemComponent");
                    if (systemComponent is int sc && sc == 1) continue;

                    var parentName = subKey.GetValue("ParentDisplayName") as string;
                    if (!string.IsNullOrEmpty(parentName)) continue; // skip sub-components

                    if (programs.ContainsKey(displayName)) continue;

                    var installLocation = subKey.GetValue("InstallLocation") as string;
                    var estimatedSize = subKey.GetValue("EstimatedSize");
                    long sizeBytes = 0;
                    if (estimatedSize is int sizeKb)
                        sizeBytes = (long)sizeKb * 1024;

                    string? installDrive = null;
                    if (!string.IsNullOrEmpty(installLocation) && installLocation.Length >= 2 && installLocation[1] == ':')
                        installDrive = installLocation[..2];

                    var installDateStr = subKey.GetValue("InstallDate") as string;
                    DateTime? installDate = null;
                    if (!string.IsNullOrEmpty(installDateStr) &&
                        DateTime.TryParseExact(installDateStr, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
                    {
                        installDate = parsedDate;
                    }

                    programs[displayName] = new InstalledProgram
                    {
                        DisplayName = displayName,
                        Publisher = subKey.GetValue("Publisher") as string ?? "",
                        InstallDate = installDate,
                        InstallLocation = installLocation,
                        EstimatedSizeBytes = sizeBytes,
                        UninstallString = subKey.GetValue("UninstallString") as string,
                        InstallDrive = installDrive
                    };
                }
                catch { }
            }
        }
        catch { }
    }

    private static void DetermineLastUsed(InstalledProgram program)
    {
        // Heuristic 1: Prefetch files
        var lastUsed = CheckPrefetch(program);
        if (lastUsed.HasValue)
        {
            program.LastUsed = lastUsed.Value;
            program.LastUsedSource = "Prefetch";
            return;
        }

        // Heuristic 2: AppData folder modification
        lastUsed = CheckAppData(program);
        if (lastUsed.HasValue)
        {
            program.LastUsed = lastUsed.Value;
            program.LastUsedSource = "AppData";
            return;
        }

        // Heuristic 3: MUI Cache
        lastUsed = CheckMuiCache(program);
        if (lastUsed.HasValue)
        {
            program.LastUsed = lastUsed.Value;
            program.LastUsedSource = "MUI Cache";
            return;
        }
    }

    private static DateTime? CheckPrefetch(InstalledProgram program)
    {
        try
        {
            var prefetchDir = @"C:\Windows\Prefetch";
            if (!Directory.Exists(prefetchDir)) return null;

            // Try to find the main executable name from install location
            string? exeName = null;
            if (!string.IsNullOrEmpty(program.InstallLocation) && Directory.Exists(program.InstallLocation))
            {
                var exeFiles = Directory.GetFiles(program.InstallLocation, "*.exe", SearchOption.TopDirectoryOnly);
                if (exeFiles.Length > 0)
                    exeName = Path.GetFileNameWithoutExtension(exeFiles[0]);
            }

            if (string.IsNullOrEmpty(exeName))
            {
                // Derive from display name
                exeName = program.DisplayName
                    .Replace(" ", "")
                    .Replace(".", "");
            }

            var prefetchFiles = Directory.GetFiles(prefetchDir, "*.pf");
            var match = prefetchFiles
                .Where(f => Path.GetFileName(f).StartsWith(exeName, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(f => File.GetLastWriteTime(f))
                .FirstOrDefault();

            if (match != null)
                return File.GetLastWriteTime(match);
        }
        catch { }

        return null;
    }

    private static DateTime? CheckAppData(InstalledProgram program)
    {
        try
        {
            var searchNames = new List<string>();

            if (!string.IsNullOrEmpty(program.Publisher))
                searchNames.Add(program.Publisher.Split(' ')[0]); // first word of publisher

            searchNames.Add(program.DisplayName.Split(' ')[0]); // first word of name

            var appDataPaths = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            };

            foreach (var appDataPath in appDataPaths)
            {
                if (!Directory.Exists(appDataPath)) continue;

                foreach (var name in searchNames)
                {
                    if (string.IsNullOrWhiteSpace(name) || name.Length < 3) continue;

                    var options = new EnumerationOptions { IgnoreInaccessible = true };
                    foreach (var dir in Directory.EnumerateDirectories(appDataPath, $"*{name}*", options))
                    {
                        var mostRecent = GetMostRecentFileTime(dir);
                        if (mostRecent.HasValue) return mostRecent;
                    }
                }
            }
        }
        catch { }

        return null;
    }

    private static DateTime? CheckMuiCache(InstalledProgram program)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Classes\Local Settings\Software\Microsoft\Windows\Shell\MuiCache");
            if (key == null) return null;

            var installLoc = program.InstallLocation;
            if (string.IsNullOrEmpty(installLoc)) return null;

            foreach (var valueName in key.GetValueNames())
            {
                if (valueName.Contains(installLoc, StringComparison.OrdinalIgnoreCase))
                {
                    // MUI cache doesn't have timestamps, but its existence means the app was used.
                    // Use the registry key's last write time approximation — install date + 1 day
                    // as a fallback indicator
                    return program.InstallDate?.AddDays(1);
                }
            }
        }
        catch { }

        return null;
    }

    private static DateTime? GetMostRecentFileTime(string dir)
    {
        try
        {
            var options = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = true,
                AttributesToSkip = FileAttributes.System
            };

            DateTime? most = null;
            int checked_ = 0;

            foreach (var file in Directory.EnumerateFiles(dir, "*", options))
            {
                if (checked_++ > 100) break; // don't scan too deep
                try
                {
                    var mod = File.GetLastWriteTime(file);
                    if (!most.HasValue || mod > most.Value)
                        most = mod;
                }
                catch { }
            }

            return most;
        }
        catch { return null; }
    }
}
