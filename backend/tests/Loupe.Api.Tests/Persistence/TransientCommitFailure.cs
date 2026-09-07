using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace Loupe.Api.Tests.Persistence;

public sealed class TransientCommitFailure(bool afterCommit) : DbTransactionInterceptor
{
    private bool armed;
    public void Arm() => armed = true;
    public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
        TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
    {
        Fail(false);
        return ValueTask.FromResult(result);
    }
    public override Task TransactionCommittedAsync(DbTransaction transaction,
        TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        Fail(true);
        return Task.CompletedTask;
    }
    private void Fail(bool committed)
    {
        if (!armed || committed != afterCommit) return;
        armed = false;
        throw new NpgsqlException("Private backend diagnostic for controlled failure.", new IOException());
    }
}
