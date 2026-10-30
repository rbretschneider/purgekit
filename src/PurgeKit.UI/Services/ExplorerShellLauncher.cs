using System.Diagnostics;
using System.IO;

namespace PurgeKit.UI.Services;

/// <summary>Opens folders with Explorer.</summary>
public sealed class ExplorerShellLauncher : IShellLauncher
{
    /// <inheritdoc />
    public void OpenFolder(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"'{path}' no longer exists.");
        }
        using var _ = Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
    }
}
