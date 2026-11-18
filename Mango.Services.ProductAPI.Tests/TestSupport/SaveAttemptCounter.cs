using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Mango.Services.ProductAPI.Tests.TestSupport;

internal sealed class SaveAttemptCounter : SaveChangesInterceptor
{
    private int _attempts;
    public int Attempts => Volatile.Read(ref _attempts);

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Interlocked.Increment(ref _attempts);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _attempts);
        return ValueTask.FromResult(result);
    }
}
