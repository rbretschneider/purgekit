namespace PurgeKit.Core.Tests.Relocation.Fakes;

/// <summary>Synchronous progress sink, so tests see reports in order and can react mid-operation.</summary>
internal sealed class CallbackProgress<T> : IProgress<T>
{
    private readonly Action<T> _onReport;

    public CallbackProgress(Action<T> onReport)
    {
        _onReport = onReport;
    }

    public List<T> Reports { get; } = [];

    public void Report(T value)
    {
        Reports.Add(value);
        _onReport(value);
    }
}
