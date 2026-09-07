using Loupe.Application.Images;

namespace Loupe.Api.Uploads;

public sealed class UploadBodyStream(Stream inner) : Stream
{
    // The HTTP request owns the underlying stream; this wrapper leaves it open.
    private long consumed;
    public override bool CanRead => inner.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override int Read(byte[] buffer, int offset, int count)
    {
        try { return Count(inner.Read(buffer, offset, count)); }
        catch (BadHttpRequestException exception) when (exception.StatusCode == 413)
        { throw new ImageValidationException(ImageFailure.TooLarge); }
    }
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        try { return Count(await inner.ReadAsync(buffer, cancellationToken)); }
        catch (BadHttpRequestException exception) when (exception.StatusCode == 413)
        { throw new ImageValidationException(ImageFailure.TooLarge); }
    }
    private int Count(int read)
    {
        consumed += read;
        if (consumed > UploadLimits.RequestBytes) throw new ImageValidationException(ImageFailure.TooLarge);
        return read;
    }
}
