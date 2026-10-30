namespace PurgeKit.Core.Relocation.Apps;

/// <summary>A Microsoft Store (MSIX/Appx) app installed for the current user.</summary>
/// <param name="FullName">Package full name, which identifies it to Windows.</param>
/// <param name="Name">Package name, e.g. <c>SpotifyAB.SpotifyMusic</c>.</param>
/// <param name="InstallPath">Where the package's files live.</param>
public sealed record StorePackage(string FullName, string Name, string InstallPath);
