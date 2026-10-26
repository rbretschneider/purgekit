namespace PurgeKit.Core.Relocation.Models;

/// <summary>Thrown when a copied folder does not match its original.</summary>
public sealed class RelocationVerificationException : IOException
{
    private const int MismatchesInMessage = 5;

    /// <summary>Creates the exception from the list of differences found.</summary>
    public RelocationVerificationException(string fromPath, string toPath, IReadOnlyList<string> mismatches)
        : base($"Copy of '{fromPath}' to '{toPath}' failed verification ({mismatches.Count} difference(s)): "
            + string.Join("; ", mismatches.Take(MismatchesInMessage)))
    {
        Mismatches = mismatches;
    }

    /// <summary>Every difference found between the original and the copy.</summary>
    public IReadOnlyList<string> Mismatches { get; }
}
