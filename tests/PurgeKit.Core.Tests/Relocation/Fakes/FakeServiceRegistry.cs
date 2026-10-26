namespace PurgeKit.Core.Tests.Relocation.Fakes;

internal sealed class FakeServiceRegistry : IServiceRegistry
{
    private readonly List<(string ImagePath, string Name)> _services = [];

    public void Register(string imagePath, string name) => _services.Add((imagePath, name));

    public IReadOnlyList<string> FindServicesUnder(string directory) =>
        _services.Where(s => RelocationPaths.IsSameOrUnder(s.ImagePath, directory)).Select(s => s.Name).ToList();
}
