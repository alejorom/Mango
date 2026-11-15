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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Mango.Services.ShoppingCartAPI.Tests;

public class EmailCartAuthorizationTests
{
    private readonly string owner = Guid.NewGuid().ToString();
    private readonly string other = Guid.NewGuid().ToString();

    [Fact]
    public async Task OwnerPublishesOnlyPersistedCartTrustedPricesAndTokenEmail()
    {
        await using var db = CreateDb();
        var header = new CartHeader { UserId = owner, CouponCode = "SAVE" };
        db.CartDetails.Add(new CartDetails { CartHeader = header, ProductId = 7, Count = 2 });
        await db.SaveChangesAsync();
        var bus = new Bus();
        var request = Request(owner);
        request.CartHeader.Email = "attacker@example.invalid";
        request.CartHeader.Name = "Untrusted name";
        request.CartHeader.Phone = "Untrusted phone";
        request.CartHeader.CartTotal = 0.01;
        request.CartDetails = new[] { new CartDetailsDto { ProductId = 99, Count = 999, Product = new ProductDto { Price = 0.01 } } };

        var response = await Controller(db, bus).EmailCartRequest(request);

        Assert.True(Assert.IsType<ResponseDto>(response.Value).IsSuccess);
        Assert.Equal(1, bus.Calls);
        Assert.Equal("emailshoppingcart", bus.Destination);
        var message = Assert.IsType<CartDto>(bus.Message);
        Assert.NotSame(request, message);
        Assert.Equal("owner@example.invalid", message.CartHeader.Email);
        Assert.Equal(owner, message.CartHeader.UserId);
        Assert.Equal(15, message.CartHeader.CartTotal);
        Assert.Equal(5, message.CartHeader.Discount);
        var item = Assert.Single(message.CartDetails!);
        Assert.Equal(7, item.ProductId);
        Assert.Equal(2, item.Count);
        Assert.Equal(10, item.Product!.Price);
        Assert.Null(message.CartHeader.Name);
        Assert.Null(message.CartHeader.Phone);
        var json = Newtonsoft.Json.Linq.JObject.Parse(Newtonsoft.Json.JsonConvert.SerializeObject(message));
        Assert.Equal("owner@example.invalid", (string?)json["CartHeader"]?["Email"]);
        Assert.Equal(15, (double?)json["CartHeader"]?["CartTotal"]);
        Assert.Equal("Trusted", (string?)json["CartDetails"]?[0]?["Product"]?["Name"]);
    }

    [Fact]
    public async Task ForeignMissingAdminAndMalformedAreRejectedWithoutPublication()
    {
        await using var db = CreateDb();
        db.CartHeaders.Add(new CartHeader { UserId = other });
        await db.SaveChangesAsync();
        var bus = new Bus();
        var foreign = await Controller(db, bus).EmailCartRequest(Request(other));
        var missing = await Controller(db, bus).EmailCartRequest(Request(owner));
        var admin = await Controller(db, bus, admin: true).EmailCartRequest(Request(other));
        var malformed = await Controller(db, bus).EmailCartRequest(Request("invalid"));
        var invalidId = Request(owner);
        invalidId.CartHeader.CartHeaderId = -1;
        var invalidHeader = await Controller(db, bus).EmailCartRequest(invalidId);

        var foreignResult = Assert.IsType<NotFoundObjectResult>(foreign.Result);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(foreignResult.Value), System.Text.Json.JsonSerializer.Serialize(Assert.IsType<NotFoundObjectResult>(missing.Result).Value));
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(foreignResult.Value), System.Text.Json.JsonSerializer.Serialize(Assert.IsType<NotFoundObjectResult>(admin.Result).Value));
        Assert.IsType<BadRequestObjectResult>(malformed.Result);
        Assert.IsType<BadRequestObjectResult>(invalidHeader.Result);
        Assert.Equal(0, bus.Calls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("Name <owner@example.invalid>")]
    public async Task MissingOrInvalidTokenEmailFailsClosed(string? email)
    {
        await using var db = CreateDb();
        db.CartHeaders.Add(new CartHeader { UserId = owner });
        await db.SaveChangesAsync();
        var bus = new Bus();
        var response = await Controller(db, bus, email: email).EmailCartRequest(Request(owner));
        Assert.Equal(503, Assert.IsType<ObjectResult>(response.Result).StatusCode);
        Assert.Equal(0, bus.Calls);
    }

    [Fact]
    public async Task MissingTrustedProductOrCouponDoesNotPublish()
    {
        await using var db = CreateDb();
        var header = new CartHeader { UserId = owner, CouponCode = "SAVE" };
        db.CartDetails.Add(new CartDetails { CartHeader = header, ProductId = 7, Count = 2 });
        await db.SaveChangesAsync();
        var bus = new Bus();
        var missingProduct = await Controller(db, bus, productsAvailable: false).EmailCartRequest(Request(owner));
        var missingCoupon = await Controller(db, bus, couponAvailable: false).EmailCartRequest(Request(owner));
        Assert.Equal(503, Assert.IsType<ObjectResult>(missingProduct.Result).StatusCode);
        Assert.Equal(503, Assert.IsType<ObjectResult>(missingCoupon.Result).StatusCode);
        Assert.Equal(0, bus.Calls);
    }

    [Fact]
    public async Task ManipulatedHeaderReferencesAndMissingUserDoNotPublish()
    {
        await using var db = CreateDb();
        var own = new CartHeader { UserId = owner };
        var foreign = new CartHeader { UserId = other };
        db.CartHeaders.AddRange(own, foreign);
        await db.SaveChangesAsync();
        var bus = new Bus();
        var foreignReference = Request(owner);
        foreignReference.CartHeader.CartHeaderId = foreign.CartHeaderId;
        var missingReference = Request(owner);
        missingReference.CartHeader.CartHeaderId = Math.Max(own.CartHeaderId, foreign.CartHeaderId) + 1;
        var missingUser = Request(owner);
        missingUser.CartHeader.UserId = null!;

        var foreignResult = Assert.IsType<NotFoundObjectResult>((await Controller(db, bus).EmailCartRequest(foreignReference)).Result);
        var missingResult = Assert.IsType<NotFoundObjectResult>((await Controller(db, bus).EmailCartRequest(missingReference)).Result);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(foreignResult.Value), System.Text.Json.JsonSerializer.Serialize(missingResult.Value));
        Assert.IsType<BadRequestObjectResult>((await Controller(db, bus).EmailCartRequest(missingUser)).Result);
        Assert.Equal(0, bus.Calls);
    }

    [Fact]
    public async Task DownstreamExceptionsDoNotPublish()
    {
        await using var db = CreateDb();
        var header = new CartHeader { UserId = owner, CouponCode = "SAVE" };
        db.CartDetails.Add(new CartDetails { CartHeader = header, ProductId = 7, Count = 2 });
        await db.SaveChangesAsync();
        var bus = new Bus();

        var productFailure = await Controller(db, bus, productThrows: true).EmailCartRequest(Request(owner));
        var couponFailure = await Controller(db, bus, couponThrows: true).EmailCartRequest(Request(owner));

        Assert.Equal(500, Assert.IsType<ObjectResult>(productFailure.Result).StatusCode);
        Assert.Equal(500, Assert.IsType<ObjectResult>(couponFailure.Result).StatusCode);
        Assert.Equal(0, bus.Calls);
    }

    [Fact]
    public async Task RejectedOwnershipAndMalformedEmailRequestsNeverCallDependencies()
    {
        await using var db = CreateDb();
        db.CartHeaders.Add(new CartHeader { UserId = other, CouponCode = "SAVE" });
        await db.SaveChangesAsync();
        var products = new CountingProducts();
        var coupons = new CountingCoupons();
        var bus = new Bus();
        var claims = new[] { new Claim(JwtRegisteredClaimNames.Sub, owner), new Claim(JwtRegisteredClaimNames.Email, "owner@example.invalid"), new Claim(ClaimTypes.Role, "ADMIN") };
        var controller = new CartAPIController(db, MappingConfig.RegisterMaps().CreateMapper(), products, coupons, bus, new ConfigurationBuilder().Build())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) } }
        };

        Assert.IsType<NotFoundObjectResult>((await controller.EmailCartRequest(Request(other))).Result);
        Assert.IsType<NotFoundObjectResult>((await controller.EmailCartRequest(Request(owner))).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.EmailCartRequest(Request("malformed"))).Result);
        Assert.Equal(0, products.Calls);
        Assert.Equal(0, coupons.Calls);
        Assert.Equal(0, bus.Calls);
        Assert.Equal("SAVE", (await db.CartHeaders.SingleAsync()).CouponCode);
        Assert.DoesNotContain(db.ChangeTracker.Entries(), e => e.State != EntityState.Unchanged);
    }

    private sealed class CountingProducts : IProductService
    {
        public int Calls { get; private set; }
        public Task<IEnumerable<ProductDto>> GetProducts() { Calls++; return Task.FromResult<IEnumerable<ProductDto>>(Array.Empty<ProductDto>()); }
    }
    private sealed class CountingCoupons : ICouponService
    {
        public int Calls { get; private set; }
        public Task<CouponDto?> GetCoupon(string couponCode) { Calls++; return Task.FromResult<CouponDto?>(null); }
    }

    private static CartDto Request(string id) => new() { CartHeader = new CartHeaderDto { UserId = id } };
    private static AppDbContext CreateDb() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private CartAPIController Controller(AppDbContext db, Bus bus, bool admin = false, string? email = "owner@example.invalid", bool productsAvailable = true, bool couponAvailable = true, bool productThrows = false, bool couponThrows = false)
    {
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, owner) };
        if (email is not null) claims.Add(new Claim(JwtRegisteredClaimNames.Email, email));
        if (admin) claims.Add(new Claim(ClaimTypes.Role, "ADMIN"));
        var controller = new CartAPIController(db, MappingConfig.RegisterMaps().CreateMapper(), new Products(productsAvailable, productThrows), new Coupons(couponAvailable, couponThrows), bus, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["TopicAndQueueNames:EmailShoppingCartQueue"] = "emailshoppingcart" }).Build());
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) } };
        return controller;
    }

    private sealed class Products(bool available, bool throws) : IProductService
    {
        public Task<IEnumerable<ProductDto>> GetProducts() => throws ? throw new InvalidOperationException("Product service unavailable") : Task.FromResult<IEnumerable<ProductDto>>(available ? new[] { new ProductDto { ProductId = 7, Price = 10, Name = "Trusted" } } : Array.Empty<ProductDto>());
    }
    private sealed class Coupons(bool available, bool throws) : ICouponService
    {
        public Task<CouponDto?> GetCoupon(string couponCode) => throws ? throw new InvalidOperationException("Coupon service unavailable") : Task.FromResult<CouponDto?>(available ? new CouponDto { CouponCode = couponCode, DiscountAmount = 5, MinAmount = 1 } : null);
    }
    private sealed class Bus : IMessageBus
    {
        public int Calls { get; private set; }
        public object? Message { get; private set; }
        public string? Destination { get; private set; }
        public Task PublishMessage(object message, string topic_queue_Name) { Calls++; Message = message; Destination = topic_queue_Name; return Task.CompletedTask; }
    }
}
