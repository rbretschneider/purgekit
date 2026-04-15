using System.Runtime.InteropServices;
using PurgeKit.Core.Models;
using PurgeKit.Core.Services;

namespace PurgeKit.Core.Cleanup;

public class CleanupEngine
{
    private readonly AppSettings _settings;

    public CleanupEngine(AppSettings settings)
    {
        _settings = settings;
    }

    public record CleanupResult(int DeletedCount, int SkippedCount, long BytesFreed);

    public async Task<CleanupResult> DeleteFilesAsync(
        IEnumerable<string> paths,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        int deleted = 0, skipped = 0;
        long bytesFreed = 0;

        await Task.Run(() =>
        {
            foreach (var path in paths)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    if (File.Exists(path))
                    {
                        var size = new FileInfo(path).Length;
                        progress?.Report($"Deleting {Path.GetFileName(path)}");

                        if (_settings.UseRecycleBin)
                            SendToRecycleBin(path);
                        else
                            File.Delete(path);

                        bytesFreed += size;
                        deleted++;
                    }
                    else if (Directory.Exists(path))
                    {
                        var size = GetDirectorySize(path);
                        progress?.Report($"Deleting {Path.GetFileName(path)}");

                        if (_settings.UseRecycleBin)
                            SendToRecycleBin(path);
                        else
                            Directory.Delete(path, true);

                        bytesFreed += size;
                        deleted++;
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    skipped++;
                }
                catch (IOException)
                {
                    skipped++;
                }
            }
        }, ct);

        Services.LogService.Log($"Cleanup: {deleted} deleted, {skipped} skipped, {Models.ScanResult.FormatSize(bytesFreed)} freed.");
        return new CleanupResult(deleted, skipped, bytesFreed);
    }

    public async Task<CleanupResult> DeleteScanResultAsync(
        ScanResult result,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var files = new List<string>();
        try
        {
            if (Directory.Exists(result.Path))
            {
                files.AddRange(Directory.EnumerateFiles(result.Path, "*", SearchOption.AllDirectories));
            }
            else if (File.Exists(result.Path))
            {
                files.Add(result.Path);
            }
        }
        catch (Exception) { }

        return await DeleteFilesAsync(files, progress, ct);
    }

    private static long GetDirectorySize(string path)
    {
        try
        {
            return new DirectoryInfo(path)
                .EnumerateFiles("*", SearchOption.AllDirectories)
                .Sum(f => { try { return f.Length; } catch { return 0; } });
        }
        catch { return 0; }
    }

    #region Recycle Bin via Shell32

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string pFrom;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? pTo;
        public ushort fFlags;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT FileOp);

    private const uint FO_DELETE = 0x0003;
    private const ushort FOF_ALLOWUNDO = 0x0040;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_NOERRORUI = 0x0400;
    private const ushort FOF_SILENT = 0x0004;

    private static void SendToRecycleBin(string path)
    {
        var shf = new SHFILEOPSTRUCT
        {
            wFunc = FO_DELETE,
            pFrom = path + '\0' + '\0',
            fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_NOERRORUI | FOF_SILENT
        };
        SHFileOperation(ref shf);
    }

    #endregion
}
