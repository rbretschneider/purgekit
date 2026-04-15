using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PurgeKit.Core.Services;

namespace PurgeKit.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string _currentSection = "Scan";

    [ObservableProperty]
    private string _driveUsageText = "Loading...";

    [ObservableProperty]
    private double _driveUsagePercent;

    [ObservableProperty]
    private bool _isAnyScanRunning;

    [ObservableProperty]
    private bool _isUpdating;

    [ObservableProperty]
    private string _updateStatusText = "";

    [ObservableProperty]
    private ScanViewModel _scanVm;

    [ObservableProperty]
    private ProgramsViewModel _programsVm;

    [ObservableProperty]
    private SettingsViewModel _settingsVm;

    public string VersionText => $"v{UpdateService.GetCurrentVersion().ToString(3)}";

    private CancellationTokenSource? _globalCts;

    public MainViewModel()
    {
        _scanVm = new ScanViewModel(this);
        _programsVm = new ProgramsViewModel();
        _settingsVm = new SettingsViewModel(this);
        RefreshDriveInfo();
        _ = CheckForUpdateAsync();
    }

    private async Task CheckForUpdateAsync()
    {
        var update = await UpdateService.CheckForUpdateAsync();
        if (update is null) return;

        var current = UpdateService.GetCurrentVersion().ToString(3);
        var result = MessageBox.Show(
            $"A new version of PurgeKit is available.\n\n" +
            $"Current: v{current}\n" +
            $"Latest:  {update.TagName}\n\n" +
            "Would you like to download and install the update now?",
            "Update Available",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information);

        if (result != MessageBoxResult.Yes)
            return;

        if (!string.IsNullOrEmpty(update.DownloadUrl) &&
            update.DownloadUrl.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            // Auto-download and apply
            IsUpdating = true;
            UpdateStatusText = "Downloading update...";
            var applied = await UpdateService.DownloadAndApplyUpdateAsync(
                update.DownloadUrl,
                new Progress<string>(s => UpdateStatusText = s));

            if (applied)
            {
                Application.Current.Shutdown();
                return;
            }

            // Download failed — fall back to opening the browser
            IsUpdating = false;
            UpdateStatusText = "";
        }

        // No direct exe link or download failed — open the release page
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = update.HtmlUrl,
                UseShellExecute = true
            });
        }
        catch { }
    }

    [RelayCommand]
    private void Navigate(string section) => CurrentSection = section;

    [RelayCommand]
    private async Task ScanAllAsync()
    {
        if (IsAnyScanRunning) return;

        IsAnyScanRunning = true;
        CurrentSection = "Scan";
        _globalCts = new CancellationTokenSource();

        try
        {
            // Run file scan and programs scan in parallel
            var fileScanTask = ScanVm.RunScanAsync(_globalCts.Token);
            var programsScanTask = ProgramsVm.RunScanAsync(_globalCts.Token);
            await Task.WhenAll(fileScanTask, programsScanTask);
        }
        finally
        {
            IsAnyScanRunning = false;
        }
    }

    [RelayCommand]
    private void StopAllScans()
    {
        _globalCts?.Cancel();
        ScanVm.CancelScan();
        ProgramsVm.CancelScan();
    }

    public void RefreshDriveInfo()
    {
        var info = DriveInfoService.GetDriveSpace("C");
        DriveUsageText = $"C: {info.UsedFormatted} used / {info.TotalFormatted} total — {info.FreeFormatted} free";
        DriveUsagePercent = info.UsedPercent;
    }
}
