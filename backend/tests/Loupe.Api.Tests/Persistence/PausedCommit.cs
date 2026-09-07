using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Loupe.Api.Tests.Persistence;

public sealed class PausedCommit : DbTransactionInterceptor
{
    private readonly TaskCompletionSource reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource resumed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int armed;
    public Task Reached => reached.Task;
    public void Arm() => Interlocked.Exchange(ref armed, 1);
    public void Resume() => resumed.TrySetResult();
    public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
        TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref armed, 0) == 1)
        {
            reached.TrySetResult();
            await resumed.Task.WaitAsync(cancellationToken);
        }
        return result;
    }
}
