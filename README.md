# PurgeKit

> **Windows only.** PurgeKit is a native Windows desktop app (WPF). It uses Windows-specific APIs (registry, Shell32, WMI, Prefetch, diskpart) and will not run on macOS or Linux.

[![Build & Publish](https://github.com/rbretschneider/purgekit/actions/workflows/build.yml/badge.svg)](https://github.com/rbretschneider/purgekit/actions/workflows/build.yml)

A Windows desktop application for reclaiming disk space. PurgeKit scans for temp files, package manager caches, developer tool bloat, large forgotten files, and unused programs — then lets you clean them up safely.

Built with WPF (.NET 8), using the ModernWpf dark theme and the CommunityToolkit.Mvvm MVVM framework.

## Download

[**Download latest release**](https://github.com/rbretschneider/purgekit/releases/latest) — grab `PurgeKit.UI.exe` from the Assets section. No install required, just run the exe.

**Requirements:** Windows 10/11 (x64). No .NET runtime install needed — the exe is self-contained.

PurgeKit checks for updates automatically on startup. When a new version is available, it will prompt you to download and install it in-place — no manual re-downloading needed.

## Why

Windows accumulates disk waste from dozens of sources: browser caches, npm/pip/NuGet package caches, GPU driver installers left behind by NVIDIA and AMD, Docker VHDX files, WSL virtual disks, old log files, crash dumps, and more. Most users don't know where to look, and built-in Disk Cleanup misses developer-specific waste entirely.

PurgeKit finds all of it in one scan, classifies each item by safety level, and gives you one-click cleanup.

## How It Works

### Unified Scan Architecture

One button does everything. Click **Scan All** in the sidebar and four scanners run in parallel:

1. **Quick Wins Scanner** — Targets well-known safe-to-delete locations
2. **Developer Tools Scanner** — Finds caches, virtual disks, and bloat from dev ecosystems
3. **Deep Scanner** — Walks the entire C: drive looking for large files, large directories, and stale AppData folders
4. **Programs Scanner** — Enumerates installed programs from the registry with last-used heuristics

Results are streamed via `System.Threading.Channels` so items appear in the UI immediately rather than waiting for all scanners to finish. Items smaller than 1 MB are filtered out. Duplicate paths are deduplicated.

### Safety Classification

Every result is assigned a safety level:

| Level | Color | Meaning |
|-------|-------|---------|
| **Safe** | Green | Caches and temp files. Tools re-download on demand. No data loss. |
| **Caution** | Orange | Might contain user data or require manual review. Prompts before deletion. |
| **Do Not Delete** | Red | System files or program binaries. Shown for context but deletion is blocked. |

Classification is handled by the **DependencyAnalyzer**, which applies rules in this order:

1. Protected system paths (System32, WinSxS, Program Files) are always Do Not Delete
2. Known cache folder names (npm-cache, pip, NuGet, Cache, CachedData, etc.) are Safe
3. Paths linked to installed programs (matched via registry) with binaries are Do Not Delete
4. Cache/data subfolders of installed programs without binaries are Safe
5. Symlinks/junctions are flagged as Caution
6. Everything else is Unknown (manual review recommended)

### Two-Column Results View

Results are displayed in two columns, sorted largest-first:

- **Safe to Clean** (left) — "Clean All" button in the header deletes everything in this column (with a confirmation warning)
- **Needs Review** (right) — Each item must be reviewed and deleted individually

Every card shows the full description (no truncation), the item size, and action buttons:

- **View** — Opens the folder in Explorer so you can inspect before deleting
- **Delete** — Deletes the item (Caution items prompt for confirmation first)
- **Compact** / **Prune** / **Git GC** — Special actions for WSL, Docker, and Git items

## What It Scans

### Quick Wins

Standard Windows cleanup targets:

- **Windows Temp** (`C:\Windows\Temp`)
- **User Temp** (system temp folder)
- **Windows Update Cache** (`C:\Windows\SoftwareDistribution\Download`)
- **Prefetch Files** (`C:\Windows\Prefetch`)
- **Windows Error Reports** (`%LOCALAPPDATA%\Microsoft\Windows\WER`)
- **Recycle Bin** (`$Recycle.Bin` on all fixed drives)
- **Browser Caches** — Chrome, Edge, Firefox (cache directories only, not history or cookies)
- **Thumbnail Cache** (`thumbcache_*.db` files)
- **Recent File Shortcuts** (`%APPDATA%\Microsoft\Windows\Recent`)
- **Old Log Files** (`*.log` older than 30 days in `%LOCALAPPDATA%`)
- **Downloads Folder** (flagged as Caution, requires confirmation)

### Developer Tools

Package manager and toolchain caches:

- **Node.js**: npm, yarn, pnpm caches and stale `node_modules` directories
- **Python**: pip cache, conda packages, `__pycache__`
- **.NET**: NuGet packages, dotnet workloads, Symbol Cache
- **JVM**: Gradle caches/wrapper, Maven repository
- **Rust**: Cargo registry cache and git DB
- **Go**: Module download cache
- **PHP**: Composer cache
- **Ruby**: Gem cache
- **Dart/Flutter**: Pub cache

IDE and editor caches:

- **VS Code**: Extension cache, CachedData
- **Visual Studio**: ComponentModelCache per version
- **JetBrains IDEs**: Transient caches
- **Android Studio**: System caches, AVD snapshots, Gradle transforms

GPU driver waste:

- **NVIDIA**: Driver installer leftovers (`C:\NVIDIA`), DXCache, GLCache, shader cache, NV_Cache, GeForce Experience temp
- **AMD**: Driver installer leftovers (`C:\AMD`), DxCache, GLCache
- **DirectX Shader Cache** (`D3DSCache`)

Container and VM tools:

- **Docker**: WSL2 backend VHDX (`ext4.vhdx`), Hyper-V VHDX, config/logs. Offers "Prune" button that runs `docker system prune -a -f --volumes`
- **WSL**: Per-distro VHDX files. Offers "Compact" button that runs `diskpart` to shrink the virtual disk without deleting data

Git repositories:

- Finds `.git` directories over 100 MB. Marked Do Not Delete but offers a **Git GC** button that runs `git gc --aggressive --prune=now` to compact history

Gaming caches:

- **Steam**: Shader cache, HTML cache
- **Epic Games**: Webcache
- **Unreal Engine**: DerivedDataCache
- **Unity**: Cache
- **GOG Galaxy**: Webcache

Electron app caches:

- Slack, Discord, Teams, Postman, Figma, Notion, Obsidian, 1Password, Claude Desktop, Spotify streaming cache

Other:

- **PostgreSQL**: WAL logs (flagged Do Not Delete with suggestion to run CHECKPOINT), stat temp, log files
- **Crash Dumps**: `%LOCALAPPDATA%\CrashDumps`, `C:\Windows\Minidump`
- **Windows Delivery Optimization** cache
- **Windows Package Cache** and Installer Patch Cache (Caution)
- **Clipchamp** temp/local state

### Deep Scan

Full-drive analysis for anything the targeted scanners missed:

- **Large files** on C: drive above the configured minimum size (default 10 MB), top 200 by size
- **Large directories** — top-level and one level deep on C:
- **Stale AppData folders** — directories in `%APPDATA%`, `%LOCALAPPDATA%`, and UWP Packages that are large (100 MB+) or moderately large (50 MB+) and haven't been modified in the configured number of days (default 180)

Protected paths (System32, WinSxS, Program Files) and system files (pagefile.sys, hiberfil.sys, swapfile.sys) are always skipped.

## Programs View

A separate tab lists all installed programs (from the Windows registry) with:

- Name, publisher, install date, estimated size, drive letter
- **Last Used** date determined heuristically from Prefetch files, AppData folder modification times, and the MUI Cache registry
- Search/filter by name, publisher, or drive
- Sortable columns (click headers to toggle sort direction)
- Uninstall button (launches the program's own uninstaller)

## Settings

| Setting | Default | Description |
|---------|---------|-------------|
| Theme | Dark | Dark, Light, or System |
| Use Recycle Bin | Off | When on, deleted files go to the Recycle Bin instead of being permanently deleted |
| Min File Age | 180 days | Files in AppData older than this are flagged by the deep scanner |
| Min File Size | 10 MB | Files smaller than this are ignored by the deep scanner |
| Excluded Paths | (empty) | Directories to skip during deep scans |

Settings are persisted to `%LOCALAPPDATA%\PurgeKit\settings.json`.

## Building

Requires Windows and the .NET 8 SDK (the `net8.0-windows` workload is included by default on Windows).

```bash
# Debug build
dotnet build src/PurgeKit.UI/PurgeKit.UI.csproj

# Publish self-contained exe
dotnet publish src/PurgeKit.UI/PurgeKit.UI.csproj -c Release -r win-x64 --self-contained -o publish
```

The published exe is at `publish/PurgeKit.UI.exe` — a single self-contained binary, no .NET runtime install required.

A GitHub Actions workflow (`.github/workflows/build.yml`) runs on every push to `main`:

- Builds and publishes a self-contained exe
- Creates a GitHub Release tagged `v0.1.<run_number>` with the exe attached
- The version is baked into the assembly so the app's auto-update checker can compare against it

## Admin Elevation

PurgeKit requests administrator privileges on startup. This is needed to scan system folders like `C:\Windows\Temp`, `C:\Windows\Prefetch`, and `C:\Windows\SoftwareDistribution`. If you decline the UAC prompt, the app still runs but some scan targets will be inaccessible.

## Project Structure

```
src/
  PurgeKit.Core/           Core library (no UI dependency)
    Scanning/
      QuickWinScanner.cs     Predefined safe cleanup targets
      DeepScanner.cs         Full-drive large/old file detection
      DevToolsScanner.cs     Developer tool cache detection
      DependencyAnalyzer.cs  Safety classification engine
      InstalledProgramsScanner.cs  Registry-based program listing
      UnifiedScanner.cs      Parallel orchestrator, streams results via Channel
    Cleanup/
      CleanupEngine.cs       File deletion (permanent or Recycle Bin via Shell32)
    Models/
      ScanResult.cs          Universal scan result model
      AppSettings.cs         Persisted configuration
      ...
    Services/
      DriveInfoService.cs    Drive space queries
      ElevationHelper.cs     UAC elevation
      LogService.cs          File-based logging to %LOCALAPPDATA%\PurgeKit\logs\

  PurgeKit.UI/             WPF application
    Views/
      ScanView.xaml          Two-column scan results (Safe / Needs Review)
      ProgramsView.xaml      Installed programs list with sort/filter
      SettingsView.xaml      Configuration panel
    ViewModels/
      ScanViewModel.cs       Scan orchestration, item actions (delete, compact, prune, gc)
      ProgramsViewModel.cs   Program listing with search/sort
      SettingsViewModel.cs   Settings binding and persistence
      MainViewModel.cs       Navigation and drive usage display
    Converters/              WPF value converters (bool-to-visibility, safety-to-color, etc.)
    Styles/
      NavStyles.xaml         Sidebar navigation button template
```

## Tech Stack

- **.NET 8** (net8.0-windows, WPF)
- **ModernWpfUI** — Fluent Design dark/light theme
- **CommunityToolkit.Mvvm** — Source-generated ObservableProperty and RelayCommand
- **System.Threading.Channels** — Streaming scan results from parallel workers to the UI
- **Shell32 P/Invoke** — Recycle Bin support via SHFileOperation
- **Microsoft.Win32.Registry** — Program detection, WSL/Docker/Steam path discovery
