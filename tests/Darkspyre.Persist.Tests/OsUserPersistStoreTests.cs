using System.Text;
using Darkspyre.Persist;

namespace Darkspyre.Persist.Tests;

public sealed class OsUserPersistStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"darkspyre-persistence-{Guid.NewGuid():N}");
    private readonly OsUserPersistStore _store;

    public OsUserPersistStoreTests()
    {
        _store = new OsUserPersistStore(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAndOpenReadRoundTripStreamBytesWithoutDisposingTheInput()
    {
        var content = new MemoryStream(Bytes("hello persistence"));

        await _store.SaveAsync("ExampleApp", "documents/note.txt", content);

        Assert.True(content.CanRead);
        Assert.Equal("hello persistence", await ReadTextAsync("ExampleApp", "documents/note.txt"));
    }

    [Fact]
    public async Task DefaultRootCreatesAMissingApplicationDataDirectory()
    {
        // On Linux, .NET resolves SpecialFolder.ApplicationData to HOME/.config when XDG_CONFIG_HOME is unset, and
        // reads both variables on each call. macOS and Windows resolve it through the operating system, which a test
        // cannot redirect in-process.
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        string home = Path.Combine(_root, "home");
        Directory.CreateDirectory(home);
        string? previousHome = Environment.GetEnvironmentVariable("HOME");
        string? previousXdgConfigHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        Environment.SetEnvironmentVariable("HOME", home);
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", null);
        try
        {
            string applicationData = Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData,
                Environment.SpecialFolderOption.DoNotVerify);
            Assert.StartsWith(home + Path.DirectorySeparatorChar, applicationData, StringComparison.Ordinal);
            Assert.False(Directory.Exists(applicationData));

            var store = new OsUserPersistStore();
            await store.SaveAsync("ExampleApp", "created.txt", new MemoryStream(Bytes("created")));

            Assert.True(File.Exists(Path.Combine(applicationData, "Darkspyre Studios", "ExampleApp", "created.txt")));
        }
        finally
        {
            Environment.SetEnvironmentVariable("HOME", previousHome);
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", previousXdgConfigHome);
        }
    }

    [Fact]
    public async Task SaveReadsFromTheInputsCurrentPosition()
    {
        var content = new MemoryStream(Bytes("skip:kept"));
        content.Position = 5;

        await _store.SaveAsync("ExampleApp", "position.txt", content);

        Assert.Equal("kept", await ReadTextAsync("ExampleApp", "position.txt"));
    }

    [Fact]
    public async Task SaveCreatesNestedDirectoriesAndReplacesAnExistingAsset()
    {
        await SaveTextAsync("ExampleApp", "deep/nested/asset.txt", "first");
        await SaveTextAsync("ExampleApp", "deep/nested/asset.txt", "second");

        Assert.Equal("second", await ReadTextAsync("ExampleApp", "deep/nested/asset.txt"));
        Assert.True(File.Exists(Path.Combine(_root, "ExampleApp", "deep", "nested", "asset.txt")));
    }

    [Fact]
    public async Task MissingAndEmptyAssetsAreDistinct()
    {
        Assert.Null(await _store.OpenReadAsync("ExampleApp", "missing.bin"));

        await _store.SaveAsync("ExampleApp", "empty.bin", ReadOnlyMemory<byte>.Empty);
        await using Stream? empty = await _store.OpenReadAsync("ExampleApp", "empty.bin");

        Assert.NotNull(empty);
        Assert.Equal(-1, empty.ReadByte());
    }

    [Fact]
    public async Task ExistsReportsTheAssetsCurrentState()
    {
        Assert.False(await _store.ExistsAsync("ExampleApp", "state.bin"));

        await _store.SaveAsync("ExampleApp", "state.bin", new byte[] { 1, 2, 3 });
        Assert.True(await _store.ExistsAsync("ExampleApp", "state.bin"));

        await _store.DeleteAsync("ExampleApp", "state.bin");
        Assert.False(await _store.ExistsAsync("ExampleApp", "state.bin"));
    }

    [Fact]
    public async Task StoresAreIsolatedByStoreName()
    {
        await SaveTextAsync("StoreOne", "same-name.txt", "one");
        await SaveTextAsync("StoreTwo", "same-name.txt", "two");

        Assert.Equal("one", await ReadTextAsync("StoreOne", "same-name.txt"));
        Assert.Equal("two", await ReadTextAsync("StoreTwo", "same-name.txt"));
    }

    [Fact]
    public async Task EnumerateFiltersRecursivelyAndOrdersLogicalNamesOrdinally()
    {
        await SaveTextAsync("ExampleApp", "gallery/z-last.txt", "z");
        await SaveTextAsync("ExampleApp", "gallery/a-first.txt", "a");
        await SaveTextAsync("ExampleApp", "gallery/nested/middle.txt", "m");
        await SaveTextAsync("ExampleApp", "other/not-in-prefix.txt", "x");

        IReadOnlyList<string> names = await _store.EnumerateAsync("ExampleApp", "gallery/");

        Assert.Equal(
            ["gallery/a-first.txt", "gallery/nested/middle.txt", "gallery/z-last.txt"],
            names);
    }

    [Fact]
    public async Task EnumerateWithAnEmptyPrefixListsTheCompleteStoreButNotProviderStaging()
    {
        await SaveTextAsync("ExampleApp", "one.txt", "1");
        await SaveTextAsync("ExampleApp", "nested/two.txt", "2");

        IReadOnlyList<string> names = await _store.EnumerateAsync("ExampleApp", string.Empty);

        Assert.Equal(["nested/two.txt", "one.txt"], names);
        Assert.DoesNotContain(names, name => name.Contains("darkspyre-persistence", StringComparison.Ordinal));
    }

    [Fact]
    public async Task EnumerateOfAMissingStoreIsEmptyAndDoesNotCreateTheStore()
    {
        Assert.Empty(await _store.EnumerateAsync("MissingStore", string.Empty));
        Assert.False(Directory.Exists(Path.Combine(_root, "MissingStore")));
    }

    [Fact]
    public async Task DeleteIsIdempotent()
    {
        await SaveTextAsync("ExampleApp", "delete-me.txt", "bye");

        await _store.DeleteAsync("ExampleApp", "delete-me.txt");
        await _store.DeleteAsync("ExampleApp", "delete-me.txt");

        Assert.Null(await _store.OpenReadAsync("ExampleApp", "delete-me.txt"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task DeleteMissingAssetSucceedsWithoutCreatingAncestors(int existingDirectoryDepth)
    {
        string[] ancestors =
        [
            _root,
            Path.Combine(_root, "ExampleApp"),
            Path.Combine(_root, "ExampleApp", "nested"),
        ];
        if (existingDirectoryDepth > 0)
        {
            Directory.CreateDirectory(ancestors[existingDirectoryDepth - 1]);
        }

        bool[] existedBefore = ancestors.Select(Directory.Exists).ToArray();

        await _store.DeleteAsync("ExampleApp", "nested/missing.bin");
        await _store.DeleteAsync("ExampleApp", "nested/missing.bin");

        Assert.Equal(existedBefore, ancestors.Select(Directory.Exists));
        Assert.False(await _store.ExistsAsync("ExampleApp", "nested/missing.bin"));
    }

    [Fact]
    public async Task DeleteDoesNotSwallowErrorsForADirectoryAtTheAssetPath()
    {
        string directory = Path.Combine(_root, "ExampleApp", "not-an-asset");
        Directory.CreateDirectory(directory);
        string retained = Path.Combine(directory, "retained.txt");
        await File.WriteAllTextAsync(retained, "keep");

        Exception? failure = await Record.ExceptionAsync(() =>
            _store.DeleteAsync("ExampleApp", "not-an-asset"));

        Assert.True(failure is IOException or UnauthorizedAccessException,
            $"Expected a filesystem error, got {failure?.GetType().Name ?? "success"}.");
        Assert.Equal("keep", await File.ReadAllTextAsync(retained));
    }

    [Fact]
    public async Task CancelledDeletePreservesAnExistingAsset()
    {
        await SaveTextAsync("ExampleApp", "keep.txt", "keep");
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _store.DeleteAsync("ExampleApp", "keep.txt", source.Token));

        Assert.Equal("keep", await ReadTextAsync("ExampleApp", "keep.txt"));
    }

    [Fact]
    public async Task AFailedReplacementPreservesThePreviousCompleteAssetAndRemovesStagingData()
    {
        await SaveTextAsync("ExampleApp", "stable.txt", "original");
        await using var failing = new FailingReadStream();

        await Assert.ThrowsAsync<IOException>(
            () => _store.SaveAsync("ExampleApp", "stable.txt", failing));

        Assert.Equal("original", await ReadTextAsync("ExampleApp", "stable.txt"));
        Assert.Equal(["stable.txt"], await _store.EnumerateAsync("ExampleApp", string.Empty));
        Assert.Empty(Directory.EnumerateFiles(
            Path.Combine(_root, "ExampleApp", ".darkspyre-persistence"),
            "*",
            SearchOption.AllDirectories));
    }

    [Fact]
    public async Task ConcurrentReplacementsLeaveOneCompletePayload()
    {
        byte[] first = Enumerable.Repeat((byte)0x11, 512 * 1024).ToArray();
        byte[] second = Enumerable.Repeat((byte)0x22, 512 * 1024).ToArray();

        await Task.WhenAll(
            _store.SaveAsync("ExampleApp", "race.bin", first),
            _store.SaveAsync("ExampleApp", "race.bin", second));

        byte[] saved = Assert.IsType<byte[]>(await _store.ReadBytesAsync("ExampleApp", "race.bin"));
        Assert.True(saved.SequenceEqual(first) || saved.SequenceEqual(second));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("../escape")]
    [InlineData("nested/store")]
    [InlineData("nested\\store")]
    [InlineData("CON")]
    public async Task InvalidStoreNamesAreRejected(string storeName)
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => _store.ExistsAsync(storeName, "asset.bin"));
        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => _store.DeleteAsync(storeName, "asset.bin"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("/absolute.txt")]
    [InlineData("../escape.txt")]
    [InlineData("nested/../escape.txt")]
    [InlineData("nested//empty.txt")]
    [InlineData("nested\\windows.txt")]
    [InlineData(".darkspyre-persistence/internal.tmp")]
    [InlineData("CON.txt")]
    public async Task InvalidAssetNamesAreRejected(string assetName)
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => _store.SaveAsync("ExampleApp", assetName, new byte[] { 1 }));
        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => _store.DeleteAsync("ExampleApp", assetName));
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("/absolute")]
    [InlineData("nested/../escape")]
    [InlineData("nested//empty")]
    [InlineData(".darkspyre-persistence/")]
    public async Task InvalidEnumerationPrefixesAreRejected(string prefix)
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => _store.EnumerateAsync("ExampleApp", prefix));
    }

    [Fact]
    public async Task ByteAndUtf8ConvenienceOperationsRoundTripAndPreserveMissingSemantics()
    {
        await _store.SaveStringAsync("ExampleApp", "text.txt", "Snowman: ☃");
        await _store.SaveAsync("ExampleApp", "bytes.bin", new byte[] { 4, 5, 6 });

        Assert.Equal("Snowman: ☃", await _store.ReadStringAsync("ExampleApp", "text.txt"));
        Assert.Equal(new byte[] { 4, 5, 6 }, await _store.ReadBytesAsync("ExampleApp", "bytes.bin"));
        Assert.Null(await _store.ReadStringAsync("ExampleApp", "missing.txt"));
        Assert.Null(await _store.ReadBytesAsync("ExampleApp", "missing.bin"));
    }

    [Fact]
    public async Task OperationsHonorAnAlreadyCancelledToken()
    {
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _store.ExistsAsync("ExampleApp", "asset.bin", source.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _store.OpenReadAsync("ExampleApp", "asset.bin", source.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _store.EnumerateAsync("ExampleApp", string.Empty, source.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _store.SaveAsync("ExampleApp", "asset.bin", new byte[] { 1 }, source.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _store.DeleteAsync("ExampleApp", "asset.bin", source.Token));
    }

    private async Task SaveTextAsync(string storeName, string assetName, string content)
    {
        using var stream = new MemoryStream(Bytes(content));
        await _store.SaveAsync(storeName, assetName, stream);
    }

    private async Task<string> ReadTextAsync(string storeName, string assetName)
    {
        await using Stream? stream = await _store.OpenReadAsync(storeName, assetName);
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    private static byte[] Bytes(string value) => Encoding.UTF8.GetBytes(value);

    private sealed class FailingReadStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new IOException("Deterministic source read failure.");

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromException<int>(new IOException("Deterministic source read failure."));

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
