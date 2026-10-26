using System.ComponentModel;
using System.Text;
using static PurgeKit.Core.Relocation.Infrastructure.RestartManagerNativeMethods;

namespace PurgeKit.Core.Relocation.Infrastructure;

/// <summary>
/// Uses the Windows Restart Manager to find apps holding a folder's executables and libraries open.
/// This is the same mechanism installers use to say "close these apps before continuing".
/// </summary>
public sealed class RestartManagerLockDetector : IFileLockDetector
{
    private static readonly HashSet<string> ModuleExtensions = new(StringComparer.OrdinalIgnoreCase) { ".exe", ".dll", ".sys" };

    private static readonly EnumerationOptions TreeWithoutLinks = new()
    {
        RecurseSubdirectories = true,
        AttributesToSkip = FileAttributes.ReparsePoint,
        IgnoreInaccessible = true
    };

    /// <inheritdoc />
    public IReadOnlyList<string> FindLockingProcesses(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        var modules = Directory.EnumerateFiles(directory, "*", TreeWithoutLinks)
            .Where(file => ModuleExtensions.Contains(Path.GetExtension(file)))
            .ToArray();
        if (modules.Length == 0)
        {
            return [];
        }

        ThrowIfFailed(RmStartSession(out var session, 0, new StringBuilder(CchRmSessionKey + 1)), "start a Restart Manager session");
        try
        {
            ThrowIfFailed(RmRegisterResources(session, (uint)modules.Length, modules, 0, null, 0, null), "register files");
            return ListApps(session);
        }
        finally
        {
            RmEndSession(session);
        }
    }

    private static IReadOnlyList<string> ListApps(uint session)
    {
        RmProcessInfo[]? infos = null;
        uint count = 0;
        uint rebootReasons = 0;
        int result;
        // The list can grow between calls, so keep asking until the buffer is big enough.
        while ((result = RmGetList(session, out var needed, ref count, infos, ref rebootReasons)) == ErrorMoreData)
        {
            infos = new RmProcessInfo[needed];
            count = needed;
        }
        ThrowIfFailed(result, "list apps using the folder");

        return infos is null
            ? []
            : infos.Take((int)count).Select(info => info.AppName).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void ThrowIfFailed(int result, string action)
    {
        if (result != ErrorSuccess)
        {
            throw new Win32Exception(result, $"Restart Manager could not {action}.");
        }
    }
}
