namespace Darkspyre.Persist;

/// <summary>Recovers abandoned writes owned by this persistence provider.</summary>
public interface IPersistRecovery
{
    /// <summary>Discards abandoned uncommitted writes in the named store.</summary>
    /// <remarks>Committed assets and live writers are preserved. Completion means recovery has finished.</remarks>
    /// <gotcha>Cancellation, unavailable coordination or deletion failure throws; callers must not report purge complete.</gotcha>
    Task RecoverAsync(string storeName, CancellationToken cancellationToken = default);
}
