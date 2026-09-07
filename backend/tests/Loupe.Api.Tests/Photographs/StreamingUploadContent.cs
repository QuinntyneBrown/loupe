using System.Net;

namespace Loupe.Api.Tests.Photographs;

public sealed class StreamingUploadContent : HttpContent
{
    private readonly HttpContent inner;
    public StreamingUploadContent(HttpContent inner)
    {
        this.inner = inner;
        foreach (var header in inner.Headers) Headers.TryAddWithoutValidation(header.Key, header.Value);
    }
    protected override bool TryComputeLength(out long length) { length = 0; return false; }
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => inner.CopyToAsync(stream);
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken) => inner.CopyToAsync(stream, cancellationToken);
    protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
}
