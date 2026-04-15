using System.Text.Json;

namespace PurgeKit.Core.Models;

public class AppSettings
{
    public string Theme { get; set; } = "Dark";
    public bool UseRecycleBin { get; set; } = false;
    public int MinFileAgeDays { get; set; } = 180;
    public long MinFileSizeBytes { get; set; } = 10L * 1024 * 1024; // 10 MB
    public List<string> ExcludedPaths { get; set; } = new();

    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PurgeKit");
    private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

    public void Save()
    {
        Directory.CreateDirectory(SettingsDir);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsPath, json);
    }

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch { }
        return new AppSettings();
    }
}
