namespace PurgeKit.UI.Services;

/// <summary>Opens things in Windows Explorer.</summary>
public interface IShellLauncher
{
    /// <summary>Opens <paramref name="path"/> in Explorer.</summary>
    void OpenFolder(string path);
}
