using Microsoft.Win32;

namespace PurgeKit.Core.Relocation.Apps.Infrastructure;

/// <summary>Finds GOG Galaxy installs from the registry.</summary>
public sealed class GogLibraryLocator : ILauncherLibraryLocator
{
    private const string LauncherName = "GOG Galaxy";
    private const string GamesKeyPath = @"SOFTWARE\WOW6432Node\GOG.com\Games";
    private const string PathValue = "path";

    /// <inheritdoc />
    public IReadOnlyList<LauncherLibrary> FindLibraries()
    {
        using var games = Registry.LocalMachine.OpenSubKey(GamesKeyPath);
        if (games is null)
        {
            return [];
        }

        return games.GetSubKeyNames()
            .Select(id => ReadPath(games, id))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => new LauncherLibrary(LauncherName, path!))
            .ToList();
    }

    private static string? ReadPath(RegistryKey games, string id)
    {
        using var game = games.OpenSubKey(id);
        return game?.GetValue(PathValue) as string;
    }
}
