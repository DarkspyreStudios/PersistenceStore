namespace Darkspyre.Persist;

/// <summary>
/// Stores complete opaque assets addressed by a store name and a logical asset name.
/// </summary>
/// <remarks>
/// Implementations must isolate stores, prevent logical names from escaping their store, and replace
/// an asset atomically. This contract stores bytes only: it does not serialize application types,
/// expose backend paths, or provide browser-delivery URLs.
/// </remarks>
public interface IPersistStore
{
    /// <summary>Determines whether an asset exists.</summary>
    Task<bool> ExistsAsync(
        string storeName,
        string assetName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens an asset for reading, or returns <see langword="null"/> when it does not exist.
    /// The caller owns the returned stream and must not assume that it is seekable.
    /// </summary>
    Task<Stream?> OpenReadAsync(
        string storeName,
        string assetName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the logical names of all assets whose names begin with <paramref name="assetNamePrefix"/>.
    /// An empty prefix enumerates the complete store.
    /// </summary>
    Task<IReadOnlyList<string>> EnumerateAsync(
        string storeName,
        string assetNamePrefix,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or atomically replaces an asset with the bytes from the current position of
    /// <paramref name="content"/>. The input stream remains owned by the caller.
    /// </summary>
    Task SaveAsync(
        string storeName,
        string assetName,
        Stream content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an asset. Deleting an asset that does not exist succeeds, including when its store
    /// or parent directories do not exist. Deletion does not create missing directories.
    /// </summary>
    Task DeleteAsync(
        string storeName,
        string assetName,
        CancellationToken cancellationToken = default);
}
