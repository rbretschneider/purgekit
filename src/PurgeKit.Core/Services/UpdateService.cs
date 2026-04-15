using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PurgeKit.Core.Services;

public record UpdateInfo(string TagName, string DownloadUrl, string HtmlUrl);

public static class UpdateService
{
    private const string RepoOwner = "rbretschneider";
    private const string RepoName = "purgekit";
    private static readonly HttpClient Http = new()
    {
        DefaultRequestHeaders =
        {
            { "User-Agent", "PurgeKit-UpdateCheck" },
            { "Accept", "application/vnd.github+json" }
        },
        Timeout = TimeSpan.FromSeconds(10)
    };

    /// <summary>
    /// Returns the current assembly version (set at build time via csproj Version property).
    /// </summary>
    public static Version GetCurrentVersion()
    {
        var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        return asm.GetName().Version ?? new Version(0, 0, 0);
    }

    /// <summary>
    /// Checks GitHub for the latest release. Returns null if up-to-date or on any error.
    /// </summary>
    public static async Task<UpdateInfo?> CheckForUpdateAsync()
    {
        try
        {
            var url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
            var response = await Http.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadFromJsonAsync<GitHubRelease>();
            if (json is null || string.IsNullOrEmpty(json.TagName))
                return null;

            // Parse remote version from tag (strip leading 'v' if present)
            var tagVersion = json.TagName.TrimStart('v');
            if (!Version.TryParse(tagVersion, out var remoteVersion))
                return null;

            var current = GetCurrentVersion();
            // Compare major.minor.build (ignore revision)
            var currentComparable = new Version(current.Major, current.Minor, Math.Max(current.Build, 0));
            var remoteComparable = new Version(remoteVersion.Major, remoteVersion.Minor, Math.Max(remoteVersion.Build, 0));

            if (remoteComparable <= currentComparable)
                return null; // Up to date

            // Find the exe asset download URL
            var exeAsset = json.Assets?.FirstOrDefault(a =>
                a.Name?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == true);
            var downloadUrl = exeAsset?.BrowserDownloadUrl ?? json.HtmlUrl ?? "";

            return new UpdateInfo(json.TagName, downloadUrl, json.HtmlUrl ?? "");
        }
        catch (Exception ex)
        {
            LogService.LogError("UpdateCheck", ex);
            return null;
        }
    }

    /// <summary>
    /// Downloads the new exe and replaces the current one via a helper batch script.
    /// Returns true if the update was initiated (app should exit).
    /// </summary>
    public static async Task<bool> DownloadAndApplyUpdateAsync(
        string downloadUrl,
        IProgress<string>? progress = null)
    {
        try
        {
            var currentExe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(currentExe))
                return false;

            var tempPath = currentExe + ".update";
            var batchPath = Path.Combine(Path.GetTempPath(), "purgekit_update.cmd");

            // Download new exe
            progress?.Report("Downloading update...");
            using var response = await Http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync();
            await using var file = File.Create(tempPath);
            await stream.CopyToAsync(file);
            file.Close();

            progress?.Report("Applying update...");

            // Write a batch script that:
            // 1. Waits for this process to exit
            // 2. Replaces the exe
            // 3. Relaunches
            // 4. Cleans up
            var pid = Environment.ProcessId;
            var script = $"""
                @echo off
                echo Waiting for PurgeKit to close...
                :wait
                tasklist /fi "PID eq {pid}" 2>NUL | find "{pid}" >NUL
                if not errorlevel 1 (
                    timeout /t 1 /nobreak >NUL
                    goto wait
                )
                echo Replacing exe...
                move /y "{tempPath}" "{currentExe}"
                echo Relaunching...
                start "" "{currentExe}"
                del "%~f0"
                """;

            await File.WriteAllTextAsync(batchPath, script);

            // Launch the batch script hidden
            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{batchPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });

            return true; // Caller should exit the app
        }
        catch (Exception ex)
        {
            LogService.LogError("UpdateDownload", ex);
            return false;
        }
    }

    private class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonPropertyName("assets")]
        public List<GitHubAsset>? Assets { get; set; }
    }

    private class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; set; }
    }
}
