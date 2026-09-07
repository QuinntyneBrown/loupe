namespace Loupe.Application.Images;

public interface IImageStore
{
    Task<string> WriteAsync(byte[] content, CancellationToken cancellationToken);
    Stream OpenRead(string key);
    void Delete(string key);
}
