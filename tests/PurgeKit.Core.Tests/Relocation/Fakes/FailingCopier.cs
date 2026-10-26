namespace PurgeKit.Core.Tests.Relocation.Fakes;

/// <summary>Copies for real, then fails as if the disk filled up, leaving a partial copy behind.</summary>
internal sealed class FailingCopier : IDirectoryCopier
{
    private readonly IDirectoryCopier _inner;

    public FailingCopier(IDirectoryCopier inner)
    {
        _inner = inner;
    }

    public async Task CopyAsync(string sourcePath, string destinationPath, IProgress<long>? bytesCopied, CancellationToken ct)
    {
        await _inner.CopyAsync(sourcePath, destinationPath, bytesCopied, ct);
        throw new IOException("There is not enough space on the disk.");
    }

    public Task<IReadOnlyList<string>> FindMismatchesAsync(string sourcePath, string destinationPath, CancellationToken ct) =>
        _inner.FindMismatchesAsync(sourcePath, destinationPath, ct);
}
