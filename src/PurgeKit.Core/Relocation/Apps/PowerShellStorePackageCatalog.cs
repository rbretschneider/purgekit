using System.Text.Json;
using System.Text.RegularExpressions;

namespace PurgeKit.Core.Relocation.Apps;

/// <summary>
/// Store apps through the Appx PowerShell module. Moving uses the same mechanism as Settings → Apps → Move:
/// the target drive is registered as an app volume (<c>X:\WindowsApps</c>) and Windows moves the package.
/// </summary>
public sealed partial class PowerShellStorePackageCatalog : IStorePackageCatalog
{
    private const string AppVolumeFolder = "WindowsApps";

    // Store packages only: frameworks, system and non-removable packages cannot be moved.
    private const string ListScript = """
        $ErrorActionPreference = 'Stop'
        $OutputEncoding = [Console]::OutputEncoding = [Text.Encoding]::UTF8
        @(Get-AppxPackage |
            Where-Object { -not $_.IsFramework -and -not $_.NonRemovable -and $_.SignatureKind -eq 'Store' -and $_.InstallLocation } |
            ForEach-Object { [pscustomobject]@{ FullName = $_.PackageFullName; Name = $_.Name; InstallLocation = $_.InstallLocation } }) |
            ConvertTo-Json -Compress
        """;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IPowerShellRunner _powerShell;

    /// <summary>Creates the catalog.</summary>
    public PowerShellStorePackageCatalog(IPowerShellRunner powerShell)
    {
        _powerShell = powerShell ?? throw new ArgumentNullException(nameof(powerShell));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StorePackage>> ListPackagesAsync(CancellationToken ct = default)
    {
        var json = await _powerShell.RunAsync(ListScript, ct).ConfigureAwait(false);
        return Parse(json);
    }

    /// <inheritdoc />
    public Task MovePackageAsync(string packageFullName, string driveRoot, CancellationToken ct = default)
    {
        RequireSafePackageName(packageFullName);
        var root = RequireDriveRoot(driveRoot);
        var script = RelocationPaths.AreSame(root, SystemDrive.Root)
            ? MoveToSystemVolumeScript(packageFullName)
            : MoveToDriveScript(packageFullName, Path.Combine(root, AppVolumeFolder));
        return _powerShell.RunAsync(script, ct);
    }

    private static IReadOnlyList<StorePackage> Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }
        try
        {
            // ConvertTo-Json writes a bare object for a single result, an array otherwise.
            var trimmed = json.Trim();
            var rows = trimmed.StartsWith('[')
                ? JsonSerializer.Deserialize<List<PackageRow>>(trimmed, JsonOptions) ?? []
                : [JsonSerializer.Deserialize<PackageRow>(trimmed, JsonOptions)!];
            return rows.Select(row => new StorePackage(row.FullName, row.Name, row.InstallLocation)).ToList();
        }
        catch (JsonException ex)
        {
            throw new StorePackageException("Windows returned a Store app list PurgeKit could not read.", ex);
        }
    }

    // Package full names and drive roots are interpolated into scripts, so only their known-safe
    // character sets are accepted; anything else is rejected before it reaches PowerShell.
    private static void RequireSafePackageName(string packageFullName)
    {
        if (string.IsNullOrWhiteSpace(packageFullName) || !PackageNamePattern().IsMatch(packageFullName))
        {
            throw new ArgumentException($"'{packageFullName}' is not a valid package full name.", nameof(packageFullName));
        }
    }

    private static string RequireDriveRoot(string driveRoot)
    {
        if (string.IsNullOrWhiteSpace(driveRoot) || !DriveRootPattern().IsMatch(driveRoot))
        {
            throw new ArgumentException($"'{driveRoot}' is not a drive root such as D:\\.", nameof(driveRoot));
        }
        return char.ToUpperInvariant(driveRoot[0]) + @":\";
    }

    private static string MoveToDriveScript(string packageFullName, string volumePath) => $$"""
        $ErrorActionPreference = 'Stop'
        $volume = Get-AppxVolume | Where-Object { $_.PackageStorePath -eq '{{volumePath}}' } | Select-Object -First 1
        if (-not $volume) { $volume = Add-AppxVolume -Path '{{volumePath}}' }
        Mount-AppxVolume -Volume $volume
        Move-AppxPackage -Package '{{packageFullName}}' -Volume $volume
        """;

    private static string MoveToSystemVolumeScript(string packageFullName) => $$"""
        $ErrorActionPreference = 'Stop'
        $volume = Get-AppxVolume | Where-Object { $_.IsSystemVolume } | Select-Object -First 1
        Move-AppxPackage -Package '{{packageFullName}}' -Volume $volume
        """;

    [GeneratedRegex(@"^[A-Za-z0-9._~-]+$")]
    private static partial Regex PackageNamePattern();

    [GeneratedRegex(@"^[A-Za-z]:\\?$")]
    private static partial Regex DriveRootPattern();
}
