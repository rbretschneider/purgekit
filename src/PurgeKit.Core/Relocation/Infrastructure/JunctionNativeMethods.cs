using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace PurgeKit.Core.Relocation.Infrastructure;

/// <summary>Win32 declarations for writing mount-point (junction) reparse data.</summary>
internal static class JunctionNativeMethods
{
    internal const uint FsctlSetReparsePoint = 0x000900A4;
    internal const uint IoReparseTagMountPoint = 0xA0000003;
    internal const uint GenericWrite = 0x40000000;
    internal const uint OpenExisting = 3;
    internal const uint FileFlagBackupSemantics = 0x02000000;
    internal const uint FileFlagOpenReparsePoint = 0x00200000;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern SafeFileHandle CreateFileW(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DeviceIoControl(
        SafeFileHandle hDevice,
        uint dwIoControlCode,
        byte[] lpInBuffer,
        int nInBufferSize,
        IntPtr lpOutBuffer,
        int nOutBufferSize,
        out int lpBytesReturned,
        IntPtr lpOverlapped);
}
