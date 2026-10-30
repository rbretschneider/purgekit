using System.Text.Json;

namespace PurgeKit.Core.Relocation.Apps.Infrastructure;

/// <summary>Finds Epic Games installs from the launcher's <c>*.item</c> manifests.</summary>
public sealed class EpicLibraryLocator : ILauncherLibraryLocator
{
    private const string LauncherName = "Epic Games Launcher";
    private const string InstallLocationProperty = "InstallLocation";

    private readonly string _manifestsDirectory;

    /// <summary>Uses the launcher's default manifest folder under ProgramData.</summary>
    public EpicLibraryLocator()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Epic", "EpicGamesLauncher", "Data", "Manifests"))
    {
    }

    /// <summary>Uses manifests in <paramref name="manifestsDirectory"/>.</summary>
    public EpicLibraryLocator(string manifestsDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestsDirectory);
        _manifestsDirectory = manifestsDirectory;
    }

    /// <inheritdoc />
    public IReadOnlyList<LauncherLibrary> FindLibraries()
    {
        if (!Directory.Exists(_manifestsDirectory))
        {
            return [];
        }

        return Directory.EnumerateFiles(_manifestsDirectory, "*.item")
            .Select(ReadInstallLocation)
            .Where(location => !string.IsNullOrWhiteSpace(location))
            .Select(location => new LauncherLibrary(LauncherName, location!))
            .ToList();
    }

    private static string? ReadInstallLocation(string manifestPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        return document.RootElement.TryGetProperty(InstallLocationProperty, out var location)
            ? location.GetString()
            : null;
    }
}
