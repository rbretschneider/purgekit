namespace PurgeKit.Core.Services;

public static class LogService
{
    private static readonly string LogDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PurgeKit", "logs");

    private static readonly string LogFile = Path.Combine(LogDir, $"scan_{DateTime.Now:yyyyMMdd}.log");

    public static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(LogDir);
            File.AppendAllText(LogFile, $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch { }
    }

    public static void LogError(string context, Exception ex)
    {
        Log($"ERROR [{context}] {ex.GetType().Name}: {ex.Message}");
    }
}
