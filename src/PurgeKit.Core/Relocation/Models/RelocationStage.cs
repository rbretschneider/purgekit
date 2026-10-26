namespace PurgeKit.Core.Relocation.Models;

/// <summary>Coarse phases reported while a relocation or restore runs.</summary>
public enum RelocationStage
{
    /// <summary>Running eligibility checks.</summary>
    Checking,

    /// <summary>Copying files.</summary>
    Copying,

    /// <summary>Comparing the copy against the original.</summary>
    Verifying,

    /// <summary>Swapping the folder and the junction.</summary>
    Swapping,

    /// <summary>Removing the superseded copy.</summary>
    Cleaning
}
