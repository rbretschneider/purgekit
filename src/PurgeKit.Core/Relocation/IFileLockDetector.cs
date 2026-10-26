namespace PurgeKit.Core.Relocation;

/// <summary>Finds running applications that hold files in a folder open.</summary>
public interface IFileLockDetector
{
    /// <summary>Display names of applications using files under <paramref name="directory"/>.</summary>
    IReadOnlyList<string> FindLockingProcesses(string directory);
}
