namespace PurgeKit.Core.Relocation.Apps;

/// <summary>Decides whether and how an installed desktop app can be moved.</summary>
public interface IAppClassifier
{
    /// <summary>Classifies <paramref name="app"/>, whose install folder is <paramref name="installPath"/>.</summary>
    ClassificationVerdict Classify(InstalledApp app, string installPath);
}
