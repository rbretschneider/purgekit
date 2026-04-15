namespace PurgeKit.Core.Services;

public record DriveSpaceInfo(string DriveName, long TotalBytes, long UsedBytes, long FreeBytes)
{
    public double UsedPercent => TotalBytes > 0 ? (double)UsedBytes / TotalBytes * 100 : 0;
    public string TotalFormatted => FormatSize(TotalBytes);
    public string UsedFormatted => FormatSize(UsedBytes);
    public string FreeFormatted => FormatSize(FreeBytes);

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
    }
}

public static class DriveInfoService
{
    public static DriveSpaceInfo GetDriveSpace(string driveLetter = "C")
    {
        var drive = new DriveInfo(driveLetter);
        if (!drive.IsReady)
            return new DriveSpaceInfo(driveLetter + ":", 0, 0, 0);

        var total = drive.TotalSize;
        var free = drive.AvailableFreeSpace;
        return new DriveSpaceInfo(driveLetter + ":", total, total - free, free);
    }
}
