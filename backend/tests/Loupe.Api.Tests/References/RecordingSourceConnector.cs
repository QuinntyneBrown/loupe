using System.Net;
using System.Text;
using Loupe.Application.ReferenceImports;

namespace Loupe.Api.Tests.References;

/// <summary>The network observer: records every address the fetcher actually attempts to
/// connect to (and the raw request bytes sent over each), without opening a real socket, and
/// returns a controlled fixture HTTP/1.1 response so tests can prove both that a forbidden
/// address is never attempted and that a permitted one is connected to exactly once, with the
/// expected outbound headers. See L2-040.</summary>
public sealed class RecordingSourceConnector : ISourceConnector
{
    private readonly List<Attempt> attempts = [];
    private readonly Queue<string> responses = new();
    public IReadOnlyList<Attempt> Attempts => attempts;
    public string DefaultResponse { get; set; } = "HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n";

    /// <summary>Queues one response per successive connect attempt, in order (e.g. a redirect
    /// for the first hop, then a page for the second). Falls back to <see cref="DefaultResponse"/>
    /// once exhausted.</summary>
    public void EnqueueResponse(string response) => responses.Enqueue(response);

    public Task<Stream> ConnectAsync(IPAddress address, int port, CancellationToken cancellationToken)
    {
        string response;
        var attempt = new Attempt(address, port);
        lock (attempts) { attempts.Add(attempt); response = responses.Count > 0 ? responses.Dequeue() : DefaultResponse; }
        return Task.FromResult<Stream>(new FixtureResponseStream(response, attempt));
    }

    public sealed class Attempt(IPAddress address, int port)
    {
        public IPAddress Address { get; } = address;
        public int Port { get; } = port;
        private readonly MemoryStream request = new();
        public string RequestText => Encoding.ASCII.GetString(request.ToArray());
        internal void Record(byte[] buffer, int offset, int count) { lock (request) request.Write(buffer, offset, count); }
    }

    /// <summary>Reads a fixed response body regardless of what is written to it, so the
    /// outbound HTTP request and the fixture response don't share one position cursor. What is
    /// written is captured on the matching <see cref="Attempt"/> for header assertions.</summary>
    private sealed class FixtureResponseStream(string response, Attempt attempt) : Stream
    {
        private readonly MemoryStream body = new(Encoding.ASCII.GetBytes(response));
        public override bool CanRead => true;
        public override bool CanWrite => true;
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => body.Read(buffer, offset, count);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            body.ReadAsync(buffer, offset, count, cancellationToken);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            body.ReadAsync(buffer, cancellationToken);
        public override void Write(byte[] buffer, int offset, int count) => attempt.Record(buffer, offset, count);
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        { attempt.Record(buffer, offset, count); return Task.CompletedTask; }
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        { attempt.Record(buffer.ToArray(), 0, buffer.Length); return ValueTask.CompletedTask; }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) body.Dispose(); base.Dispose(disposing); }
    }
}
