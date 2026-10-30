namespace PurgeKit.Core.Relocation.Apps;

/// <summary>Lists Store apps without letting a Store failure hide everything else on screen.</summary>
internal static class StorePackageListing
{
    public static async Task<ListResult<StorePackage>> TryListAsync(IStorePackageCatalog store, CancellationToken ct)
    {
        try
        {
            return new ListResult<StorePackage>(await store.ListPackagesAsync(ct).ConfigureAwait(false));
        }
        catch (StorePackageException ex)
        {
            // Handled: Store apps are left out and the reason travels to the user as a visible warning.
            return new ListResult<StorePackage>([], $"Microsoft Store apps could not be listed: {ex.Message}");
        }
    }
}
