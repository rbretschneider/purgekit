namespace PurgeKit.Core.Relocation.Models;

/// <summary>Progress snapshot for a running relocation or restore.</summary>
/// <param name="Stage">Current phase.</param>
/// <param name="BytesDone">Bytes copied so far.</param>
/// <param name="BytesTotal">Total bytes to copy.</param>
public sealed record RelocationProgress(RelocationStage Stage, long BytesDone, long BytesTotal);
