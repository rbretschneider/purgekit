namespace PurgeKit.Core.Models;

public class InstalledProgram
{
    public string DisplayName { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public DateTime? InstallDate { get; set; }
    public string? InstallLocation { get; set; }
    public long EstimatedSizeBytes { get; set; }
    public string? UninstallString { get; set; }
    public DateTime? LastUsed { get; set; }
    public string? LastUsedSource { get; set; }
    public string? InstallDrive { get; set; }

    public string SizeFormatted => ScanResult.FormatSize(EstimatedSizeBytes);
    public string LastUsedFormatted => LastUsed?.ToString("yyyy-MM-dd") ?? "Unknown";
    public string InstallDateFormatted => InstallDate?.ToString("yyyy-MM-dd") ?? "Unknown";
    public bool HasUninstaller => !string.IsNullOrEmpty(UninstallString);
}
