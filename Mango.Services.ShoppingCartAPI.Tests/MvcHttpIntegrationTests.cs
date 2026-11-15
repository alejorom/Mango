using System.Net;
using Mango.MessageBus.Service.IService;
using Mango.Services.AuthAPI.Models;
using Mango.Services.AuthAPI.Service;
using Mango.Services.ShoppingCartAPI;
using Mango.Services.ShoppingCartAPI.Controllers;
using Mango.Services.ShoppingCartAPI.Data;
using Mango.Services.ShoppingCartAPI.Extensions;
using Mango.Web.Service;
using Mango.Web.Service.IService;
using Mango.Web.Utility;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Xunit;
using WebModels = Mango.Web.Models;
using ApiModels = Mango.Services.ShoppingCartAPI.Models.Dto;

namespace Mango.Services.ShoppingCartAPI.Tests;

public class MvcHttpIntegrationTests
{
    [Fact]
    public async Task LoginCookieMvcViewsAndCartAuthorizationUseRealJwt()
    {
        const string secret = "test-only-signing-key-32-bytes-long-12345";
        var owner = Guid.NewGuid().ToString();
        var other = Guid.NewGuid().ToString();
        await using var cleanup = SqlLabTestContext.Create(owner, other);
        var token = new JwtTokenGenerator(Options.Create(new JwtOptions { Secret = secret, Issuer = "mvc-test", Audience = "mvc-test" })).GenerateToken(new ApplicationUser { Id = owner, Email = "owner@example.invalid", UserName = "synthetic" }, new[] { "CUSTOMER" });
        var apiBuilder = WebApplication.CreateBuilder();
        apiBuilder.Logging.ClearProviders();
        apiBuilder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        apiBuilder.WebHost.UseUrls("http://127.0.0.1:0");
        apiBuilder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["ApiSettings:Secret"] = secret, ["ApiSettings:Issuer"] = "mvc-test", ["ApiSettings:Audience"] = "mvc-test", ["TopicAndQueueNames:EmailShoppingCartQueue"] = "synthetic" });
        apiBuilder.AddAppAuthetication();
        apiBuilder.Services.AddAuthorization();
        apiBuilder.Services.AddControllers().AddApplicationPart(typeof(CartAPIController).Assembly);
        apiBuilder.Services.AddScoped<AppDbContext>(_ => SqlLabTestContext.Create());
        apiBuilder.Services.AddSingleton(MappingConfig.RegisterMaps().CreateMapper());
        apiBuilder.Services.AddSingleton<Mango.Services.ShoppingCartAPI.Service.IService.IProductService, Products>();
        apiBuilder.Services.AddSingleton<Mango.Services.ShoppingCartAPI.Service.IService.ICouponService, Coupons>();
        var bus = new Bus();
        apiBuilder.Services.AddSingleton<IMessageBus>(bus);
        await using var api = apiBuilder.Build();
        string? received = null;
        api.Use(async (context, next) => { received = context.Request.Headers.Authorization.ToString(); await next(context); });
        api.UseAuthentication();
        api.UseAuthorization();
        api.MapControllers();
        using (var scope = api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.CartDetails.Add(new Mango.Services.ShoppingCartAPI.Models.CartDetails { CartHeader = new Mango.Services.ShoppingCartAPI.Models.CartHeader { UserId = owner }, ProductId = 7, Count = 1 });
            await db.SaveChangesAsync();
        }
        await api.StartAsync();
        SD.ShoppingCartAPIBase = Address(api);

        var webBuilder = WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName = typeof(Mango.Web.Controllers.AuthController).Assembly.FullName });
        webBuilder.Logging.ClearProviders();
        webBuilder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        webBuilder.WebHost.UseUrls("http://127.0.0.1:0");
        webBuilder.Services.AddControllersWithViews().AddApplicationPart(typeof(Mango.Web.Controllers.AuthController).Assembly);
        webBuilder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        webBuilder.Services.AddHttpContextAccessor();
        webBuilder.Services.AddHttpClient();
        webBuilder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o => o.LoginPath = "/Auth/Login");
        webBuilder.Services.AddAuthorization();
        webBuilder.Services.AddScoped<ITokenProvider, TokenProvider>();
        webBuilder.Services.AddScoped<IBaseService, BaseService>();
        webBuilder.Services.AddScoped<ICartService, CartService>();
        webBuilder.Services.AddScoped<Mango.Web.Service.IService.IProductService, Mango.Web.Service.ProductService>();
        webBuilder.Services.AddScoped<IOrderService, OrderService>();
        webBuilder.Services.AddSingleton<IAuthService>(new Auth(token));
        await using var web = webBuilder.Build();
        web.UseRouting();
        web.UseAuthentication();
        web.UseAuthorization();
        web.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
        await web.StartAsync();
        var cookieJar = new CookieContainer();
        using var browser = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = cookieJar }) { BaseAddress = new Uri(Address(web)) };
        using var anonymous = await browser.GetAsync("/Cart/CartIndex");
        Assert.Equal(HttpStatusCode.Redirect, anonymous.StatusCode);
        Assert.Equal("/Auth/Login", anonymous.Headers.Location!.IsAbsoluteUri ? anonymous.Headers.Location.AbsolutePath : anonymous.Headers.Location.OriginalString.Split('?')[0]);
        using var loginPage = await browser.GetAsync("/Auth/Login");
        Assert.Equal(HttpStatusCode.OK, loginPage.StatusCode);
        Assert.Contains("Login", await loginPage.Content.ReadAsStringAsync());
        using var login = await browser.PostAsync("/Auth/Login", new FormUrlEncodedContent(new Dictionary<string, string> { ["UserName"] = "synthetic", ["Password"] = "synthetic" }));
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.True(login.Headers.TryGetValues("Set-Cookie", out var cookies));
        Assert.Contains(cookies!, c => c.StartsWith(".AspNetCore.Cookies="));
        Assert.Contains(cookies!, c => c.StartsWith(SD.TokenCookie + "="));
        using var own = await browser.GetAsync("/Cart/CartIndex");
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.Contains("Shopping Cart", await own.Content.ReadAsStringAsync());
        Assert.Equal("Bearer " + token, received);
        using var foreign = await browser.PostAsync("/Cart/ApplyCoupon", new FormUrlEncodedContent(new Dictionary<string, string> { ["CartHeader.UserId"] = other, ["CartHeader.CouponCode"] = "SAVE" }));
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Null((await cleanup.CartHeaders.AsNoTracking().SingleAsync(x => x.UserId == owner)).CouponCode);
        Assert.Equal(0, bus.Calls);
        using var malformed = await browser.PostAsync("/Cart/ApplyCoupon", new FormUrlEncodedContent(new Dictionary<string, string> { ["CartHeader.UserId"] = "bad" }));
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        using var upsert = await browser.PostAsync("/Home/ProductDetails", new FormUrlEncodedContent(new Dictionary<string, string> { ["ProductId"] = "7", ["Count"] = "1" }));
        Assert.Equal(HttpStatusCode.Redirect, upsert.StatusCode);
        using var apply = await browser.PostAsync("/Cart/ApplyCoupon", new FormUrlEncodedContent(new Dictionary<string, string> { ["CartHeader.UserId"] = owner, ["CartHeader.CouponCode"] = "SAVE" }));
        Assert.Equal(HttpStatusCode.Redirect, apply.StatusCode);
        Assert.Equal("SAVE", (await cleanup.CartHeaders.AsNoTracking().SingleAsync(x => x.UserId == owner)).CouponCode);
        using var email = await browser.PostAsync("/Cart/EmailCart", new FormUrlEncodedContent(new Dictionary<string, string>()));
        Assert.Equal(HttpStatusCode.Redirect, email.StatusCode);
        Assert.Equal(1, bus.Calls);
        var detail = await cleanup.CartDetails.AsNoTracking().SingleAsync(x => x.CartHeader.UserId == owner);
        Assert.Equal(2, detail.Count);
        using var remove = await browser.GetAsync("/Cart/Remove?cartDetailsId=" + detail.CartDetailsId);
        Assert.Equal(HttpStatusCode.Redirect, remove.StatusCode);
        Assert.False(await cleanup.CartDetails.AsNoTracking().AnyAsync(x => x.CartDetailsId == detail.CartDetailsId));
        cookieJar.Add(browser.BaseAddress!, new Cookie(SD.TokenCookie, "invalid", "/"));
        using var invalidToken = await browser.GetAsync("/Cart/CartIndex");
        Assert.Equal(HttpStatusCode.Unauthorized, invalidToken.StatusCode);
    }

    private static string Address(WebApplication app) => app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
    private sealed class Auth(string token) : IAuthService
    {
        public Task<WebModels.ResponseDto?> LoginAsync(WebModels.LoginRequestDto request) => Task.FromResult<WebModels.ResponseDto?>(new WebModels.ResponseDto { Result = JsonConvert.SerializeObject(new WebModels.LoginResponseDto { Token = token }) });
        public Task<WebModels.ResponseDto?> RegisterAsync(WebModels.RegistrationRequestDto request) => throw new NotImplementedException();
        public Task<WebModels.ResponseDto?> AssignRoleAsync(WebModels.RegistrationRequestDto request) => throw new NotImplementedException();
    }
    private sealed class Products : Mango.Services.ShoppingCartAPI.Service.IService.IProductService
    {
        public Task<IEnumerable<ApiModels.ProductDto>> GetProducts() => Task.FromResult<IEnumerable<ApiModels.ProductDto>>(new[] { new ApiModels.ProductDto { ProductId = 7, Name = "Synthetic", Price = 10, Description = "Synthetic", CategoryName = "Synthetic", ImageUrl = "https://example.invalid/image" } });
    }
    private sealed class Coupons : Mango.Services.ShoppingCartAPI.Service.IService.ICouponService { public Task<ApiModels.CouponDto?> GetCoupon(string code) => Task.FromResult<ApiModels.CouponDto?>(new ApiModels.CouponDto { CouponCode = code, DiscountAmount = 1, MinAmount = 0 }); }
    private sealed class Bus : IMessageBus { public int Calls { get; private set; } public Task PublishMessage(object message, string topic_queue_Name) { Calls++; return Task.CompletedTask; } }
}
