namespace PurgeKit.Core.Relocation.Apps;

/// <summary>Shape of one Store package in the JSON the list script prints.</summary>
internal sealed record PackageRow(string FullName, string Name, string InstallLocation);
