using CommunityToolkit.Mvvm.ComponentModel;
using PurgeKit.Core.Relocation.Models;

namespace PurgeKit.UI.ViewModels;

/// <summary>Live status of a move or move-back for one row: busy flag, progress and a message.</summary>
public partial class OperationStatusVm : ObservableObject
{
    private const double FullPercent = 100;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isIndeterminate = true;

    [ObservableProperty]
    private double _percent;

    [ObservableProperty]
    private string _message = "";

    [ObservableProperty]
    private bool _isError;

    /// <summary>Marks the row busy with an initial message.</summary>
    public void Start(string message)
    {
        IsBusy = true;
        IsError = false;
        IsIndeterminate = true;
        Percent = 0;
        Message = message;
    }

    /// <summary>Shows a progress report from the mover.</summary>
    public void Report(RelocationProgress progress)
    {
        var hasTotal = progress.BytesTotal > 0;
        IsIndeterminate = progress.Stage != RelocationStage.Copying || !hasTotal;
        Percent = hasTotal ? FullPercent * progress.BytesDone / progress.BytesTotal : 0;
        Message = progress.Stage switch
        {
            RelocationStage.Checking => "Checking…",
            RelocationStage.Copying when hasTotal => $"Copying {Percent:0}%",
            RelocationStage.Copying => "Windows is moving it…",
            RelocationStage.Verifying => "Verifying the copy…",
            RelocationStage.Swapping => "Switching over…",
            _ => "Cleaning up…"
        };
    }

    /// <summary>Ends the operation successfully.</summary>
    public void Succeed(string message) => Finish(message, isError: false);

    /// <summary>Ends the operation with an error the user can act on.</summary>
    public void Fail(string message) => Finish(message, isError: true);

    /// <summary>Clears any status.</summary>
    public void Clear() => Finish("", isError: false);

    private void Finish(string message, bool isError)
    {
        IsBusy = false;
        IsError = isError;
        Message = message;
    }
}
