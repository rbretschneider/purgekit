using PurgeKit.Core.Relocation.Infrastructure;

namespace PurgeKit.Core.Tests.Relocation;

public sealed class PathRulesTests
{
    private static readonly string ProgramFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
    private static readonly string LocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    private readonly WindowsProtectedPathPolicy _policy = new();

    [Fact]
    public void MapTarget_MirrorsDriveLetterAndPathUnderTargetRoot()
    {
        RelocationPaths.MapTarget(@"C:\Program Files\Foo", @"D:\PurgeKit\Relocated")
            .Should().Be(@"D:\PurgeKit\Relocated\C\Program Files\Foo");
    }

    [Fact]
    public void MapTarget_RejectsNetworkPaths()
    {
        var act = () => RelocationPaths.MapTarget(@"\\server\share\Foo", @"D:\PurgeKit\Relocated");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Request_RejectsRelativePaths()
    {
        var act = () => new RelocationRequest(@"Program Files\Foo", @"D:\PurgeKit\Relocated");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void IsSameOrUnder_DoesNotMatchSiblingWithSharedPrefix()
    {
        RelocationPaths.IsSameOrUnder(@"C:\AppData", @"C:\App").Should().BeFalse();
        RelocationPaths.IsSameOrUnder(@"C:\App\bin", @"C:\App").Should().BeTrue();
    }

    [Fact]
    public void IsSameOrUnder_TreatsEverythingOnADriveAsUnderItsRoot()
    {
        RelocationPaths.IsSameOrUnder(@"C:\Program Files\App", @"C:\").Should().BeTrue();
        RelocationPaths.IsSameOrUnder(@"D:\Games", @"C:\").Should().BeFalse();
    }

    [Fact]
    public void RequiredFreeBytes_AddsTenPercentHeadroom()
    {
        RelocationCapacity.RequiredFreeBytes(1000).Should().Be(1100);
    }

    [Theory]
    [InlineData(@"C:\")]
    [InlineData(@"C:\Windows\System32")]
    public void Policy_ProtectsWindowsAndDriveRoots(string path)
    {
        _policy.IsProtected(path).Should().BeTrue();
    }

    [Fact]
    public void Policy_ProtectsProgramFilesRootAndSharedComponents()
    {
        _policy.IsProtected(ProgramFiles).Should().BeTrue();
        _policy.IsProtected(Path.Combine(ProgramFiles, "Common Files", "Foo")).Should().BeTrue();
    }

    [Fact]
    public void Policy_AllowsAppsInProgramFilesWithElevation()
    {
        var app = Path.Combine(ProgramFiles, "SomeApp");

        _policy.IsProtected(app).Should().BeFalse();
        _policy.RequiresElevation(app).Should().BeTrue();
    }

    [Fact]
    public void Policy_AllowsPerUserAppsWithoutElevation()
    {
        var app = Path.Combine(LocalAppData, "Programs", "SomeApp");

        _policy.IsProtected(app).Should().BeFalse();
        _policy.RequiresElevation(app).Should().BeFalse();
    }

    [Fact]
    public void Policy_ProtectsFoldersContainingPurgeKitItself()
    {
        var ownFolder = Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);

        _policy.IsProtected(Path.GetDirectoryName(ownFolder)!).Should().BeTrue();
    }
}
