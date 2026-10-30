using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace PurgeKit.Core.Relocation.Apps.Infrastructure;

/// <summary>Finds Steam library folders from <c>steamapps\libraryfolders.vdf</c>.</summary>
public sealed partial class SteamLibraryLocator : ILauncherLibraryLocator
{
    private const string LauncherName = "Steam";
    private const string SteamKeyPath = @"Software\Valve\Steam";
    private const string SteamPathValue = "SteamPath";

    private readonly string? _steamRoot;

    /// <summary>Uses the Steam install recorded in the registry.</summary>
    public SteamLibraryLocator()
        : this(ReadSteamRootFromRegistry())
    {
    }

    /// <summary>Uses the Steam install at <paramref name="steamRoot"/>; null when Steam is not installed.</summary>
    public SteamLibraryLocator(string? steamRoot)
    {
        _steamRoot = steamRoot;
    }

    /// <inheritdoc />
    public IReadOnlyList<LauncherLibrary> FindLibraries()
    {
        if (string.IsNullOrEmpty(_steamRoot) || !Directory.Exists(_steamRoot))
        {
            return [];
        }

        var vdf = Path.Combine(_steamRoot, "steamapps", "libraryfolders.vdf");
        var roots = File.Exists(vdf) ? ParseLibraryRoots(File.ReadAllText(vdf)) : [];
        return roots.Append(_steamRoot)
            .Select(root => Path.GetFullPath(Path.Combine(root, "steamapps")))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(steamapps => new LauncherLibrary(LauncherName, steamapps))
            .ToList();
    }

    private static IEnumerable<string> ParseLibraryRoots(string vdf) =>
        LibraryPathPattern().Matches(vdf).Select(match => match.Groups["path"].Value.Replace(@"\\", @"\"));

    private static string? ReadSteamRootFromRegistry()
    {
        using var key = Registry.CurrentUser.OpenSubKey(SteamKeyPath);
        return (key?.GetValue(SteamPathValue) as string)?.Replace('/', Path.DirectorySeparatorChar);
    }

    [GeneratedRegex("\"path\"\\s+\"(?<path>[^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex LibraryPathPattern();
}
