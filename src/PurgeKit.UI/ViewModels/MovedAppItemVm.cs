using PurgeKit.Core.Models;
using PurgeKit.Core.Relocation.Apps;

namespace PurgeKit.UI.ViewModels;

/// <summary>One row of the Moved apps list.</summary>
public sealed class MovedAppItemVm
{
    /// <summary>Wraps <paramref name="app"/>.</summary>
    public MovedAppItemVm(MovedApp app)
    {
        App = app ?? throw new ArgumentNullException(nameof(app));
    }

    /// <summary>The underlying moved app.</summary>
    public MovedApp App { get; }

    /// <summary>Live status of a move-back started from this row.</summary>
    public OperationStatusVm Operation { get; } = new();

    public string Name => App.Name;

    public string CurrentPath => App.CurrentPath;

    public string OriginalPath => App.OriginalPath;

    public string SizeText => App.SizeBytes > 0 ? ScanResult.FormatSize(App.SizeBytes) : "—";

    public string MovedText => App.MovedUtc?.ToLocalTime().ToString("yyyy-MM-dd") ?? "—";

    public string KindText => App.Kind == MovedAppKind.StorePackage ? "Store app (moved by Windows)" : "Moved by PurgeKit";

    public bool IsInterrupted => App.IsInterrupted;

    public bool CanMoveBack => !App.IsInterrupted;
}
