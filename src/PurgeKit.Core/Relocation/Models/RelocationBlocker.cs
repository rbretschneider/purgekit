namespace PurgeKit.Core.Relocation.Models;

/// <summary>A single reason an operation is blocked, with a user-facing explanation.</summary>
/// <param name="Kind">Category of the blocker.</param>
/// <param name="Detail">Plain-English explanation, including what to do about it.</param>
public sealed record RelocationBlocker(BlockerKind Kind, string Detail);
