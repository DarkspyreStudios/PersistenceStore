namespace Darkspyre.Persistence;

/// <summary>
/// Filesystem-backed persistence rooted in the current user's operating-system application-data directory.
/// </summary>
/// <remarks>
/// The default root is <c>Environment.SpecialFolder.ApplicationData/Darkspyre Studios</c>. The explicit-root
/// constructor supports hosts with a deliberate alternate application-data root and real temporary-filesystem tests.
/// </remarks>
public sealed class OsUserPersistenceStore : IPersistenceStore
{
    private const int CopyBufferSize = 81920;
    private const string OrganizationDirectoryName = "Darkspyre Studios";

    private readonly string _rootDirectory;

    /// <summary>Creates a store rooted in the current user's OS-specific application-data directory.</summary>
    public OsUserPersistenceStore()
        : this(GetDefaultRootDirectory())
    {
    }

    /// <summary>Creates a store beneath an explicit application-data root.</summary>
    /// <param name="rootDirectory">
    /// The directory beneath which named stores are created. The caller retains ownership of the directory.
    /// </param>
    public OsUserPersistenceStore(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        _rootDirectory = Path.GetFullPath(rootDirectory);
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(
        string storeName,
        string assetName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string path = ResolveAssetPath(storeName, assetName);
        return Task.FromResult(File.Exists(path));
    }

    /// <inheritdoc />
    public Task<Stream?> OpenReadAsync(
        string storeName,
        string assetName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string path = ResolveAssetPath(storeName, assetName);

        try
        {
            Stream stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read | FileShare.Delete,
                CopyBufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            return Task.FromResult<Stream?>(stream);
        }
        catch (FileNotFoundException)
        {
            return Task.FromResult<Stream?>(null);
        }
        catch (DirectoryNotFoundException)
        {
            return Task.FromResult<Stream?>(null);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> EnumerateAsync(
        string storeName,
        string assetNamePrefix,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string storeDirectory = ResolveStoreDirectory(storeName);
        string prefix = LogicalName.AssetNamePrefix(assetNamePrefix);
        if (!Directory.Exists(storeDirectory))
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        var options = new EnumerationOptions
        {
            AttributesToSkip = FileAttributes.ReparsePoint,
            IgnoreInaccessible = false,
            RecurseSubdirectories = true,
            ReturnSpecialDirectories = false,
        };

        var names = new List<string>();
        try
        {
            foreach (string path in Directory.EnumerateFiles(storeDirectory, "*", options))
            {
                cancellationToken.ThrowIfCancellationRequested();
                string logicalName = Path.GetRelativePath(storeDirectory, path).Replace('\\', '/');
                if (IsProviderInternal(logicalName) ||
                    !logicalName.StartsWith(prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                names.Add(logicalName);
            }
        }
        catch (DirectoryNotFoundException)
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        names.Sort(StringComparer.Ordinal);
        return Task.FromResult<IReadOnlyList<string>>(names);
    }

    /// <inheritdoc />
    public async Task SaveAsync(
        string storeName,
        string assetName,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (!content.CanRead)
        {
            throw new ArgumentException("The content stream must be readable.", nameof(content));
        }

        cancellationToken.ThrowIfCancellationRequested();
        string storeDirectory = ResolveStoreDirectory(storeName);
        string destinationPath = ResolveAssetPathWithinStore(storeDirectory, assetName);
        string destinationDirectory = Path.GetDirectoryName(destinationPath)!;
        string stagingDirectory = Path.Combine(storeDirectory, LogicalName.InternalDirectoryName);

        Directory.CreateDirectory(destinationDirectory);
        Directory.CreateDirectory(stagingDirectory);

        string stagingPath = Path.Combine(stagingDirectory, $"{Guid.NewGuid():N}.tmp");
        try
        {
            var options = new FileStreamOptions
            {
                Access = FileAccess.Write,
                BufferSize = CopyBufferSize,
                Mode = FileMode.CreateNew,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.WriteThrough,
                Share = FileShare.None,
            };

            await using (var stagingStream = new FileStream(stagingPath, options))
            {
                await content.CopyToAsync(stagingStream, CopyBufferSize, cancellationToken).ConfigureAwait(false);
                await stagingStream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stagingStream.Flush(flushToDisk: true);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(stagingPath, destinationPath, overwrite: true);
        }
        finally
        {
            File.Delete(stagingPath);
        }
    }

    /// <inheritdoc />
    public Task DeleteAsync(
        string storeName,
        string assetName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string path = ResolveAssetPath(storeName, assetName);
        File.Delete(path);
        return Task.CompletedTask;
    }

    private static string GetDefaultRootDirectory()
    {
        string applicationData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(applicationData))
        {
            throw new InvalidOperationException("The operating system did not provide a per-user application-data directory.");
        }

        return Path.Combine(applicationData, OrganizationDirectoryName);
    }

    private string ResolveStoreDirectory(string storeName) =>
        Path.Combine(_rootDirectory, LogicalName.StoreName(storeName));

    private string ResolveAssetPath(string storeName, string assetName) =>
        ResolveAssetPathWithinStore(ResolveStoreDirectory(storeName), assetName);

    private static string ResolveAssetPathWithinStore(string storeDirectory, string assetName)
    {
        string logicalName = LogicalName.AssetName(assetName);
        string localName = logicalName.Replace('/', Path.DirectorySeparatorChar);
        string path = Path.GetFullPath(Path.Combine(storeDirectory, localName));
        string storePrefix = storeDirectory.EndsWith(Path.DirectorySeparatorChar)
            ? storeDirectory
            : storeDirectory + Path.DirectorySeparatorChar;

        if (!path.StartsWith(storePrefix, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException($"Asset '{assetName}' resolves outside store '{Path.GetFileName(storeDirectory)}'.");
        }

        return path;
    }

    private static bool IsProviderInternal(string logicalName) =>
        string.Equals(logicalName, LogicalName.InternalDirectoryName, StringComparison.OrdinalIgnoreCase) ||
        logicalName.StartsWith(LogicalName.InternalDirectoryName + "/", StringComparison.OrdinalIgnoreCase);
}
