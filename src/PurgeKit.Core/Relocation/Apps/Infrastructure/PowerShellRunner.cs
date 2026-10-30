using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace PurgeKit.Core.Relocation.Apps.Infrastructure;

/// <summary>
/// Runs scripts in Windows PowerShell 5.1, which ships with Windows and has the Appx module.
/// Scripts are passed base64-encoded so no quoting reaches the command line.
/// </summary>
public sealed class PowerShellRunner : IPowerShellRunner
{
    private const string PowerShellExe = "powershell.exe";

    /// <inheritdoc />
    public async Task<string> RunAsync(string script, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(script);
        using var process = new Process { StartInfo = StartInfoFor(script) };
        try
        {
            process.Start();
        }
        catch (Win32Exception ex)
        {
            throw new StorePackageException("Windows PowerShell could not be started.", ex);
        }

        var output = process.StandardOutput.ReadToEndAsync(ct);
        var error = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct).ConfigureAwait(false);
        var stdout = await output.ConfigureAwait(false);
        var stderr = await error.ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            throw new StorePackageException(string.IsNullOrWhiteSpace(stderr) ? $"PowerShell exited with code {process.ExitCode}." : stderr.Trim());
        }
        return stdout;
    }

    private static ProcessStartInfo StartInfoFor(string script)
    {
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        return new ProcessStartInfo(PowerShellExe, $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encoded}")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
    }
}
