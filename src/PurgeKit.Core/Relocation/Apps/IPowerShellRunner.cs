namespace PurgeKit.Core.Relocation.Apps;

/// <summary>Runs a Windows PowerShell script and returns its output.</summary>
public interface IPowerShellRunner
{
    /// <summary>Runs <paramref name="script"/> and returns standard output.</summary>
    /// <exception cref="StorePackageException">The script failed; the message carries PowerShell's error.</exception>
    Task<string> RunAsync(string script, CancellationToken ct = default);
}
