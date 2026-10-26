namespace PurgeKit.Core.Tests.Relocation.Fakes;

internal sealed class FakeFileLockDetector : IFileLockDetector
{
    private readonly List<(string Path, string App)> _locks = [];

    public void Lock(string path, string app) => _locks.Add((path, app));

    public IReadOnlyList<string> FindLockingProcesses(string directory) =>
        _locks.Where(l => RelocationPaths.IsSameOrUnder(l.Path, directory)).Select(l => l.App).ToList();
}
