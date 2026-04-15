namespace PurgeKit.Core.Models;

public enum SafetyLevel
{
    Safe,
    Caution,
    DoNotDelete,
    Unknown
}

public class ScanResult
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public int FileCount { get; set; }
    public SafetyLevel Safety { get; set; } = SafetyLevel.Safe;
    public string SafetyExplanation { get; set; } = string.Empty;
    public DateTime? LastModified { get; set; }
    public string? LinkedApplication { get; set; }
    public bool IsSymlink { get; set; }
    public bool IsDeleted { get; set; }
    public int SkippedFiles { get; set; }

    public string SizeFormatted => FormatSize(SizeBytes);

    public static string FormatSize(long bytes)
    {
        if (bytes < 0) return "0 B";
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }
}
