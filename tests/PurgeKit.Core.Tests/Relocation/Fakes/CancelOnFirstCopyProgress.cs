namespace PurgeKit.Core.Tests.Relocation.Fakes;

/// <summary>Cancels the operation as soon as the first bytes are copied.</summary>
internal sealed class CancelOnFirstCopyProgress : IProgress<RelocationProgress>
{
    private readonly CancellationTokenSource _cts;

    public CancelOnFirstCopyProgress(CancellationTokenSource cts)
    {
        _cts = cts;
    }

    public void Report(RelocationProgress value)
    {
        if (value.Stage == RelocationStage.Copying)
        {
            _cts.Cancel();
        }
    }
}
