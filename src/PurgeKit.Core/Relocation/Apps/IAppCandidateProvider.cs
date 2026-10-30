namespace PurgeKit.Core.Relocation.Apps;

/// <summary>Finds apps on the system drive and decides how each can be moved.</summary>
public interface IAppCandidateProvider
{
    /// <summary>
    /// Desktop and Store apps installed on the system drive, classified. If Store apps cannot be listed,
    /// desktop apps are still returned and the result carries a warning.
    /// </summary>
    Task<ListResult<AppCandidate>> GetCandidatesAsync(CancellationToken ct = default);
}
