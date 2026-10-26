using System.Buffers;
using System.Security.AccessControl;
using System.Security.Cryptography;

namespace PurgeKit.Core.Relocation.Infrastructure;

/// <summary>
/// Copies folder trees with attributes, timestamps and access rules, and verifies copies by comparing
/// file lists and sizes plus SHA-256 of executables and libraries.
/// </summary>
public sealed class AclPreservingDirectoryCopier : IDirectoryCopier
{
    private const int BufferSize = 1024 * 1024;

    private static readonly HashSet<string> HashedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".exe", ".dll", ".sys" };

    private static readonly EnumerationOptions TreeWithoutLinks = new()
    {
        RecurseSubdirectories = true,
        AttributesToSkip = FileAttributes.ReparsePoint,
        IgnoreInaccessible = false
    };

    /// <inheritdoc />
    public async Task CopyAsync(string sourcePath, string destinationPath, IProgress<long>? bytesCopied, CancellationToken ct)
    {
        var source = new DirectoryInfo(RelocationPaths.NormalizeDirectory(sourcePath, nameof(sourcePath)));
        var destinationFull = RelocationPaths.NormalizeDirectory(destinationPath, nameof(destinationPath));
        if (!source.Exists)
        {
            throw new DirectoryNotFoundException($"'{source.FullName}' does not exist.");
        }
        if (Path.Exists(destinationFull))
        {
            throw new IOException($"'{destinationFull}' already exists.");
        }

        var destination = Directory.CreateDirectory(destinationFull);
        destination.SetAccessControl(EffectiveSecurityOf(source));
        await CopyContentsAsync(source, destination, new ByteCounter(bytesCopied), ct).ConfigureAwait(false);
        CopyTimesAndAttributes(source, destination);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> FindMismatchesAsync(string sourcePath, string destinationPath, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        var source = ListFiles(sourcePath);
        var destination = ListFiles(destinationPath);

        var mismatches = new List<string>();
        mismatches.AddRange(destination.Keys.Except(source.Keys, StringComparer.OrdinalIgnoreCase).Select(p => $"Unexpected: {p}"));
        foreach (var (relative, size) in source)
        {
            var mismatch = await CompareFileAsync(relative, size, destination, sourcePath, destinationPath, ct).ConfigureAwait(false);
            if (mismatch is not null)
            {
                mismatches.Add(mismatch);
            }
        }
        return mismatches;
    }

    private static async Task CopyContentsAsync(DirectoryInfo source, DirectoryInfo destination, ByteCounter counter, CancellationToken ct)
    {
        foreach (var file in source.EnumerateFiles())
        {
            await CopyFileAsync(file, Path.Combine(destination.FullName, file.Name), counter, ct).ConfigureAwait(false);
        }
        foreach (var directory in source.EnumerateDirectories())
        {
            var copy = destination.CreateSubdirectory(directory.Name);
            copy.SetAccessControl(directory.GetAccessControl(AccessControlSections.Access));
            await CopyContentsAsync(directory, copy, counter, ct).ConfigureAwait(false);
            CopyTimesAndAttributes(directory, copy);
        }
    }

    private static async Task CopyFileAsync(FileInfo source, string destinationPath, ByteCounter counter, CancellationToken ct)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        try
        {
            await using var input = new FileStream(source.FullName, FileMode.Open, FileAccess.Read, FileShare.Read,
                BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
            await using var output = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                BufferSize, FileOptions.Asynchronous);
            int read;
            while ((read = await input.ReadAsync(buffer.AsMemory(0, BufferSize), ct).ConfigureAwait(false)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                counter.Add(read);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        var copy = new FileInfo(destinationPath);
        copy.SetAccessControl(source.GetAccessControl(AccessControlSections.Access));
        CopyTimesAndAttributes(source, copy);
    }

    // The top folder's permissions are normally inherited from its old parent (e.g. Program Files).
    // Freeze them as explicit rules so the copy keeps them under its new, differently-secured parent.
    private static DirectorySecurity EffectiveSecurityOf(DirectoryInfo directory)
    {
        var security = directory.GetAccessControl(AccessControlSections.Access);
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: true);
        return security;
    }

    // Attributes go last: a read-only flag would block the timestamp writes.
    private static void CopyTimesAndAttributes(FileSystemInfo source, FileSystemInfo destination)
    {
        destination.CreationTimeUtc = source.CreationTimeUtc;
        destination.LastWriteTimeUtc = source.LastWriteTimeUtc;
        destination.Attributes = source.Attributes;
    }

    private static Dictionary<string, long> ListFiles(string root) =>
        new DirectoryInfo(root).EnumerateFiles("*", TreeWithoutLinks)
            .ToDictionary(file => Path.GetRelativePath(root, file.FullName), file => file.Length, StringComparer.OrdinalIgnoreCase);

    private static async Task<string?> CompareFileAsync(
        string relative,
        long size,
        IReadOnlyDictionary<string, long> destination,
        string sourceRoot,
        string destinationRoot,
        CancellationToken ct)
    {
        if (!destination.TryGetValue(relative, out var copySize))
        {
            return $"Missing: {relative}";
        }
        if (copySize != size)
        {
            return $"Size differs: {relative}";
        }
        if (!HashedExtensions.Contains(Path.GetExtension(relative)))
        {
            return null;
        }
        var matches = await HashesMatchAsync(Path.Combine(sourceRoot, relative), Path.Combine(destinationRoot, relative), ct)
            .ConfigureAwait(false);
        return matches ? null : $"Content differs: {relative}";
    }

    private static async Task<bool> HashesMatchAsync(string left, string right, CancellationToken ct)
    {
        var leftHash = await HashAsync(left, ct).ConfigureAwait(false);
        var rightHash = await HashAsync(right, ct).ConfigureAwait(false);
        return leftHash.AsSpan().SequenceEqual(rightHash);
    }

    private static async Task<byte[]> HashAsync(string path, CancellationToken ct)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false);
    }

    private sealed class ByteCounter
    {
        private readonly IProgress<long>? _progress;
        private long _total;

        public ByteCounter(IProgress<long>? progress)
        {
            _progress = progress;
        }

        public void Add(int bytes)
        {
            _total += bytes;
            _progress?.Report(_total);
        }
    }
}
