namespace PurgeKit.Core.Relocation.Apps;

/// <summary>How a moved app was moved, which decides how it is moved back.</summary>
public enum MovedAppKind
{
    /// <summary>Moved by PurgeKit; a junction sits at the original path.</summary>
    Junction,

    /// <summary>A Store app moved by Windows' app mover.</summary>
    StorePackage
}
