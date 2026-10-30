using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PurgeKit.Core.Models;
using PurgeKit.Core.Relocation.Apps;
using PurgeKit.Core.Relocation.Models;
using PurgeKit.Core.Services;
using PurgeKit.UI.Services;

namespace PurgeKit.UI.ViewModels;

/// <summary>
/// The Move Apps screen: lists apps on the system drive that have not been used for a while, moves them to
/// another drive, and lists moved apps so they can be moved back.
/// </summary>
public partial class RelocateViewModel : ObservableObject
{
    private const string MoveTitle = "Move app";
    private const string MoveBackTitle = "Move app back";
    private const long MegaByte = 1024 * 1024;

    private readonly AppRelocationServices _services;
    private readonly IUserPrompt _prompt;
    private readonly IShellLauncher _shell;
    private readonly Action _onDiskSpaceChanged;
    private CancellationTokenSource? _operationCts;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    private bool _hasLoaded;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MoveCommand), nameof(MoveBackCommand), nameof(RefreshCommand))]
    private bool _isOperationRunning;

    [ObservableProperty]
    private string _statusText = "Finds apps on the system drive you rarely use and moves them to another drive.";

    [ObservableProperty]
    private string _warningText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    private int _visibleCount;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MoveCommand))]
    private TargetDrive? _selectedDrive;

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private bool _showUnmovable;

    [ObservableProperty]
    private FilterOption<int?> _selectedIdle;

    [ObservableProperty]
    private FilterOption<long> _selectedSize;

    /// <summary>Creates the view model.</summary>
    public RelocateViewModel(AppRelocationServices services, IUserPrompt prompt, IShellLauncher shell, Action onDiskSpaceChanged)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _prompt = prompt ?? throw new ArgumentNullException(nameof(prompt));
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _onDiskSpaceChanged = onDiskSpaceChanged ?? throw new ArgumentNullException(nameof(onDiskSpaceChanged));
        _selectedIdle = IdleOptions[2];
        _selectedSize = SizeOptions[2];
        CandidatesView = CollectionViewSource.GetDefaultView(Candidates);
        CandidatesView.Filter = item => CurrentFilter().Matches(((AppCandidateItemVm)item).Candidate, DateTime.UtcNow);
        CandidatesView.SortDescriptions.Add(new SortDescription(nameof(AppCandidateItemVm.SizeBytes), ListSortDirection.Descending));
    }

    public ObservableCollection<AppCandidateItemVm> Candidates { get; } = [];

    public ICollectionView CandidatesView { get; }

    public ObservableCollection<MovedAppItemVm> MovedApps { get; } = [];

    public ObservableCollection<TargetDrive> TargetDrives { get; } = [];

    public IReadOnlyList<FilterOption<int?>> IdleOptions { get; } =
    [
        new("Any time", null),
        new("30 days", 30),
        new("90 days", 90),
        new("6 months", 182),
        new("1 year", 365)
    ];

    public IReadOnlyList<FilterOption<long>> SizeOptions { get; } =
    [
        new("Any size", 0),
        new("50 MB", 50 * MegaByte),
        new("200 MB", 200 * MegaByte),
        new("1 GB", 1024 * MegaByte)
    ];

    public bool ShowEmptyState => HasLoaded && !IsLoading && VisibleCount == 0;

    public bool HasNoTargetDrive => TargetDrives.Count == 0;

    public string MovedAppsHeader => $"Moved apps ({MovedApps.Count})";

    public bool HasNoMovedApps => MovedApps.Count == 0;

    /// <summary>Settles interrupted moves and loads moved apps. Call once at startup.</summary>
    public async Task InitializeAsync()
    {
        LoadDrives();
        try
        {
            var recovered = await _services.Recovery.RecoverAsync();
            if (recovered.Count > 0)
            {
                StatusText = $"Finished or undid {recovered.Count} move(s) that were interrupted last time.";
            }
        }
        catch (AggregateException ex)
        {
            LogService.LogError("Relocation recovery", ex);
            WarningText = "Some interrupted moves could not be cleaned up. They are listed under Moved apps; restart PurgeKit to retry.";
            _prompt.ShowError("Interrupted moves", $"{WarningText}\n\n{DescribeFailure(ex)}");
        }
        await LoadMovedAppsAsync();
    }

    /// <summary>Scans for candidates the first time the screen is shown.</summary>
    public async Task EnsureLoadedAsync()
    {
        if (!HasLoaded && !IsLoading)
        {
            await RefreshAsync();
        }
    }

    partial void OnSearchTextChanged(string value) => RefreshFilter();

    partial void OnShowUnmovableChanged(bool value) => RefreshFilter();

    partial void OnSelectedIdleChanged(FilterOption<int?> value) => RefreshFilter();

    partial void OnSelectedSizeChanged(FilterOption<long> value) => RefreshFilter();

    private bool CanStartOperation => !IsOperationRunning;

    private bool CanMove(AppCandidateItemVm? item) => !IsOperationRunning && SelectedDrive is not null && item?.CanMove == true;

    private bool CanMoveBack(MovedAppItemVm? item) => !IsOperationRunning && item?.CanMoveBack == true;

    [RelayCommand(CanExecute = nameof(CanStartOperation))]
    private async Task RefreshAsync()
    {
        IsLoading = true;
        WarningText = "";
        StatusText = $"Looking for apps on {SystemDrive.Root}…";
        try
        {
            var result = await _services.Candidates.GetCandidatesAsync();
            ReplaceAll(Candidates, result.Items.Select(c => new AppCandidateItemVm(c)));
            WarningText = result.Warning ?? "";
            LoadDrives();
            RefreshFilter();
            StatusText = Summarize(result.Items);
            HasLoaded = true;
        }
        catch (Exception ex) when (IsExpectedFailure(ex))
        {
            LogService.LogError("Relocation scan", ex);
            StatusText = $"Could not list apps: {DescribeFailure(ex)} Click Refresh to try again.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanMove))]
    private async Task MoveAsync(AppCandidateItemVm? item)
    {
        if (item is null || SelectedDrive is not { } drive)
        {
            return;
        }

        await RunOperationAsync(item.Operation, MoveTitle, async ct =>
        {
            var report = await _services.Mover.CheckMoveAsync(item.Candidate, drive, ct);
            if (!ConfirmMove(item, drive, report))
            {
                return false;
            }
            await _services.Mover.MoveAsync(item.Candidate, drive, new Progress<RelocationProgress>(item.Operation.Report), ct);
            item.Operation.Succeed($"Moved to {drive.RootPath}");
            Candidates.Remove(item);
            await LoadMovedAppsAsync();
            return true;
        });
    }

    [RelayCommand(CanExecute = nameof(CanMoveBack))]
    private async Task MoveBackAsync(MovedAppItemVm? item)
    {
        if (item is null)
        {
            return;
        }

        var moved = await RunOperationAsync(item.Operation, MoveBackTitle, async ct =>
        {
            var report = await _services.Mover.CheckMoveBackAsync(item.App, ct);
            if (!ConfirmMoveBack(item, report))
            {
                return false;
            }
            await _services.Mover.MoveBackAsync(item.App, new Progress<RelocationProgress>(item.Operation.Report), ct);
            item.Operation.Succeed($"Moved back to {item.OriginalPath}");
            return true;
        });

        if (moved)
        {
            await LoadMovedAppsAsync();
            await RefreshAsync();
        }
    }

    [RelayCommand]
    private void CancelOperation() => _operationCts?.Cancel();

    [RelayCommand]
    private void OpenFolder(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }
        try
        {
            _shell.OpenFolder(path);
        }
        catch (DirectoryNotFoundException ex)
        {
            _prompt.ShowError("Open folder", ex.Message);
        }
    }

    // Shared shell for move and move-back: one operation at a time, cancellable, every outcome visible on the row.
    private async Task<bool> RunOperationAsync(OperationStatusVm status, string title, Func<CancellationToken, Task<bool>> operation)
    {
        IsOperationRunning = true;
        _operationCts = new CancellationTokenSource();
        status.Start("Checking…");
        try
        {
            var completed = await operation(_operationCts.Token);
            if (!completed && !status.IsError)
            {
                status.Clear();
            }
            return completed;
        }
        catch (OperationCanceledException)
        {
            status.Fail("Cancelled. Nothing was changed.");
            return false;
        }
        catch (Exception ex) when (IsExpectedFailure(ex))
        {
            LogService.LogError(title, ex);
            status.Fail("Failed — see details");
            _prompt.ShowError(title, $"{DescribeFailure(ex)}\n\nThe app was left where it was. Fix the problem above and try again.");
            return false;
        }
        finally
        {
            _operationCts.Dispose();
            _operationCts = null;
            IsOperationRunning = false;
            LoadDrives();
            _onDiskSpaceChanged();
        }
    }

    private bool ConfirmMove(AppCandidateItemVm item, TargetDrive drive, EligibilityReport report)
    {
        if (!report.IsEligible)
        {
            item.Operation.Fail("Can't move right now — see details");
            _prompt.Inform(MoveTitle, $"{item.Name} can't be moved right now:\n\n• {JoinBlockers(report)}");
            return false;
        }

        var size = ScanResult.FormatSize(report.SizeBytes > 0 ? report.SizeBytes : item.SizeBytes);
        return _prompt.Confirm(MoveTitle,
            $"Move {item.Name} ({size}) to {drive.RootPath}?\n\n" +
            $"From: {report.FromPath}\nTo:   {report.ToPath}\n\n" +
            "Shortcuts, settings and uninstallers keep working.\n" +
            $"If {drive.RootPath} is disconnected, {item.Name} won't start until it's back.\n" +
            "You can move it back at any time from Moved apps.");
    }

    private bool ConfirmMoveBack(MovedAppItemVm item, EligibilityReport report)
    {
        if (!report.IsEligible)
        {
            item.Operation.Fail("Can't move back right now — see details");
            _prompt.Inform(MoveBackTitle, $"{item.Name} can't be moved back right now:\n\n• {JoinBlockers(report)}");
            return false;
        }

        return _prompt.Confirm(MoveBackTitle,
            $"Move {item.Name} back to {item.OriginalPath}?\n\nThis copies it back to {SystemDrive.Root} and frees the space on {Path.GetPathRoot(item.CurrentPath)}.");
    }

    private async Task LoadMovedAppsAsync()
    {
        try
        {
            var result = await _services.Mover.GetMovedAppsAsync();
            ReplaceAll(MovedApps, result.Items.Select(app => new MovedAppItemVm(app)));
            if (result.Warning is not null)
            {
                WarningText = result.Warning;
            }
        }
        catch (Exception ex) when (IsExpectedFailure(ex))
        {
            LogService.LogError("Moved apps", ex);
            WarningText = $"Could not list moved apps: {DescribeFailure(ex)}";
        }
        OnPropertyChanged(nameof(MovedAppsHeader));
        OnPropertyChanged(nameof(HasNoMovedApps));
    }

    private void LoadDrives()
    {
        var previous = SelectedDrive?.RootPath;
        ReplaceAll(TargetDrives, _services.Drives.ListTargets());
        SelectedDrive = TargetDrives.FirstOrDefault(d => d.RootPath == previous) ?? TargetDrives.FirstOrDefault();
        OnPropertyChanged(nameof(HasNoTargetDrive));
    }

    private CandidateFilter CurrentFilter() =>
        new(SelectedIdle.Value, SelectedSize.Value, SearchText, ShowUnmovable);

    private void RefreshFilter()
    {
        CandidatesView.Refresh();
        VisibleCount = CandidatesView.Cast<object>().Count();
    }

    private static string Summarize(IReadOnlyList<AppCandidate> candidates)
    {
        var movable = candidates.Where(c => c.CanMove).ToList();
        var bytes = ScanResult.FormatSize(movable.Sum(c => c.SizeBytes));
        return $"{candidates.Count} apps on {SystemDrive.Root} — {movable.Count} can be moved ({bytes}).";
    }

    private static string JoinBlockers(EligibilityReport report) =>
        string.Join("\n• ", report.Blockers.Select(b => b.Detail));

    private static void ReplaceAll<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }

    // Failures the mover and OS can produce; anything else is a bug and goes to the global handler.
    private static bool IsExpectedFailure(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or InvalidOperationException
            or AggregateException or StorePackageException or System.ComponentModel.Win32Exception;

    private static string DescribeFailure(Exception ex) => ex is AggregateException aggregate
        ? string.Join("\n", aggregate.Flatten().InnerExceptions.Select(inner => inner.Message))
        : ex.Message;
}
