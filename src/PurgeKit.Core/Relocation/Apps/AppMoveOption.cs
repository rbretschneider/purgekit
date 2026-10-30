namespace PurgeKit.Core.Relocation.Apps;

/// <summary>How (or whether) an installed app can be moved to another drive.</summary>
public enum AppMoveOption
{
    /// <summary>PurgeKit can move it and leave a junction behind.</summary>
    Movable,

    /// <summary>A Microsoft Store app; Windows' own app mover relocates it.</summary>
    StoreApp,

    /// <summary>A game library owned by a launcher, which must do the move itself.</summary>
    UseLauncher,

    /// <summary>Moving it would break Windows, drivers or shared components.</summary>
    Blocked
}
