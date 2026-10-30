namespace PurgeKit.Core.Relocation.Apps;

/// <summary>A name or publisher fragment that marks an app as unsafe to move, and why.</summary>
/// <param name="Fragment">Case-insensitive text matched against the app's name and publisher.</param>
/// <param name="Reason">Plain-English explanation shown to the user.</param>
/// <param name="MatchInsideWords">
/// Match inside other words too (e.g. "VPN" in "ProtonVPN"). Off by default so short fragments
/// such as "ESET" do not catch unrelated names like "Preset Manager".
/// </param>
public sealed record DenylistEntry(string Fragment, string Reason, bool MatchInsideWords = false);
