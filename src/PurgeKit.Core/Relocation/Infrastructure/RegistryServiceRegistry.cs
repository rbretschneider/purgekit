using Microsoft.Win32;

namespace PurgeKit.Core.Relocation.Infrastructure;

/// <summary>Reads service and driver image paths from the Services registry key.</summary>
public sealed class RegistryServiceRegistry : IServiceRegistry
{
    private const string ServicesKeyPath = @"SYSTEM\CurrentControlSet\Services";
    private const string ImagePathValue = "ImagePath";

    /// <inheritdoc />
    public IReadOnlyList<string> FindServicesUnder(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        var prefix = Path.TrimEndingDirectorySeparator(directory) + Path.DirectorySeparatorChar;
        using var services = Registry.LocalMachine.OpenSubKey(ServicesKeyPath)
            ?? throw new InvalidOperationException($@"HKLM\{ServicesKeyPath} could not be opened.");

        // Image paths may be quoted, carry arguments, or use \??\ and %SystemRoot% forms,
        // so a containment test is more reliable than parsing out the executable.
        return services.GetSubKeyNames()
            .Where(name => ImagePathOf(services, name)?.Contains(prefix, StringComparison.OrdinalIgnoreCase) == true)
            .ToList();
    }

    private static string? ImagePathOf(RegistryKey services, string name)
    {
        using var service = services.OpenSubKey(name);
        var raw = service?.GetValue(ImagePathValue, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
        return raw is null ? null : Environment.ExpandEnvironmentVariables(raw);
    }
}
