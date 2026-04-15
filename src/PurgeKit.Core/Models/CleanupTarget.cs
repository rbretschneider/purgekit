namespace PurgeKit.Core.Models;

public class CleanupTarget
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Paths { get; set; } = new();
    public SafetyLevel Safety { get; set; } = SafetyLevel.Safe;
    public string? FilePattern { get; set; }
    public int? MaxAgeDays { get; set; }
    public bool RequiresConfirmation { get; set; }
}
