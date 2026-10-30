namespace PurgeKit.Core.Services;

/// <summary>
/// Picks which release download fits how PurgeKit is running: installed copies update through the
/// installer, the portable exe replaces itself.
/// </summary>
public static class ReleaseAssetSelector
{
    /// <summary>File name of the portable single-file build.</summary>
    public const string PortableExeName = "PurgeKit.UI.exe";

    /// <summary>File name prefix of the installer, followed by the version.</summary>
    public const string InstallerPrefix = "PurgeKit-Setup-";

    // Inno Setup always places its uninstaller next to the installed app.
    private const string InnoUninstallerName = "unins000.exe";

    /// <summary>True when the app in <paramref name="appDirectory"/> was installed by the PurgeKit installer.</summary>
    public static bool IsInstalledCopy(string appDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appDirectory);
        return File.Exists(Path.Combine(appDirectory, InnoUninstallerName));
    }

    /// <summary>The asset name to download, or null when the release has no matching asset.</summary>
    public static string? Select(IEnumerable<string> assetNames, bool isInstalledCopy)
    {
        ArgumentNullException.ThrowIfNull(assetNames);
        return isInstalledCopy
            ? assetNames.FirstOrDefault(IsInstaller)
            : assetNames.FirstOrDefault(name => string.Equals(name, PortableExeName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>True when <paramref name="assetName"/> is the installer.</summary>
    public static bool IsInstaller(string assetName) =>
        assetName.StartsWith(InstallerPrefix, StringComparison.OrdinalIgnoreCase)
        && assetName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
}
