using PurgeKit.Core.Relocation.Apps;

namespace PurgeKit.Core.Tests.Relocation.Apps.Fakes;

internal sealed class FakePowerShellRunner : IPowerShellRunner
{
    public string Output { get; set; } = "";

    public Exception? Failure { get; set; }

    public List<string> Scripts { get; } = [];

    public Task<string> RunAsync(string script, CancellationToken ct = default)
    {
        Scripts.Add(script);
        return Failure is null ? Task.FromResult(Output) : Task.FromException<string>(Failure);
    }
}
