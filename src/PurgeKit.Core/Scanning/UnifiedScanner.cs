using System.Collections.Concurrent;
using System.Threading.Channels;
using PurgeKit.Core.Models;
using PurgeKit.Core.Services;

namespace PurgeKit.Core.Scanning;

public class UnifiedScanner
{
    private readonly AppSettings _settings;

    public record ScanProgress(string Phase, string Detail, int PhasesCompleted, int TotalPhases);

    public UnifiedScanner(AppSettings settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// Runs all scanners in parallel, streaming results through the returned ChannelReader.
    /// Results appear as soon as each scanner finds them.
    /// </summary>
    public ChannelReader<ScanResult> ScanAll(
        IProgress<ScanProgress>? progress = null,
        CancellationToken ct = default)
    {
        var channel = Channel.CreateUnbounded<ScanResult>(new UnboundedChannelOptions
        {
            SingleWriter = false,
            SingleReader = true
        });

        _ = RunAllScannersAsync(channel.Writer, progress, ct);

        return channel.Reader;
    }

    private async Task RunAllScannersAsync(
        ChannelWriter<ScanResult> writer,
        IProgress<ScanProgress>? progress,
        CancellationToken ct)
    {
        var completedPhases = 0;
        const int totalPhases = 3;

        try
        {
            // Run Quick Wins and Dev Tools in parallel (both fast, different paths)
            // Deep Scan runs concurrently too but is slower
            var quickTask = Task.Run(async () =>
            {
                try
                {
                    progress?.Report(new("Quick Wins", "Scanning temp files, caches...", completedPhases, totalPhases));
                    var scanner = new QuickWinScanner();
                    var results = await scanner.ScanAsync(
                        new Progress<string>(s => progress?.Report(new("Quick Wins", s, completedPhases, totalPhases))),
                        ct);

                    foreach (var r in results)
                        await writer.WriteAsync(r, ct);

                    Interlocked.Increment(ref completedPhases);
                    LogService.Log($"UnifiedScanner: Quick Wins complete, {results.Count} results");
                }
                catch (OperationCanceledException) { }
                catch (Exception ex) { LogService.LogError("UnifiedScanner.QuickWins", ex); }
            }, ct);

            var devTask = Task.Run(async () =>
            {
                try
                {
                    progress?.Report(new("Developer Tools", "Scanning GPU, Docker, dev caches...", completedPhases, totalPhases));
                    var scanner = new DevToolsScanner(_settings);
                    var results = await scanner.ScanAsync(
                        new Progress<DevToolsScanner.DevScanProgress>(p =>
                            progress?.Report(new("Developer Tools", p.CurrentTarget, completedPhases, totalPhases))),
                        ct);

                    foreach (var r in results)
                        await writer.WriteAsync(r, ct);

                    Interlocked.Increment(ref completedPhases);
                    LogService.Log($"UnifiedScanner: DevTools complete, {results.Count} results");
                }
                catch (OperationCanceledException) { }
                catch (Exception ex) { LogService.LogError("UnifiedScanner.DevTools", ex); }
            }, ct);

            var deepTask = Task.Run(async () =>
            {
                try
                {
                    progress?.Report(new("Deep Scan", "Scanning drive for large files...", completedPhases, totalPhases));
                    var scanner = new DeepScanner(_settings);
                    var analyzer = new DependencyAnalyzer();

                    var results = await scanner.ScanAsync(
                        new Progress<DeepScanner.ScanProgress>(p =>
                            progress?.Report(new("Deep Scan", $"{p.FilesScanned:N0} files — {p.CurrentPath}", completedPhases, totalPhases))),
                        ct);

                    // Classify safety
                    analyzer.ClassifyAll(results);

                    // Convert DeepScanItems to ScanResults for unified display
                    foreach (var item in results)
                    {
                        var r = new ScanResult
                        {
                            Name = item.Name,
                            Description = item.SafetyExplanation,
                            Path = item.FullPath,
                            SizeBytes = item.SizeBytes,
                            FileCount = item.ItemType == DeepScanItemType.File ? 1 : 0,
                            Safety = item.Safety,
                            SafetyExplanation = item.SafetyExplanation,
                            LastModified = item.LastModified,
                            LinkedApplication = item.LinkedApplication,
                            IsSymlink = item.IsSymlink
                        };
                        await writer.WriteAsync(r, ct);
                    }

                    Interlocked.Increment(ref completedPhases);
                    LogService.Log($"UnifiedScanner: Deep Scan complete, {results.Count} results");
                }
                catch (OperationCanceledException) { }
                catch (Exception ex) { LogService.LogError("UnifiedScanner.DeepScan", ex); }
            }, ct);

            await Task.WhenAll(quickTask, devTask, deepTask);

            progress?.Report(new("Complete", "All scans finished.", totalPhases, totalPhases));
        }
        catch (OperationCanceledException) { }
        finally
        {
            writer.Complete();
        }
    }
}
