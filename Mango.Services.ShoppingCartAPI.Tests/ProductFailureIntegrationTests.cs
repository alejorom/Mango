using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Mango.MessageBus.Service.IService;
using Mango.Services.ShoppingCartAPI;
using Mango.Services.ShoppingCartAPI.Controllers;
using Mango.Services.ShoppingCartAPI.Data;
using Mango.Services.ShoppingCartAPI.Models;
using Mango.Services.ShoppingCartAPI.Models.Dto;
using Mango.Services.ShoppingCartAPI.Service;
using Mango.Services.ShoppingCartAPI.Service.IService;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Mango.Services.ShoppingCartAPI.Tests;

public class ProductFailureIntegrationTests
{
    [Fact]
    public async Task ProductFailureDoesNotSucceedForCartWithoutItems()
    {
        var owner = Guid.NewGuid().ToString();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(owner).Options);
        db.CartHeaders.Add(new CartHeader { UserId = owner });
        await db.SaveChangesAsync();
        var bus = new Bus();
        var controller = new CartAPIController(db, MappingConfig.RegisterMaps().CreateMapper(), new ProductService(new Factory(false)), new Coupons(), bus, new ConfigurationBuilder().Build());
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(JwtRegisteredClaimNames.Sub, owner) }, "test")) } };
        var result = await controller.GetCart(owner);
        var http = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, http.StatusCode);
        Assert.False(Assert.IsType<ResponseDto>(http.Value).IsSuccess);
        Assert.Equal(0, bus.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProductFailureDoesNotSucceedLeakOrPublish(bool transportFailure)
    {
        var owner = Guid.NewGuid().ToString();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(owner).Options);
        db.CartDetails.Add(new CartDetails { CartHeader = new CartHeader { UserId = owner }, ProductId = 7, Count = 2 });
        await db.SaveChangesAsync();
        var products = new ProductService(new Factory(transportFailure));
        if (transportFailure)
            await Assert.ThrowsAsync<HttpRequestException>(() => products.GetProducts());
        else
            await Assert.ThrowsAsync<InvalidDataException>(() => products.GetProducts());
        var bus = new Bus();
        var claims = new[] { new Claim(JwtRegisteredClaimNames.Sub, owner), new Claim(JwtRegisteredClaimNames.Email, "owner@example.invalid") };
        CartAPIController Controller()
        {
            var controller = new CartAPIController(db, MappingConfig.RegisterMaps().CreateMapper(), products, new Coupons(), bus, new ConfigurationBuilder().Build());
            controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) } };
            return controller;
        }

        var get = await Controller().GetCart(owner);
        var getResponse = Assert.IsType<ResponseDto>(Assert.IsType<ObjectResult>(get.Result).Value);
        Assert.False(getResponse.IsSuccess);
        Assert.Equal(500, Assert.IsType<ObjectResult>(get.Result).StatusCode);
        Assert.DoesNotContain("sensitive-test-detail", JsonSerializer.Serialize(getResponse));
        var email = await Controller().EmailCartRequest(new CartDto { CartHeader = new CartHeaderDto { UserId = owner } });
        var emailResult = Assert.IsType<ObjectResult>(email.Result);
        Assert.Equal(500, emailResult.StatusCode);
        Assert.DoesNotContain("sensitive-test-detail", JsonSerializer.Serialize(emailResult.Value));
        Assert.Equal(0, bus.Calls);
        Assert.Equal(2, (await db.CartDetails.SingleAsync()).Count);
        Assert.DoesNotContain(db.ChangeTracker.Entries(), e => e.State != EntityState.Unchanged);
    }

    [Fact]
    public async Task ValidEmptyProductListAllowsEmptyCart()
    {
        var owner = Guid.NewGuid().ToString();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(owner).Options);
        db.CartHeaders.Add(new CartHeader { UserId = owner });
        await db.SaveChangesAsync();
        var products = new ProductService(new EmptyFactory());
        Assert.Empty(await products.GetProducts());
        var bus = new Bus();
        var controller = new CartAPIController(db, MappingConfig.RegisterMaps().CreateMapper(), products, new Coupons(), bus, new ConfigurationBuilder().Build());
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(JwtRegisteredClaimNames.Sub, owner) }, "test")) } };
        var result = await controller.GetCart(owner);
        Assert.True(Assert.IsType<ResponseDto>(result.Value).IsSuccess);
        Assert.Equal(0, bus.Calls);
    }

    private sealed class EmptyFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new EmptyHandler()) { BaseAddress = new Uri("https://example.invalid") };
    }
    private sealed class EmptyHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"IsSuccess\":true,\"Result\":[]}") });
    }

    private sealed class Factory(bool transportFailure) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new Handler(transportFailure)) { BaseAddress = new Uri("https://example.invalid") };
    }
    private sealed class Handler(bool transportFailure) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (transportFailure) throw new HttpRequestException("sensitive-test-detail");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"IsSuccess\":false,\"Message\":\"sensitive-test-detail\"}") });
        }
    }
    private sealed class Coupons : ICouponService { public Task<CouponDto?> GetCoupon(string couponCode) => throw new InvalidOperationException("Unexpected coupon call"); }
    private sealed class Bus : IMessageBus { public int Calls { get; private set; } public Task PublishMessage(object message, string topic_queue_Name) { Calls++; return Task.CompletedTask; } }
}
