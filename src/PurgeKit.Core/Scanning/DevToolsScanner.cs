using PurgeKit.Core.Models;
using PurgeKit.Core.Services;
using Microsoft.Win32;

namespace PurgeKit.Core.Scanning;

public class DevToolsScanner
{
    private readonly AppSettings _settings;

    public DevToolsScanner(AppSettings settings)
    {
        _settings = settings;
    }

    public record DevScanProgress(string CurrentTarget, int TargetsCompleted, int TotalTargets);

    public async Task<List<ScanResult>> ScanAsync(
        IProgress<DevScanProgress>? progress = null,
        CancellationToken ct = default)
    {
        var results = new List<ScanResult>();
        int completed = 0;
        const int totalPhases = 10;

        await Task.Run(() =>
        {
            progress?.Report(new("Scanning WSL distros...", completed, totalPhases));
            results.AddRange(ScanWsl(ct));
            completed++;

            progress?.Report(new("Scanning Docker...", completed, totalPhases));
            results.AddRange(ScanDocker(ct));
            completed++;

            progress?.Report(new("Scanning developer tool caches...", completed, totalPhases));
            results.AddRange(ScanDevCaches(ct));
            completed++;

            progress?.Report(new("Scanning IDE caches...", completed, totalPhases));
            results.AddRange(ScanIdeCaches(ct));
            completed++;

            progress?.Report(new("Scanning GPU driver caches...", completed, totalPhases));
            results.AddRange(ScanGpuCaches(ct));
            completed++;

            progress?.Report(new("Scanning gaming caches...", completed, totalPhases));
            results.AddRange(ScanGamingCaches(ct));
            completed++;

            progress?.Report(new("Scanning Android SDK...", completed, totalPhases));
            results.AddRange(ScanAndroidSdk(ct));
            completed++;

            progress?.Report(new("Scanning app-specific caches...", completed, totalPhases));
            results.AddRange(ScanAppSpecificCaches(ct));
            completed++;

            progress?.Report(new("Scanning for abandoned node_modules...", completed, totalPhases));
            results.AddRange(ScanNodeModules(ct));
            completed++;

            progress?.Report(new("Scanning for large Git repos...", completed, totalPhases));
            results.AddRange(ScanGitRepos(ct));
            completed++;

        }, ct);

        return results.Where(r => r.SizeBytes > 0).ToList();
    }

    #region WSL

    private List<ScanResult> ScanWsl(CancellationToken ct)
    {
        var results = new List<ScanResult>();

        try
        {
            // Check registry for installed distros
            using var lxssKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Lxss");
            if (lxssKey == null) return results;

            foreach (var subKeyName in lxssKey.GetSubKeyNames())
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    using var distroKey = lxssKey.OpenSubKey(subKeyName);
                    if (distroKey == null) continue;

                    var distroName = distroKey.GetValue("DistributionName") as string ?? "Unknown Distro";
                    var basePath = distroKey.GetValue("BasePath") as string;

                    if (string.IsNullOrEmpty(basePath)) continue;

                    // Look for the VHDX
                    var vhdxPath = Path.Combine(basePath, "ext4.vhdx");
                    if (!File.Exists(vhdxPath))
                    {
                        // Try LocalState pattern
                        var localState = Path.Combine(basePath, "LocalState", "ext4.vhdx");
                        if (File.Exists(localState))
                            vhdxPath = localState;
                        else
                            continue;
                    }

                    var fi = new FileInfo(vhdxPath);
                    results.Add(new ScanResult
                    {
                        Name = $"WSL: {distroName}",
                        Description = $"WSL2 virtual disk for {distroName}. This VHDX file contains the entire Linux filesystem. It often grows larger than its actual contents — compacting can reclaim space without losing data.",
                        Path = vhdxPath,
                        SizeBytes = fi.Length,
                        FileCount = 1,
                        Safety = SafetyLevel.Caution,
                        SafetyExplanation = "Deleting this removes the entire WSL distro. Use 'Compact' to shrink it safely instead.",
                        LastModified = fi.LastWriteTime
                    });

                    LogService.Log($"WSL [{distroName}]: {ScanResult.FormatSize(fi.Length)} at {vhdxPath}");
                }
                catch (Exception ex)
                {
                    LogService.LogError($"WSL distro {subKeyName}", ex);
                }
            }
        }
        catch (Exception ex)
        {
            LogService.LogError("WSL scan", ex);
        }

        // Also scan the Packages directory for any WSL-related distros not in registry
        try
        {
            var packagesDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages");

            if (Directory.Exists(packagesDir))
            {
                var wslPatterns = new[] { "*Ubuntu*", "*Debian*", "*SUSE*", "*Kali*", "*Linux*", "*Fedora*", "*Arch*" };
                var existingPaths = results.Select(r => r.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var pattern in wslPatterns)
                {
                    foreach (var dir in SafeEnumerateDirectories(packagesDir, pattern))
                    {
                        ct.ThrowIfCancellationRequested();
                        var vhdx = Path.Combine(dir, "LocalState", "ext4.vhdx");
                        if (File.Exists(vhdx) && !existingPaths.Contains(vhdx))
                        {
                            var fi = new FileInfo(vhdx);
                            var name = Path.GetFileName(dir);
                            results.Add(new ScanResult
                            {
                                Name = $"WSL: {name}",
                                Description = $"WSL2 virtual disk found in Packages. Contains a Linux filesystem that may be compactable.",
                                Path = vhdx,
                                SizeBytes = fi.Length,
                                FileCount = 1,
                                Safety = SafetyLevel.Caution,
                                SafetyExplanation = "Deleting removes the WSL distro. Compact to reclaim space safely.",
                                LastModified = fi.LastWriteTime
                            });
                        }
                    }
                }
            }
        }
        catch { }

        return results;
    }

    #endregion

    #region Docker

    private List<ScanResult> ScanDocker(CancellationToken ct)
    {
        var results = new List<ScanResult>();

        // Docker WSL2 backend VHDX
        var dockerWslPaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Docker", "wsl", "data", "ext4.vhdx"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Docker", "wsl", "distro", "ext4.vhdx"),
        };

        foreach (var vhdxPath in dockerWslPaths)
        {
            ct.ThrowIfCancellationRequested();
            if (File.Exists(vhdxPath))
            {
                var fi = new FileInfo(vhdxPath);
                results.Add(new ScanResult
                {
                    Name = "Docker Desktop (WSL2 Data)",
                    Description = "Docker's WSL2 virtual disk stores all images, containers, volumes, and build cache. This file typically grows much larger than the data it contains.",
                    Path = vhdxPath,
                    SizeBytes = fi.Length,
                    FileCount = 1,
                    Safety = SafetyLevel.Caution,
                    SafetyExplanation = "Deleting this destroys all Docker images, containers, and volumes. Use 'docker system prune -a' to clean up safely, or compact the VHDX.",
                    LastModified = fi.LastWriteTime
                });
                LogService.Log($"Docker VHDX: {ScanResult.FormatSize(fi.Length)} at {vhdxPath}");
            }
        }

        // Docker Hyper-V backend
        var hyperVPaths = new[]
        {
            @"C:\ProgramData\DockerDesktop\vm-data\DockerDesktop.vhdx",
            @"C:\ProgramData\DockerDesktop\vm-data\Docker.vhdx",
        };

        foreach (var path in hyperVPaths)
        {
            ct.ThrowIfCancellationRequested();
            if (File.Exists(path))
            {
                var fi = new FileInfo(path);
                results.Add(new ScanResult
                {
                    Name = "Docker Desktop (Hyper-V)",
                    Description = "Docker Hyper-V virtual machine disk. Contains all Docker data.",
                    Path = path,
                    SizeBytes = fi.Length,
                    FileCount = 1,
                    Safety = SafetyLevel.Caution,
                    SafetyExplanation = "Deleting this destroys all Docker data. Use Docker's built-in cleanup commands instead.",
                    LastModified = fi.LastWriteTime
                });
            }
        }

        // Docker config/misc
        var dockerConfigDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Docker");
        if (Directory.Exists(dockerConfigDir))
        {
            var size = GetDirectorySizeSafe(dockerConfigDir, ct);
            if (size > 10 * 1024 * 1024) // > 10 MB
            {
                results.Add(new ScanResult
                {
                    Name = "Docker Config & Logs",
                    Description = "Docker Desktop configuration, logs, and metadata.",
                    Path = dockerConfigDir,
                    SizeBytes = size,
                    FileCount = CountFilesSafe(dockerConfigDir),
                    Safety = SafetyLevel.Caution,
                    SafetyExplanation = "Contains Docker settings. Safe to clean logs, but config files are needed by Docker."
                });
            }
        }

        return results;
    }

    #endregion

    #region Dev Tool Caches

    private List<ScanResult> ScanDevCaches(CancellationToken ct)
    {
        var results = new List<ScanResult>();
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        var cacheTargets = new (string Name, string Description, string[] Paths, SafetyLevel Safety)[]
        {
            ("npm Cache",
             "Cached npm packages. Safe to delete — npm re-downloads packages on demand.",
             new[] {
                 Path.Combine(localAppData, "npm-cache"),
                 Path.Combine(home, ".npm", "_cacache")
             },
             SafetyLevel.Safe),

            ("Yarn Cache",
             "Cached Yarn packages. Safe to delete — Yarn re-downloads on install.",
             new[] {
                 Path.Combine(home, ".yarn", "cache"),
                 Path.Combine(localAppData, "Yarn", "Cache")
             },
             SafetyLevel.Safe),

            ("pnpm Store",
             "pnpm content-addressable store. Safe to delete — packages are re-downloaded as needed.",
             new[] {
                 Path.Combine(home, ".pnpm-store"),
                 Path.Combine(localAppData, "pnpm-store")
             },
             SafetyLevel.Safe),

            ("pip Cache",
             "Cached Python packages. Safe to delete — pip re-downloads on install.",
             new[] {
                 Path.Combine(localAppData, "pip", "cache"),
                 Path.Combine(home, ".cache", "pip")
             },
             SafetyLevel.Safe),

            ("Conda Packages",
             "Cached Conda packages. Safe to delete — Conda re-downloads on environment creation.",
             new[] {
                 Path.Combine(home, ".conda", "pkgs")
             },
             SafetyLevel.Safe),

            ("NuGet Package Cache",
             "Cached NuGet packages used by .NET projects. Safe to delete — Visual Studio and dotnet restore re-download as needed.",
             new[] {
                 Path.Combine(home, ".nuget", "packages")
             },
             SafetyLevel.Safe),

            ("Gradle Cache",
             "Cached Gradle dependencies and wrapper distributions. Safe to delete — Gradle re-downloads on next build.",
             new[] {
                 Path.Combine(home, ".gradle", "caches"),
                 Path.Combine(home, ".gradle", "wrapper", "dists")
             },
             SafetyLevel.Safe),

            ("Maven Repository",
             "Local Maven repository. Safe to delete — Maven re-downloads dependencies on build.",
             new[] {
                 Path.Combine(home, ".m2", "repository")
             },
             SafetyLevel.Safe),

            ("Cargo Cache (Rust)",
             "Cached Rust crate registry and source. Safe to delete — Cargo re-downloads on build.",
             new[] {
                 Path.Combine(home, ".cargo", "registry", "cache"),
                 Path.Combine(home, ".cargo", "registry", "src"),
                 Path.Combine(home, ".cargo", "git", "db")
             },
             SafetyLevel.Safe),

            ("Go Module Cache",
             "Cached Go modules. Safe to delete — Go re-downloads on build.",
             new[] {
                 Path.Combine(home, "go", "pkg", "mod", "cache", "download")
             },
             SafetyLevel.Safe),

            ("Composer Cache (PHP)",
             "Cached PHP Composer packages. Safe to delete.",
             new[] {
                 Path.Combine(appData, "Composer", "cache")
             },
             SafetyLevel.Safe),

            ("Ruby Gems Cache",
             "Cached Ruby gems. Safe to delete.",
             new[] {
                 Path.Combine(home, ".gem")
             },
             SafetyLevel.Safe),

            (".NET Workload Cache",
             "Cached .NET workload installation files.",
             new[] {
                 Path.Combine(localAppData, "dotnet", "workloads")
             },
             SafetyLevel.Safe),

            ("Symbol Cache",
             "Debugger symbol files cached by Visual Studio. Safe to delete — symbols are re-downloaded when needed.",
             new[] {
                 Path.Combine(localAppData, "SymbolCache"),
                 Path.Combine(localAppData, "Temp", "SymbolCache")
             },
             SafetyLevel.Safe),

            ("Dart/Flutter Pub Cache",
             "Cached Dart and Flutter packages. Safe to delete — 'pub get' re-downloads on demand.",
             new[] {
                 Path.Combine(localAppData, "Pub", "Cache"),
                 Path.Combine(home, ".pub-cache")
             },
             SafetyLevel.Safe),

            ("Gradle Daemon Logs",
             "Gradle daemon log files. Safe to delete.",
             new[] {
                 Path.Combine(home, ".gradle", "daemon")
             },
             SafetyLevel.Safe),

            ("Java WebStart/JRE Cache",
             "Java web deployment and runtime cache files. Safe to delete.",
             new[] {
                 Path.Combine(home, ".java", "deployment", "cache"),
                 Path.Combine(localAppData, "Sun", "Java", "Deployment", "cache")
             },
             SafetyLevel.Safe),

            ("Windows Package Cache",
             "Cached installers for Visual Studio, .NET runtimes, and other MSI-based software. Windows and installers re-download if needed, but uninstall/repair may temporarily need these.",
             new[] {
                 @"C:\ProgramData\Package Cache"
             },
             SafetyLevel.Caution),

            ("Windows Installer Cache",
             "Cached Windows Installer patch files. Orphaned entries are safe; active ones are needed for uninstall/repair.",
             new[] {
                 Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Installer", "$PatchCache$")
             },
             SafetyLevel.Caution),
        };

        var bag = new System.Collections.Concurrent.ConcurrentBag<ScanResult>();
        Parallel.ForEach(cacheTargets, new ParallelOptions { MaxDegreeOfParallelism = 6, CancellationToken = ct }, target =>
        {
            long totalSize = 0;
            int totalFiles = 0;
            string? firstExistingPath = null;

            foreach (var path in target.Paths)
            {
                if (!Directory.Exists(path)) continue;
                firstExistingPath ??= path;
                totalSize += GetDirectorySizeSafe(path, ct);
                totalFiles += CountFilesSafe(path);
            }

            if (totalSize > 0 && firstExistingPath != null)
            {
                bag.Add(new ScanResult
                {
                    Name = target.Name,
                    Description = target.Description,
                    Path = firstExistingPath,
                    SizeBytes = totalSize,
                    FileCount = totalFiles,
                    Safety = target.Safety,
                    SafetyExplanation = target.Safety == SafetyLevel.Safe
                        ? "Cache folder — tools re-download contents on demand."
                        : "Review before deleting — may be needed for uninstall/repair."
                });
                LogService.Log($"DevCache [{target.Name}]: {ScanResult.FormatSize(totalSize)}");
            }
        });
        results.AddRange(bag);

        return results;
    }

    #endregion

    #region IDE Caches

    private List<ScanResult> ScanIdeCaches(CancellationToken ct)
    {
        var results = new List<ScanResult>();
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        // VS Code
        AddIfExists(results, "VS Code Extensions",
            "Installed VS Code extensions. Old/orphaned extensions accumulate here. Safe to clean — reinstall from marketplace.",
            Path.Combine(home, ".vscode", "extensions"),
            SafetyLevel.Caution,
            "Deleting removes all VS Code extensions. You'll need to reinstall them from the marketplace.", ct);

        AddIfExists(results, "VS Code Cached Data",
            "VS Code internal caches (language servers, compiled extensions). Safe to delete — rebuilt on next launch.",
            Path.Combine(localAppData, "Code", "CachedData"),
            SafetyLevel.Safe, null, ct);

        AddIfExists(results, "VS Code Cache",
            "VS Code browser-style cache. Safe to delete.",
            Path.Combine(localAppData, "Code", "Cache"),
            SafetyLevel.Safe, null, ct);

        // Visual Studio
        var vsBasePath = Path.Combine(localAppData, "Microsoft", "VisualStudio");
        if (Directory.Exists(vsBasePath))
        {
            foreach (var vsDir in SafeEnumerateDirectories(vsBasePath, "*"))
            {
                ct.ThrowIfCancellationRequested();
                var componentCache = Path.Combine(vsDir, "ComponentModelCache");
                if (Directory.Exists(componentCache))
                {
                    var version = Path.GetFileName(vsDir);
                    AddIfExists(results, $"Visual Studio {version} Component Cache",
                        "Visual Studio MEF component cache. Safe to delete — rebuilt on next launch.",
                        componentCache, SafetyLevel.Safe, null, ct);
                }
            }
        }

        // JetBrains IDEs
        var jetbrainsCache = Path.Combine(home, ".cache", "JetBrains");
        if (Directory.Exists(jetbrainsCache))
        {
            AddIfExists(results, "JetBrains IDE Caches",
                "Caches for JetBrains IDEs (IntelliJ, Rider, WebStorm, etc.). Safe to delete — rebuilt when the IDE restarts.",
                jetbrainsCache, SafetyLevel.Safe, null, ct);
        }

        // JetBrains Transient (ReSharper etc.)
        var jetbrainsTransient = Path.Combine(localAppData, "JetBrains", "Transient");
        if (Directory.Exists(jetbrainsTransient))
        {
            AddIfExists(results, "JetBrains Transient Data",
                "Temporary data from JetBrains tools (ReSharper, dotPeek, etc.). Safe to delete.",
                jetbrainsTransient, SafetyLevel.Safe, null, ct);
        }

        // Android Studio caches
        var androidStudioPaths = new[]
        {
            Path.Combine(home, ".AndroidStudio*"),
            Path.Combine(localAppData, "Google", "AndroidStudio*")
        };
        foreach (var pattern in androidStudioPaths)
        {
            var parentDir = Path.GetDirectoryName(pattern) ?? "";
            var searchPattern = Path.GetFileName(pattern);
            if (!Directory.Exists(parentDir)) continue;

            foreach (var dir in SafeEnumerateDirectories(parentDir, searchPattern))
            {
                ct.ThrowIfCancellationRequested();
                var cacheDir = Path.Combine(dir, "system", "caches");
                if (Directory.Exists(cacheDir))
                {
                    var version = Path.GetFileName(dir);
                    AddIfExists(results, $"Android Studio {version} Caches",
                        "Android Studio IDE caches. Safe to delete — rebuilt on next launch.",
                        cacheDir, SafetyLevel.Safe, null, ct);
                }
            }
        }

        return results;
    }

    #endregion

    #region GPU Caches

    private List<ScanResult> ScanGpuCaches(CancellationToken ct)
    {
        var results = new List<ScanResult>();
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var temp = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);

        // === NVIDIA ===

        // NVIDIA driver installer leftovers (the huge extracted installer packages)
        if (Directory.Exists(@"C:\NVIDIA"))
        {
            AddIfExists(results, "NVIDIA Driver Installer Leftovers",
                "Extracted NVIDIA driver installation files left behind after installation. Completely safe to delete — the driver is already installed.",
                @"C:\NVIDIA", SafetyLevel.Safe,
                "These are temporary extraction files, not the installed driver.", ct);
        }

        // NVIDIA Corporation downloader cache
        AddIfExists(results, "NVIDIA Downloader Cache",
            "NVIDIA GeForce Experience download cache. Contains downloaded driver packages. Safe to delete — re-downloaded when updating.",
            @"C:\ProgramData\NVIDIA Corporation\Downloader",
            SafetyLevel.Safe, null, ct);

        // NVIDIA DXCache (DirectX shader cache)
        AddIfExists(results, "NVIDIA DX Shader Cache",
            "NVIDIA DirectX shader cache. Safe to delete — shaders are recompiled automatically. You may notice brief stuttering in games as shaders rebuild.",
            Path.Combine(localAppData, "NVIDIA", "DXCache"),
            SafetyLevel.Safe,
            "Shader cache — rebuilt automatically during gameplay.", ct);

        // NVIDIA GLCache (OpenGL shader cache)
        AddIfExists(results, "NVIDIA GL Shader Cache",
            "NVIDIA OpenGL shader cache. Safe to delete — rebuilt as needed.",
            Path.Combine(localAppData, "NVIDIA", "GLCache"),
            SafetyLevel.Safe, null, ct);

        // NVIDIA temp files
        AddIfExists(results, "NVIDIA Temp Files",
            "Temporary files created by NVIDIA drivers and tools.",
            Path.Combine(temp, "NVIDIA Corporation"),
            SafetyLevel.Safe, null, ct);

        // NVIDIA NV_Cache in ProgramData
        AddIfExists(results, "NVIDIA NV_Cache",
            "NVIDIA driver compilation cache. Safe to delete.",
            @"C:\ProgramData\NVIDIA Corporation\NV_Cache",
            SafetyLevel.Safe, null, ct);

        // NVIDIA installer GFExperience leftovers
        AddIfExists(results, "NVIDIA GeForce Experience Cache",
            "GeForce Experience application cache and logs.",
            @"C:\ProgramData\NVIDIA Corporation\GeForce Experience",
            SafetyLevel.Caution,
            "Contains GFE configuration. Logs and cache are safe; deleting entirely may require re-login to GFE.", ct);

        // === AMD ===

        // AMD driver installer leftovers
        if (Directory.Exists(@"C:\AMD"))
        {
            AddIfExists(results, "AMD Driver Installer Leftovers",
                "Extracted AMD driver installation files. Completely safe to delete — the driver is already installed.",
                @"C:\AMD", SafetyLevel.Safe,
                "Temporary extraction files from AMD driver installation.", ct);
        }

        // AMD DxCache
        AddIfExists(results, "AMD DX Shader Cache",
            "AMD Radeon DirectX shader cache. Safe to delete — rebuilt automatically during gameplay.",
            Path.Combine(localAppData, "AMD", "DxCache"),
            SafetyLevel.Safe,
            "Shader cache — rebuilt as needed. Brief stuttering may occur in games.", ct);

        // AMD GLCache
        AddIfExists(results, "AMD GL Shader Cache",
            "AMD Radeon OpenGL shader cache. Safe to delete.",
            Path.Combine(localAppData, "AMD", "GLCache"),
            SafetyLevel.Safe, null, ct);

        // AMD CN cache
        AddIfExists(results, "AMD CN Cache",
            "AMD Radeon Software cache. Safe to delete.",
            Path.Combine(localAppData, "AMD", "CN"),
            SafetyLevel.Safe, null, ct);

        // === DirectX Shader Cache (all GPUs) ===
        AddIfExists(results, "DirectX Shader Cache",
            "Windows DirectX shader compilation cache. Safe to delete — rebuilt by the GPU driver as needed.",
            Path.Combine(localAppData, "D3DSCache"),
            SafetyLevel.Safe, null, ct);

        // DXGI adapter cache
        AddIfExists(results, "DXGI Adapter Cache",
            "DirectX Graphics Infrastructure adapter cache.",
            Path.Combine(localAppData, "Microsoft", "DirectX", "UserDxgiCache"),
            SafetyLevel.Safe, null, ct);

        return results;
    }

    #endregion

    #region Gaming Caches

    private List<ScanResult> ScanGamingCaches(CancellationToken ct)
    {
        var results = new List<ScanResult>();
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        // Steam shader cache
        // Steam default install paths
        var steamPaths = new List<string>
        {
            @"C:\Program Files (x86)\Steam",
            @"C:\Program Files\Steam",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"),
        };

        // Check registry for actual Steam path
        try
        {
            using var steamKey = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            var steamExe = steamKey?.GetValue("SteamExe") as string;
            if (!string.IsNullOrEmpty(steamExe))
            {
                var steamDir = Path.GetDirectoryName(steamExe);
                if (!string.IsNullOrEmpty(steamDir))
                    steamPaths.Insert(0, steamDir);
            }
        }
        catch { }

        foreach (var steamPath in steamPaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            ct.ThrowIfCancellationRequested();
            var shaderCache = Path.Combine(steamPath, "steamapps", "shadercache");
            if (Directory.Exists(shaderCache))
            {
                AddIfExists(results, "Steam Shader Cache",
                    "Pre-compiled shader cache for Steam games. Safe to delete — Steam recompiles shaders when you next launch a game. May cause brief stuttering on first launch.",
                    shaderCache, SafetyLevel.Safe,
                    "Shader cache — rebuilt per-game on launch.", ct);
                break; // only need to find it once
            }
        }

        // Steam HTML cache
        foreach (var steamPath in steamPaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var htmlCache = Path.Combine(steamPath, "config", "htmlcache");
            if (Directory.Exists(htmlCache))
            {
                AddIfExists(results, "Steam Browser Cache",
                    "Steam's built-in browser cache (store pages, community). Safe to delete.",
                    htmlCache, SafetyLevel.Safe, null, ct);
                break;
            }
        }

        // Epic Games Launcher
        AddIfExists(results, "Epic Games Launcher Cache",
            "Epic Games Launcher web and asset cache. Safe to delete — rebuilt on next launch.",
            Path.Combine(localAppData, "EpicGamesLauncher", "Saved", "webcache"),
            SafetyLevel.Safe, null, ct);

        // Unreal Engine derived data cache
        AddIfExists(results, "Unreal Engine Derived Data Cache",
            "Unreal Engine shader and asset compilation cache. Safe to delete — rebuilt on next project load. Large in UE5 projects.",
            Path.Combine(localAppData, "UnrealEngine", "Common", "DerivedDataCache"),
            SafetyLevel.Safe,
            "Build cache — UE rebuilds on next project open.", ct);

        // Unity shader cache
        var unityPath = Path.Combine(localAppData, "Unity");
        if (Directory.Exists(unityPath))
        {
            var cacheDir = Path.Combine(unityPath, "cache");
            AddIfExists(results, "Unity Shader Cache",
                "Unity editor shader cache. Safe to delete — rebuilt on next project open.",
                cacheDir, SafetyLevel.Safe, null, ct);
        }

        // GOG Galaxy cache
        AddIfExists(results, "GOG Galaxy Cache",
            "GOG Galaxy web and download cache.",
            Path.Combine(localAppData, "GOG.com", "Galaxy", "webcache"),
            SafetyLevel.Safe, null, ct);

        return results;
    }

    #endregion

    #region Android SDK

    private List<ScanResult> ScanAndroidSdk(CancellationToken ct)
    {
        var results = new List<ScanResult>();
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        // Find Android SDK location
        var sdkPaths = new[]
        {
            Path.Combine(localAppData, "Android", "Sdk"),
            Path.Combine(home, "Android", "Sdk"),
            Path.Combine(home, "AppData", "Local", "Android", "Sdk"),
        };

        // Also check ANDROID_HOME / ANDROID_SDK_ROOT env vars
        var envHome = Environment.GetEnvironmentVariable("ANDROID_HOME");
        var envSdkRoot = Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT");

        string? sdkPath = null;
        foreach (var candidate in new[] { envHome, envSdkRoot }.Concat(sdkPaths))
        {
            if (!string.IsNullOrEmpty(candidate) && Directory.Exists(candidate))
            {
                sdkPath = candidate;
                break;
            }
        }

        if (sdkPath != null)
        {
            // SDK temp files
            var sdkTemp = Path.Combine(sdkPath, ".temp");
            AddIfExists(results, "Android SDK Temp",
                "Temporary download files from Android SDK manager. Safe to delete.",
                sdkTemp, SafetyLevel.Safe, null, ct);

            // Old build-tools versions (keep the latest, flag older ones)
            var buildToolsDir = Path.Combine(sdkPath, "build-tools");
            if (Directory.Exists(buildToolsDir))
            {
                var versions = SafeEnumerateDirectories(buildToolsDir, "*")
                    .OrderByDescending(d => Path.GetFileName(d))
                    .ToList();

                if (versions.Count > 1)
                {
                    // Skip the newest, flag the rest
                    long oldSize = 0;
                    int oldCount = 0;
                    foreach (var oldVer in versions.Skip(1))
                    {
                        ct.ThrowIfCancellationRequested();
                        oldSize += GetDirectorySizeSafe(oldVer, ct);
                        oldCount++;
                    }

                    if (oldSize > 10 * 1024 * 1024)
                    {
                        results.Add(new ScanResult
                        {
                            Name = "Android Old Build Tools",
                            Description = $"{oldCount} older Android build-tools versions. Keeping latest ({Path.GetFileName(versions[0])}). Older versions are usually unnecessary.",
                            Path = buildToolsDir,
                            SizeBytes = oldSize,
                            FileCount = oldCount,
                            Safety = SafetyLevel.Caution,
                            SafetyExplanation = "Some projects may pin a specific build-tools version. Check your build.gradle before deleting."
                        });
                    }
                }
            }

            // Emulator snapshots
            var emulatorDir = Path.Combine(sdkPath, "emulator");
            if (Directory.Exists(emulatorDir))
            {
                // Snapshots are stored per-AVD, handled below
            }
        }

        // Android AVD (Virtual Devices) — often the biggest space hog
        var avdDir = Path.Combine(home, ".android", "avd");
        if (Directory.Exists(avdDir))
        {
            var options = new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = false };
            foreach (var avdFolder in Directory.EnumerateDirectories(avdDir, "*.avd", options))
            {
                ct.ThrowIfCancellationRequested();
                var avdName = Path.GetFileNameWithoutExtension(avdFolder);
                var size = GetDirectorySizeSafe(avdFolder, ct);

                if (size < 50 * 1024 * 1024) continue; // skip small ones

                // Check for snapshots
                var snapshotsDir = Path.Combine(avdFolder, "snapshots");
                long snapshotSize = 0;
                if (Directory.Exists(snapshotsDir))
                    snapshotSize = GetDirectorySizeSafe(snapshotsDir, ct);

                // Check for cache
                var cacheImg = Path.Combine(avdFolder, "cache.img");
                long cacheSize = 0;
                if (File.Exists(cacheImg))
                    cacheSize = new FileInfo(cacheImg).Length;

                // Full AVD as caution
                results.Add(new ScanResult
                {
                    Name = $"Android AVD: {avdName}",
                    Description = $"Android emulator virtual device '{avdName}'. Total size: {ScanResult.FormatSize(size)}."
                        + (snapshotSize > 0 ? $" Snapshots: {ScanResult.FormatSize(snapshotSize)}." : "")
                        + " Deleting removes the entire emulator — you'll need to recreate it in Android Studio.",
                    Path = avdFolder,
                    SizeBytes = size,
                    FileCount = CountFilesSafe(avdFolder),
                    Safety = SafetyLevel.Caution,
                    SafetyExplanation = "Deleting removes the emulator image. Recreate from Android Studio AVD Manager."
                });

                // Snapshots as safe-to-clear sub-item
                if (snapshotSize > 50 * 1024 * 1024)
                {
                    results.Add(new ScanResult
                    {
                        Name = $"AVD Snapshots: {avdName}",
                        Description = $"Quick-boot snapshots for emulator '{avdName}'. Safe to delete — emulator cold-boots instead (slower first launch).",
                        Path = snapshotsDir,
                        SizeBytes = snapshotSize,
                        FileCount = CountFilesSafe(snapshotsDir),
                        Safety = SafetyLevel.Safe,
                        SafetyExplanation = "Emulator snapshots — only affects boot speed, not functionality."
                    });
                }
            }
        }

        // Android Gradle caches (Android-specific transforms)
        var androidGradleCache = Path.Combine(home, ".gradle", "caches", "transforms-*");
        var gradleCachesDir = Path.Combine(home, ".gradle", "caches");
        if (Directory.Exists(gradleCachesDir))
        {
            foreach (var transformDir in SafeEnumerateDirectories(gradleCachesDir, "transforms-*"))
            {
                ct.ThrowIfCancellationRequested();
                var size = GetDirectorySizeSafe(transformDir, ct);
                if (size > 50 * 1024 * 1024)
                {
                    results.Add(new ScanResult
                    {
                        Name = $"Gradle Transform Cache ({Path.GetFileName(transformDir)})",
                        Description = "Android Gradle build transform cache. Safe to delete — rebuilt on next build.",
                        Path = transformDir,
                        SizeBytes = size,
                        FileCount = CountFilesSafe(transformDir),
                        Safety = SafetyLevel.Safe,
                        SafetyExplanation = "Build cache — Gradle recreates on next build."
                    });
                }
            }
        }

        return results;
    }

    #endregion

    #region App-Specific Caches

    private List<ScanResult> ScanAppSpecificCaches(CancellationToken ct)
    {
        var results = new List<ScanResult>();
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var temp = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);

        // === Electron app caches (generic pattern) ===
        var electronApps = new (string FolderName, string DisplayName)[]
        {
            ("Slack", "Slack"),
            ("discord", "Discord"),
            ("Microsoft Teams", "Microsoft Teams"),
            ("Teams", "Microsoft Teams (new)"),
            ("Postman", "Postman"),
            ("Figma", "Figma"),
            ("Notion", "Notion"),
            ("obsidian", "Obsidian"),
            ("1Password", "1Password"),
        };

        foreach (var (folder, name) in electronApps)
        {
            ct.ThrowIfCancellationRequested();
            var cacheDir = Path.Combine(appData, folder, "Cache");
            if (!Directory.Exists(cacheDir))
                cacheDir = Path.Combine(localAppData, folder, "Cache");

            AddIfExists(results, $"{name} Cache",
                $"{name} application cache. Safe to delete — rebuilt on next launch.",
                cacheDir, SafetyLevel.Safe, null, ct);

            // Also check Code Cache
            var codeCache = Path.Combine(Path.GetDirectoryName(cacheDir) ?? "", "Code Cache");
            if (Directory.Exists(codeCache))
            {
                var size = GetDirectorySizeSafe(codeCache, ct);
                if (size > 5 * 1024 * 1024)
                {
                    AddIfExists(results, $"{name} Code Cache",
                        $"{name} compiled code cache. Safe to delete.",
                        codeCache, SafetyLevel.Safe, null, ct);
                }
            }
        }

        // === Claude Desktop ===
        // Claude vm_bundles and cache
        AddIfExists(results, "Claude Desktop Cache",
            "Claude Desktop application cache. Safe to delete.",
            Path.Combine(appData, "Claude", "Cache"),
            SafetyLevel.Safe, null, ct);

        AddIfExists(results, "Claude Desktop Code Cache",
            "Claude Desktop compiled code cache. Safe to delete.",
            Path.Combine(appData, "Claude", "Code Cache"),
            SafetyLevel.Safe, null, ct);

        // Claude vm_bundles (can be large)
        var claudeVmBundles = Path.Combine(appData, "Claude", "vm_bundles");
        if (!Directory.Exists(claudeVmBundles))
            claudeVmBundles = Path.Combine(localAppData, "AnthropicClaude", "vm_bundles");

        AddIfExists(results, "Claude VM Bundles",
            "Claude Desktop virtual machine bundles. These can grow quite large. Safe to delete — re-downloaded as needed.",
            claudeVmBundles, SafetyLevel.Safe,
            "VM bundles are cached tool environments. Claude re-downloads them when needed.", ct);

        // Also scan for Claude under Anthropic paths
        AddIfExists(results, "Claude Desktop Cache (Anthropic)",
            "Claude Desktop application cache. Safe to delete.",
            Path.Combine(localAppData, "AnthropicClaude", "Cache"),
            SafetyLevel.Safe, null, ct);

        // === Clipchamp ===
        // Clipchamp stores temp render data in Packages
        var packagesDir = Path.Combine(localAppData, "Packages");
        if (Directory.Exists(packagesDir))
        {
            foreach (var dir in SafeEnumerateDirectories(packagesDir, "*Clipchamp*"))
            {
                ct.ThrowIfCancellationRequested();
                var localState = Path.Combine(dir, "LocalState");
                if (Directory.Exists(localState))
                {
                    AddIfExists(results, "Clipchamp Temp/Render Data",
                        "Clipchamp video editor temporary and render cache files. Safe to delete if you have no in-progress projects.",
                        localState, SafetyLevel.Caution,
                        "If you have in-progress Clipchamp projects, they may lose cached render data.", ct);
                }

                var tempState = Path.Combine(dir, "TempState");
                AddIfExists(results, "Clipchamp TempState",
                    "Clipchamp temporary state files. Safe to delete.",
                    tempState, SafetyLevel.Safe, null, ct);
            }
        }

        // === PostgreSQL ===
        // Postgres WAL and temp files
        var pgDataDirs = new List<string>();

        // Check common Postgres data directory locations
        var pgBasePaths = new[]
        {
            @"C:\Program Files\PostgreSQL",
            @"C:\ProgramData\PostgreSQL",
            @"C:\PostgreSQL",
        };

        foreach (var pgBase in pgBasePaths)
        {
            if (!Directory.Exists(pgBase)) continue;
            foreach (var versionDir in SafeEnumerateDirectories(pgBase, "*"))
            {
                var dataDir = Path.Combine(versionDir, "data");
                if (Directory.Exists(dataDir))
                    pgDataDirs.Add(dataDir);
            }
        }

        // Check PGDATA env var
        var pgData = Environment.GetEnvironmentVariable("PGDATA");
        if (!string.IsNullOrEmpty(pgData) && Directory.Exists(pgData))
            pgDataDirs.Add(pgData);

        foreach (var dataDir in pgDataDirs.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            ct.ThrowIfCancellationRequested();

            // pg_wal can grow large with leftover WAL segments
            var pgWal = Path.Combine(dataDir, "pg_wal");
            if (Directory.Exists(pgWal))
            {
                var walSize = GetDirectorySizeSafe(pgWal, ct);
                if (walSize > 256 * 1024 * 1024) // > 256 MB
                {
                    results.Add(new ScanResult
                    {
                        Name = "PostgreSQL WAL Logs",
                        Description = $"PostgreSQL write-ahead logs in {dataDir}. Large WAL directory may indicate stale replication slots or missed checkpoints. Run CHECKPOINT in psql to flush, then old segments are recycled.",
                        Path = pgWal,
                        SizeBytes = walSize,
                        FileCount = CountFilesSafe(pgWal),
                        Safety = SafetyLevel.DoNotDelete,
                        SafetyExplanation = "Do NOT delete directly — this causes data loss. Use PostgreSQL CHECKPOINT command to flush safely."
                    });
                }
            }

            // pg_stat_tmp
            var pgStatTmp = Path.Combine(dataDir, "pg_stat_tmp");
            AddIfExists(results, "PostgreSQL Stat Temp",
                "PostgreSQL temporary statistics files. Safe to delete — rebuilt by the server automatically.",
                pgStatTmp, SafetyLevel.Safe, null, ct);

            // Postgres log files
            var pgLog = Path.Combine(dataDir, "log");
            if (!Directory.Exists(pgLog))
                pgLog = Path.Combine(dataDir, "pg_log");
            AddIfExists(results, "PostgreSQL Logs",
                "PostgreSQL server log files. Safe to delete old logs.",
                pgLog, SafetyLevel.Safe, null, ct);
        }

        // === Chrome additional caches not in Quick Wins ===
        AddIfExists(results, "Chrome Service Worker Cache",
            "Chrome service worker and background sync cache. Safe to delete.",
            Path.Combine(localAppData, "Google", "Chrome", "User Data", "Default", "Service Worker", "CacheStorage"),
            SafetyLevel.Safe, null, ct);

        AddIfExists(results, "Chrome GPU Cache",
            "Chrome GPU shader cache. Safe to delete.",
            Path.Combine(localAppData, "Google", "Chrome", "User Data", "Default", "GPUCache"),
            SafetyLevel.Safe, null, ct);

        // Edge additional caches
        AddIfExists(results, "Edge Service Worker Cache",
            "Edge service worker cache. Safe to delete.",
            Path.Combine(localAppData, "Microsoft", "Edge", "User Data", "Default", "Service Worker", "CacheStorage"),
            SafetyLevel.Safe, null, ct);

        // === Spotify ===
        AddIfExists(results, "Spotify Cache",
            "Spotify streaming cache. Safe to delete — songs re-stream on play. Downloaded songs are stored separately.",
            Path.Combine(localAppData, "Spotify", "Storage"),
            SafetyLevel.Safe,
            "Only the streaming cache — not your downloaded offline songs.", ct);

        // === Windows Delivery Optimization ===
        AddIfExists(results, "Windows Delivery Optimization Cache",
            "Windows Update peer-to-peer delivery cache. Safe to delete — Windows re-downloads if needed.",
            @"C:\Windows\SoftwareDistribution\DeliveryOptimization",
            SafetyLevel.Safe, null, ct);

        // === Crash dumps ===
        AddIfExists(results, "Windows Crash Dumps",
            "Windows crash dump files. Safe to delete unless you're debugging a BSOD.",
            Path.Combine(localAppData, "CrashDumps"),
            SafetyLevel.Safe, null, ct);

        AddIfExists(results, "Windows Memory Dumps",
            "Full/mini memory dump files from system crashes.",
            @"C:\Windows\Minidump",
            SafetyLevel.Safe, null, ct);

        // === Misc temp ===
        AddIfExists(results, "Windows Temp Install Files",
            "Temporary installation files left behind by various installers.",
            @"C:\Windows\Temp\*",
            SafetyLevel.Safe, null, ct);

        return results;
    }

    #endregion

    #region Node Modules

    private List<ScanResult> ScanNodeModules(CancellationToken ct)
    {
        var results = new List<ScanResult>();
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        // Scan common project locations
        var searchRoots = new List<string> { home };

        // Add common dev folders
        var commonDevDirs = new[] { "Projects", "Development", "Dev", "repos", "Repos", "Source", "src", "code", "Code", "workspace", "Workspace" };
        foreach (var dir in commonDevDirs)
        {
            var path = Path.Combine(home, dir);
            if (Directory.Exists(path) && !searchRoots.Contains(path))
                searchRoots.Add(path);
        }

        // Also check drive roots
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
        {
            foreach (var dir in commonDevDirs)
            {
                var path = Path.Combine(drive.RootDirectory.FullName, dir);
                if (Directory.Exists(path) && !searchRoots.Contains(path, StringComparer.OrdinalIgnoreCase))
                    searchRoots.Add(path);
            }
        }

        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var staleDays = _settings.MinFileAgeDays;

        foreach (var root in searchRoots)
        {
            ct.ThrowIfCancellationRequested();
            FindNodeModules(root, 0, 5, found, results, staleDays, ct); // max depth 5
        }

        return results;
    }

    private void FindNodeModules(string dir, int depth, int maxDepth,
        HashSet<string> found, List<ScanResult> results, int staleDays, CancellationToken ct)
    {
        if (depth > maxDepth) return;

        var options = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = false,
            AttributesToSkip = FileAttributes.System | FileAttributes.ReparsePoint
        };

        IEnumerable<string> subdirs;
        try { subdirs = Directory.EnumerateDirectories(dir, "*", options); }
        catch { return; }

        foreach (var subdir in subdirs)
        {
            ct.ThrowIfCancellationRequested();
            var name = Path.GetFileName(subdir);

            if (name.Equals("node_modules", StringComparison.OrdinalIgnoreCase))
            {
                if (found.Contains(subdir)) continue;
                found.Add(subdir);

                var parentDir = Path.GetDirectoryName(subdir) ?? subdir;
                var packageJson = Path.Combine(parentDir, "package.json");
                var hasPackageJson = File.Exists(packageJson);

                // Check staleness
                DateTime? lastMod = null;
                try
                {
                    if (hasPackageJson)
                        lastMod = File.GetLastWriteTime(packageJson);
                }
                catch { }

                bool isStale = lastMod.HasValue && lastMod.Value < DateTime.Now.AddDays(-staleDays);
                var size = GetDirectorySizeSafe(subdir, ct);

                if (size < 10 * 1024 * 1024) continue; // skip tiny ones (< 10 MB)

                var projectName = Path.GetFileName(parentDir);
                var safety = isStale ? SafetyLevel.Safe : SafetyLevel.Caution;
                var staleText = isStale
                    ? $"Project hasn't been modified in {staleDays}+ days — likely safe to remove."
                    : "Project appears active. Deleting node_modules requires running 'npm install' to restore.";

                results.Add(new ScanResult
                {
                    Name = $"node_modules: {projectName}",
                    Description = $"Node.js dependencies for '{projectName}'. {staleText}",
                    Path = subdir,
                    SizeBytes = size,
                    FileCount = CountFilesSafe(subdir),
                    Safety = safety,
                    SafetyExplanation = $"Recoverable — run 'npm install' in {parentDir} to restore.",
                    LastModified = lastMod
                });

                continue; // don't recurse into node_modules
            }

            // Skip certain dirs for performance
            if (name.StartsWith('.') || name.Equals("bin", StringComparison.OrdinalIgnoreCase)
                || name.Equals("obj", StringComparison.OrdinalIgnoreCase))
                continue;

            FindNodeModules(subdir, depth + 1, maxDepth, found, results, staleDays, ct);
        }
    }

    #endregion

    #region Git Repos

    private List<ScanResult> ScanGitRepos(CancellationToken ct)
    {
        var results = new List<ScanResult>();
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var searchRoots = new List<string>();
        var commonDevDirs = new[] { "Projects", "Development", "Dev", "repos", "Repos", "Source", "src", "code", "Code", "workspace", "Workspace" };

        foreach (var dir in commonDevDirs)
        {
            var path = Path.Combine(home, dir);
            if (Directory.Exists(path))
                searchRoots.Add(path);
        }

        // Also check drive roots for dev dirs
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
        {
            foreach (var dir in commonDevDirs)
            {
                var path = Path.Combine(drive.RootDirectory.FullName, dir);
                if (Directory.Exists(path) && !searchRoots.Contains(path, StringComparer.OrdinalIgnoreCase))
                    searchRoots.Add(path);
            }
        }

        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        const long minGitSize = 100L * 1024 * 1024; // 100 MB

        foreach (var root in searchRoots)
        {
            ct.ThrowIfCancellationRequested();
            FindLargeGitDirs(root, 0, 4, found, results, minGitSize, ct);
        }

        return results;
    }

    private void FindLargeGitDirs(string dir, int depth, int maxDepth,
        HashSet<string> found, List<ScanResult> results, long minSize, CancellationToken ct)
    {
        if (depth > maxDepth) return;

        var options = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = false,
            AttributesToSkip = FileAttributes.System | FileAttributes.ReparsePoint
        };

        IEnumerable<string> subdirs;
        try { subdirs = Directory.EnumerateDirectories(dir, "*", options); }
        catch { return; }

        foreach (var subdir in subdirs)
        {
            ct.ThrowIfCancellationRequested();
            var name = Path.GetFileName(subdir);

            if (name.Equals(".git", StringComparison.OrdinalIgnoreCase))
            {
                if (found.Contains(subdir)) continue;
                found.Add(subdir);

                var size = GetDirectorySizeSafe(subdir, ct);
                if (size < minSize) continue;

                var repoName = Path.GetFileName(Path.GetDirectoryName(subdir) ?? subdir);

                results.Add(new ScanResult
                {
                    Name = $".git: {repoName}",
                    Description = $"Git history for '{repoName}'. Large .git directories can be compacted with 'git gc'. Do NOT delete — this contains all version history.",
                    Path = subdir,
                    SizeBytes = size,
                    FileCount = CountFilesSafe(subdir),
                    Safety = SafetyLevel.DoNotDelete,
                    SafetyExplanation = "Deleting .git permanently loses all version history. Use 'git gc --aggressive' to compact instead."
                });

                continue; // don't recurse into .git
            }

            if (name.Equals("node_modules", StringComparison.OrdinalIgnoreCase)
                || name.Equals("bin", StringComparison.OrdinalIgnoreCase)
                || name.Equals("obj", StringComparison.OrdinalIgnoreCase))
                continue;

            FindLargeGitDirs(subdir, depth + 1, maxDepth, found, results, minSize, ct);
        }
    }

    #endregion

    #region Helpers

    private void AddIfExists(List<ScanResult> results, string name, string description,
        string path, SafetyLevel safety, string? safetyExplanation, CancellationToken ct)
    {
        if (!Directory.Exists(path)) return;

        var size = GetDirectorySizeSafe(path, ct);
        if (size < 1024 * 1024) return; // skip < 1 MB

        results.Add(new ScanResult
        {
            Name = name,
            Description = description,
            Path = path,
            SizeBytes = size,
            FileCount = CountFilesSafe(path),
            Safety = safety,
            SafetyExplanation = safetyExplanation ?? (safety == SafetyLevel.Safe
                ? "Cache folder — contents are rebuilt automatically."
                : "Review before deleting.")
        });

        LogService.Log($"DevIDE [{name}]: {ScanResult.FormatSize(size)}");
    }

    private static long GetDirectorySizeSafe(string path, CancellationToken ct)
    {
        long total = 0;
        var options = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.System | FileAttributes.ReparsePoint
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

    private static int CountFilesSafe(string path)
    {
        try
        {
            var options = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = true,
                AttributesToSkip = FileAttributes.System | FileAttributes.ReparsePoint
            };
            return Directory.EnumerateFiles(path, "*", options).Count();
        }
        catch { return 0; }
    }

    private static IEnumerable<string> SafeEnumerateDirectories(string path, string pattern)
    {
        var options = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = false
        };

        IEnumerable<string> dirs;
        try { dirs = Directory.EnumerateDirectories(path, pattern, options); }
        catch { yield break; }

        foreach (var dir in dirs)
            yield return dir;
    }

    #endregion
}
