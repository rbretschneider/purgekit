namespace PurgeKit.Core.Relocation.Models;

/// <summary>
/// Lifecycle of a relocation as recorded in the journal. Each state is persisted before the
/// filesystem change it guards, so an interrupted operation can be recovered on the next start.
/// </summary>
public enum RelocationState
{
    /// <summary>Copying the original folder to the target drive.</summary>
    Copying,

    /// <summary>Copy verified; the original is about to be swapped for a junction.</summary>
    Verified,

    /// <summary>Junction in place; the renamed-aside original has not been deleted yet.</summary>
    Swapped,

    /// <summary>Relocation finished. The original path is a junction to the target.</summary>
    Completed,

    /// <summary>Copying the relocated folder back to its original drive.</summary>
    Restoring,

    /// <summary>Restore copy verified; the junction is about to be replaced by the real folder.</summary>
    RestoreVerified
}
