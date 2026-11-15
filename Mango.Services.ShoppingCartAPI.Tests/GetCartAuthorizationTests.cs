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

    [Fact]
    public async Task ForeignAndMissingCartsHaveIdenticalResponsesWithoutExternalCallsOrWrites()
    {
        await using var db = CreateDb();
        db.CartHeaders.Add(new CartHeader { UserId = Other });
        await db.SaveChangesAsync();
        var products = new Products();
        var coupons = new Coupons();
        var foreign = await CreateController(db, Owner, products, coupons).GetCart(Other);
        var missing = await CreateController(db, Owner, products, coupons).GetCart(Owner);
        var admin = await CreateController(db, Owner, products, coupons, true).GetCart(Other);
        var malformed = await CreateController(db, Owner, products, coupons).GetCart("bad-id");

        var foreignResult = Assert.IsType<NotFoundObjectResult>(foreign.Result);
        var missingResult = Assert.IsType<NotFoundObjectResult>(missing.Result);
        var adminResult = Assert.IsType<NotFoundObjectResult>(admin.Result);
        Assert.Equal(JsonSerializer.Serialize(foreignResult.Value), JsonSerializer.Serialize(missingResult.Value));
        Assert.Equal(JsonSerializer.Serialize(foreignResult.Value), JsonSerializer.Serialize(adminResult.Value));
        Assert.IsType<BadRequestObjectResult>(malformed.Result);
        Assert.Equal(0, products.Calls);
        Assert.Equal(0, coupons.Calls);
        Assert.Single(db.CartHeaders);
        Assert.DoesNotContain(db.ChangeTracker.Entries(), e => e.State != EntityState.Unchanged);
    }

    private static AppDbContext CreateDb() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static CartAPIController CreateController(AppDbContext db, string subject, Products products, Coupons coupons, bool admin = false)
    {
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, subject) };
        if (admin) claims.Add(new Claim(ClaimTypes.Role, "ADMIN"));
        var controller = new CartAPIController(db, MappingConfig.RegisterMaps().CreateMapper(), products, coupons, new Bus(), new ConfigurationBuilder().Build());
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) } };
        return controller;
    }

    private sealed class Products : IProductService
    {
        public int Calls { get; private set; }
        public Task<IEnumerable<ProductDto>> GetProducts()
        {
            Calls++;
            return Task.FromResult<IEnumerable<ProductDto>>(new[] { new ProductDto { ProductId = 7, Price = 10 } });
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
}
