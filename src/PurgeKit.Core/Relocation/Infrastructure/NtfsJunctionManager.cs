using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using static PurgeKit.Core.Relocation.Infrastructure.JunctionNativeMethods;

namespace PurgeKit.Core.Relocation.Infrastructure;

/// <summary>
/// NTFS directory junctions. Unlike directory symlinks, junctions need no special privilege and are
/// followed by every Windows API, which is what keeps relocated apps working at their old path.
/// </summary>
public sealed class NtfsJunctionManager : IJunctionManager
{
    private const string NtPathPrefix = @"\??\";
    private const int HeaderFieldsBytes = 8;
    private const int NullTerminatorBytes = 2;

    /// <inheritdoc />
    public void Create(string linkPath, string targetPath)
    {
        var link = RelocationPaths.NormalizeDirectory(linkPath, nameof(linkPath));
        var target = RelocationPaths.NormalizeDirectory(targetPath, nameof(targetPath));
        if (!Directory.Exists(target))
        {
            throw new DirectoryNotFoundException($"Junction target '{target}' does not exist.");
        }
        if (Path.Exists(link))
        {
            throw new IOException($"Cannot create a junction at '{link}': something already exists there.");
        }

        Directory.CreateDirectory(link);
        try
        {
            WriteMountPoint(link, target);
        }
        catch
        {
            Directory.Delete(link);
            throw;
        }
    }

    /// <inheritdoc />
    public bool IsLink(string path) => GetTarget(path) is not null;

    /// <inheritdoc />
    public string? GetTarget(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var info = new DirectoryInfo(path);
        if (!info.Exists || !info.Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            return null;
        }

        var target = info.LinkTarget;
        return target is not null && target.StartsWith(NtPathPrefix, StringComparison.Ordinal)
            ? target[NtPathPrefix.Length..]
            : target;
    }

    /// <inheritdoc />
    public void Remove(string linkPath)
    {
        if (!IsLink(linkPath))
        {
            throw new IOException($"'{linkPath}' is not a junction; refusing to remove it.");
        }
        // Non-recursive delete removes only the reparse point, never the target's contents.
        Directory.Delete(linkPath, recursive: false);
    }

    private static void WriteMountPoint(string link, string target)
    {
        using var handle = CreateFileW(link, GenericWrite, 0, IntPtr.Zero, OpenExisting,
            FileFlagBackupSemantics | FileFlagOpenReparsePoint, IntPtr.Zero);
        if (handle.IsInvalid)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not open '{link}' to create a junction.");
        }

        var buffer = BuildMountPointBuffer(target);
        if (!DeviceIoControl(handle, FsctlSetReparsePoint, buffer, buffer.Length, IntPtr.Zero, 0, out _, IntPtr.Zero))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not point '{link}' at '{target}'.");
        }
    }

    // REPARSE_DATA_BUFFER for IO_REPARSE_TAG_MOUNT_POINT: tag, data length, reserved, then the
    // substitute ("\??\D:\...") and print ("D:\...") names, each null-terminated.
    private static byte[] BuildMountPointBuffer(string target)
    {
        var substitute = Encoding.Unicode.GetBytes(NtPathPrefix + target);
        var print = Encoding.Unicode.GetBytes(target);
        var pathBufferBytes = substitute.Length + NullTerminatorBytes + print.Length + NullTerminatorBytes;

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(IoReparseTagMountPoint);
        writer.Write((ushort)(HeaderFieldsBytes + pathBufferBytes));
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write((ushort)substitute.Length);
        writer.Write((ushort)(substitute.Length + NullTerminatorBytes));
        writer.Write((ushort)print.Length);
        writer.Write(substitute);
        writer.Write((ushort)0);
        writer.Write(print);
        writer.Write((ushort)0);
        writer.Flush();
        return stream.ToArray();
    }
}
