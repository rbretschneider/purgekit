namespace PurgeKit.Core.Relocation.Apps;

/// <summary>A classification rule's decision about an app.</summary>
/// <param name="Option">Whether and how it can be moved.</param>
/// <param name="Reason">Plain-English explanation shown to the user.</param>
public sealed record ClassificationVerdict(AppMoveOption Option, string Reason);
