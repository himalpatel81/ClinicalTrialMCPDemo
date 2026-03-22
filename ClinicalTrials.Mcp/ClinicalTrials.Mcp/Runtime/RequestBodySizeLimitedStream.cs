namespace ClinicalTrials.Mcp.Runtime;

internal sealed class RequestBodySizeLimitedStream(Stream innerStream, long maxBytes) : Stream
{
    private long bytesRead;

    public override bool CanRead => innerStream.CanRead;

    public override bool CanSeek => innerStream.CanSeek;

    public override bool CanWrite => false;

    public override long Length => innerStream.Length;

    public override long Position
    {
        get => innerStream.Position;
        set => innerStream.Position = value;
    }

    public override void Flush() => innerStream.Flush();

    public override int Read(byte[] buffer, int offset, int count)
    {
        var bytes = innerStream.Read(buffer, offset, count);
        TrackBytes(bytes);

        return bytes;
    }

    public override int Read(Span<byte> buffer)
    {
        var bytes = innerStream.Read(buffer);
        TrackBytes(bytes);

        return bytes;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var bytes = await innerStream.ReadAsync(buffer, cancellationToken);
        TrackBytes(bytes);

        return bytes;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        var bytes = await innerStream.ReadAsync(buffer.AsMemory(offset, count), cancellationToken);
        TrackBytes(bytes);

        return bytes;
    }

    public override long Seek(long offset, SeekOrigin origin) => innerStream.Seek(offset, origin);

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override void Write(ReadOnlySpan<byte> buffer) => throw new NotSupportedException();

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    private void TrackBytes(int bytesJustRead)
    {
        if (bytesJustRead <= 0)
        {
            return;
        }

        bytesRead += bytesJustRead;
        if (bytesRead > maxBytes)
        {
            throw new RequestBodyTooLargeException(maxBytes);
        }
    }
}
