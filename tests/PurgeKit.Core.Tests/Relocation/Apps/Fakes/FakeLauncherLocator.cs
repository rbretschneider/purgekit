using PurgeKit.Core.Relocation.Apps;

namespace PurgeKit.Core.Tests.Relocation.Apps.Fakes;

internal sealed class FakeLauncherLocator : ILauncherLibraryLocator
{
    private readonly LauncherLibrary[] _libraries;

    public FakeLauncherLocator(params LauncherLibrary[] libraries)
    {
        _libraries = libraries;
    }

    public IReadOnlyList<LauncherLibrary> FindLibraries() => _libraries;
}
