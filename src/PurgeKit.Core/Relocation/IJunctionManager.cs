namespace PurgeKit.Core.Relocation;

/// <summary>Creates, inspects and removes directory junctions.</summary>
public interface IJunctionManager
{
    /// <summary>Creates a junction at <paramref name="linkPath"/> that resolves to <paramref name="targetPath"/>.</summary>
    /// <exception cref="IOException">Something already exists at <paramref name="linkPath"/>.</exception>
    /// <exception cref="DirectoryNotFoundException"><paramref name="targetPath"/> does not exist.</exception>
    void Create(string linkPath, string targetPath);

    /// <summary>True when <paramref name="path"/> is a junction or directory symlink.</summary>
    bool IsLink(string path);

    /// <summary>The folder a link resolves to, or null when <paramref name="path"/> is not a link.</summary>
    string? GetTarget(string path);

    /// <summary>Removes the link only; the folder it points to is untouched.</summary>
    /// <exception cref="IOException"><paramref name="linkPath"/> is not a link.</exception>
    void Remove(string linkPath);
}
