using Loupe.Application.Images;
using Microsoft.Extensions.Options;

namespace Loupe.Infrastructure.Images;

public sealed class FileImageStore(IOptions<MediaOptions> options) : IImageStore
{
    public async Task<string> WriteAsync(byte[] content, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(options.Value.Root);
        var key = Guid.NewGuid().ToString("N");
        var destination = Resolve(key);
        var staging = destination + ".partial";
        try
        {
            await File.WriteAllBytesAsync(staging, content, cancellationToken);
            File.Move(staging, destination);
            return key;
        }
        catch
        {
            File.Delete(staging);
            throw;
        }
    }

    public Stream OpenRead(string key) => File.OpenRead(Resolve(key));
    public void Delete(string key) => File.Delete(Resolve(key));
    private string Resolve(string key)
    {
        if (key.Length != 32 || !key.All(Uri.IsHexDigit)) throw new InvalidOperationException("Invalid managed media key.");
        return Path.Combine(options.Value.Root, key);
    }
}
