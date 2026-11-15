using System.Net;
using Mango.Web.Controllers;
using Mango.Web.Models;
using Mango.Web.Service;
using Mango.Web.Service.IService;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;
using Mango.Web.Utility;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using Mango.Services.ShoppingCartAPI.Utility;

namespace Mango.Services.ShoppingCartAPI.Tests;

public class WebCompatibilityTests
{
    [Fact]
    public async Task FailedGetCartDoesNotThrowOrSendEmail()
    {
        var cart = new CartStub();
        var controller = new CartController(cart, null!) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        var result = await controller.EmailCart(new CartDto());
        Assert.NotNull(result);
        Assert.False(cart.EmailCalled);
    }

    [Fact]
    public async Task BadRequestWithSuccessShapedBodyIsStillFailure()
    {
        var service = new BaseService(new Factory(), new Token());
        var response = await service.SendAsync(new RequestDto { Url = "https://example.invalid/api/cart/GetCart/id" });
        Assert.NotNull(response);
        Assert.False(response.IsSuccess);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, true)]
    [InlineData(HttpStatusCode.BadRequest, false)]
    [InlineData(HttpStatusCode.Unauthorized, false)]
    [InlineData(HttpStatusCode.NotFound, false)]
    public async Task FiveCartClientsForwardBearerAndRespectHttpStatus(HttpStatusCode status, bool success)
    {
        var handler = new Handler(status);
        var cart = new CartService(new BaseService(new Factory(handler), new Token()));
        SD.ShoppingCartAPIBase = "https://example.invalid";
        var payload = new CartDto { CartHeader = new CartHeaderDto { UserId = Guid.NewGuid().ToString() } };
        var results = new[] { await cart.GetCartByUserIdAsnyc(payload.CartHeader.UserId), await cart.ApplyCouponAsync(payload), await cart.UpsertCartAsync(payload), await cart.RemoveFromCartAsync(1), await cart.EmailCart(payload) };
        Assert.All(results, result => Assert.Equal(success, Assert.IsType<ResponseDto>(result).IsSuccess));
        Assert.Equal(5, handler.Paths.Count);
        Assert.Equal(new[] { "/api/cart/GetCart/", "/api/cart/ApplyCoupon", "/api/cart/CartUpsert", "/api/cart/RemoveCart", "/api/cart/EmailCartRequest" }, handler.Paths.Select((path, index) => index == 0 ? path[..(path.LastIndexOf('/') + 1)] : path));
        Assert.All(handler.Tokens, token => Assert.Equal("Bearer synthetic", token));
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task CartActionsPreserveRejectedHttpStatus(HttpStatusCode status)
    {
        var handler = new Handler(status);
        SD.ShoppingCartAPIBase = "https://example.invalid";
        var service = new CartService(new BaseService(new Factory(handler), new Token()));
        var controller = new CartController(service, null!) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        var dto = new CartDto { CartHeader = new CartHeaderDto { UserId = Guid.NewGuid().ToString() } };
        Assert.Equal((int)status, Assert.IsType<StatusCodeResult>(await controller.CartIndex()).StatusCode);
        Assert.Equal((int)status, Assert.IsType<StatusCodeResult>(await controller.Remove(1)).StatusCode);
        Assert.Equal((int)status, Assert.IsType<StatusCodeResult>(await controller.ApplyCoupon(dto)).StatusCode);
        Assert.Equal((int)status, Assert.IsType<StatusCodeResult>(await controller.RemoveCoupon(dto)).StatusCode);
        Assert.Equal((int)status, Assert.IsType<StatusCodeResult>(await controller.EmailCart(dto)).StatusCode);
    }

    [Theory]
    [InlineData("Product")]
    [InlineData("Coupon")]
    public async Task DownstreamClientsForwardStoredAccessToken(string clientName)
    {
        var context = new DefaultHttpContext();
        context.RequestServices = new ServiceCollection().AddSingleton<IAuthenticationService, AuthStub>().BuildServiceProvider();
        var capture = new DownstreamHandler();
        var forwarding = new BackendApiAuthenticationHttpClientHandler(new HttpContextAccessor { HttpContext = context }) { InnerHandler = capture };
        var factory = new DownstreamFactory(new HttpClient(forwarding) { BaseAddress = new Uri("https://example.invalid") });
        if (clientName == "Product")
            Assert.Single(await new Mango.Services.ShoppingCartAPI.Service.ProductService(factory).GetProducts());
        else
            Assert.NotNull(await new Mango.Services.ShoppingCartAPI.Service.CouponService(factory).GetCoupon("SAVE"));
        Assert.Equal(clientName, factory.LastName);
        Assert.Equal("Bearer synthetic-access-token", capture.Authorization);
    }

    [Fact]
    public async Task DownstreamUnauthorizedEmptyBodyBehavior()
    {
        var product = new Mango.Services.ShoppingCartAPI.Service.ProductService(new DownstreamFactory(new HttpClient(new UnauthorizedHandler()) { BaseAddress = new Uri("https://example.invalid") }));
        var coupon = new Mango.Services.ShoppingCartAPI.Service.CouponService(new DownstreamFactory(new HttpClient(new UnauthorizedHandler()) { BaseAddress = new Uri("https://example.invalid") }));
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => product.GetProducts());
        Assert.Equal(HttpStatusCode.Unauthorized, error.StatusCode);
        var couponError = await Assert.ThrowsAsync<HttpRequestException>(() => coupon.GetCoupon("SAVE"));
        Assert.Equal(HttpStatusCode.Unauthorized, couponError.StatusCode);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task ProductServiceRejectsDownstreamHttpFailuresWithoutTrustingBody(HttpStatusCode status)
    {
        var service = new Mango.Services.ShoppingCartAPI.Service.ProductService(new DownstreamFactory(new HttpClient(new Handler(status)) { BaseAddress = new Uri("https://example.invalid") }));
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => service.GetProducts());
        Assert.Equal(status, error.StatusCode);
        Assert.Equal("Product service request failed.", error.Message);
    }

    [Fact]
    public async Task ProductServiceRejectsEmptySuccessfulResponse()
    {
        var service = new Mango.Services.ShoppingCartAPI.Service.ProductService(new DownstreamFactory(new HttpClient(new EmptySuccessHandler()) { BaseAddress = new Uri("https://example.invalid") }));
        await Assert.ThrowsAsync<InvalidDataException>(() => service.GetProducts());
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task CouponServiceRejectsHttpFailures(HttpStatusCode status)
    {
        var service = new Mango.Services.ShoppingCartAPI.Service.CouponService(new DownstreamFactory(new HttpClient(new Handler(status)) { BaseAddress = new Uri("https://example.invalid") }));
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => service.GetCoupon("SAVE"));
        Assert.Equal(status, error.StatusCode);
        Assert.Equal("Coupon service request failed.", error.Message);
    }

    [Fact]
    public async Task CouponServiceDistinguishesNotFoundEmptySuccessAndCommunicationFailure()
    {
        var missing = new Mango.Services.ShoppingCartAPI.Service.CouponService(new DownstreamFactory(new HttpClient(new Handler(HttpStatusCode.NotFound)) { BaseAddress = new Uri("https://example.invalid") }));
        Assert.Null(await missing.GetCoupon("MISSING"));
        var empty = new Mango.Services.ShoppingCartAPI.Service.CouponService(new DownstreamFactory(new HttpClient(new EmptySuccessHandler()) { BaseAddress = new Uri("https://example.invalid") }));
        await Assert.ThrowsAsync<InvalidDataException>(() => empty.GetCoupon("SAVE"));
        var failed = new Mango.Services.ShoppingCartAPI.Service.CouponService(new DownstreamFactory(new HttpClient(new FailedHandler()) { BaseAddress = new Uri("https://example.invalid") }));
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => failed.GetCoupon("SAVE"));
        Assert.Equal("Coupon service request failed.", error.Message);
    }

    private sealed class FailedHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => throw new HttpRequestException("sensitive test detail");
    }

    private sealed class EmptySuccessHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("") });
    }

    private sealed class UnauthorizedHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("") });
    }

    private sealed class DownstreamFactory(HttpClient client) : IHttpClientFactory
    {
        public string? LastName { get; private set; }
        public HttpClient CreateClient(string name) { LastName = name; return client; }
    }
    private sealed class DownstreamHandler : HttpMessageHandler
    {
        public string? Authorization { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization?.ToString();
            var result = request.RequestUri!.AbsolutePath == "/api/product" ? "[{\"ProductId\":7}]" : "{\"CouponCode\":\"SAVE\"}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"IsSuccess\":true,\"Result\":" + result + "}") });
        }
    }
    private sealed class AuthStub : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
        {
            var properties = new AuthenticationProperties();
            properties.StoreTokens(new[] { new AuthenticationToken { Name = "access_token", Value = "synthetic-access-token" } });
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity()), properties, "test")));
        }
        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
    }

    private sealed class Factory(Handler? handler = null) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler ?? new Handler(), false);
    }
    private sealed class Handler(HttpStatusCode status = HttpStatusCode.BadRequest) : HttpMessageHandler
    {
        public List<string> Paths { get; } = new();
        public List<string?> Tokens { get; } = new();
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Paths.Add(request.RequestUri!.AbsolutePath);
            Tokens.Add(request.Headers.Authorization?.ToString());
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent("{}") });
        }
    }
    private sealed class Token : ITokenProvider
    {
        public string? GetToken() => "synthetic";
        public void SetToken(string token) { }
        public void ClearToken() { }
    }
    private sealed class CartStub : ICartService
    {
        public bool EmailCalled { get; private set; }
        public Task<ResponseDto?> GetCartByUserIdAsnyc(string userId) => Task.FromResult<ResponseDto?>(new ResponseDto { IsSuccess = false, Message = "Not Found" });
        public Task<ResponseDto?> EmailCart(CartDto cartDto) { EmailCalled = true; return Task.FromResult<ResponseDto?>(new ResponseDto()); }
        public Task<ResponseDto?> ApplyCouponAsync(CartDto cartDto) => throw new NotImplementedException();
        public Task<ResponseDto?> RemoveFromCartAsync(int cartDetailsId) => throw new NotImplementedException();
        public Task<ResponseDto?> UpsertCartAsync(CartDto cartDto) => throw new NotImplementedException();
    }
}
