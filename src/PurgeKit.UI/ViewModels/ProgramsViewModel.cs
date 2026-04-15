using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PurgeKit.Core.Models;
using PurgeKit.Core.Scanning;

namespace PurgeKit.UI.ViewModels;

public partial class ProgramsViewModel : ObservableObject
{
    private CancellationTokenSource? _cts;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    private bool _isScanning;

    [ObservableProperty]
    private string _statusText = "Click Scan All in the sidebar to list installed programs.";

    [ObservableProperty]
    private ObservableCollection<ProgramItemVm> _programs = new();

    public bool ShowEmptyState => !IsScanning && Programs.Count == 0;

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private string _driveFilter = "All";

    [ObservableProperty]
    private ObservableCollection<string> _availableDrives = new() { "All" };

    [ObservableProperty]
    private string _sortBy = "LastUsed";

    // Track current sort direction per column so clicking toggles
    private string _lastSortColumn = "LastUsed";
    private ListSortDirection _lastSortDir = ListSortDirection.Ascending;

    partial void OnSearchTextChanged(string value) => ApplyFilterAndSort();
    partial void OnSortByChanged(string value) => ApplySort();
    partial void OnDriveFilterChanged(string value) => ApplyFilterAndSort();

    public async Task RunScanAsync(CancellationToken ct) => await DoScanAsync(ct);

    [RelayCommand]
    private async Task ScanAsync() => await DoScanAsync(default);

    private async Task DoScanAsync(CancellationToken externalCt)
    {
        IsScanning = true;
        Programs.Clear();
        StatusText = "Scanning installed programs...";
        _cts = CancellationTokenSource.CreateLinkedTokenSource(externalCt);

        try
        {
            var scanner = new InstalledProgramsScanner();
            var results = await scanner.ScanAsync(_cts.Token);

            foreach (var p in results)
                Programs.Add(new ProgramItemVm(p));

            PopulateDriveFilter();
            ApplySort();
            StatusText = $"Found {results.Count} installed programs.";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Scan cancelled.";
        }
        finally
        {
            IsScanning = false;
            OnPropertyChanged(nameof(ShowEmptyState));
        }
    }

    public void CancelScan() => _cts?.Cancel();

    [RelayCommand]
    private void StopScan() => _cts?.Cancel();

    [RelayCommand]
    private void SortByColumn(string column)
    {
        // Toggle direction if clicking same column
        if (_lastSortColumn == column)
            _lastSortDir = _lastSortDir == ListSortDirection.Ascending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;
        else
            _lastSortDir = GetDefaultDirection(column);

        _lastSortColumn = column;
        ApplySort();
    }

    private void PopulateDriveFilter()
    {
        var drives = Programs
            .Select(p => p.InstallDrive)
            .Where(d => !string.IsNullOrEmpty(d))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(d => d)
            .ToList();

        AvailableDrives.Clear();
        AvailableDrives.Add("All");
        foreach (var d in drives)
            AvailableDrives.Add(d!);
    }

    private void ApplyFilterAndSort()
    {
        var view = CollectionViewSource.GetDefaultView(Programs);

        bool hasSearch = !string.IsNullOrWhiteSpace(SearchText);
        bool hasDrive = DriveFilter != "All" && !string.IsNullOrEmpty(DriveFilter);

        if (!hasSearch && !hasDrive)
        {
            view.Filter = null;
        }
        else
        {
            var term = SearchText?.ToLowerInvariant() ?? "";
            view.Filter = o =>
            {
                var p = (ProgramItemVm)o;
                if (hasSearch &&
                    !p.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase) &&
                    !p.Publisher.Contains(term, StringComparison.OrdinalIgnoreCase))
                    return false;

                if (hasDrive &&
                    !string.Equals(p.InstallDrive, DriveFilter, StringComparison.OrdinalIgnoreCase))
                    return false;

                return true;
            };
        }
    }

    private void ApplySort()
    {
        var view = CollectionViewSource.GetDefaultView(Programs);
        view.SortDescriptions.Clear();

        var prop = _lastSortColumn switch
        {
            "Name" => nameof(ProgramItemVm.DisplayName),
            "Publisher" => nameof(ProgramItemVm.Publisher),
            "InstallDate" => nameof(ProgramItemVm.InstallDate),
            "Size" => nameof(ProgramItemVm.EstimatedSizeBytes),
            "Drive" => nameof(ProgramItemVm.InstallDrive),
            _ => nameof(ProgramItemVm.LastUsedSortKey)
        };

        view.SortDescriptions.Add(new SortDescription(prop, _lastSortDir));
    }

    private static ListSortDirection GetDefaultDirection(string column) => column switch
    {
        "Size" => ListSortDirection.Descending,
        _ => ListSortDirection.Ascending
    };
}

public partial class ProgramItemVm : ObservableObject
{
    private readonly InstalledProgram _program;

    public string DisplayName => _program.DisplayName;
    public string Publisher => _program.Publisher;
    public DateTime? InstallDate => _program.InstallDate;
    public string InstallDateFormatted => _program.InstallDateFormatted;
    public DateTime? LastUsed => _program.LastUsed;
    public string LastUsedFormatted => _program.LastUsedFormatted;
    public string? LastUsedSource => _program.LastUsedSource;
    public long EstimatedSizeBytes => _program.EstimatedSizeBytes;
    public string SizeFormatted => _program.SizeFormatted;
    public string? InstallDrive => _program.InstallDrive;
    public bool HasUninstaller => _program.HasUninstaller;
    public string? InstallLocation => _program.InstallLocation;

    // Sort key: unknown last-used dates sort to the top (oldest first)
    public DateTime LastUsedSortKey => _program.LastUsed ?? DateTime.MinValue;

    [ObservableProperty]
    private string _uninstallStatus = "";

    public ProgramItemVm(InstalledProgram program)
    {
        _program = program;
    }

    [RelayCommand]
    private async Task UninstallAsync()
    {
        if (!HasUninstaller) return;

        var answer = System.Windows.MessageBox.Show(
            $"This will run {DisplayName}'s official uninstaller. Proceed?",
            "Confirm Uninstall",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);
        if (answer != System.Windows.MessageBoxResult.Yes) return;

        UninstallStatus = "Launching uninstaller...";
        try
        {
            var uninstallString = _program.UninstallString!;

            // Parse the uninstall string — might be a path with args
            string fileName;
            string arguments = "";

            if (uninstallString.StartsWith('"'))
            {
                var endQuote = uninstallString.IndexOf('"', 1);
                if (endQuote > 0)
                {
                    fileName = uninstallString[1..endQuote];
                    arguments = uninstallString[(endQuote + 1)..].Trim();
                }
                else
                {
                    fileName = uninstallString;
                }
            }
            else
            {
                var parts = uninstallString.Split(' ', 2);
                fileName = parts[0];
                if (parts.Length > 1) arguments = parts[1];
            }

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = true
            };

            var proc = System.Diagnostics.Process.Start(psi);
            if (proc != null)
            {
                await proc.WaitForExitAsync();
                UninstallStatus = "Uninstaller finished.";
            }
        }
        catch (Exception ex)
        {
            UninstallStatus = $"Failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void OpenInstallFolder()
    {
        if (!string.IsNullOrEmpty(InstallLocation) && Directory.Exists(InstallLocation))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = InstallLocation,
                UseShellExecute = true
            });
        }
    }
}
