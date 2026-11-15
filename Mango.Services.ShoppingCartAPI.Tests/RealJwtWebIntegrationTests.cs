using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Mango.MessageBus.Service.IService;
using Mango.Services.AuthAPI.Models;
using Mango.Services.AuthAPI.Service;
using Mango.Services.ShoppingCartAPI;
using Mango.Services.ShoppingCartAPI.Controllers;
using Mango.Services.ShoppingCartAPI.Data;
using Mango.Services.ShoppingCartAPI.Extensions;
using Mango.Services.ShoppingCartAPI.Service;
using Mango.Services.ShoppingCartAPI.Service.IService;
using Mango.Services.ShoppingCartAPI.Utility;
using Mango.Web.Service;
using Mango.Web.Utility;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;
using CartHeader = Mango.Services.ShoppingCartAPI.Models.CartHeader;
using CartDetails = Mango.Services.ShoppingCartAPI.Models.CartDetails;

namespace Mango.Services.ShoppingCartAPI.Tests;

public class RealJwtWebIntegrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IssuedJwtFlowsFromWebToOwnedCartAndDownstream(bool useSqlLab)
    {
        const string secret = "test-only-signing-key-32-bytes-long-12345";
        var owner = Guid.NewGuid().ToString();
        var foreign = Guid.NewGuid().ToString();
        await using var cleanup = useSqlLab ? SqlLabTestContext.Create(owner, foreign) : null;
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["ApiSettings:Secret"] = secret, ["ApiSettings:Issuer"] = "integration-test", ["ApiSettings:Audience"] = "integration-test", ["TopicAndQueueNames:EmailShoppingCartQueue"] = "synthetic" });
        builder.AddAppAuthetication();
        builder.Services.AddAuthorization();
        builder.Services.AddControllers().AddApplicationPart(typeof(CartAPIController).Assembly);
        if (useSqlLab)
            builder.Services.AddScoped<AppDbContext>(_ => SqlLabTestContext.Create());
        else
            builder.Services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(owner));
        builder.Services.AddSingleton(MappingConfig.RegisterMaps().CreateMapper());
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddTransient<BackendApiAuthenticationHttpClientHandler>();
        var product = new CaptureHandler(true);
        var coupon = new CaptureHandler(false);
        builder.Services.AddHttpClient("Product", c => c.BaseAddress = new Uri("http://product.invalid")).ConfigurePrimaryHttpMessageHandler(() => product).AddHttpMessageHandler<BackendApiAuthenticationHttpClientHandler>();
        builder.Services.AddHttpClient("Coupon", c => c.BaseAddress = new Uri("http://coupon.invalid")).ConfigurePrimaryHttpMessageHandler(() => coupon).AddHttpMessageHandler<BackendApiAuthenticationHttpClientHandler>();
        builder.Services.AddScoped<IProductService, Mango.Services.ShoppingCartAPI.Service.ProductService>();
        builder.Services.AddScoped<ICouponService, Mango.Services.ShoppingCartAPI.Service.CouponService>();
        var bus = new Bus();
        builder.Services.AddSingleton<IMessageBus>(bus);
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.CartDetails.Add(new CartDetails { CartHeader = new CartHeader { UserId = owner, CouponCode = "SAVE" }, ProductId = 7, Count = 1 });
            await db.SaveChangesAsync();
        }
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        var token = new JwtTokenGenerator(Options.Create(new JwtOptions { Secret = secret, Issuer = "integration-test", Audience = "integration-test" })).GenerateToken(new ApplicationUser { Id = owner, Email = "owner@example.invalid", UserName = "synthetic" }, Array.Empty<string>());
        var webContext = new DefaultHttpContext();
        webContext.Request.Headers.Cookie = SD.TokenCookie + "=" + token;
        var cart = new CartService(new BaseService(new WebFactory(), new TokenProvider(new HttpContextAccessor { HttpContext = webContext })));
        SD.ShoppingCartAPIBase = address;
        var result = await cart.GetCartByUserIdAsnyc(owner);
        Assert.True(result!.IsSuccess);
        var validHeader = (Newtonsoft.Json.Linq.JObject)Newtonsoft.Json.Linq.JObject.FromObject(result.Result!).GetValue("CartHeader", StringComparison.OrdinalIgnoreCase)!;
        Assert.Equal(1, (double?)validHeader.GetValue("Discount", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("Bearer " + token, product.Authorization);
        Assert.Equal("Bearer " + token, coupon.Authorization);
        using var client = new HttpClient { BaseAddress = new Uri(address) };
        foreach (var path in new[] { "ApplyCoupon", "CartUpsert", "RemoveCart", "EmailCartRequest" })
        {
            using var response = await client.PostAsync("/api/cart/" + path, new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/cart/GetCart/" + owner)).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/cart/GetCart/malformed")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/cart/GetCart/" + foreign)).StatusCode);
        Assert.Equal(0, bus.Calls);
        var message = new Mango.Web.Models.CartDto { CartHeader = new Mango.Web.Models.CartHeaderDto { UserId = owner } };
        Assert.True((await cart.EmailCart(message))!.IsSuccess);
        Assert.Equal(1, bus.Calls);
        product.Status = HttpStatusCode.Unauthorized;
        Assert.False((await cart.GetCartByUserIdAsnyc(owner))!.IsSuccess);
        Assert.False((await cart.EmailCart(message))!.IsSuccess);
        Assert.Equal(1, bus.Calls);
        if (useSqlLab)
        {
            product.Status = HttpStatusCode.OK;
            coupon.Status = HttpStatusCode.OK;
            var claims = new JwtSecurityTokenHandler().ReadJwtToken(token).Claims;
            webContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "integration"));
            var mvc = new Mango.Web.Controllers.CartController(cart, null!) { ControllerContext = new ControllerContext { HttpContext = webContext }, TempData = new TempDataDictionary(webContext, new TempProvider()) };
            var home = new Mango.Web.Controllers.HomeController(null!, cart) { ControllerContext = new ControllerContext { HttpContext = webContext }, TempData = new TempDataDictionary(webContext, new TempProvider()) };
            Assert.IsType<ViewResult>(await mvc.CartIndex());
            Assert.IsType<RedirectToActionResult>(await home.ProductDetails(new Mango.Web.Models.ProductDto { ProductId = 7, Count = 1 }));
            Assert.IsType<RedirectToActionResult>(await mvc.ApplyCoupon(new Mango.Web.Models.CartDto { CartHeader = new Mango.Web.Models.CartHeaderDto { UserId = owner, CouponCode = "SAVE" } }));
            var emailResult = await mvc.EmailCart(message);
            Assert.True(emailResult is RedirectToActionResult, $"EmailCart returned HTTP {(emailResult as StatusCodeResult)?.StatusCode}");
            Assert.Equal(2, bus.Calls);
            await using var verify = SqlLabTestContext.Create();
            var detail = await verify.CartDetails.AsNoTracking().SingleAsync(x => x.CartHeader.UserId == owner);
            Assert.Equal(2, detail.Count);
            var otherToken = new JwtTokenGenerator(Options.Create(new JwtOptions { Secret = secret, Issuer = "integration-test", Audience = "integration-test" })).GenerateToken(new ApplicationUser { Id = foreign, Email = "other@example.invalid", UserName = "synthetic-other" }, Array.Empty<string>());
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);
            using var rejected = await client.PostAsync("/api/cart/RemoveCart", new StringContent(detail.CartDetailsId.ToString(), System.Text.Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.NotFound, rejected.StatusCode);
            var foreignPayload = new Mango.Web.Models.CartDto { CartHeader = new Mango.Web.Models.CartHeaderDto { UserId = owner, CouponCode = "ATTACK" }, CartDetails = new[] { new Mango.Web.Models.CartDetailsDto { ProductId = 7, Count = 99 } } };
            using var rejectedCoupon = await client.PostAsJsonAsync("/api/cart/ApplyCoupon", foreignPayload);
            using var rejectedUpsert = await client.PostAsJsonAsync("/api/cart/CartUpsert", foreignPayload);
            Assert.Equal(HttpStatusCode.NotFound, rejectedCoupon.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, rejectedUpsert.StatusCode);
            Assert.Equal(2, (await verify.CartDetails.AsNoTracking().SingleAsync(x => x.CartDetailsId == detail.CartDetailsId)).Count);
            Assert.Equal("SAVE", (await verify.CartHeaders.AsNoTracking().SingleAsync(x => x.UserId == owner)).CouponCode);
            Assert.IsType<RedirectToActionResult>(await mvc.Remove(detail.CartDetailsId));
            Assert.False(await verify.CartDetails.AsNoTracking().AnyAsync(x => x.CartDetailsId == detail.CartDetailsId));
            return;
        }
        product.Status = HttpStatusCode.OK;
        coupon.Status = HttpStatusCode.Unauthorized;
        var couponFailureCart = await cart.GetCartByUserIdAsnyc(owner);
        Assert.False(couponFailureCart!.IsSuccess);
        Assert.Equal(HttpStatusCode.InternalServerError, (await client.GetAsync("/api/cart/GetCart/" + owner)).StatusCode);
        Assert.False((await cart.EmailCart(message))!.IsSuccess);
        Assert.Equal(1, bus.Calls);
        coupon.Status = HttpStatusCode.NotFound;
        var missingCouponCart = await cart.GetCartByUserIdAsnyc(owner);
        Assert.True(missingCouponCart!.IsSuccess);
        var missingHeader = (Newtonsoft.Json.Linq.JObject)Newtonsoft.Json.Linq.JObject.FromObject(missingCouponCart.Result!).GetValue("CartHeader", StringComparison.OrdinalIgnoreCase)!;
        Assert.Equal(0, (double?)missingHeader.GetValue("Discount", StringComparison.OrdinalIgnoreCase));
        Assert.False((await cart.EmailCart(message))!.IsSuccess);
        Assert.Equal(1, bus.Calls);
    }

    private sealed class WebFactory : IHttpClientFactory { public HttpClient CreateClient(string name) => new(); }
    private sealed class TempProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
    private sealed class Bus : IMessageBus
    {
        public int Calls { get; private set; }
        public Task PublishMessage(object message, string topic_queue_Name) { Calls++; return Task.CompletedTask; }
    }
    private sealed class CaptureHandler(bool isProduct) : HttpMessageHandler
    {
        public string? Authorization { get; private set; }
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization?.ToString();
            var data = isProduct ? "[{\"ProductId\":7,\"Price\":10,\"Name\":\"Synthetic\",\"Description\":\"Synthetic\",\"CategoryName\":\"Synthetic\",\"ImageUrl\":\"https://example.invalid/image\"}]" : "{\"CouponCode\":\"SAVE\",\"DiscountAmount\":1,\"MinAmount\":0}";
            return Task.FromResult(new HttpResponseMessage(Status) { Content = new StringContent(Status == HttpStatusCode.OK ? "{\"IsSuccess\":true,\"Result\":" + data + "}" : "") });
        }
    }
}
