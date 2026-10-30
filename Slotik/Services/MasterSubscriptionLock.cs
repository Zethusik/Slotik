using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Slotik.Data;

namespace Slotik.Services;

public interface IMasterSubscriptionTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct);
}

public interface IMasterSubscriptionLock
{
    Task<IMasterSubscriptionTransaction> AcquireAsync(int masterId, CancellationToken ct);
}

// Uses the same PostgreSQL transaction lock as checkout/trial/admin subscription writers.
// The interface lets isolated tests exercise concurrent requests without a database connection.
public sealed class MasterSubscriptionLock(AppDbContext db) : IMasterSubscriptionLock
{
    public async Task<IMasterSubscriptionTransaction> AcquireAsync(int masterId, CancellationToken ct)
    {
        var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({masterId})", ct);
            return new Lease(transaction);
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    private sealed class Lease(IDbContextTransaction transaction) : IMasterSubscriptionTransaction
    {
        public Task CommitAsync(CancellationToken ct) => transaction.CommitAsync(ct);
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
