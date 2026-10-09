using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Slotik.Data;

namespace Slotik.Services;

public interface IUserWriteLock
{
    Task<IMasterSubscriptionTransaction> AcquireAsync(int userId, CancellationToken ct);
}

// Separate from the bigint master lock and the (-1, userId) booking lock.
public sealed class UserWriteLock(AppDbContext db) : IUserWriteLock
{
    public async Task<IMasterSubscriptionTransaction> AcquireAsync(int userId, CancellationToken ct)
    {
        var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(-2, {userId})", ct);
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
