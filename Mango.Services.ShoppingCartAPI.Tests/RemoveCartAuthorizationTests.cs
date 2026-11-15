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
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Mango.Services.ShoppingCartAPI.Tests;

public class RemoveCartAuthorizationTests
{
    private readonly string Owner = Guid.NewGuid().ToString();
    private readonly string Other = Guid.NewGuid().ToString();

    [Fact]
    public async Task OwnerRemovesOneItemAndPreservesOtherCarts()
    {
        await using var db = CreateDb();
        var own = new CartHeader { UserId = Owner };
        var other = new CartHeader { UserId = Other };
        var first = new CartDetails { CartHeader = own, ProductId = 1, Count = 1 };
        var second = new CartDetails { CartHeader = own, ProductId = 2, Count = 1 };
        var foreign = new CartDetails { CartHeader = other, ProductId = 3, Count = 1 };
        db.CartDetails.AddRange(first, second, foreign);
        await db.SaveChangesAsync();
        db.SaveCalls = 0;
        var bus = new Bus();

        var result = await Controller(db, bus).RemoveCart(first.CartDetailsId);

        var response = Assert.IsType<ResponseDto>(result.Value);
        Assert.True(response.IsSuccess);
        Assert.Equal(true, response.Result);
        Assert.Equal(2, await db.CartHeaders.CountAsync());
        Assert.Equal(new[] { second.CartDetailsId, foreign.CartDetailsId }.Order(), (await db.CartDetails.Select(x => x.CartDetailsId).ToListAsync()).Order());
        Assert.Equal(2, db.SaveCalls);
        Assert.Equal(0, bus.Calls);
    }

    [Fact]
    public async Task RemovingLastItemAlsoRemovesOwnHeader()
    {
        await using var db = CreateDb();
        var own = new CartHeader { UserId = Owner };
        var item = new CartDetails { CartHeader = own, ProductId = 1, Count = 1 };
        var other = new CartHeader { UserId = Other };
        db.CartDetails.AddRange(item, new CartDetails { CartHeader = other, ProductId = 2, Count = 1 });
        await db.SaveChangesAsync();
        var result = await Controller(db, new Bus()).RemoveCart(item.CartDetailsId);

        Assert.True(Assert.IsType<ResponseDto>(result.Value).IsSuccess);
        Assert.Equal(Other, (await db.CartHeaders.SingleAsync()).UserId);
        Assert.Equal(other.CartHeaderId, (await db.CartDetails.SingleAsync()).CartHeaderId);
    }

    [Fact]
    public async Task ForeignMissingAndAdminReceiveIdentical404WithoutWritesOrPublication()
    {
        await using var db = CreateDb();
        var item = new CartDetails { CartHeader = new CartHeader { UserId = Other }, ProductId = 1, Count = 1 };
        db.CartDetails.Add(item);
        await db.SaveChangesAsync();
        db.SaveCalls = 0;
        var bus = new Bus();

        var foreign = await Controller(db, bus).RemoveCart(item.CartDetailsId);
        var missing = await Controller(db, bus).RemoveCart(item.CartDetailsId + 1000);
        var admin = await Controller(db, bus, true).RemoveCart(item.CartDetailsId);

        var foreignResult = Assert.IsType<NotFoundObjectResult>(foreign.Result);
        Assert.Equal(JsonSerializer.Serialize(foreignResult.Value), JsonSerializer.Serialize(Assert.IsType<NotFoundObjectResult>(missing.Result).Value));
        Assert.Equal(JsonSerializer.Serialize(foreignResult.Value), JsonSerializer.Serialize(Assert.IsType<NotFoundObjectResult>(admin.Result).Value));
        Assert.Equal(0, db.SaveCalls);
        Assert.Equal(0, bus.Calls);
        Assert.Single(db.CartHeaders);
        Assert.Single(db.CartDetails);
        Assert.DoesNotContain(db.ChangeTracker.Entries(), e => e.State != EntityState.Unchanged);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task InvalidIdentifierIsRejectedWithoutDatabaseOrPublication(int id)
    {
        await using var db = new SqlLabTestContext(new DbContextOptionsBuilder<AppDbContext>().Options);
        var bus = new Bus();
        var result = await Controller(db, bus).RemoveCart(id);
        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(0, db.SaveCalls);
        Assert.Equal(0, bus.Calls);
    }

    private SqlLabTestContext CreateDb() => SqlLabTestContext.Create(Owner, Other);

    private CartAPIController Controller(AppDbContext db, Bus bus, bool admin = false)
    {
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, Owner) };
        if (admin) claims.Add(new Claim(ClaimTypes.Role, "ADMIN"));
        var controller = new CartAPIController(db, MappingConfig.RegisterMaps().CreateMapper(), new Products(), new Coupons(), bus, new ConfigurationBuilder().Build());
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) } };
        return controller;
    }

    private sealed class Bus : IMessageBus
    {
        public int Calls { get; private set; }
        public Task PublishMessage(object message, string topic_queue_Name) { Calls++; return Task.CompletedTask; }
    }
    private sealed class Products : IProductService { public Task<IEnumerable<ProductDto>> GetProducts() => throw new InvalidOperationException(); }
    private sealed class Coupons : ICouponService { public Task<CouponDto?> GetCoupon(string couponCode) => throw new InvalidOperationException(); }
}
