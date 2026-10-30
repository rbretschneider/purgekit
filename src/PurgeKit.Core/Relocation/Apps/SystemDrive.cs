namespace PurgeKit.Core.Relocation.Apps;

/// <summary>The drive Windows is installed on.</summary>
public static class SystemDrive
{
    /// <summary>Root of the system drive, e.g. <c>C:\</c>.</summary>
    public static string Root { get; } = Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\";

    /// <summary>True when <paramref name="path"/> is on the system drive.</summary>
    public static bool Contains(string path) => RelocationPaths.IsSameOrUnder(path, Root);
}
