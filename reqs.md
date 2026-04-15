# DriveDoctor — Windows Disk Space Cleanup Tool
## Product Requirements Document (PRD) v1.0

---

## 1. Overview

**DriveDoctor** is a Windows-native desktop application that helps users reclaim disk space on their C: drive (and optionally other drives). It combines automated scanning of well-known cleanup targets (temp files, caches, downloads, etc.) with a deep-scan engine that surfaces large files, rarely-used application data, and installed programs ordered by last-used date.

The application is designed to be:
- **Cautious** — it never deletes anything without user confirmation, and flags uncertainty explicitly.
- **Informative** — every finding includes a plain-English explanation of what was found and why it is (or isn't) safe to delete.
- **Modern** — the UI is clean, minimal, and professional. No classic WinForms aesthetic.

---

## 2. Technical Stack

| Concern | Recommendation |
|---|---|
| Language | C# (.NET 8+) |
| UI Framework | WPF with a modern MVVM library (e.g. CommunityToolkit.Mvvm), **or** WinUI 3 (Windows App SDK) for a more native Fluent look |
| Styling | Custom resource dictionaries with a dark/light theme; no default WinForms or stock WPF chrome |
| Admin Elevation | UAC elevation via manifest (`requestedExecutionLevel = requireAdministrator`) or a self-elevating launcher |
| Packaging | Single-file publish (`dotnet publish -r win-x64 --self-contained`) or MSIX package |
| Minimum Target | Windows 10 1903+ |

> **Note to implementer:** WinUI 3 gives the most "modern Windows 11" feel out of the box. If WPF is chosen, use a third-party theme library (e.g. ModernWpf or MahApps.Metro) to avoid the default grey system look.

---

## 3. Architecture

```
DriveDoctor
├── Core (Class Library)
│   ├── Scanning/
│   │   ├── QuickWinScanner.cs        # Temp, cache, trash, downloads
│   │   ├── DeepScanner.cs            # Large files, AppData mining
│   │   ├── InstalledProgramsScanner.cs
│   │   └── DependencyAnalyzer.cs     # Checks registry, shortcuts, known paths
│   ├── Models/
│   │   ├── ScanResult.cs
│   │   ├── CleanupTarget.cs
│   │   └── InstalledProgram.cs
│   └── Cleanup/
│       └── CleanupEngine.cs          # Safe deletion with recycle/shred options
└── UI (WPF / WinUI 3)
    ├── Views/
    │   ├── MainWindow
    │   ├── QuickWinsView
    │   ├── DeepScanView
    │   └── ProgramsView
    └── ViewModels/
```

---

## 4. UAC / Administrator Elevation

- The application **must run as Administrator** to access certain system folders, registry hives, and program installation data.
- On launch, the app checks if it is running elevated. If not, it:
  1. Shows a brief, friendly prompt: *"DriveDoctor needs administrator access to scan system folders. Click Continue to re-launch with elevated permissions."*
  2. Re-launches itself via `Process.Start` with `UseShellExecute = true` and `Verb = "runas"`.
- The elevated instance must not show a blank window before the UAC prompt fires.
- The non-elevated launch window should close cleanly after the elevated one starts.

---

## 5. Application Sections

The app has three primary sections accessible via a left-side navigation rail (icon + label):

1. **Quick Wins** — Safe, well-known cleanup targets
2. **Deep Scan** — Large files and AppData analysis
3. **Installed Programs** — Programs by last-used date

A persistent **status bar** at the bottom shows: current C: drive usage (used / total / free), updated after any cleanup action.

---

## 6. Section 1 — Quick Wins

### 6.1 Purpose
Surface clearly safe targets that can be deleted in a single click with zero risk to application function.

### 6.2 Targets to Scan

| Target | Path(s) | Safety |
|---|---|---|
| Windows Temp | `%SystemRoot%\Temp\*` | Safe |
| User Temp | `%TEMP%\*` | Safe |
| Windows Update Cache | `C:\Windows\SoftwareDistribution\Download\*` | Safe (Windows re-downloads if needed) |
| Prefetch Files | `C:\Windows\Prefetch\*` | Safe (Windows rebuilds; minor perf cost on next boot) |
| Windows Error Reports | `%LOCALAPPDATA%\Microsoft\Windows\WER\**` | Safe |
| Recycle Bin | Per-drive `$Recycle.Bin` for current user | Safe |
| Browser Caches | Chrome, Edge, Firefox known cache paths under `%LOCALAPPDATA%` and `%APPDATA%` | Safe |
| Downloads Folder | `%USERPROFILE%\Downloads\*` | **Flag only — user must confirm each file or select all** |
| Thumbnail Cache | `%LOCALAPPDATA%\Microsoft\Windows\Explorer\thumbcache_*.db` | Safe |
| Recent Files Shortcuts | `%APPDATA%\Microsoft\Windows\Recent\*` | Safe (removes MRU list only, not files) |
| Installer Leftovers | `%WINDIR%\Installer` — orphaned `.msi` patches only | Caution — flag and explain |
| Log Files | `%LOCALAPPDATA%\**\*.log` older than 30 days | Caution |

### 6.3 Scan Behaviour
- Scanning runs asynchronously with a progress indicator.
- Each target shows: name, path(s) scanned, files found, total size recoverable.
- Scanning should not block the UI thread at any point.

### 6.4 Display
- Results grouped into two tiers:
  - **Safe to delete now** — green badge
  - **Review recommended** — yellow badge (Downloads, log files, etc.)
- Each result card shows:
  - Target name
  - Plain-English description of what it is
  - File count + total size
  - A **"Delete"** button (or **"Open Folder"** link for Downloads)
- A **"Clean All Safe Items"** button at the top cleans everything in the green tier in one action.
- After deletion: card updates to show "0 files — 0 MB" with a checkmark. The status bar updates.

### 6.5 Failure Handling
- Files that cannot be deleted (e.g. locked by a running process) are skipped silently, with a summary at the end: *"3 files were in use and could not be removed."*
- No crashes or error dialogs for individual file failures.

---

## 7. Section 2 — Deep Scan

### 7.1 Purpose
Identify large files and directories across C: and flag AppData contents that are large and/or infrequently accessed. Surface possible space hogs that the user may not be aware of.

### 7.2 Scan Scope
- **Drive-wide file scan:** Recursively enumerate all files on C: (skip system-protected paths listed in §7.5).
- **Top large files:** Return top 200 files by size.
- **Top large directories:** Aggregate directory sizes, return top 50 directories by total size.
- **AppData spotlight:** Recursively enumerate `%APPDATA%`, `%LOCALAPPDATA%`, `%LOCALAPPDATA%\Packages` and surface folders/files that are:
  - Larger than 100 MB, **or**
  - Last modified more than 180 days ago and larger than 50 MB

### 7.3 Dependency Analysis

For each large AppData folder or suspicious file, the app attempts to determine if it is:

| Signal | Method |
|---|---|
| Tied to an installed application | Match parent folder name or publisher against `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall` and `HKCU\SOFTWARE\...` registry keys |
| A cache or data folder (not a binary) | Heuristic: folder contains no `.exe`/`.dll`, name contains "cache", "data", "logs", "temp", "storage" |
| On another drive but linked here | Check for junction points / symlinks (`FileAttributes.ReparsePoint`) |
| A known safe-to-clean path | Static allowlist of vendor cache paths (e.g. `npm`, `pip`, `nuget`, `gradle`, VS Code extensions cache, etc.) |

**Safety Classification:**

- 🟢 **Safe** — Known cache/temp; confirmed no executable dependency.
- 🟡 **Caution** — Linked to an installed application; deleting may affect it. State which app.
- 🔴 **Do Not Delete** — System folder, active runtime, or contains application binaries.
- ⚪ **Unknown** — Cannot determine safety. Explicitly say: *"DriveDoctor cannot determine whether this is safe to delete. Manual review recommended."*

### 7.4 Display
- Results in a sortable, filterable table/list:
  - Columns: Name, Path, Size, Last Modified, Safety, Linked Application (if any)
  - Sort by: Size (default), Last Modified, Name
  - Filter by: Safety level
- Selecting a row shows a detail panel:
  - Full path
  - Safety explanation in plain English
  - Linked app name (if found)
  - Whether it appears to be a junction or symlink
  - **"Delete"** button — disabled for 🔴 items; shows a warning dialog for 🟡 items before proceeding
- Multi-select with **"Delete Selected"** is supported for 🟢 items only.

### 7.5 Paths to Skip / Protect
The scanner must **never** enumerate or offer to delete:

- `C:\Windows\System32\`
- `C:\Windows\SysWOW64\`
- `C:\Windows\WinSxS\`
- `C:\Program Files\` *(surface only — never offer delete here; direct user to uninstaller)*
- `C:\Program Files (x86)\` *(same)*
- Paths containing active page files (`pagefile.sys`, `swapfile.sys`, `hiberfil.sys`)
- Any path flagged as a system volume or mount point

If a directory is protected, it may appear in results with a 🔴 badge and a tooltip explanation, but the Delete button must be absent.

---

## 8. Section 3 — Installed Programs

### 8.1 Purpose
List all installed programs ranked by **last-used date** so the user can quickly identify stale software that can be safely uninstalled.

### 8.2 Data Collection

**Source of installed programs:**
- `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*`
- `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*`
- `HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*`

**Fields to extract per program:**
- Display name
- Publisher
- Install date (`InstallDate` registry value)
- Install location (`InstallLocation`)
- Estimated size (`EstimatedSize`)
- Uninstall string (`UninstallString`)

**Last-used date heuristic (attempt in order, use first that yields a result):**
1. Check Prefetch files in `C:\Windows\Prefetch\` for a `.pf` file whose name matches the program's main executable. Use the file's `LastWriteTime`.
2. Check `%APPDATA%` and `%LOCALAPPDATA%` for a vendor folder matching the publisher/app name. Use the most recent file modification time within it.
3. Check Windows MUI cache: `HKCU\Software\Classes\Local Settings\Software\Microsoft\Windows\Shell\MuiCache` for entries matching the install path. MUI entries are updated when an app is launched.
4. If none of the above yield a date, display **"Last used: Unknown"**.

> The implementer should document clearly which heuristic produced the date, showing it as a tooltip (e.g. *"Date sourced from Prefetch"*).

### 8.3 Display
- Sortable table with columns: Program Name, Publisher, Install Date, **Last Used**, Size, Install Drive
- Default sort: **Last Used (oldest first)**
- Filter bar: search by name or publisher
- Each row has an **"Uninstall"** button that:
  1. Shows a confirmation dialog: *"This will run [Program]'s official uninstaller. Proceed?"*
  2. Launches the program's `UninstallString` via `Process.Start`.
  3. After the uninstaller exits, refreshes the list.
- Programs installed on drives other than C: are shown with their drive letter indicated, but are still listed (they may have AppData/registry entries on C:).
- Programs with no `UninstallString` show a **"No uninstaller found"** label and an **"Open Install Folder"** link instead.

---

## 9. Non-Functional Requirements

### 9.1 Performance
- Quick Wins scan must complete in under 10 seconds on a typical system.
- Deep Scan is expected to take longer (30 seconds to several minutes); a progress bar with live file count and current path being scanned must be shown.
- UI must never freeze. All I/O runs on background threads. Use `async/await` throughout.
- Cancellation: the user can cancel any in-progress scan via a **"Stop Scan"** button.

### 9.2 Safety
- **No file is ever deleted without an explicit user action.** Scanning is purely read-only.
- Deleted files go to the **Recycle Bin** by default (`FileSystem.DeleteFile` with `UIOption.OnlyErrorDialogs` and `RecycleOption.SendToRecycleBin`).
- A settings toggle allows switching to **Permanent Delete** (with a prominent warning label).
- Before any bulk delete operation, show a summary dialog: *"You are about to permanently delete X files totalling Y MB. This cannot be undone."*

### 9.3 Error Handling
- Access denied exceptions are swallowed silently during scanning (common for system paths).
- A scan error log is written to `%LOCALAPPDATA%\DriveDoctor\logs\` for diagnostic purposes.
- The UI must never crash. Unhandled exceptions should show a friendly error screen with a **"Copy Error Details"** button.

### 9.4 Settings
Accessible via a gear icon in the nav rail:
- Theme: Light / Dark / System default
- Delete mode: Recycle Bin (default) / Permanent
- Minimum file age for AppData flagging (default: 180 days)
- Minimum file size to include in Deep Scan results (default: 10 MB)
- Excluded paths (user-definable list of paths to skip)

---

## 10. UI / UX Design Guidelines

- **Design language:** Fluent Design System (Windows 11 aesthetic). Acrylic/Mica backgrounds if on WinUI 3; flat card-based layout if WPF.
- **Navigation:** Left-side vertical nav rail with icon + label. Active section highlighted.
- **Colour palette:** Neutral dark/light base. Use colour sparingly — only for safety badges (green / amber / red) and primary action buttons.
- **Typography:** Segoe UI Variable (system default on Windows 11) or Segoe UI. No decorative fonts.
- **Animations:** Subtle fade-in for scan results. Progress indicators use indeterminate bars during scan, switching to percentage once total size is known.
- **Empty states:** Each section has a friendly empty state with an icon and a prompt to start scanning.
- **Iconography:** Use Segoe Fluent Icons (built into Windows) or a small SVG icon set (e.g. Lucide). No raster icons.
- **Responsive:** Window must be usable at 1024×600 minimum resolution. No horizontal scroll on results lists.

---

## 11. Out of Scope (v1.0)

- Scheduled/automatic cleanup runs
- Registry cleaning (out of scope due to risk profile)
- Duplicate file detection
- Cloud storage integration
- Network drive scanning
- Startup program management
- Defragmentation / SSD TRIM

These may be considered for v2.0 as clearly labelled cards in a "Coming Soon" section.

---

## 12. Suggested Implementation Order

1. Project scaffold + UAC elevation + nav shell + theme
2. Quick Wins scanner + UI (fastest user-visible value)
3. Status bar + deletion engine (Recycle Bin mode)
4. Installed Programs scanner + UI
5. Deep Scan file/directory enumeration + UI
6. Dependency analysis and safety classification
7. Settings screen
8. Error handling, logging, edge case hardening
9. Packaging and final polish

---

## 13. Glossary

| Term | Meaning |
|---|---|
| Quick Win | A well-known, safe-to-delete target requiring minimal user judgment |
| Deep Scan | A full drive enumeration surfacing large or stale items |
| Safety Classification | The app's assessment of whether a file/folder is safe to delete |
| AppData | `%APPDATA%` and `%LOCALAPPDATA%` — per-user application data folders |
| Dependency | A file or folder required by an installed application to function |
| Prefetch | Windows cache of recently executed programs, stored in `C:\Windows\Prefetch\` |
| Junction / Symlink | A filesystem reparse point that makes a path on C: point to another location |

---

*Document version 1.0 — ready for engineering handoff.*