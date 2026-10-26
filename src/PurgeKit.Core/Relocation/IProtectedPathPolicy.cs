namespace PurgeKit.Core.Relocation;

/// <summary>Decides which folders must never be relocated and which need elevation.</summary>
public interface IProtectedPathPolicy
{
    /// <summary>True when moving <paramref name="path"/> could break Windows or shared components.</summary>
    bool IsProtected(string path);

    /// <summary>True when changing <paramref name="path"/> normally requires administrator rights.</summary>
    bool RequiresElevation(string path);
}
