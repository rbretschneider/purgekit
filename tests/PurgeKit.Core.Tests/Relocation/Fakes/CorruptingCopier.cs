namespace PurgeKit.Core.Tests.Relocation.Fakes;

/// <summary>Copies for real, then damages one file in the copy so verification must catch it.</summary>
internal sealed class CorruptingCopier : IDirectoryCopier
{
    private readonly IDirectoryCopier _inner;
    private readonly string _relativeFileToDamage;

    public CorruptingCopier(IDirectoryCopier inner, string relativeFileToDamage)
    {
        _inner = inner;
        _relativeFileToDamage = relativeFileToDamage;
    }

    public async Task CopyAsync(string sourcePath, string destinationPath, IProgress<long>? bytesCopied, CancellationToken ct)
    {
        await _inner.CopyAsync(sourcePath, destinationPath, bytesCopied, ct);
        await File.AppendAllTextAsync(Path.Combine(destinationPath, _relativeFileToDamage), "garbage", ct);
    }

    public Task<IReadOnlyList<string>> FindMismatchesAsync(string sourcePath, string destinationPath, CancellationToken ct) =>
        _inner.FindMismatchesAsync(sourcePath, destinationPath, ct);
}
