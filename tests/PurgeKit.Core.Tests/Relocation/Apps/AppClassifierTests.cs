using PurgeKit.Core.Relocation.Apps;
using PurgeKit.Core.Tests.Relocation.Apps.Fakes;
using PurgeKit.Core.Tests.Relocation.Fakes;

namespace PurgeKit.Core.Tests.Relocation.Apps;

public sealed class AppClassifierTests
{
    private const string SteamCommon = @"C:\Program Files (x86)\Steam\steamapps";
    private const string GamePath = SteamCommon + @"\common\Portal 2";

    private readonly FakeProtectedPathPolicy _policy = new();

    [Fact]
    public void OrdinaryApp_IsMovable()
    {
        Classify("Blender", "Blender Foundation", @"C:\Program Files\Blender").Option.Should().Be(AppMoveOption.Movable);
    }

    [Theory]
    [InlineData("Microsoft Visual C++ 2015-2022 Redistributable (x64)", "Microsoft Corporation")]
    [InlineData("NVIDIA Graphics Driver 551.86", "NVIDIA Corporation")]
    [InlineData("EasyAntiCheat", "Epic Games")]
    [InlineData("ProtonVPN", "Proton AG")]
    [InlineData("Microsoft Visual Studio Installer", "Microsoft Corporation")]
    public void SharedRuntimesDriversAndSecuritySoftware_AreBlocked(string name, string publisher)
    {
        Classify(name, publisher, @"C:\Program Files\Something").Option.Should().Be(AppMoveOption.Blocked);
    }

    [Theory]
    [InlineData("Preset Manager")]
    [InlineData("DriverPack Notifier Theme")]
    [InlineData("Microsoft Visual Studio Code (User)")]
    public void DenylistMatchesWholeWordsOnly(string name)
    {
        Classify(name, "Indie Dev", @"C:\Program Files\Tool").Option.Should().Be(AppMoveOption.Movable);
    }

    [Fact]
    public void GameInsideLauncherLibrary_MustBeMovedByLauncher()
    {
        var verdict = Classify("Portal 2", "Valve", GamePath);

        verdict.Option.Should().Be(AppMoveOption.UseLauncher);
        verdict.Reason.Should().Contain("Steam");
    }

    [Fact]
    public void ProtectedInstallFolder_IsBlocked()
    {
        _policy.Protect(@"C:\Program Files");

        Classify("Odd App", "Odd", @"C:\Program Files").Option.Should().Be(AppMoveOption.Blocked);
    }

    [Fact]
    public void FirstApplicableRuleWins()
    {
        // A launcher-managed game whose name trips the denylist is still sent to the launcher.
        Classify("Anti-Cheat Sample Game", "Valve", GamePath).Option.Should().Be(AppMoveOption.UseLauncher);
    }

    private ClassificationVerdict Classify(string name, string publisher, string path)
    {
        var classifier = new AppClassifier(
        [
            new ProtectedLocationRule(_policy),
            new LauncherManagedRule([new FakeLauncherLocator(new LauncherLibrary("Steam", SteamCommon))]),
            new DenylistRule()
        ]);
        return classifier.Classify(new InstalledApp(name, publisher, path, 0, null, null), path);
    }
}
