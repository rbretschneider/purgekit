namespace PurgeKit.Core.Relocation.Apps;

/// <summary>Blocks categories of software that break when moved: runtimes, drivers, security tools, platform suites.</summary>
public sealed class DenylistRule : IAppClassificationRule
{
    private const string SharedRuntime = "A shared runtime or SDK other apps depend on.";
    private const string DriverSoftware = "Hardware driver software; it has to stay on the system drive.";
    private const string SecuritySoftware = "Security, anti-cheat or network software with drivers that load before other drives.";
    private const string PlatformSuite = "Uses its own servicing and repair, which breaks when its folder moves.";
    private const string Virtualization = "Virtualization software with services and drivers.";

    /// <summary>The built-in list, in match order.</summary>
    public static IReadOnlyList<DenylistEntry> DefaultEntries { get; } =
    [
        new("Visual C++", SharedRuntime),
        new("Redistributable", SharedRuntime),
        new(".NET", SharedRuntime),
        new("Runtime", SharedRuntime),
        new("Windows SDK", SharedRuntime),
        new("WebView2", SharedRuntime),
        new("Driver", DriverSoftware),
        new("Drivers", DriverSoftware),
        new("Chipset", DriverSoftware),
        new("NVIDIA", DriverSoftware),
        new("Realtek", DriverSoftware),
        new("Intel(R)", DriverSoftware),
        new("AMD Software", DriverSoftware),
        new("Anti-Cheat", SecuritySoftware),
        new("AntiCheat", SecuritySoftware, MatchInsideWords: true),
        new("BattlEye", SecuritySoftware),
        new("Vanguard", SecuritySoftware),
        new("VPN", SecuritySoftware, MatchInsideWords: true),
        new("Antivirus", SecuritySoftware),
        new("Defender", SecuritySoftware),
        new("Malwarebytes", SecuritySoftware),
        new("Norton", SecuritySoftware),
        new("McAfee", SecuritySoftware),
        new("Kaspersky", SecuritySoftware),
        new("Bitdefender", SecuritySoftware),
        new("ESET", SecuritySoftware),
        new("Avast", SecuritySoftware),
        new("Microsoft Office", PlatformSuite),
        new("Microsoft 365", PlatformSuite),
        // Visual Studio editions only; "Visual Studio Code" is an ordinary self-updating app.
        new("Visual Studio Community", PlatformSuite),
        new("Visual Studio Professional", PlatformSuite),
        new("Visual Studio Enterprise", PlatformSuite),
        new("Visual Studio Build Tools", PlatformSuite),
        new("Visual Studio Installer", PlatformSuite),
        new("Microsoft Edge", PlatformSuite),
        new("Hyper-V", Virtualization),
        new("VMware", Virtualization),
        new("VirtualBox", Virtualization),
        new("Docker Desktop", Virtualization),
    ];

    private readonly IReadOnlyList<DenylistEntry> _entries;

    /// <summary>Creates the rule with <paramref name="entries"/>, or the built-in list.</summary>
    public DenylistRule(IEnumerable<DenylistEntry>? entries = null)
    {
        _entries = (entries ?? DefaultEntries).ToList();
    }

    /// <inheritdoc />
    public ClassificationVerdict? Classify(InstalledApp app, string installPath)
    {
        ArgumentNullException.ThrowIfNull(app);
        var match = _entries.FirstOrDefault(entry => Mentions(app.Name, entry) || Mentions(app.Publisher, entry));
        return match is null ? null : new ClassificationVerdict(AppMoveOption.Blocked, match.Reason);
    }

    private static bool Mentions(string text, DenylistEntry entry) =>
        entry.MatchInsideWords
            ? text.Contains(entry.Fragment, StringComparison.OrdinalIgnoreCase)
            : ContainsWord(text, entry.Fragment);

    // Whole-word match, so "ESET" does not catch "Preset Manager" and "Driver" does not catch "Drivergen".
    private static bool ContainsWord(string text, string word)
    {
        for (var at = text.IndexOf(word, StringComparison.OrdinalIgnoreCase);
             at >= 0;
             at = text.IndexOf(word, at + 1, StringComparison.OrdinalIgnoreCase))
        {
            if (IsBoundary(text, at - 1) && IsBoundary(text, at + word.Length))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsBoundary(string text, int index) =>
        index < 0 || index >= text.Length || !char.IsLetterOrDigit(text[index]);
}
