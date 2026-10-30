using PurgeKit.Core.Models;

namespace PurgeKit.Core.Relocation.Apps;

/// <summary>A drive that can receive moved apps.</summary>
/// <param name="RootPath">Drive root, e.g. <c>D:\</c>.</param>
/// <param name="Label">Volume label, or empty.</param>
/// <param name="FreeBytes">Free space available to the current user.</param>
public sealed record TargetDrive(string RootPath, string Label, long FreeBytes)
{
    /// <summary>Text for drive pickers, e.g. <c>D:\ Games — 812.4 GB free</c>.</summary>
    public string DisplayText =>
        $"{RootPath}{(string.IsNullOrWhiteSpace(Label) ? "" : " " + Label)} — {ScanResult.FormatSize(FreeBytes)} free";
}
