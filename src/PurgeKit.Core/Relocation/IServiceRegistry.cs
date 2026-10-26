namespace PurgeKit.Core.Relocation;

/// <summary>Looks up Windows services and drivers installed from a folder.</summary>
public interface IServiceRegistry
{
    /// <summary>Names of services or drivers whose image lives under <paramref name="directory"/>.</summary>
    IReadOnlyList<string> FindServicesUnder(string directory);
}
