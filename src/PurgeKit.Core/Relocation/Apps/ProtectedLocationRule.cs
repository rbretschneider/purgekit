namespace PurgeKit.Core.Relocation.Apps;

/// <summary>Blocks apps whose recorded install folder is a Windows or shared system folder.</summary>
public sealed class ProtectedLocationRule : IAppClassificationRule
{
    private readonly IProtectedPathPolicy _policy;

    /// <summary>Creates the rule.</summary>
    public ProtectedLocationRule(IProtectedPathPolicy policy)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    /// <inheritdoc />
    public ClassificationVerdict? Classify(InstalledApp app, string installPath) =>
        _policy.IsProtected(installPath)
            ? new ClassificationVerdict(AppMoveOption.Blocked,
                "Installed into a Windows or shared folder, so it cannot be moved on its own.")
            : null;
}
