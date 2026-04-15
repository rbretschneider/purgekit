namespace PurgeKit.Core.Models;

public enum DeepScanItemType
{
    File,
    Directory
}

public class DeepScanItem
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime? LastModified { get; set; }
    public SafetyLevel Safety { get; set; } = SafetyLevel.Unknown;
    public string SafetyExplanation { get; set; } = string.Empty;
    public string? LinkedApplication { get; set; }
    public bool IsSymlink { get; set; }
    public DeepScanItemType ItemType { get; set; }

    public string SizeFormatted => ScanResult.FormatSize(SizeBytes);
    public string LastModifiedFormatted => LastModified?.ToString("yyyy-MM-dd") ?? "Unknown";
    public bool CanDelete => Safety != SafetyLevel.DoNotDelete;
    public bool CanBulkDelete => Safety == SafetyLevel.Safe;
}
