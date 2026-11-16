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

public class ApplyCouponAuthorizationTests
{
    private readonly string Owner = Guid.NewGuid().ToString();
    private readonly string Other = Guid.NewGuid().ToString();

    [Fact]
    public async Task DuplicateOwnedHeadersRejectCouponWithoutMutation()
    {
        await using var db = CreateDb();
        db.CartHeaders.AddRange(new CartHeader { UserId = Owner, CouponCode = "FIRST" },
            new CartHeader { UserId = Owner, CouponCode = "SECOND" });
        await db.SaveChangesAsync();
        var result = await CreateController(db, new Bus()).ApplyCoupon(Request(Owner, "NEW"));
        Assert.Equal(500, Assert.IsType<ObjectResult>(result.Result).StatusCode);
        Assert.Equal(new[] { "FIRST", "SECOND" }, await db.CartHeaders.AsNoTracking()
            .OrderBy(x => x.CouponCode).Select(x => x.CouponCode).ToArrayAsync());
    }

    [Fact]
    public async Task OwnerCanApplyAndClearCoupon()
    {
        await using var db = CreateDb();
        db.CartHeaders.Add(new CartHeader { UserId = Owner, CouponCode = "OLD" });
        await db.SaveChangesAsync();
        db.SaveCalls = 0;
        var bus = new Bus();

        var response = await CreateController(db, bus).ApplyCoupon(Request(Owner, "SAVE"));

        Assert.True(Assert.IsType<ResponseDto>(response.Value).IsSuccess);
        Assert.Equal("SAVE", (await db.CartHeaders.AsNoTracking().SingleAsync()).CouponCode);
        Assert.Equal(1, db.SaveCalls);
        Assert.Equal(0, bus.Calls);

        var clear = await CreateController(db, bus).ApplyCoupon(Request(Owner, ""));
        Assert.True(Assert.IsType<ResponseDto>(clear.Value).IsSuccess);
        Assert.Equal("", (await db.CartHeaders.AsNoTracking().SingleAsync()).CouponCode);
    }

    [Fact]
    public async Task RejectionsNeverSaveOrPublishAndHideForeignCartExistence()
    {
        await using var db = CreateDb();
        db.CartHeaders.Add(new CartHeader { UserId = Other, CouponCode = "OLD" });
        await db.SaveChangesAsync();
        db.SaveCalls = 0;
        var bus = new Bus();

        var foreign = await CreateController(db, bus).ApplyCoupon(Request(Other, "NEW"));
        var missing = await CreateController(db, bus).ApplyCoupon(Request(Owner, "NEW"));
        var admin = await CreateController(db, bus, true).ApplyCoupon(Request(Other, "NEW"));
        var malformed = await CreateController(db, bus).ApplyCoupon(Request("invalid", "NEW"));
        var absent = await CreateController(db, bus).ApplyCoupon(Request(null, "NEW"));
        var noHeader = await CreateController(db, bus).ApplyCoupon(new CartDto { CartHeader = null! });

        var foreignResult = Assert.IsType<NotFoundObjectResult>(foreign.Result);
        var missingResult = Assert.IsType<NotFoundObjectResult>(missing.Result);
        var adminResult = Assert.IsType<NotFoundObjectResult>(admin.Result);
        Assert.Equal(JsonSerializer.Serialize(foreignResult.Value), JsonSerializer.Serialize(missingResult.Value));
        Assert.Equal(JsonSerializer.Serialize(foreignResult.Value), JsonSerializer.Serialize(adminResult.Value));
        Assert.IsType<BadRequestObjectResult>(malformed.Result);
        Assert.IsType<BadRequestObjectResult>(absent.Result);
        Assert.IsType<BadRequestObjectResult>(noHeader.Result);
        Assert.Equal(0, db.SaveCalls);
        Assert.Equal(0, bus.Calls);
        Assert.Equal("OLD", (await db.CartHeaders.SingleAsync()).CouponCode);
        Assert.DoesNotContain(db.ChangeTracker.Entries(), e => e.State != EntityState.Unchanged);
    }

    private static CartDto Request(string? userId, string coupon) => new() { CartHeader = new CartHeaderDto { UserId = userId, CouponCode = coupon } };

    private SqlLabTestContext CreateDb() => SqlLabTestContext.Create(Owner, Other);

    private CartAPIController CreateController(AppDbContext db, Bus bus, bool admin = false)
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

    private sealed class Products : IProductService
    {
        public Task<IEnumerable<ProductDto>> GetProducts() => throw new InvalidOperationException();
    }

    private sealed class Coupons : ICouponService
    {
        public Task<CouponDto?> GetCoupon(string couponCode) => throw new InvalidOperationException();
    }
}
