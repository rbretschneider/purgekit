namespace PurgeKit.Core.Relocation.Apps;

/// <summary>One reason an app might not be movable by PurgeKit. Add a rule to add a reason.</summary>
public interface IAppClassificationRule
{
    /// <summary>A verdict when this rule applies to <paramref name="app"/>; null when it has no opinion.</summary>
    ClassificationVerdict? Classify(InstalledApp app, string installPath);
}
