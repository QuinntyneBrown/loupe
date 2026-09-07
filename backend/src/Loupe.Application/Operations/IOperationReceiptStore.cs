namespace Loupe.Application.Operations;

public interface IOperationReceiptStore
{
    Task<Guid> ExecuteAsync(string ownerId, string operationType, string key, string payloadHash,
        Func<CancellationToken, Task<Guid>> create, CancellationToken cancellationToken);
}
