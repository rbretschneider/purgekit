using PurgeKit.Core.Relocation.Models;

namespace PurgeKit.Core.Relocation.Apps;

/// <summary>Moves apps to another drive and back, choosing the right mechanism for each kind of app.</summary>
public interface IAppMover
{
    /// <summary>Reports whether <paramref name="candidate"/> can be moved to <paramref name="drive"/> right now.</summary>
    /// <exception cref="InvalidOperationException">The candidate is not movable by PurgeKit.</exception>
    Task<EligibilityReport> CheckMoveAsync(AppCandidate candidate, TargetDrive drive, CancellationToken ct = default);

    /// <summary>Moves <paramref name="candidate"/> to <paramref name="drive"/>.</summary>
    /// <exception cref="RelocationBlockedException">Checks failed.</exception>
    /// <exception cref="StorePackageException">Windows refused to move a Store app.</exception>
    Task MoveAsync(AppCandidate candidate, TargetDrive drive, IProgress<RelocationProgress>? progress = null, CancellationToken ct = default);

    /// <summary>
    /// Apps that currently live off the system drive, including interrupted moves. If Store apps cannot be
    /// listed, PurgeKit's own moves are still returned and the result carries a warning.
    /// </summary>
    Task<ListResult<MovedApp>> GetMovedAppsAsync(CancellationToken ct = default);

    /// <summary>Reports whether <paramref name="app"/> can be moved back right now.</summary>
    Task<EligibilityReport> CheckMoveBackAsync(MovedApp app, CancellationToken ct = default);

    /// <summary>Moves <paramref name="app"/> back to where it came from.</summary>
    Task MoveBackAsync(MovedApp app, IProgress<RelocationProgress>? progress = null, CancellationToken ct = default);
}
