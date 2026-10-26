using PurgeKit.Core.Relocation.Models;

namespace PurgeKit.Core.Relocation.Rules;

/// <summary>
/// Blocks folders that Windows services or drivers run from. Services may start before the target
/// drive is ready at boot, and drivers must stay on the system drive.
/// </summary>
public sealed class ServiceRule : IEligibilityRule
{
    private readonly IServiceRegistry _services;

    /// <summary>Creates the rule.</summary>
    public ServiceRule(IServiceRegistry services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    /// <inheritdoc />
    public IReadOnlyList<RelocationBlocker> Evaluate(EligibilityContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var names = _services.FindServicesUnder(context.Request.SourcePath);
        return names.Count > 0
            ? [new RelocationBlocker(BlockerKind.ServiceRegistered,
                $"Windows runs {string.Join(", ", names)} from this folder, so it has to stay where it is.")]
            : [];
    }
}
