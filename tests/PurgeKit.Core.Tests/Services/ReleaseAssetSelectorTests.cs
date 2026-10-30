using PurgeKit.Core.Services;

namespace PurgeKit.Core.Tests.Services;

public sealed class ReleaseAssetSelectorTests
{
    private static readonly string[] ReleaseAssets = ["PurgeKit-Setup-0.1.42.exe", "PurgeKit.UI.exe"];

    [Fact]
    public void InstalledCopy_UpdatesThroughInstaller()
    {
        ReleaseAssetSelector.Select(ReleaseAssets, isInstalledCopy: true).Should().Be("PurgeKit-Setup-0.1.42.exe");
    }

    [Fact]
    public void PortableCopy_UpdatesWithPortableExe()
    {
        ReleaseAssetSelector.Select(ReleaseAssets.Reverse(), isInstalledCopy: false).Should().Be("PurgeKit.UI.exe");
    }

    [Fact]
    public void OlderReleaseWithoutInstaller_HasNothingForInstalledCopy()
    {
        ReleaseAssetSelector.Select(["PurgeKit.UI.exe"], isInstalledCopy: true).Should().BeNull();
    }

    [Fact]
    public void InstalledCopy_IsRecognisedByInnoUninstallerNextToIt()
    {
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "purgekit-tests", Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            ReleaseAssetSelector.IsInstalledCopy(dir).Should().BeFalse();
            File.WriteAllText(Path.Combine(dir, "unins000.exe"), "");
            ReleaseAssetSelector.IsInstalledCopy(dir).Should().BeTrue();
        }
        finally
        {
            DirectoryDeletion.DeleteTree(dir);
        }
    }
}
