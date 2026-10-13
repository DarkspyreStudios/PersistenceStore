using System.Text;

namespace Darkspyre.Persist;

/// <summary>Convenience operations over the stream-based persistence contract.</summary>
public static class PersistStoreExtensions
{
    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>Saves a complete in-memory asset.</summary>
    public static async Task SaveAsync(
        this IPersist store,
        string storeName,
        string assetName,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        using var stream = new MemoryStream(content.ToArray(), writable: false);
        await store.SaveAsync(storeName, assetName, stream, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Saves a string as UTF-8 without a byte-order mark.</summary>
    public static Task SaveStringAsync(
        this IPersist store,
        string storeName,
        string assetName,
        string content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(content);
        return store.SaveAsync(storeName, assetName, Utf8.GetBytes(content), cancellationToken);
    }

    /// <summary>Reads a complete asset into memory, or returns <see langword="null"/> when it is missing.</summary>
    public static async Task<byte[]?> ReadBytesAsync(
        this IPersist store,
        string storeName,
        string assetName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        await using Stream? stream = await store.OpenReadAsync(storeName, assetName, cancellationToken).ConfigureAwait(false);
        if (stream is null)
        {
            return null;
        }

        using var destination = new MemoryStream();
        await stream.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
        return destination.ToArray();
    }

    /// <summary>Reads a complete UTF-8 asset, or returns <see langword="null"/> when it is missing.</summary>
    public static async Task<string?> ReadStringAsync(
        this IPersist store,
        string storeName,
        string assetName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        await using Stream? stream = await store.OpenReadAsync(storeName, assetName, cancellationToken).ConfigureAwait(false);
        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(
            stream,
            Utf8,
            detectEncodingFromByteOrderMarks: true,
            leaveOpen: false);
        return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
    }
}
