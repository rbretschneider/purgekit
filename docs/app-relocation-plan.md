# App Relocation ("Move to another drive") — Plan

Status: slice 1 (core `RelocationService`) implemented in `src/PurgeKit.Core/Relocation`; slices 2–5 not started.

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

1. **Core: `RelocationService`** — **done.** Eligibility checks, copy+verify, junction create/remove (`DeviceIoControl FSCTL_SET_REPARSE_POINT`), journal + crash recovery. Covered by `tests/PurgeKit.Core.Tests`, and checked once by hand with a real C: → D: move and restore. See "Slice 1 as built" below.
2. **Core: classifier** — Movable / Launcher / Store / Blocked with reasons; launcher detection for Steam (`libraryfolders.vdf`), Epic (`Manifests\*.item`), GOG.
3. **UI: Relocate view + view model** — list, filter, drive picker, progress, moved-apps list.
4. **Store apps** — `PackageManager.MovePackageAsync` path, only if slices 1–3 land well.
5. **Docs** — README section, safety table.

## Slice 1 as built

Entry point: `RelocationModule.CreateDefault()` gives `Service` (`IRelocationService`) and `Recovery`
(`IRelocationRecovery`). Call `Recovery.RecoverAsync()` once at startup before showing relocation actions.

- **Eligibility** is a set of independent `IEligibilityRule`s (protected path, already relocated, links inside,
  target volume type/file system, same volume / free space, target path collisions, open files via Restart
  Manager, registered services/drivers, elevation). Add a rule to add a check.
- **Relocate**: journal `Copying` → copy (ACLs, attributes, timestamps) → verify (file list, sizes, SHA-256 of
  .exe/.dll/.sys) → `Verified` → rename original to `*.purgekit-pending` → junction → `Swapped` → delete original →
  `Completed`. Cancellation is honoured until the swap; any failure before `Completed` rolls back.
- **Restore**: blocked while the relocated files are open, the junction was changed, or the original drive lacks
  space. Copy back to `*.purgekit-restoring` → verify → swap junction for the real folder → delete the relocated copy.
- **Recovery** finishes a `Swapped` move whose junction works and rolls back anything earlier; an interrupted
  restore is finished if the real folder is back, otherwise rolled back.
- **Safety invariant**: the relocated copy is only ever deleted while a real folder exists at the original path.

Decisions taken for the open questions (change them if Ryan prefers otherwise):

- Target layout: `<root>\<drive letter>\<original path>`. The caller picks `root`, and
  `RelocationPaths.DefaultTargetRoot("D:\")` gives `D:\PurgeKit\Relocated`.
- Scope: the service works on any folder. AppData folders are not special-cased; the UI decides what to offer.
- Elevation: no elevated helper. Folders under Program Files, ProgramData or Windows get a `RequiresElevation`
  blocker unless PurgeKit runs as administrator.

Known limits of slice 1:

- Folders containing junctions, symlinks or cloud placeholders are refused (`ContainsLinks`), not copied as links.
- Services and drivers are refused rather than stopped and restarted.
- The journal is safe within one process only. Two PurgeKit instances relocating at once are not guarded against.
