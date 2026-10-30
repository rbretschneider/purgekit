namespace PurgeKit.Core.Relocation.Apps;

/// <summary>A folder whose contents a game launcher manages.</summary>
/// <param name="LauncherName">Launcher shown to the user, e.g. Steam.</param>
/// <param name="RootPath">Folder the launcher owns; anything under it must be moved by the launcher.</param>
public sealed record LauncherLibrary(string LauncherName, string RootPath);
