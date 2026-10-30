using PurgeKit.Core.Relocation.Apps.Infrastructure;

namespace PurgeKit.Core.Tests.Relocation.Apps;

public sealed class LauncherLocatorTests : IDisposable
{
    private readonly string _root = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), "purgekit-tests", Guid.NewGuid().ToString("N"))).FullName;

    public void Dispose() => DirectoryDeletion.DeleteTree(_root);

    [Fact]
    public void Steam_ReadsEveryLibraryFromLibraryFolders()
    {
        var steam = Directory.CreateDirectory(Path.Combine(_root, "Steam", "steamapps")).Parent!.FullName;
        File.WriteAllText(Path.Combine(steam, "steamapps", "libraryfolders.vdf"), """
            "libraryfolders"
            {
                "0" { "path"  "C:\\Program Files (x86)\\Steam" "label" "" }
                "1" { "path"  "E:\\SteamLibrary" "label" "" }
            }
            """);

        var roots = new SteamLibraryLocator(steam).FindLibraries().Select(l => l.RootPath);

        roots.Should().Contain([
            @"C:\Program Files (x86)\Steam\steamapps",
            @"E:\SteamLibrary\steamapps",
            Path.Combine(steam, "steamapps")]);
    }

    [Fact]
    public void Steam_NotInstalled_FindsNothing()
    {
        new SteamLibraryLocator(steamRoot: null).FindLibraries().Should().BeEmpty();
    }

    [Fact]
    public void Epic_ReadsInstallLocationFromManifests()
    {
        File.WriteAllText(Path.Combine(_root, "game.item"), """{ "DisplayName": "Fortnite", "InstallLocation": "C:\\Program Files\\Epic Games\\Fortnite" }""");

        var library = new EpicLibraryLocator(_root).FindLibraries().Should().ContainSingle().Subject;

        library.RootPath.Should().Be(@"C:\Program Files\Epic Games\Fortnite");
        library.LauncherName.Should().Contain("Epic");
    }
}
