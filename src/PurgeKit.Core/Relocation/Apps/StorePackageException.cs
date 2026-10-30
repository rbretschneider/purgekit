namespace PurgeKit.Core.Relocation.Apps;

/// <summary>Thrown when Windows cannot list or move a Store app.</summary>
public sealed class StorePackageException : Exception
{
    /// <summary>Creates the exception.</summary>
    public StorePackageException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
