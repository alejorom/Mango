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
using Xunit;

namespace Mango.Services.ShoppingCartAPI.Tests;

public class SqlServerConcurrencyTests
{
    private const string Lab = "Mango_ShoppingCart_Concurrency_Test";
    private const string Machine = "22LAP5CD1195RMS";

    [Fact]
    public async Task LastItemDeletionAndConcurrentInsertNeverLoseSuccessfulInsert()
    {
        var owner = Guid.NewGuid().ToString();
        var pause = new PauseDetailDelete();
        await using var seed = await OpenLab();
        await using var removal = await OpenLab(pause);
        await using var competitor = await OpenLab();
        try
        {
            var item = new CartDetails { CartHeader = new CartHeader { UserId = owner }, ProductId = 1, Count = 1 };
            seed.CartDetails.Add(item);
            await seed.SaveChangesAsync();
            var remove = Controller(removal, owner).RemoveCart(item.CartDetailsId);
            await pause.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            competitor.CartDetails.Add(new CartDetails { CartHeaderId = item.CartHeaderId, ProductId = 2, Count = 1 });
            var insert = competitor.SaveChangesAsync();
            try { Assert.NotSame(insert, await Task.WhenAny(insert, Task.Delay(300))); }
            finally { pause.Continue.TrySetResult(); }
            var removeResult = await remove.WaitAsync(TimeSpan.FromSeconds(15));
            var inserted = false;
            try { inserted = await insert.WaitAsync(TimeSpan.FromSeconds(15)) == 1; }
            catch (DbUpdateException) { }
            catch (SqlException) { }
            catch (InvalidOperationException ex) when (ex.InnerException is SqlException { Number: 1205 }) { }
            var succeeded = removeResult.Value is ResponseDto { IsSuccess: true };
            Assert.True(succeeded || removeResult.Result is ObjectResult { StatusCode: 500 });
            if (inserted) Assert.True(await seed.CartDetails.AsNoTracking().AnyAsync(x => x.CartHeaderId == item.CartHeaderId && x.ProductId == 2));
            if (!succeeded) Assert.True(await seed.CartDetails.AsNoTracking().AnyAsync(x => x.CartDetailsId == item.CartDetailsId));
        }
        finally
        {
            pause.Continue.TrySetResult();
            await seed.CartHeaders.Where(x => x.UserId == owner).ExecuteDeleteAsync();
        }
    }

    [Fact]
    public async Task OwnershipChangeWaitsUntilUpsertCompletes()
    {
        var owner = Guid.NewGuid().ToString();
        var other = Guid.NewGuid().ToString();
        var pause = new PauseCartRead();
        await using var seed = await OpenLab();
        await using var upsert = await OpenLab(pause);
        await using var competitor = await OpenLab();
        try
        {
            seed.CartHeaders.Add(new CartHeader { UserId = owner });
            await seed.SaveChangesAsync();
            var request = new CartDto { CartHeader = new CartHeaderDto { UserId = owner }, CartDetails = new[] { new CartDetailsDto { ProductId = 42, Count = 1 } } };
            var action = Controller(upsert, owner).CartUpsert(request);
            await pause.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var transfer = competitor.CartHeaders.Where(x => x.UserId == owner).ExecuteUpdateAsync(s => s.SetProperty(x => x.UserId, other));
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
            await seed.CartHeaders.Where(x => x.UserId == owner || x.UserId == other).ExecuteDeleteAsync();
        }
    }

    [Fact]
    public async Task RollbackOfFailedUpsertLeavesNoHeaderOrDetail()
    {
        var owner = Guid.NewGuid().ToString();
        await using var db = await OpenLab(new FailInsert());
        await using var verify = await OpenLab();
        try
        {
            var request = new CartDto { CartHeader = new CartHeaderDto { UserId = owner }, CartDetails = new[] { new CartDetailsDto { ProductId = 7, Count = 1 } } };
            var result = await Controller(db, owner).CartUpsert(request);
            Assert.Equal(500, Assert.IsType<ObjectResult>(result.Result).StatusCode);
            Assert.False(await verify.CartHeaders.AnyAsync(x => x.UserId == owner));
        }
        finally { await verify.CartHeaders.Where(x => x.UserId == owner).ExecuteDeleteAsync(); }
    }

    [Fact]
    public async Task OwnerCanCreateIncrementApplyCouponAndRemoveLastItem()
    {
        var owner = Guid.NewGuid().ToString();
        await using var db = await OpenLab();
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
            await db.CartHeaders.Where(x => x.UserId == owner).ExecuteDeleteAsync();
        }
    }

    [Fact]
    public async Task LastItemRemovalRacingInsertDoesNotCascadeDeleteSuccessfulInsert()
    {
        var owner = Guid.NewGuid().ToString();
        await using var seed = await OpenLab();
        await using var removal = await OpenLab();
        await using var insertion = await OpenLab();
        try
        {
            var item = new CartDetails { CartHeader = new CartHeader { UserId = owner }, ProductId = 1, Count = 1 };
            seed.CartDetails.Add(item);
            await seed.SaveChangesAsync();
            var newItem = new CartDto { CartHeader = new CartHeaderDto { UserId = owner }, CartDetails = new[] { new CartDetailsDto { ProductId = 2, Count = 1 } } };
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var results = await Task.WhenAll(Controller(removal, owner).RemoveCart(item.CartDetailsId), Controller(insertion, owner).CartUpsert(newItem)).WaitAsync(TimeSpan.FromSeconds(25));
            sw.Stop();
            var removed = results[0].Value is ResponseDto { IsSuccess: true };
            var inserted = results[1].Value is ResponseDto { IsSuccess: true };
            if (removed && inserted)
                Assert.True(await seed.CartDetails.AsNoTracking().AnyAsync(x => x.ProductId == 2 && x.CartHeader.UserId == owner));
            Assert.True(removed || results[0].Result is ObjectResult { StatusCode: 404 or 500 });
            Assert.True(inserted || results[1].Result is ObjectResult { StatusCode: 404 or 500 });
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(25));
        }
        finally
        {
            await seed.CartHeaders.Where(x => x.UserId == owner).ExecuteDeleteAsync();
        }
    }

    [Fact]
    public async Task ConcurrentCartCreationUsesSerializableTransactions()
    {
        var owner = Guid.NewGuid().ToString();
        await using var first = await OpenLab();
        await using var second = await OpenLab();
        try
        {
            CartDto Request() => new() { CartHeader = new CartHeaderDto { UserId = owner }, CartDetails = new[] { new CartDetailsDto { ProductId = 7, Count = 1 } } };
            var results = await Task.WhenAll(Controller(first, owner).CartUpsert(Request()), Controller(second, owner).CartUpsert(Request())).WaitAsync(TimeSpan.FromSeconds(25));
            Assert.All(results, r => Assert.True(r.Value is ResponseDto { IsSuccess: true } || r.Result is ObjectResult { StatusCode: 500 }));
            Assert.True(await first.CartHeaders.CountAsync(x => x.UserId == owner) <= 1);
        }
        finally
        {
            await first.CartHeaders.Where(x => x.UserId == owner).ExecuteDeleteAsync();
        }
    }

    [Fact]
    public async Task CouponUpdateRejectsOwnershipChangedAfterRead()
    {
        var owner = Guid.NewGuid().ToString();
        var other = Guid.NewGuid().ToString();
        var pause = new PauseUpdate();
        await using var db = await OpenLab(pause);
        await using var concurrent = await OpenLab();
        try
        {
            db.CartHeaders.Add(new CartHeader { UserId = owner, CouponCode = "OLD" });
            await db.SaveChangesAsync();
            var operation = Controller(db, owner).ApplyCoupon(new CartDto { CartHeader = new CartHeaderDto { UserId = owner, CouponCode = "NEW" } });
            await pause.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await concurrent.CartHeaders.Where(x => x.UserId == owner).ExecuteUpdateAsync(s => s.SetProperty(x => x.UserId, other));
            pause.Continue.TrySetResult();
            Assert.IsType<NotFoundObjectResult>((await operation.WaitAsync(TimeSpan.FromSeconds(10))).Result);
            Assert.Equal("OLD", (await concurrent.CartHeaders.AsNoTracking().SingleAsync(x => x.UserId == other)).CouponCode);
        }
        finally
        {
            pause.Continue.TrySetResult();
            await concurrent.CartHeaders.Where(x => x.UserId == owner || x.UserId == other).ExecuteDeleteAsync();
        }
    }

    private static async Task<AppDbContext> OpenLab(IInterceptor? interceptor = null)
    {
        var cs = new SqlConnectionStringBuilder { DataSource = ".", InitialCatalog = Lab, IntegratedSecurity = true, TrustServerCertificate = true, ConnectTimeout = 5 }.ConnectionString;
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(cs, o => o.CommandTimeout(10));
        if (interceptor is not null) options.AddInterceptors(interceptor);
        var db = new AppDbContext(options.Options);
        await db.Database.OpenConnectionAsync();
        await using var cmd = db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = "SELECT CASE WHEN DB_NAME() = N'Mango_ShoppingCart_Concurrency_Test' AND CAST(SERVERPROPERTY('MachineName') AS nvarchar(128)) = N'22LAP5CD1195RMS' AND SERVERPROPERTY('InstanceName') IS NULL THEN 1 ELSE 0 END";
        if (Convert.ToInt32(await cmd.ExecuteScalarAsync()) != 1) { await db.DisposeAsync(); throw new InvalidOperationException("Unexpected SQL laboratory target"); }
        return db;
    }

    private static CartAPIController Controller(AppDbContext db, string subject)
    {
        var controller = new CartAPIController(db, MappingConfig.RegisterMaps().CreateMapper(), new Products(), new Coupons(), new Bus(), new ConfigurationBuilder().Build());
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(JwtRegisteredClaimNames.Sub, subject) }, "test")) } };
        return controller;
    }

    private sealed class PauseUpdate : DbCommandInterceptor
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Continue { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("UPDATE", StringComparison.OrdinalIgnoreCase))
            {
                Started.TrySetResult();
                await Continue.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            }
            return result;
        }
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("UPDATE", StringComparison.OrdinalIgnoreCase))
            {
                Started.TrySetResult();
                await Continue.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            }
            return result;
        }
    }

    private sealed class PauseCartRead : DbCommandInterceptor
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Continue { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("FROM [CartHeaders]", StringComparison.OrdinalIgnoreCase) && !Started.Task.IsCompleted)
            {
                Started.TrySetResult();
                await Continue.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
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

    private sealed class PauseDetailDelete : DbCommandInterceptor
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Continue { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private async Task Pause(DbCommand command, CancellationToken cancellationToken)
        {
            if (command.CommandText.Contains("DELETE", StringComparison.OrdinalIgnoreCase) && command.CommandText.Contains("CartDetails", StringComparison.OrdinalIgnoreCase) && !Started.Task.IsCompleted)
            {
                Started.TrySetResult();
                await Continue.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
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
