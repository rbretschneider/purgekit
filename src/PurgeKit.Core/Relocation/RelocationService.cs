using PurgeKit.Core.Relocation.Models;

namespace PurgeKit.Core.Relocation;

/// <summary>
/// Moves a folder by copy → verify → rename original aside → junction → delete original, journaling each
/// step first so any interruption can be finished or rolled back by <see cref="RelocationRecovery"/>.
/// </summary>
public sealed class RelocationService : IRelocationService
{
    private readonly RelocationCheckers _checkers;
    private readonly RelocationFileSystem _fileSystem;
    private readonly IRelocationJournal _journal;
    private readonly TimeProvider _time;
    private readonly RelocationSteps _steps;

    /// <summary>Creates the service.</summary>
    public RelocationService(
        RelocationCheckers checkers,
        RelocationFileSystem fileSystem,
        IRelocationJournal journal,
        TimeProvider time)
    {
        _checkers = checkers ?? throw new ArgumentNullException(nameof(checkers));
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _journal = journal ?? throw new ArgumentNullException(nameof(journal));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _steps = new RelocationSteps(fileSystem.Junctions, journal, time);
    }

    /// <inheritdoc />
    public Task<EligibilityReport> CheckEligibilityAsync(RelocationRequest request, CancellationToken ct = default) =>
        _checkers.Relocate.CheckAsync(request, ct);

    /// <inheritdoc />
    public async Task<RelocationEntry> RelocateAsync(
        RelocationRequest request,
        IProgress<RelocationProgress>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        progress?.Report(new RelocationProgress(RelocationStage.Checking, 0, 0));
        var report = await _checkers.Relocate.CheckAsync(request, ct).ConfigureAwait(false);
        if (!report.IsEligible)
        {
            throw new RelocationBlockedException(report);
        }

        var entry = await StartEntryAsync(report, ct).ConfigureAwait(false);
        entry = await CopyToTargetAsync(entry, progress, ct).ConfigureAwait(false);
        return await SwapInJunctionAsync(entry, progress).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<EligibilityReport> CheckRestoreAsync(Guid relocationId, CancellationToken ct = default)
    {
        var entry = await RequireCompletedAsync(relocationId, ct).ConfigureAwait(false);
        return await _checkers.Restore.CheckAsync(entry, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RestoreAsync(
        Guid relocationId,
        IProgress<RelocationProgress>? progress = null,
        CancellationToken ct = default)
    {
        progress?.Report(new RelocationProgress(RelocationStage.Checking, 0, 0));
        var entry = await RequireCompletedAsync(relocationId, ct).ConfigureAwait(false);
        var report = await _checkers.Restore.CheckAsync(entry, ct).ConfigureAwait(false);
        if (!report.IsEligible)
        {
            throw new RelocationBlockedException(report);
        }

        entry = await _steps.AdvanceAsync(entry, RelocationState.Restoring).ConfigureAwait(false);
        entry = await CopyBackAsync(entry, progress, ct).ConfigureAwait(false);
        await SwapOutJunctionAsync(entry, progress).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<RelocationEntry>> GetRelocationsAsync(CancellationToken ct = default) =>
        _journal.GetAllAsync(ct);

    private async Task<RelocationEntry> StartEntryAsync(EligibilityReport report, CancellationToken ct)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var entry = new RelocationEntry(
            Guid.NewGuid(), report.FromPath, report.ToPath, RelocationState.Copying, report.SizeBytes, now, now, report.DisplayName);
        await _journal.SaveAsync(entry, ct).ConfigureAwait(false);
        return entry;
    }

    private async Task<RelocationEntry> CopyToTargetAsync(
        RelocationEntry entry,
        IProgress<RelocationProgress>? progress,
        CancellationToken ct)
    {
        try
        {
            await CopyAndVerifyAsync(entry.SourcePath, entry.TargetPath, entry.SizeBytes, progress, ct).ConfigureAwait(false);
            // Last point at which cancelling is honoured; the swap that follows is short and must not stop halfway.
            ct.ThrowIfCancellationRequested();
            return await _steps.AdvanceAsync(entry, RelocationState.Verified).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await RollBackAsync(ex, () => _steps.RollBackRelocationAsync(entry)).ConfigureAwait(false);
            throw;
        }
    }

    private async Task<RelocationEntry> SwapInJunctionAsync(RelocationEntry entry, IProgress<RelocationProgress>? progress)
    {
        progress?.Report(new RelocationProgress(RelocationStage.Swapping, entry.SizeBytes, entry.SizeBytes));
        try
        {
            Directory.Move(entry.SourcePath, RelocationPaths.PendingPath(entry.SourcePath));
            _fileSystem.Junctions.Create(entry.SourcePath, entry.TargetPath);
            entry = await _steps.AdvanceAsync(entry, RelocationState.Swapped).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await RollBackAsync(ex, () => _steps.RollBackRelocationAsync(entry)).ConfigureAwait(false);
            throw;
        }

        progress?.Report(new RelocationProgress(RelocationStage.Cleaning, entry.SizeBytes, entry.SizeBytes));
        return await _steps.CompleteRelocationAsync(entry).ConfigureAwait(false);
    }

    private async Task<RelocationEntry> CopyBackAsync(
        RelocationEntry entry,
        IProgress<RelocationProgress>? progress,
        CancellationToken ct)
    {
        var restoring = RelocationPaths.RestoringPath(entry.SourcePath);
        try
        {
            await CopyAndVerifyAsync(entry.TargetPath, restoring, entry.SizeBytes, progress, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            return await _steps.AdvanceAsync(entry, RelocationState.RestoreVerified).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await RollBackAsync(ex, () => _steps.RollBackRestoreAsync(entry)).ConfigureAwait(false);
            throw;
        }
    }

    private async Task SwapOutJunctionAsync(RelocationEntry entry, IProgress<RelocationProgress>? progress)
    {
        progress?.Report(new RelocationProgress(RelocationStage.Swapping, entry.SizeBytes, entry.SizeBytes));
        try
        {
            _fileSystem.Junctions.Remove(entry.SourcePath);
            Directory.Move(RelocationPaths.RestoringPath(entry.SourcePath), entry.SourcePath);
        }
        catch (Exception ex)
        {
            await RollBackAsync(ex, () => _steps.RollBackRestoreAsync(entry)).ConfigureAwait(false);
            throw;
        }

        progress?.Report(new RelocationProgress(RelocationStage.Cleaning, entry.SizeBytes, entry.SizeBytes));
        await _steps.CompleteRestoreAsync(entry).ConfigureAwait(false);
    }

    private async Task CopyAndVerifyAsync(
        string from,
        string to,
        long sizeBytes,
        IProgress<RelocationProgress>? progress,
        CancellationToken ct)
    {
        var bytes = progress is null ? null : new CopyProgressAdapter(progress, sizeBytes);
        await _fileSystem.Copier.CopyAsync(from, to, bytes, ct).ConfigureAwait(false);

        progress?.Report(new RelocationProgress(RelocationStage.Verifying, sizeBytes, sizeBytes));
        var mismatches = await _fileSystem.Copier.FindMismatchesAsync(from, to, ct).ConfigureAwait(false);
        if (mismatches.Count > 0)
        {
            throw new RelocationVerificationException(from, to, mismatches);
        }
    }

    private async Task<RelocationEntry> RequireCompletedAsync(Guid relocationId, CancellationToken ct)
    {
        var entry = await _journal.FindAsync(relocationId, ct).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"No relocation with id {relocationId}.");
        if (entry.State != RelocationState.Completed)
        {
            throw new InvalidOperationException(
                $"The move of '{entry.SourcePath}' is {entry.State}. Restart PurgeKit to finish or undo it first.");
        }
        return entry;
    }

    // Runs a rollback after a failure. If the rollback itself fails, both errors surface together and the
    // journal entry stays put, so startup recovery can try again.
    private static async Task RollBackAsync(Exception cause, Func<Task> rollback)
    {
        try
        {
            await rollback().ConfigureAwait(false);
        }
        catch (Exception rollbackError)
        {
            throw new AggregateException("The move failed and could not be undone automatically.", cause, rollbackError);
        }
    }
}
