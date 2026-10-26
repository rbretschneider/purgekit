using PurgeKit.Core.Relocation.Models;

namespace PurgeKit.Core.Relocation;

/// <summary>
/// The finishing and rollback steps shared by the live service and startup recovery. One invariant is
/// enforced here: the relocated copy is only ever deleted while a real folder sits at the original path.
/// </summary>
internal sealed class RelocationSteps
{
    private readonly IJunctionManager _junctions;
    private readonly IRelocationJournal _journal;
    private readonly TimeProvider _time;

    public RelocationSteps(IJunctionManager junctions, IRelocationJournal journal, TimeProvider time)
    {
        _junctions = junctions;
        _journal = journal;
        _time = time;
    }

    // State changes are never cancellable: the journal must always describe what is on disk.
    public async Task<RelocationEntry> AdvanceAsync(RelocationEntry entry, RelocationState state)
    {
        var next = entry with { State = state, UpdatedUtc = _time.GetUtcNow().UtcDateTime };
        await _journal.SaveAsync(next, CancellationToken.None).ConfigureAwait(false);
        return next;
    }

    public bool IsJunctionToTarget(RelocationEntry entry) =>
        RelocationPaths.AreSame(_junctions.GetTarget(entry.SourcePath), entry.TargetPath);

    public bool IsRealDirectory(string path) => Directory.Exists(path) && !_junctions.IsLink(path);

    public async Task<RelocationEntry> CompleteRelocationAsync(RelocationEntry entry)
    {
        DeleteSuperseded(RelocationPaths.PendingPath(entry.SourcePath));
        return await AdvanceAsync(entry, RelocationState.Completed).ConfigureAwait(false);
    }

    public async Task RollBackRelocationAsync(RelocationEntry entry)
    {
        PutOriginalBack(entry.SourcePath);
        RequireRealDirectory(entry);
        DirectoryDeletion.DeleteTree(entry.TargetPath);
        await _journal.RemoveAsync(entry.Id, CancellationToken.None).ConfigureAwait(false);
    }

    public async Task CompleteRestoreAsync(RelocationEntry entry)
    {
        RequireRealDirectory(entry);
        DeleteSuperseded(entry.TargetPath);
        await _journal.RemoveAsync(entry.Id, CancellationToken.None).ConfigureAwait(false);
    }

    public async Task<RelocationEntry> RollBackRestoreAsync(RelocationEntry entry)
    {
        if (!Path.Exists(entry.SourcePath))
        {
            _junctions.Create(entry.SourcePath, entry.TargetPath);
        }
        DirectoryDeletion.DeleteTree(RelocationPaths.RestoringPath(entry.SourcePath));
        return await AdvanceAsync(entry, RelocationState.Completed).ConfigureAwait(false);
    }

    private void PutOriginalBack(string sourcePath)
    {
        var pending = RelocationPaths.PendingPath(sourcePath);
        if (!Directory.Exists(pending))
        {
            return;
        }
        if (_junctions.IsLink(sourcePath))
        {
            _junctions.Remove(sourcePath);
        }
        if (!Path.Exists(sourcePath))
        {
            Directory.Move(pending, sourcePath);
        }
    }

    private void RequireRealDirectory(RelocationEntry entry)
    {
        if (!IsRealDirectory(entry.SourcePath))
        {
            throw new InvalidOperationException(
                $"Refusing to delete '{entry.TargetPath}': no real folder exists at '{entry.SourcePath}', "
                + "so it may hold the only copy.");
        }
    }

    private static void DeleteSuperseded(string path)
    {
        try
        {
            DirectoryDeletion.DeleteTree(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new IOException(
                $"The move finished, but the old copy at '{path}' could not be fully removed. "
                + "PurgeKit will retry on its next start.", ex);
        }
    }
}
