namespace PurgeKit.Core.Relocation;

/// <summary>Free-space rule shared by relocation and restore checks.</summary>
public static class RelocationCapacity
{
    // Headroom so the receiving drive is not filled to the brim by the move itself.
    private const int HeadroomDivisor = 10;

    /// <summary>Free bytes the receiving drive needs to accept a folder of <paramref name="sizeBytes"/>.</summary>
    public static long RequiredFreeBytes(long sizeBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sizeBytes);
        return sizeBytes + sizeBytes / HeadroomDivisor;
    }
}
