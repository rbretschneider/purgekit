namespace PurgeKit.Core.Relocation.Models;

/// <summary>Thrown when a relocation or restore is attempted while blockers exist.</summary>
public sealed class RelocationBlockedException : InvalidOperationException
{
    /// <summary>Creates the exception from the report that blocked the operation.</summary>
    public RelocationBlockedException(EligibilityReport report)
        : base(BuildMessage(report))
    {
        Report = report;
    }

    /// <summary>The report listing every blocker.</summary>
    public EligibilityReport Report { get; }

    private static string BuildMessage(EligibilityReport report)
    {
        var reasons = string.Join("; ", report.Blockers.Select(b => b.Detail));
        return $"'{report.FromPath}' cannot be moved: {reasons}";
    }
}
