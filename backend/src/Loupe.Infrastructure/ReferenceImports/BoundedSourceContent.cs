namespace Loupe.Infrastructure.ReferenceImports;

public static class BoundedSourceContent
{
    public static async Task<byte[]> ReadAsync(HttpContent content, int limit, CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength > limit) throw new InvalidDataException("Source response exceeds the size limit.");
        await using var input = await content.ReadAsStreamAsync(cancellationToken);
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        while (true)
        {
            var count = await input.ReadAsync(buffer, cancellationToken);
            if (count == 0) return output.ToArray();
            if (output.Length + count > limit) throw new InvalidDataException("Source response exceeds the size limit.");
            await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
        }
    }
}
