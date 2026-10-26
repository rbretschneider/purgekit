using PurgeKit.Core.Relocation.Models;

namespace PurgeKit.Core.Relocation;

/// <summary>
/// Turns byte counts from the copier into <see cref="RelocationProgress"/> reports, synchronously so
/// reports reach the caller's <see cref="IProgress{T}"/> in order.
/// </summary>
internal sealed class CopyProgressAdapter : IProgress<long>
{
    private readonly IProgress<RelocationProgress> _target;
    private readonly long _totalBytes;

    public CopyProgressAdapter(IProgress<RelocationProgress> target, long totalBytes)
    {
        _target = target;
        _totalBytes = totalBytes;
    }

    public void Report(long value) =>
        _target.Report(new RelocationProgress(RelocationStage.Copying, value, _totalBytes));
}
