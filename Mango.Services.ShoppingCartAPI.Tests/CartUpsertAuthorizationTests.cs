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

public class CartUpsertAuthorizationTests
{
    private readonly string Owner = Guid.NewGuid().ToString();
    private readonly string Other = Guid.NewGuid().ToString();
    private readonly string Missing = Guid.NewGuid().ToString();

    [Fact]
    public async Task OwnerCreatesCartAndAddsAndIncrementsProducts()
    {
        await using var db = CreateDb();
        var create = await Controller(db).CartUpsert(Request(Owner, 7, 2));
        Assert.True(Assert.IsType<ResponseDto>(create.Value).IsSuccess);
        var header = await db.CartHeaders.SingleAsync();
        Assert.Equal(Owner, header.UserId);
        Assert.Equal(2, (await db.CartDetails.SingleAsync()).Count);
        Assert.Equal(1, db.SaveCalls);

        var add = await Controller(db).CartUpsert(Request(Owner, 8, 1));
        Assert.True(Assert.IsType<ResponseDto>(add.Value).IsSuccess);
        Assert.Equal(2, await db.CartDetails.CountAsync());

        var update = await Controller(db).CartUpsert(Request(Owner, 7, 3));
        var response = Assert.IsType<ResponseDto>(update.Value);
        Assert.Equal(5, (await db.CartDetails.AsNoTracking().SingleAsync(x => x.ProductId == 7)).Count);
        Assert.Equal(5, Assert.Single(Assert.IsType<CartDto>(response.Result).CartDetails!).Count);
        Assert.Equal(3, db.SaveCalls);
    }

    [Fact]
    public async Task RejectsInvalidForeignAndAdminWithoutWritesOrDisclosure()
    {
        await using var db = CreateDb();
        db.CartHeaders.Add(new CartHeader { UserId = Other });
        await db.SaveChangesAsync();
        db.SaveCalls = 0;

        var foreign = await Controller(db).CartUpsert(Request(Other, 7, 1));
        var absent = await Controller(db).CartUpsert(Request(Missing, 7, 1));
        var admin = await Controller(db, true).CartUpsert(Request(Other, 7, 1));
        var invalid = await Controller(db).CartUpsert(Request("bad", 7, 1));
        var missing = await Controller(db).CartUpsert(Request(null, 7, 1));
        var noItems = await Controller(db).CartUpsert(new CartDto { CartHeader = new CartHeaderDto { UserId = Owner } });

        var foreignResult = Assert.IsType<NotFoundObjectResult>(foreign.Result);
        Assert.Equal(JsonSerializer.Serialize(foreignResult.Value), JsonSerializer.Serialize(Assert.IsType<NotFoundObjectResult>(absent.Result).Value));
        Assert.Equal(JsonSerializer.Serialize(foreignResult.Value), JsonSerializer.Serialize(Assert.IsType<NotFoundObjectResult>(admin.Result).Value));
        Assert.IsType<BadRequestObjectResult>(invalid.Result);
        Assert.IsType<BadRequestObjectResult>(missing.Result);
        Assert.IsType<BadRequestObjectResult>(noItems.Result);
        Assert.Equal(0, db.SaveCalls);
        Assert.Single(db.CartHeaders);
        Assert.Empty(db.CartDetails);
        Assert.DoesNotContain(db.ChangeTracker.Entries(), e => e.State != EntityState.Unchanged);
    }

    [Fact]
    public async Task ClientSuppliedNestedIdentifiersCannotMutateForeignEntities()
    {
        await using var db = CreateDb();
        var foreign = new CartHeader { UserId = Other, CouponCode = "ORIGINAL" };
        var foreignDetail = new CartDetails { CartHeader = foreign, ProductId = 7, Count = 4 };
        db.CartDetails.Add(foreignDetail);
        await db.SaveChangesAsync();
        var request = Request(Owner, 7, 2);
        request.CartHeader.CartHeaderId = foreign.CartHeaderId;
        var detail = request.CartDetails!.Single();
        detail.CartDetailsId = foreignDetail.CartDetailsId;
        detail.CartHeaderId = foreign.CartHeaderId;
        detail.CartHeader = new CartHeaderDto { CartHeaderId = foreign.CartHeaderId, UserId = Other, CouponCode = "ALTERED" };

        var result = await Controller(db).CartUpsert(request);

        Assert.IsType<NotFoundObjectResult>(result.Result);
        Assert.Equal("ORIGINAL", (await db.CartHeaders.SingleAsync(x => x.UserId == Other)).CouponCode);
        Assert.Equal(4, (await db.CartDetails.SingleAsync(x => x.CartDetailsId == foreignDetail.CartDetailsId)).Count);
        Assert.False(await db.CartHeaders.AnyAsync(x => x.UserId == Owner));
    }

    [Fact]
    public async Task ConsistentPersistedReferencesAreAccepted()
    {
        await using var db = CreateDb();
        var header = new CartHeader { UserId = Owner };
        var existing = new CartDetails { CartHeader = header, ProductId = 7, Count = 2 };
        db.CartDetails.Add(existing);
        await db.SaveChangesAsync();
        var request = Request(Owner, 7, 3);
        request.CartHeader.CartHeaderId = header.CartHeaderId;
        var item = request.CartDetails!.Single();
        item.CartDetailsId = existing.CartDetailsId;
        item.CartHeaderId = header.CartHeaderId;
        item.CartHeader = new CartHeaderDto { CartHeaderId = header.CartHeaderId, UserId = Owner };
        item.Product = new ProductDto { ProductId = 7 };

        var result = await Controller(db).CartUpsert(request);

        Assert.True(Assert.IsType<ResponseDto>(result.Value).IsSuccess);
        Assert.Equal(5, (await db.CartDetails.AsNoTracking().SingleAsync()).Count);
    }

    [Theory]
    [InlineData("foreign-header", 404)]
    [InlineData("foreign-detail", 404)]
    [InlineData("foreign-nested-user", 404)]
    [InlineData("missing-header", 404)]
    [InlineData("missing-detail", 404)]
    [InlineData("contradictory-header", 404)]
    [InlineData("contradictory-product", 404)]
    [InlineData("negative-header", 400)]
    [InlineData("negative-detail", 400)]
    [InlineData("malformed-nested-user", 400)]
    public async Task RejectsInvalidReferencesWithoutWritesOrDtoMutation(string testCase, int expectedStatus)
    {
        await using var db = CreateDb();
        var own = new CartHeader { UserId = Owner };
        var other = new CartHeader { UserId = Other };
        var ownDetail = new CartDetails { CartHeader = own, ProductId = 7, Count = 2 };
        var otherDetail = new CartDetails { CartHeader = other, ProductId = 7, Count = 4 };
        db.CartDetails.AddRange(ownDetail, otherDetail);
        await db.SaveChangesAsync();
        db.SaveCalls = 0;
        var request = Request(Owner, 7, 1);
        var item = request.CartDetails!.Single();
        switch (testCase)
        {
            case "foreign-header": request.CartHeader.CartHeaderId = other.CartHeaderId; break;
            case "foreign-detail": item.CartDetailsId = otherDetail.CartDetailsId; break;
            case "foreign-nested-user": item.CartHeader = new CartHeaderDto { UserId = Other }; break;
            case "missing-header": request.CartHeader.CartHeaderId = 99999; break;
            case "missing-detail": item.CartDetailsId = 99999; break;
            case "contradictory-header": request.CartHeader.CartHeaderId = own.CartHeaderId; item.CartHeaderId = other.CartHeaderId; break;
            case "contradictory-product": item.Product = new ProductDto { ProductId = 8 }; break;
            case "negative-header": item.CartHeaderId = -1; break;
            case "negative-detail": item.CartDetailsId = -1; break;
            case "malformed-nested-user": item.CartHeader = new CartHeaderDto { UserId = "invalid" }; break;
        }
        var before = JsonSerializer.Serialize(request);

        var result = await Controller(db).CartUpsert(request);

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result.Result);
        Assert.Equal(expectedStatus, objectResult.StatusCode);
        Assert.Equal(before, JsonSerializer.Serialize(request));
        Assert.Equal(0, db.SaveCalls);
        Assert.Equal(2, (await db.CartDetails.SingleAsync(x => x.CartDetailsId == ownDetail.CartDetailsId)).Count);
        Assert.Equal(4, (await db.CartDetails.SingleAsync(x => x.CartDetailsId == otherDetail.CartDetailsId)).Count);
        Assert.DoesNotContain(db.ChangeTracker.Entries(), e => e.State != EntityState.Unchanged);
    }

    private static CartDto Request(string? id, int product, int count) => new()
    {
        CartHeader = new CartHeaderDto { UserId = id },
        CartDetails = new[] { new CartDetailsDto { ProductId = product, Count = count } }
    };

    private SqlLabTestContext CreateDb() => SqlLabTestContext.Create(Owner, Other, Missing);

    private CartAPIController Controller(AppDbContext db, bool admin = false)
    {
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, Owner) };
        if (admin) claims.Add(new Claim(ClaimTypes.Role, "ADMIN"));
        var controller = new CartAPIController(db, MappingConfig.RegisterMaps().CreateMapper(), new Products(), new Coupons(), new Bus(), new ConfigurationBuilder().Build());
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) } };
        return controller;
    }

    private sealed class Bus : IMessageBus { public Task PublishMessage(object message, string topic_queue_Name) => throw new InvalidOperationException(); }
    private sealed class Products : IProductService { public Task<IEnumerable<ProductDto>> GetProducts() => throw new InvalidOperationException(); }
    private sealed class Coupons : ICouponService { public Task<CouponDto?> GetCoupon(string couponCode) => throw new InvalidOperationException(); }
}
