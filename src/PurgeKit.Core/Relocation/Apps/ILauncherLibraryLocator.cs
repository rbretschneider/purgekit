namespace PurgeKit.Core.Relocation.Apps;

/// <summary>Finds the folders a game launcher manages.</summary>
public interface ILauncherLibraryLocator
{
    /// <summary>Library folders; empty when the launcher is not installed.</summary>
    IReadOnlyList<LauncherLibrary> FindLibraries();
}
