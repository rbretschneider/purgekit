namespace PurgeKit.Core.Relocation.Apps;

/// <summary>A list that may be incomplete, with the reason when part of it could not be read.</summary>
/// <param name="Items">What could be listed.</param>
/// <param name="Warning">Why the list is incomplete; null when it is complete.</param>
public sealed record ListResult<T>(IReadOnlyList<T> Items, string? Warning = null);
