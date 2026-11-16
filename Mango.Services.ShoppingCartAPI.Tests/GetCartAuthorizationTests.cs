using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Mango.MessageBus.Service.IService;
using Mango.Services.ShoppingCartAPI;
using Mango.Services.ShoppingCartAPI.Controllers;
using Mango.Services.ShoppingCartAPI.Data;
using Mango.Services.ShoppingCartAPI.Models;
using Mango.Services.ShoppingCartAPI.Models.Dto;
using Mango.Services.ShoppingCartAPI.Service.IService;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Mango.Services.ShoppingCartAPI.Tests;

public class GetCartAuthorizationTests
{
    private static readonly string Owner = "11111111-1111-1111-1111-111111111111";
    private static readonly string Other = "22222222-2222-2222-2222-222222222222";

    [Fact]
    public async Task OwnerReceivesComposedCartAndCoupon()
    {
        await using var db = CreateDb();
        var header = new CartHeader { UserId = Owner, CouponCode = "SAVE" };
        db.CartHeaders.Add(header);
        db.CartDetails.Add(new CartDetails { CartHeader = header, ProductId = 7, Count = 2 });
        await db.SaveChangesAsync();
        var products = new Products();
        var coupons = new Coupons();
        var controller = CreateController(db, Owner, products, coupons);

        var result = await controller.GetCart(Owner);

        var response = Assert.IsType<ResponseDto>(result.Value);
        var cart = Assert.IsType<CartDto>(response.Result);
        Assert.Equal(15, cart.CartHeader.CartTotal);
        Assert.Equal(5, cart.CartHeader.Discount);
        Assert.Equal(7, Assert.Single(cart.CartDetails!).Product!.ProductId);
        Assert.Equal(1, products.Calls);
        Assert.Equal(1, coupons.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ForeignExistingAndMissingCartsHaveIdenticalResponsesWithoutExternalCallsOrWrites(bool admin)
    {
        await using var db = CreateDb();
        db.CartHeaders.Add(new CartHeader { UserId = Other });
        await db.SaveChangesAsync();
        var products = new Products();
        var coupons = new Coupons();
        var foreign = await CreateController(db, Owner, products, coupons, admin).GetCart(Other);
        var missing = await CreateController(db, Owner, products, coupons, admin).GetCart("33333333-3333-3333-3333-333333333333");
        var malformed = await CreateController(db, Owner, products, coupons).GetCart("bad-id");

        var foreignResult = Assert.IsType<NotFoundObjectResult>(foreign.Result);
        var missingResult = Assert.IsType<NotFoundObjectResult>(missing.Result);
        Assert.Equal(JsonSerializer.Serialize(foreignResult.Value), JsonSerializer.Serialize(missingResult.Value));
        Assert.IsType<BadRequestObjectResult>(malformed.Result);
        Assert.Equal(0, products.Calls);
        Assert.Equal(0, coupons.Calls);
        Assert.Single(db.CartHeaders);
        Assert.DoesNotContain(db.ChangeTracker.Entries(), e => e.State != EntityState.Unchanged);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RepeatedOwnMissingReadsReturnVirtualCartWithoutWritesOrExternalCalls(bool admin)
    {
        var writes = new WriteGuard();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).AddInterceptors(writes).Options);
        var products = new Products { Fail = true };
        var coupons = new Coupons();

        for (var read = 0; read < 3; read++)
        {
            var result = await CreateController(db, Owner, products, coupons, admin).GetCart(Owner);
            Assert.Null(result.Result);
            var response = Assert.IsType<ResponseDto>(result.Value);
            Assert.True(response.IsSuccess);
            var cart = Assert.IsType<CartDto>(response.Result);
            Assert.NotNull(cart.CartHeader);
            Assert.Equal(0, cart.CartHeader.CartHeaderId);
            Assert.Equal(Owner, cart.CartHeader.UserId);
            Assert.Equal(0, cart.CartHeader.CartTotal);
            Assert.Equal(0, cart.CartHeader.Discount);
            Assert.Null(cart.CartHeader.CouponCode);
            Assert.Null(cart.CartHeader.Name);
            Assert.Null(cart.CartHeader.Phone);
            Assert.Null(cart.CartHeader.Email);
            Assert.NotNull(cart.CartDetails);
            Assert.Empty(cart.CartDetails);
        }

        Assert.Equal(0, writes.Calls);
        Assert.Equal(0, products.Calls);
        Assert.Equal(0, coupons.Calls);
        Assert.Empty(db.CartHeaders);
        Assert.Empty(db.CartDetails);
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ForeignIdentityIsRejectedBeforeAccessingStorage(bool admin)
    {
        await using var db = CreateDb();
        await db.DisposeAsync();
        var result = await CreateController(db, Owner, new Products(), new Coupons(), admin).GetCart(Other);
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task StorageFailureIsNotReturnedAsVirtualCart()
    {
        await using var db = CreateDb();
        await db.DisposeAsync();
        var result = await CreateController(db, Owner, new Products(), new Coupons()).GetCart(Owner);
        var http = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, http.StatusCode);
        var response = Assert.IsType<ResponseDto>(http.Value);
        Assert.False(response.IsSuccess);
        Assert.Null(response.Result);
        Assert.Equal("An unexpected error occurred", response.Message);
    }

    [Fact]
    public async Task UnexpectedFailureReturnsGenericErrorAndLogsWithoutExceptionDetails()
    {
        await using var db = CreateDb();
        db.CartHeaders.Add(new CartHeader { UserId = Owner });
        await db.SaveChangesAsync();
        var products = new Products { Fail = true };
        var logger = new SafeLogger();
        var result = await CreateController(db, Owner, products, new Coupons(), logger: logger).GetCart(Owner);
        var http = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, http.StatusCode);
        var response = Assert.IsType<ResponseDto>(http.Value);
        Assert.False(response.IsSuccess);
        Assert.Equal("An unexpected error occurred", response.Message);
        Assert.DoesNotContain("sensitive-test-detail", JsonSerializer.Serialize(response));
        Assert.Equal(LogLevel.Error, logger.Level);
        Assert.DoesNotContain("sensitive-test-detail", logger.Message);
        Assert.Null(logger.Exception);
    }

    private static AppDbContext CreateDb() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static CartAPIController CreateController(AppDbContext db, string subject, Products products, Coupons coupons, bool admin = false, ILogger<CartAPIController>? logger = null)
    {
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, subject) };
        if (admin) claims.Add(new Claim(ClaimTypes.Role, "ADMIN"));
        var controller = new CartAPIController(db, MappingConfig.RegisterMaps().CreateMapper(), products, coupons, new Bus(), new ConfigurationBuilder().Build(), logger);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) } };
        return controller;
    }

    private sealed class Products : IProductService
    {
        public int Calls { get; private set; }
        public bool Fail { get; set; }
        public Task<IEnumerable<ProductDto>> GetProducts()
        {
            Calls++;
            if (Fail) throw new InvalidOperationException("sensitive-test-detail");
            return Task.FromResult<IEnumerable<ProductDto>>(new[] { new ProductDto { ProductId = 7, Price = 10 } });
        }
    }

    private sealed class SafeLogger : ILogger<CartAPIController>
    {
        public LogLevel Level { get; private set; }
        public string Message { get; private set; } = "";
        public Exception? Exception { get; private set; }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Level = level;
            Exception = exception;
            Message = formatter(state, exception);
        }
    }

    private sealed class Coupons : ICouponService
    {
        public int Calls { get; private set; }
        public Task<CouponDto?> GetCoupon(string couponCode)
        {
            Calls++;
            return Task.FromResult<CouponDto?>(new CouponDto { CouponCode = couponCode, DiscountAmount = 5, MinAmount = 1 });
        }
    }

    private sealed class Bus : IMessageBus
    {
        public Task PublishMessage(object message, string topic_queue_Name) => throw new InvalidOperationException();
    }

    private sealed class WriteGuard : SaveChangesInterceptor
    {
        public int Calls { get; private set; }
        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            Calls++;
            throw new InvalidOperationException("Unexpected write");
        }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Calls++;
            throw new InvalidOperationException("Unexpected write");
        }
    }
}
