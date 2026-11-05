using Darkspyre.Persist;

using var content = new PausedContent();
await new OsUserPersist(args[0]).SaveAsync(args[1], args[2], content);

// The second read occurs after CopyToAsync writes the first nonzero block to real staging.
internal sealed class PausedContent : Stream
{
    private bool _written;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (!_written)
        {
            int count = Math.Min(buffer.Length, 81920);
            buffer.Span[..count].Fill(0x5a);
            _written = true;
            return count;
        }

        Console.WriteLine("staged");
        await Console.Out.FlushAsync(cancellationToken);
        if (await Console.In.ReadLineAsync(cancellationToken) != "continue")
        {
            throw new IOException("The fixture requires explicit continuation.");
        }

        return 0;
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
