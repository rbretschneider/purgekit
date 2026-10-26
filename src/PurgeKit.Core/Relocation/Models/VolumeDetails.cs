namespace PurgeKit.Core.Relocation.Models;

/// <summary>The facts about a volume that decide whether it can receive a relocated folder.</summary>
/// <param name="RootPath">Root of the volume, for example <c>D:\</c>.</param>
/// <param name="DriveType">Fixed, removable, network, and so on.</param>
/// <param name="FileSystem">File system name, for example <c>NTFS</c>.</param>
/// <param name="AvailableFreeBytes">Free space available to the current user.</param>
public sealed record VolumeDetails(string RootPath, DriveType DriveType, string FileSystem, long AvailableFreeBytes);
