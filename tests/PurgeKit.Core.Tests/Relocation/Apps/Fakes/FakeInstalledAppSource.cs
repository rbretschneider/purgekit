using PurgeKit.Core.Relocation.Apps;

namespace PurgeKit.Core.Tests.Relocation.Apps.Fakes;

internal sealed class FakeInstalledAppSource : IInstalledAppSource
{
    public List<InstalledApp> Apps { get; } = [];

    public Task<IReadOnlyList<InstalledApp>> GetAppsAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<InstalledApp>>(Apps);
}
