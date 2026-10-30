using PurgeKit.Core.Relocation.Apps;
using PurgeKit.Core.Tests.Relocation.Apps.Fakes;

namespace PurgeKit.Core.Tests.Relocation.Apps;

public sealed class PowerShellStorePackageCatalogTests
{
    private const string Spotify = "SpotifyAB.SpotifyMusic_1.247.414.0_x64__zpdnekdrzrea0";

    private readonly FakePowerShellRunner _powerShell = new();
    private readonly PowerShellStorePackageCatalog _catalog;

    public PowerShellStorePackageCatalogTests()
    {
        _catalog = new PowerShellStorePackageCatalog(_powerShell);
    }

    [Fact]
    public async Task List_ReadsArrayOutput()
    {
        _powerShell.Output = $$"""[{"FullName":"{{Spotify}}","Name":"SpotifyAB.SpotifyMusic","InstallLocation":"C:\\Program Files\\WindowsApps\\{{Spotify}}"},{"FullName":"A_1_x64__b","Name":"A","InstallLocation":"D:\\WindowsApps\\A"}]""";

        var packages = await _catalog.ListPackagesAsync();

        packages.Should().HaveCount(2);
        packages[0].Should().Be(new StorePackage(Spotify, "SpotifyAB.SpotifyMusic", $@"C:\Program Files\WindowsApps\{Spotify}"));
    }

    [Fact]
    public async Task List_ReadsSingleObjectOutput()
    {
        _powerShell.Output = """{"FullName":"A_1_x64__b","Name":"A","InstallLocation":"C:\\A"}""";

        (await _catalog.ListPackagesAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task List_WithNoStoreApps_IsEmpty()
    {
        _powerShell.Output = "";

        (await _catalog.ListPackagesAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task List_WithUnreadableOutput_ThrowsStorePackageException()
    {
        _powerShell.Output = "Get-AppxPackage : The term is not recognized";

        var act = () => _catalog.ListPackagesAsync();

        await act.Should().ThrowAsync<StorePackageException>();
    }

    [Fact]
    public async Task Move_ToOtherDrive_RegistersThatDrivesAppVolume()
    {
        await _catalog.MovePackageAsync(Spotify, @"d:\");

        var script = _powerShell.Scripts.Should().ContainSingle().Subject;
        script.Should().Contain(@"Add-AppxVolume -Path 'D:\WindowsApps'");
        script.Should().Contain($"Move-AppxPackage -Package '{Spotify}'");
    }

    [Fact]
    public async Task Move_ToSystemDrive_UsesTheSystemAppVolume()
    {
        await _catalog.MovePackageAsync(Spotify, SystemDrive.Root);

        _powerShell.Scripts.Should().ContainSingle().Which.Should().Contain("IsSystemVolume").And.NotContain("Add-AppxVolume");
    }

    [Theory]
    [InlineData("Evil'; Remove-Item C:\\ -Recurse; '")]
    [InlineData("A B")]
    public async Task Move_RejectsPackageNamesThatCouldInjectScript(string packageName)
    {
        var act = () => _catalog.MovePackageAsync(packageName, @"D:\");

        await act.Should().ThrowAsync<ArgumentException>();
        _powerShell.Scripts.Should().BeEmpty();
    }

    [Fact]
    public async Task Move_RejectsTargetsThatAreNotDriveRoots()
    {
        var act = () => _catalog.MovePackageAsync(Spotify, @"D:\Games'; whoami; '");

        await act.Should().ThrowAsync<ArgumentException>();
        _powerShell.Scripts.Should().BeEmpty();
    }
}
