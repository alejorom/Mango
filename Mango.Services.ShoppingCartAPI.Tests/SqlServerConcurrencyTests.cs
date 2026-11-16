using System.Data.Common;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Mango.MessageBus.Service.IService;
using Mango.Services.ShoppingCartAPI;
using Mango.Services.ShoppingCartAPI.Controllers;
using Mango.Services.ShoppingCartAPI.Data;
using Mango.Services.ShoppingCartAPI.Models;
using Mango.Services.ShoppingCartAPI.Models.Dto;
using Mango.Services.ShoppingCartAPI.Service.IService;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using Xunit;
using Xunit.Abstractions;

namespace Mango.Services.ShoppingCartAPI.Tests;

public class SqlServerConcurrencyTests(ITestOutputHelper output) : IAsyncLifetime
{
    private const string Lab = "Mango_ShoppingCart_Concurrency_Test";
    private const string Machine = "22LAP5CD1195RMS";
    private readonly List<AppDbContext> _contexts = [];
    private readonly List<Task> _operations = [];
    private readonly List<IPause> _pauses = [];
    private readonly CancellationTokenSource _stop = new();
    private static readonly List<AppDbContext> QuarantinedContexts = [];
    private static bool _blocked;
    private string _testName = "harness";

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (var pause in _pauses) pause.Continue.TrySetResult();
        _stop.Cancel();
        try
        {
            if (_blocked) throw new InvalidOperationException("Laboratory resources are quarantined; isolated host termination required.");
            await Drain();
        }
        catch
        {
            lock (QuarantinedContexts) QuarantinedContexts.AddRange(_contexts);
            throw;
        }
        try
        {
            foreach (var context in _contexts) await context.DisposeAsync();
        }
        catch (Exception ex)
        {
            _blocked = true;
            Report("dispose", ex);
            throw;
        }
        finally { _stop.Dispose(); }
        var failures = _operations.Where(task => task.IsFaulted).SelectMany(task => task.Exception!.InnerExceptions).ToArray();
        if (failures.Length != 0) throw new AggregateException("Concurrent operation failures", failures);
    }

    private T Register<T>(T pause) where T : IPause
    {
        _pauses.Add(pause);
        pause.Stop = _stop.Token;
        return pause;
    }

    private static async Task WaitAtBarrier(Task barrier, CancellationToken commandToken, CancellationToken stop)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(commandToken, stop);
        await barrier.WaitAsync(TimeSpan.FromSeconds(10), linked.Token);
    }

    private Task<T> Track<T>(Task<T> task)
    {
        _operations.Add(task);
        _ = task.ContinueWith(completed => { _ = completed.Exception; }, CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        return task;
    }

    private void Report(string phase, Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            output.WriteLine("Test={0}; Phase={1}; Exception={2}; SqlNumber={3}; Stack={4}",
                _testName, phase, current.GetType().FullName,
                current is SqlException sql ? sql.Number : null, current.StackTrace ?? "unavailable");
        }
    }

    [Fact]
    public async Task HarnessDrainReleasesBarrierAndWaitsForCompletion()
    {
        _testName = nameof(HarnessDrainReleasesBarrierAndWaitsForCompletion);
        var pause = Register(new PauseCartRead());
        async Task<int> Operation() { await pause.Continue.Task; return 1; }
        var operation = Track(Operation());
        await Drain();
        Assert.True(operation.IsCompletedSuccessfully);
        Assert.Equal(1, await operation);
    }

    [Fact]
    public async Task HarnessReportsSecondaryFaultInsteadOfSilentlyPassing()
    {
        var harness = new SqlServerConcurrencyTests(output);
        harness._testName = nameof(HarnessReportsSecondaryFaultInsteadOfSilentlyPassing);
        _ = harness.Track(Task.FromException<int>(new InvalidOperationException("Synthetic secondary failure")));
        await harness.Drain();
        var failure = await Assert.ThrowsAsync<AggregateException>(harness.DisposeAsync);
        Assert.IsType<InvalidOperationException>(Assert.Single(failure.InnerExceptions));
    }

    private async Task Cleanup(AppDbContext context, params string[] owners)
    {
        await Drain();
        await context.CartHeaders.Where(header => owners.Contains(header.UserId!)).ExecuteDeleteAsync();
    }

    [Fact]
    public async Task ConcurrentSameProductPreservesCommittedWrites()
    {
        var owner = Guid.NewGuid().ToString();
        var firstPause = Register(new PauseCartRead());
        var secondPause = Register(new PauseCartRead());
        var seed = await OpenLab();
        var first = await OpenLab(firstPause);
        var second = await OpenLab(secondPause);
        Task? firstTask = null;
        Task? secondTask = null;
        try
        {
            seed.CartHeaders.Add(new CartHeader { UserId = owner });
            await seed.SaveChangesAsync();
            CartDto Request() => new() { CartHeader = new CartHeaderDto { UserId = owner }, CartDetails = new[] { new CartDetailsDto { ProductId = 7, Count = 1 } } };
            var a = Track(Controller(first, owner).CartUpsert(Request()));
            firstTask = a;
            var b = Track(Controller(second, owner).CartUpsert(Request()));
            secondTask = b;
            await Task.WhenAll(firstPause.Started.Task, secondPause.Started.Task).WaitAsync(TimeSpan.FromSeconds(5));
            firstPause.Continue.TrySetResult();
            secondPause.Continue.TrySetResult();
            var results = await Task.WhenAll(a, b).WaitAsync(TimeSpan.FromSeconds(15));
            Assert.All(results, result => Assert.True(result.Value is ResponseDto { IsSuccess: true }));
            var details = await seed.CartDetails.AsNoTracking().Where(x => x.CartHeader.UserId == owner && x.ProductId == 7).ToListAsync();
            Assert.Equal(2, details.Sum(x => x.Count));
        }
        finally
        {
            firstPause.Continue.TrySetResult();
            secondPause.Continue.TrySetResult();
            await Drain(firstTask, secondTask);
            await Cleanup(seed, owner);
        }
    }

    [Fact]
    public async Task UpsertFailureBeforeCommitRollsBackPersistedWrites()
    {
        var owner = Guid.NewGuid().ToString();
        var failure = new FailCommit();
        var db = await OpenLab(failure);
        var verify = await OpenLab();
        try
        {
            var request = new CartDto { CartHeader = new CartHeaderDto { UserId = owner }, CartDetails = new[] { new CartDetailsDto { ProductId = 7, Count = 1 } } };
            var result = await Controller(db, owner).CartUpsert(request);
            Assert.True(failure.Reached);
            Assert.Equal(500, Assert.IsType<ObjectResult>(result.Result).StatusCode);
            Assert.False(await verify.CartHeaders.AsNoTracking().AnyAsync(x => x.UserId == owner));
            Assert.False(await verify.CartDetails.AsNoTracking().AnyAsync(x => x.CartHeader.UserId == owner));
        }
        finally { await Cleanup(verify, owner); }
    }

    [Fact]
    public async Task RemovalFailureBeforeCommitRestoresHeaderAndDetail()
    {
        var owner = Guid.NewGuid().ToString();
        var failure = new FailCommit();
        var seed = await OpenLab();
        var db = await OpenLab(failure);
        try
        {
            var item = new CartDetails { CartHeader = new CartHeader { UserId = owner }, ProductId = 7, Count = 1 };
            seed.CartDetails.Add(item);
            await seed.SaveChangesAsync();
            var result = await Controller(db, owner).RemoveCart(item.CartDetailsId);
            Assert.True(failure.Reached);
            Assert.Equal(500, Assert.IsType<ObjectResult>(result.Result).StatusCode);
            Assert.True(await seed.CartHeaders.AsNoTracking().AnyAsync(x => x.CartHeaderId == item.CartHeaderId && x.UserId == owner));
            Assert.True(await seed.CartDetails.AsNoTracking().AnyAsync(x => x.CartDetailsId == item.CartDetailsId && x.Count == 1));
        }
        finally { await Cleanup(seed, owner); }
    }

    private sealed class FailCommit : DbTransactionInterceptor
    {
        public bool Reached { get; private set; }
        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            Reached = true;
            throw new InvalidOperationException("Synthetic failure before commit");
        }
    }

    [Fact]
    public async Task LastItemDeletionAndConcurrentInsertNeverLoseSuccessfulInsert()
    {
        var owner = Guid.NewGuid().ToString();
        var pause = Register(new PauseDetailDelete());
        var seed = await OpenLab();
        var removal = await OpenLab(pause);
        var competitor = await OpenLab();
        using var cancellation = new CancellationTokenSource();
        Task? removeTask = null;
        Task? insertTask = null;
        try
        {
            var item = new CartDetails { CartHeader = new CartHeader { UserId = owner }, ProductId = 1, Count = 1 };
            seed.CartDetails.Add(item);
            await seed.SaveChangesAsync();
            var remove = Track(Controller(removal, owner).RemoveCart(item.CartDetailsId));
            removeTask = remove;
            await pause.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            competitor.CartDetails.Add(new CartDetails { CartHeaderId = item.CartHeaderId, ProductId = 2, Count = 1 });
            var insert = Track(competitor.SaveChangesAsync(cancellation.Token));
            insertTask = insert;
            try { Assert.NotSame(insert, await Task.WhenAny(insert, Task.Delay(300))); }
            finally { pause.Continue.TrySetResult(); }
            var removeResult = await remove.WaitAsync(TimeSpan.FromSeconds(15));
            var inserted = await insert.WaitAsync(TimeSpan.FromSeconds(15)) == 1;
            var succeeded = removeResult.Value is ResponseDto { IsSuccess: true };
            Assert.True(succeeded);
            if (inserted) Assert.True(await seed.CartDetails.AsNoTracking().AnyAsync(x => x.CartHeaderId == item.CartHeaderId && x.ProductId == 2));
            if (!succeeded) Assert.True(await seed.CartDetails.AsNoTracking().AnyAsync(x => x.CartDetailsId == item.CartDetailsId));
        }
        finally
        {
            pause.Continue.TrySetResult();
            cancellation.Cancel();
            await Drain(removeTask, insertTask);
            await Cleanup(seed, owner);
        }
    }

    [Fact]
    public async Task OwnershipChangeWaitsUntilUpsertCompletes()
    {
        var owner = Guid.NewGuid().ToString();
        var other = Guid.NewGuid().ToString();
        var pause = Register(new PauseCartRead());
        var seed = await OpenLab();
        var upsert = await OpenLab(pause);
        var competitor = await OpenLab();
        Task? actionTask = null;
        Task? transferTask = null;
        try
        {
            seed.CartHeaders.Add(new CartHeader { UserId = owner });
            await seed.SaveChangesAsync();
            var request = new CartDto { CartHeader = new CartHeaderDto { UserId = owner }, CartDetails = new[] { new CartDetailsDto { ProductId = 42, Count = 1 } } };
            var action = Track(Controller(upsert, owner).CartUpsert(request));
            actionTask = action;
            await pause.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var transfer = Track(competitor.CartHeaders.Where(x => x.UserId == owner).ExecuteUpdateAsync(s => s.SetProperty(x => x.UserId, other), _stop.Token));
            transferTask = transfer;
            try
            {
                Assert.NotSame(transfer, await Task.WhenAny(transfer, Task.Delay(300)));
            }
            finally { pause.Continue.TrySetResult(); }
            Assert.True(Assert.IsType<ResponseDto>((await action.WaitAsync(TimeSpan.FromSeconds(15))).Value).IsSuccess);
            Assert.Equal(1, await transfer.WaitAsync(TimeSpan.FromSeconds(15)));
            Assert.True(await seed.CartDetails.AsNoTracking().AnyAsync(x => x.ProductId == 42 && x.CartHeader.UserId == other));
        }
        finally
        {
            pause.Continue.TrySetResult();
            await Drain(actionTask, transferTask);
            await Cleanup(seed, owner, other);
        }
    }

    [Fact]
    public async Task RollbackOfFailedUpsertLeavesNoHeaderOrDetail()
    {
        var owner = Guid.NewGuid().ToString();
        var db = await OpenLab(new FailInsert());
        var verify = await OpenLab();
        try
        {
            var request = new CartDto { CartHeader = new CartHeaderDto { UserId = owner }, CartDetails = new[] { new CartDetailsDto { ProductId = 7, Count = 1 } } };
            var result = await Controller(db, owner).CartUpsert(request);
            Assert.Equal(500, Assert.IsType<ObjectResult>(result.Result).StatusCode);
            Assert.False(await verify.CartHeaders.AnyAsync(x => x.UserId == owner));
        }
        finally { await Cleanup(verify, owner); }
    }

    [Fact]
    public async Task OwnerCanCreateIncrementApplyCouponAndRemoveLastItem()
    {
        var owner = Guid.NewGuid().ToString();
        var db = await OpenLab();
        try
        {
            CartDto Request(int count) => new() { CartHeader = new CartHeaderDto { UserId = owner }, CartDetails = new[] { new CartDetailsDto { ProductId = 7, Count = count } } };
            Assert.True(Assert.IsType<ResponseDto>((await Controller(db, owner).CartUpsert(Request(1))).Value).IsSuccess);
            db.ChangeTracker.Clear();
            Assert.True(Assert.IsType<ResponseDto>((await Controller(db, owner).CartUpsert(Request(2))).Value).IsSuccess);
            db.ChangeTracker.Clear();
            var item = await db.CartDetails.AsNoTracking().SingleAsync(x => x.CartHeader.UserId == owner);
            Assert.Equal(3, item.Count);
            Assert.True(Assert.IsType<ResponseDto>((await Controller(db, owner).ApplyCoupon(new CartDto { CartHeader = new CartHeaderDto { UserId = owner, CouponCode = "TEST" } })).Value).IsSuccess);
            db.ChangeTracker.Clear();
            Assert.Equal("TEST", (await db.CartHeaders.AsNoTracking().SingleAsync(x => x.UserId == owner)).CouponCode);
            Assert.True(Assert.IsType<ResponseDto>((await Controller(db, owner).RemoveCart(item.CartDetailsId)).Value).IsSuccess);
            db.ChangeTracker.Clear();
            Assert.False(await db.CartHeaders.AnyAsync(x => x.UserId == owner));
        }
        finally
        {
            await Cleanup(db, owner);
        }
    }

    [Fact]
    public async Task LastItemRemovalRacingInsertDoesNotCascadeDeleteSuccessfulInsert()
    {
        var owner = Guid.NewGuid().ToString();
        var seed = await OpenLab();
        var removal = await OpenLab();
        var insertion = await OpenLab();
        Task? removeTask = null;
        Task? insertTask = null;
        try
        {
            var item = new CartDetails { CartHeader = new CartHeader { UserId = owner }, ProductId = 1, Count = 1 };
            seed.CartDetails.Add(item);
            await seed.SaveChangesAsync();
            var newItem = new CartDto { CartHeader = new CartHeaderDto { UserId = owner }, CartDetails = new[] { new CartDetailsDto { ProductId = 2, Count = 1 } } };
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var remove = Track(Controller(removal, owner).RemoveCart(item.CartDetailsId));
            var insert = Track(Controller(insertion, owner).CartUpsert(newItem));
            removeTask = remove;
            insertTask = insert;
            var results = await Task.WhenAll(remove, insert).WaitAsync(TimeSpan.FromSeconds(25));
            sw.Stop();
            var removed = results[0].Value is ResponseDto { IsSuccess: true };
            var inserted = results[1].Value is ResponseDto { IsSuccess: true };
            if (removed && inserted)
                Assert.True(await seed.CartDetails.AsNoTracking().AnyAsync(x => x.ProductId == 2 && x.CartHeader.UserId == owner));
            Assert.True(removed);
            Assert.True(inserted);
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(25));
        }
        finally
        {
            await Drain(removeTask, insertTask);
            await Cleanup(seed, owner);
        }
    }

    [Fact]
    public async Task ConcurrentCartCreationUsesSerializableTransactions()
    {
        var owner = Guid.NewGuid().ToString();
        var first = await OpenLab();
        var second = await OpenLab();
        Task? firstTask = null;
        Task? secondTask = null;
        try
        {
            CartDto Request() => new() { CartHeader = new CartHeaderDto { UserId = owner }, CartDetails = new[] { new CartDetailsDto { ProductId = 7, Count = 1 } } };
            var firstOperation = Track(Controller(first, owner).CartUpsert(Request()));
            var secondOperation = Track(Controller(second, owner).CartUpsert(Request()));
            firstTask = firstOperation;
            secondTask = secondOperation;
            var results = await Task.WhenAll(firstOperation, secondOperation).WaitAsync(TimeSpan.FromSeconds(25));
            Assert.All(results, r => Assert.True(r.Value is ResponseDto { IsSuccess: true }));
            Assert.True(await first.CartHeaders.CountAsync(x => x.UserId == owner) <= 1);
        }
        finally
        {
            await Drain(firstTask, secondTask);
            await Cleanup(first, owner);
        }
    }

    [Fact]
    public async Task CouponUpdateRejectsOwnershipChangedAfterRead()
    {
        var owner = Guid.NewGuid().ToString();
        var other = Guid.NewGuid().ToString();
        var pause = Register(new PauseUpdate());
        var db = await OpenLab(pause);
        var concurrent = await OpenLab();
        Task? operationTask = null;
        try
        {
            db.CartHeaders.Add(new CartHeader { UserId = owner, CouponCode = "OLD" });
            await db.SaveChangesAsync();
            var operation = Track(Controller(db, owner).ApplyCoupon(new CartDto { CartHeader = new CartHeaderDto { UserId = owner, CouponCode = "NEW" } }));
            operationTask = operation;
            await pause.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await concurrent.CartHeaders.Where(x => x.UserId == owner).ExecuteUpdateAsync(s => s.SetProperty(x => x.UserId, other), _stop.Token);
            pause.Continue.TrySetResult();
            Assert.IsType<NotFoundObjectResult>((await operation.WaitAsync(TimeSpan.FromSeconds(10))).Result);
            Assert.Equal("OLD", (await concurrent.CartHeaders.AsNoTracking().SingleAsync(x => x.UserId == other)).CouponCode);
        }
        finally
        {
            pause.Continue.TrySetResult();
            await Drain(operationTask);
            await Cleanup(concurrent, owner, other);
        }
    }

    private async Task Drain(params Task?[] operations)
    {
        foreach (var pause in _pauses) pause.Continue.TrySetResult();
        _stop.Cancel();
        var tasks = _operations.Concat(operations.OfType<Task>()).Distinct().ToArray();
        try { await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(15)); }
        catch (Exception ex)
        {
            Report("drain", ex);
            foreach (var task in tasks.Where(task => task.IsFaulted))
                foreach (var failure in task.Exception!.Flatten().InnerExceptions) Report("secondary operation", failure);
            if (tasks.Any(task => !task.IsCompleted))
            {
                _blocked = true;
                throw new InvalidOperationException("Operations still active; cleanup and context disposal blocked. Isolated host termination required.");
            }
        }
        if (_blocked || _contexts.Any(context => context.Database.CurrentTransaction is not null))
        {
            _blocked = true;
            throw new InvalidOperationException("Transaction closure is unconfirmed; cleanup blocked.");
        }
    }

    private async Task<AppDbContext> OpenLab(IInterceptor? interceptor = null, [CallerMemberName] string testName = "")
    {
        _testName = testName;
        if (_blocked) throw new InvalidOperationException("A prior laboratory operation is unresolved; isolated host termination required.");
        var cs = new SqlConnectionStringBuilder { DataSource = ".", InitialCatalog = Lab, IntegratedSecurity = true, TrustServerCertificate = true, ConnectTimeout = 5 }.ConnectionString;
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(cs, o => o.CommandTimeout(10))
            .AddInterceptors(new FailureDiagnostics(this));
        if (interceptor is not null) options.AddInterceptors(interceptor);
        var db = new AppDbContext(options.Options);
        _contexts.Add(db);
        await db.Database.OpenConnectionAsync();
        using var cmd = db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = "SELECT CASE WHEN DB_NAME() = N'Mango_ShoppingCart_Concurrency_Test' AND CAST(SERVERPROPERTY('MachineName') AS nvarchar(128)) = N'22LAP5CD1195RMS' AND SERVERPROPERTY('InstanceName') IS NULL THEN 1 ELSE 0 END";
        if (Convert.ToInt32(await cmd.ExecuteScalarAsync()) != 1)
        {
            await db.DisposeAsync();
            _contexts.Remove(db);
            throw new InvalidOperationException("Unexpected SQL laboratory target");
        }
        return db;
    }

    private CartAPIController Controller(AppDbContext db, string subject)
    {
        var controller = new CartAPIController(db, MappingConfig.RegisterMaps().CreateMapper(), new Products(), new Coupons(), new Bus(), new ConfigurationBuilder().Build(), new ControllerDiagnostics(output, _testName));
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(JwtRegisteredClaimNames.Sub, subject) }, "test")) } };
        return controller;
    }

    private interface IPause
    {
        TaskCompletionSource Continue { get; }
        CancellationToken Stop { get; set; }
    }

    private sealed class FailureDiagnostics(SqlServerConcurrencyTests harness) : DbCommandInterceptor
    {
        public override Task CommandFailedAsync(DbCommand command, CommandErrorEventData eventData, CancellationToken cancellationToken = default)
        {
            harness.Report("SQL command", eventData.Exception);
            return Task.CompletedTask;
        }
    }

    private sealed class ControllerDiagnostics(ITestOutputHelper output, string testName) : ILogger<CartAPIController>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            output.WriteLine("Test={0}; Phase=controller; Level={1}; {2}", testName, logLevel, formatter(state, exception));
        }
    }

    private sealed class PauseUpdate : DbCommandInterceptor, IPause
    {
        public CancellationToken Stop { get; set; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Continue { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("UPDATE", StringComparison.OrdinalIgnoreCase))
            {
                Started.TrySetResult();
                await WaitAtBarrier(Continue.Task, cancellationToken, Stop);
            }
            return result;
        }
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("UPDATE", StringComparison.OrdinalIgnoreCase))
            {
                Started.TrySetResult();
                await WaitAtBarrier(Continue.Task, cancellationToken, Stop);
            }
            return result;
        }
    }

    private sealed class PauseCartRead : DbCommandInterceptor, IPause
    {
        public CancellationToken Stop { get; set; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Continue { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("FROM [CartHeaders]", StringComparison.OrdinalIgnoreCase) && !Started.Task.IsCompleted)
            {
                Started.TrySetResult();
                await WaitAtBarrier(Continue.Task, cancellationToken, Stop);
            }
            return result;
        }
    }

    private sealed class FailInsert : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("INSERT INTO [CartDetails]", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Synthetic failure");
            return ValueTask.FromResult(result);
        }
    }

    private sealed class PauseDetailDelete : DbCommandInterceptor, IPause
    {
        public CancellationToken Stop { get; set; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Continue { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private async Task Pause(DbCommand command, CancellationToken cancellationToken)
        {
            if (command.CommandText.Contains("DELETE", StringComparison.OrdinalIgnoreCase) && command.CommandText.Contains("CartDetails", StringComparison.OrdinalIgnoreCase) && !Started.Task.IsCompleted)
            {
                Started.TrySetResult();
                await WaitAtBarrier(Continue.Task, cancellationToken, Stop);
            }
        }
        public override async ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            await Pause(command, cancellationToken);
            return result;
        }
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            await Pause(command, cancellationToken);
            return result;
        }
    }

    private sealed class Bus : IMessageBus { public Task PublishMessage(object message, string topic_queue_Name) => throw new InvalidOperationException(); }
    private sealed class Products : IProductService { public Task<IEnumerable<ProductDto>> GetProducts() => throw new InvalidOperationException(); }
    private sealed class Coupons : ICouponService { public Task<CouponDto?> GetCoupon(string couponCode) => throw new InvalidOperationException(); }
}
