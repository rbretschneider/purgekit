using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PurgeKit.Core.Cleanup;
using PurgeKit.Core.Models;
using PurgeKit.Core.Scanning;
using PurgeKit.Core.Services;

namespace PurgeKit.UI.ViewModels;

public partial class ScanViewModel : ObservableObject
{
    private readonly MainViewModel _main;
    private readonly Dispatcher _dispatcher;
    private CancellationTokenSource? _cts;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    private bool _isScanning;

    [ObservableProperty]
    private string _statusText = "Click Scan All in the sidebar to find everything that can be cleaned up.";

    [ObservableProperty]
    private string _progressPhase = "";

    [ObservableProperty]
    private string _progressDetail = "";

    [ObservableProperty]
    private ObservableCollection<ScanItemVm> _safeItems = new();

    [ObservableProperty]
    private ObservableCollection<ScanItemVm> _reviewItems = new();

    [ObservableProperty]
    private string _safeTotalText = "";

    [ObservableProperty]
    private string _reviewTotalText = "";

    public bool ShowEmptyState => !IsScanning && SafeItems.Count == 0 && ReviewItems.Count == 0;

    public ScanViewModel(MainViewModel main)
    {
        _main = main;
        _dispatcher = Application.Current.Dispatcher;
    }

    public void CancelScan() => _cts?.Cancel();

    public async Task RunScanAsync(CancellationToken ct) => await DoScanAsync(ct);

    [RelayCommand]
    private async Task ScanAsync() => await DoScanAsync(default);

    private async Task DoScanAsync(CancellationToken externalCt)
    {
        IsScanning = true;
        SafeItems.Clear();
        ReviewItems.Clear();
        SafeTotalText = "";
        ReviewTotalText = "";
        StatusText = "Scanning...";
        _cts = CancellationTokenSource.CreateLinkedTokenSource(externalCt);

        // Track paths to deduplicate
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long safeTotal = 0, reviewTotal = 0;

        try
        {
            var scanner = new UnifiedScanner(App.Settings);
            var reader = scanner.ScanAll(
                new Progress<UnifiedScanner.ScanProgress>(p =>
                {
                    ProgressPhase = p.Phase;
                    ProgressDetail = p.Detail;
                    StatusText = $"Scanning: {p.Phase} ({p.PhasesCompleted}/{p.TotalPhases} phases)...";
                }),
                _cts.Token);

            // Consume results as they stream in
            await foreach (var result in reader.ReadAllAsync(_cts.Token))
            {
                // Deduplicate by path
                if (!string.IsNullOrEmpty(result.Path) && !seenPaths.Add(result.Path))
                    continue;

                // Skip tiny results (< 1 MB)
                if (result.SizeBytes < 1024 * 1024)
                    continue;

                var item = new ScanItemVm(result, _main);

                // Insert sorted by size descending
                if (result.Safety == SafetyLevel.Safe)
                {
                    InsertSorted(SafeItems, item);
                    safeTotal += result.SizeBytes;
                    SafeTotalText = $"{SafeItems.Count} items — {ScanResult.FormatSize(safeTotal)} reclaimable";
                }
                else
                {
                    InsertSorted(ReviewItems, item);
                    reviewTotal += result.SizeBytes;
                    ReviewTotalText = $"{ReviewItems.Count} items — {ScanResult.FormatSize(reviewTotal)}";
                }
            }

            StatusText = $"Scan complete. {SafeItems.Count + ReviewItems.Count} items found.";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Scan stopped.";
        }
        finally
        {
            IsScanning = false;
            ProgressPhase = "";
            ProgressDetail = "";
            OnPropertyChanged(nameof(ShowEmptyState));
        }
    }

    [RelayCommand]
    private void StopScan() => _cts?.Cancel();

    [RelayCommand]
    private async Task CleanAllSafeAsync()
    {
        var items = SafeItems.Where(i => !i.IsDeleted && !i.IsBusy && i.SizeBytes > 0).ToList();
        if (items.Count == 0) return;

        var totalSize = items.Sum(i => i.SizeBytes);
        var sizeText = ScanResult.FormatSize(totalSize);
        var mode = App.Settings.UseRecycleBin ? "send to Recycle Bin" : "permanently delete";

        var result = MessageBox.Show(
            $"WARNING: You are about to {mode} {items.Count} items totalling {sizeText}.\n\n" +
            "This will only delete items in the \"Safe to Clean\" column — caches and temp files that tools re-download on demand.\n\n" +
            "Items in \"Needs Review\" will NOT be touched.\n\n" +
            $"{(App.Settings.UseRecycleBin ? "" : "This cannot be undone. ")}Are you sure you want to continue?",
            "Confirm Clean All",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        foreach (var item in items)
            await item.PerformDeleteAsync();

        _main.RefreshDriveInfo();
        UpdateTotals();
    }

    private void UpdateTotals()
    {
        var safeTotal = SafeItems.Where(i => !i.IsDeleted).Sum(i => i.SizeBytes);
        var reviewTotal = ReviewItems.Where(i => !i.IsDeleted).Sum(i => i.SizeBytes);
        SafeTotalText = $"{SafeItems.Count(i => !i.IsDeleted)} items — {ScanResult.FormatSize(safeTotal)} reclaimable";
        ReviewTotalText = $"{ReviewItems.Count(i => !i.IsDeleted)} items — {ScanResult.FormatSize(reviewTotal)}";
    }

    private static void InsertSorted(ObservableCollection<ScanItemVm> collection, ScanItemVm item)
    {
        for (int i = 0; i < collection.Count; i++)
        {
            if (item.SizeBytes > collection[i].SizeBytes)
            {
                collection.Insert(i, item);
                return;
            }
        }
        collection.Add(item);
    }
}

public partial class ScanItemVm : ObservableObject
{
    private readonly MainViewModel _main;
    private readonly ScanResult _result;

    public string Name => _result.Name;
    public string Description => _result.Description;
    public string Path => _result.Path;
    public SafetyLevel Safety => _result.Safety;
    public string SafetyExplanation => _result.SafetyExplanation;
    public long SizeBytes => _result.SizeBytes;
    public string? LinkedApplication => _result.LinkedApplication;

    [ObservableProperty]
    private string _sizeText;

    [ObservableProperty]
    private int _fileCount;

    [ObservableProperty]
    private bool _isDeleted;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _busyText = "";

    [ObservableProperty]
    private string _resultText = "";

    public bool IsWsl => Name.StartsWith("WSL:");
    public bool IsDocker => Name.StartsWith("Docker");
    public bool IsGit => Name.StartsWith(".git:");
    public bool ShowDeleteButton => Safety != SafetyLevel.DoNotDelete;
    public bool ShowCompactButton => IsWsl;
    public bool ShowDockerPruneButton => IsDocker && Name.Contains("WSL2 Data");
    public bool ShowGitGcButton => IsGit;

    public ScanItemVm(ScanResult result, MainViewModel main)
    {
        _result = result;
        _main = main;
        _sizeText = result.SizeFormatted;
        _fileCount = result.FileCount;
    }

    public async Task PerformDeleteAsync() => await DeleteAsync();

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (IsDeleted || IsBusy) return;

        if (Safety == SafetyLevel.Caution)
        {
            var msg = $"Are you sure you want to delete '{Name}'?\n\n{SafetyExplanation}";
            var answer = MessageBox.Show(msg, "Confirm Deletion",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (answer != MessageBoxResult.Yes) return;
        }

        IsBusy = true;
        BusyText = "Deleting...";

        try
        {
            var engine = new CleanupEngine(App.Settings);
            var cleanResult = await engine.DeleteScanResultAsync(
                _result,
                new Progress<string>(s => BusyText = $"Deleting: {s}"));

            IsDeleted = true;
            FileCount = 0;
            SizeText = "0 B";
            ResultText = cleanResult.SkippedCount > 0
                ? $"Cleaned. {cleanResult.SkippedCount} files in use."
                : $"Cleaned. Freed {ScanResult.FormatSize(cleanResult.BytesFreed)}.";

            _main.RefreshDriveInfo();
        }
        finally
        {
            IsBusy = false;
            BusyText = "";
        }
    }

    [RelayCommand]
    private async Task CompactWslAsync()
    {
        if (!IsWsl || IsBusy) return;
        IsBusy = true;
        BusyText = "Shutting down WSL...";

        try
        {
            var distroName = Name.Replace("WSL: ", "");
            BusyText = "Setting sparse mode...";
            await RunProcessAsync("wsl.exe", $"--manage {distroName} --set-sparse true");
            BusyText = "Shutting down WSL...";
            await RunProcessAsync("wsl.exe", "--shutdown");
            BusyText = "Compacting VHDX (may take minutes)...";

            var script = System.IO.Path.GetTempFileName();
            await File.WriteAllTextAsync(script,
                $"select vdisk file=\"{Path}\"\r\nattach vdisk readonly\r\ncompact vdisk\r\ndetach vdisk\r\nexit\r\n");
            await RunProcessAsync("diskpart.exe", $"/s \"{script}\"");
            try { File.Delete(script); } catch { }

            if (File.Exists(Path))
            {
                var newSize = new FileInfo(Path).Length;
                var saved = _result.SizeBytes - newSize;
                SizeText = ScanResult.FormatSize(newSize);
                ResultText = saved > 0
                    ? $"Compacted. Reclaimed {ScanResult.FormatSize(saved)}."
                    : "Already near optimal size.";
            }
            _main.RefreshDriveInfo();
        }
        catch (Exception ex)
        {
            ResultText = $"Failed: {ex.Message}";
            LogService.LogError("WSL compact", ex);
        }
        finally { IsBusy = false; BusyText = ""; }
    }

    [RelayCommand]
    private async Task DockerPruneAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        BusyText = "Running Docker prune...";
        try
        {
            await RunProcessAsync("docker", "system prune -a -f --volumes");
            ResultText = "Prune complete. Re-scan to see updated size.";
            _main.RefreshDriveInfo();
        }
        catch (Exception ex)
        {
            ResultText = $"Failed: {ex.Message}. Is Docker running?";
            LogService.LogError("Docker prune", ex);
        }
        finally { IsBusy = false; BusyText = ""; }
    }

    [RelayCommand]
    private async Task GitGcAsync()
    {
        if (!IsGit || IsBusy) return;
        IsBusy = true;
        BusyText = "Running git gc --aggressive...";
        try
        {
            var repoDir = System.IO.Path.GetDirectoryName(Path);
            if (string.IsNullOrEmpty(repoDir)) return;
            await RunProcessAsync("git", $"-C \"{repoDir}\" gc --aggressive --prune=now");

            if (Directory.Exists(Path))
            {
                long newSize = 0;
                var opts = new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true };
                foreach (var f in Directory.EnumerateFiles(Path, "*", opts))
                    try { newSize += new FileInfo(f).Length; } catch { }

                var saved = _result.SizeBytes - newSize;
                SizeText = ScanResult.FormatSize(newSize);
                ResultText = saved > 0
                    ? $"Reclaimed {ScanResult.FormatSize(saved)}."
                    : "Already well-packed.";
            }
        }
        catch (Exception ex)
        {
            ResultText = $"Failed: {ex.Message}";
            LogService.LogError("Git GC", ex);
        }
        finally { IsBusy = false; BusyText = ""; }
    }

    [RelayCommand]
    private void OpenFolder()
    {
        var target = Directory.Exists(Path) ? Path : System.IO.Path.GetDirectoryName(Path);
        if (target != null && Directory.Exists(target))
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                { FileName = target, UseShellExecute = true });
    }

    private static async Task<string> RunProcessAsync(string fileName, string arguments)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = fileName, Arguments = arguments,
            RedirectStandardOutput = true, RedirectStandardError = true,
            UseShellExecute = false, CreateNoWindow = true
        };
        using var proc = System.Diagnostics.Process.Start(psi)
            ?? throw new Exception($"Failed to start {fileName}");
        var output = await proc.StandardOutput.ReadToEndAsync();
        var error = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();
        LogService.Log($"[{fileName} {arguments}] exit={proc.ExitCode}");
        return output + error;
    }
}
