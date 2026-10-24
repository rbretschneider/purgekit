# App Relocation ("Move to another drive") — Plan

Status: proposal, not implemented.

## Goal

A new **Relocate** panel that lists installed programs on C: that haven't been used in a while and lets
the user move them to another drive with one click — and move them back just as easily.

## Is it possible?

Yes, for most desktop apps. The hard-coded `C:\...` references are the problem, and the standard fix
(used by FreeMove, Steam Mover, Application Mover) is an **NTFS directory junction**:

1. Copy `C:\Program Files\Foo` → `D:\PurgeKit\Relocated\Program Files\Foo`
2. Verify the copy (file count + sizes + hashes of executables)
3. Rename the original to `Foo.purgekit-pending`
4. Create a junction `C:\Program Files\Foo` → `D:\PurgeKit\Relocated\Program Files\Foo`
5. Delete `Foo.purgekit-pending` once the junction resolves

Every registry entry, shortcut, service path and uninstaller still says `C:\Program Files\Foo`, and the
junction transparently redirects it. Nothing in the registry is rewritten — that is what keeps this safe
and reversible. Undo is the same steps in reverse.

Junction constraints:
- Target must be a **local, fixed NTFS/ReFS volume** (not a USB stick that might be unplugged, not exFAT/FAT32, not a network share).
- If the target drive is missing at boot, the app breaks until the drive returns. The UI must say so plainly.

## What can and can't be moved

| Category | Verdict | How |
|---|---|---|
| Classic Win32 apps in Program Files / Program Files (x86) / custom dirs | **Movable** | Junction (needs admin) |
| Per-user apps in `%LOCALAPPDATA%\Programs` (VS Code, Discord, etc.) | **Movable** | Junction (no admin) |
| Large per-app data dirs in AppData (e.g. `AnthropicClaude`, model caches) | **Movable** | Junction, flagged Caution |
| Steam / Epic / GOG / Battle.net games | **Redirect** | Point the user to the launcher's own "move install folder" — it updates its own manifests. Detect via library folders. |
| Microsoft Store / MSIX apps (`WindowsApps`) | **Native only** | Use `Move-AppxPackage`-equivalent (`PackageManager.MovePackageAsync`) when the package allows it; otherwise not movable |
| Apps with kernel drivers, anti-cheat, VPN/AV/security suites, Office Click-to-Run, Visual Studio, .NET/VC++ runtimes, Windows components | **Blocked** | Denylist by publisher/path + "has driver/service registered with boot/system start" check |
| Anything under `C:\Windows`, `ProgramData\Microsoft` | **Blocked** | Already protected by DependencyAnalyzer rules |

## Pre-move checks (all must pass, each shown in the UI)

1. No running process has an executable or loaded module under the folder (reuse `GetRunningExecutablePaths`; extend to modules via Restart Manager API `RmGetList`, which also names the locking app).
2. Windows services whose `ImagePath` is inside the folder are stopped first and restarted after; drivers (`Type` 1/2) block the move.
3. Scheduled tasks pointing into the folder are listed (informational — junction keeps them working).
4. Target has free space ≥ folder size + 10%.
5. Target volume is fixed + NTFS/ReFS (`DriveInfo.DriveType == Fixed`, `DriveFormat`).
6. Folder isn't already a junction/symlink (already moved).
7. No nested junctions inside the folder that point elsewhere (copy them as links, don't follow).

## Safety & rollback

- **Journal**: every move is recorded in `%LOCALAPPDATA%\PurgeKit\relocations.json` (original path, target path, timestamp, state). State machine: `Copying → Verified → Swapped → Done`. On startup PurgeKit resumes or rolls back any entry not in `Done`.
- Original is only deleted after the junction is in place and a read-through check passes.
- ACLs: copy security descriptors (`robocopy /COPYALL` semantics or `FileSystemAclExtensions`) so Program Files permissions survive.
- **Move back** button for every journal entry. Also a "Repair" action that recreates a missing junction.
- Recycle-bin setting does not apply (it's a move, not a delete).

## UI: new "Relocate" nav item

- Reuses `InstalledProgramsScanner` (already has `LastUsed`, `InstallLocation`, `InstallDrive`, size).
- Default filter: on C:, not used in 90+ days, ≥ 200 MB, sorted by size. Slider for the days threshold.
- Each row: name, size, last used (+ source), verdict chip (Movable / Use launcher / Store / Blocked + reason).
- Target drive dropdown (only eligible drives, with free space).
- "Moved apps" section listing journal entries with **Move back** and **Open location**.
- Progress dialog with cancel (cancel = roll back).

## Implementation slices

1. **Core: `RelocationService`** — eligibility checks, copy+verify, junction create/remove (`DeviceIoControl FSCTL_SET_REPARSE_POINT`, or `cmd /c mklink /J` as a first cut), journal + crash recovery. Unit tests against a temp dir on a second volume.
2. **Core: classifier** — Movable / Launcher / Store / Blocked with reasons; launcher detection for Steam (`libraryfolders.vdf`), Epic (`Manifests\*.item`), GOG.
3. **UI: Relocate view + view model** — list, filter, drive picker, progress, moved-apps list.
4. **Store apps** — `PackageManager.MovePackageAsync` path, only if slices 1–3 land well.
5. **Docs** — README section, safety table.

## Open questions

- Default relocation root: `D:\PurgeKit\Relocated\...` mirroring the original path (proposed), or let the user pick?
- Should AppData data dirs (not just program binaries) be in scope for v1, or a later slice?
- Elevation: PurgeKit already elevates for some actions — relocate Program Files apps via a per-action elevated helper, or require running elevated?
