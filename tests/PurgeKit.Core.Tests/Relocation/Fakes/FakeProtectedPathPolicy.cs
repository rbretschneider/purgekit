namespace PurgeKit.Core.Tests.Relocation.Fakes;

internal sealed class FakeProtectedPathPolicy : IProtectedPathPolicy
{
    private readonly List<string> _protected = [];
    private readonly List<string> _elevated = [];

    public void Protect(string path) => _protected.Add(path);

    public void RequireElevationFor(string path) => _elevated.Add(path);

    public bool IsProtected(string path) => _protected.Any(p => RelocationPaths.IsSameOrUnder(path, p));

    public bool RequiresElevation(string path) => _elevated.Any(p => RelocationPaths.IsSameOrUnder(path, p));
}
