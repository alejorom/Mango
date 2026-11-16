using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Mango.MessageBus.Service.IService;
using Mango.Services.AuthAPI.Models;
using Mango.Services.AuthAPI.Service;
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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Xunit;
using Api = Mango.Services.ShoppingCartAPI.Models.Dto;
using WebModels = Mango.Web.Models;

namespace Mango.Services.ShoppingCartAPI.Tests;

public class CartEmptyContractTests
{
    [Fact]
    public async Task OwnMissingCartRoundTripsOverHttpAndRendersEmptyWithoutSideEffects()
    {
        await using var host = await Harness.StartAsync();
        host.State.ProductStatus = HttpStatusCode.ServiceUnavailable;
        host.State.CouponStatus = HttpStatusCode.ServiceUnavailable;
        for (var read = 0; read < 2; read++)
        {
            using var response = await host.ApiClient.GetAsync("/api/cart/GetCart/" + host.Owner);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var dto = JsonConvert.DeserializeObject<WebModels.ResponseDto>(await response.Content.ReadAsStringAsync())!;
            Assert.True(dto.IsSuccess);
            var cart = JsonConvert.DeserializeObject<WebModels.CartDto>(Convert.ToString(dto.Result)!)!;
            Assert.NotNull(cart.CartHeader);
            Assert.Equal(0, cart.CartHeader.CartHeaderId);
            Assert.Equal(host.Owner, cart.CartHeader.UserId);
            Assert.Equal(0, cart.CartHeader.CartTotal);
            Assert.Equal(0, cart.CartHeader.Discount);
            Assert.NotNull(cart.CartDetails);
            Assert.Empty(cart.CartDetails);
            using var page = await host.Browser.GetAsync("/Cart/CartIndex");
            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            Assert.Contains("Please add items to cart.", await page.Content.ReadAsStringAsync());
        }
        await using var db = host.CreateDb();
        Assert.Empty(await db.CartHeaders.ToListAsync());
        Assert.Empty(await db.CartDetails.ToListAsync());
        host.AssertNoEffects();
        Assert.Equal(0, host.State.ProductCalls);
        Assert.Equal(0, host.State.CouponCalls);
        Assert.Equal("Bearer " + host.Token, host.State.LastBearer);
    }

    [Fact]
    public async Task SuccessfulLastRemovalRedirectIsFollowedWithValidSessionToEmptyView()
    {
        await using var host = await Harness.StartAsync(true, true);
        using var populated = await host.Browser.GetAsync("/Cart/CartIndex");
        Assert.Contains("Synthetic product", await populated.Content.ReadAsStringAsync());
        host.State.FakeRemoval = true;
        using var removed = await host.Browser.GetAsync("/Cart/Remove?cartDetailsId=" + host.DetailId);
        Assert.Equal(HttpStatusCode.Redirect, removed.StatusCode);
        Assert.Equal("/Cart/CartIndex", removed.Headers.Location!.OriginalString);
        using var empty = await host.Browser.GetAsync(removed.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, empty.StatusCode);
        Assert.Contains("Please add items to cart.", await empty.Content.ReadAsStringAsync());
        Assert.Equal(1, host.State.RemoveCalls);
        host.AssertNoEffects();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PersistedHeaderRetainsPresentationCheckoutAndEmail(bool withDetails)
    {
        await using var host = await Harness.StartAsync(true, withDetails);
        using var api = await host.ApiClient.GetAsync("/api/cart/GetCart/" + host.Owner);
        Assert.Equal(HttpStatusCode.OK, api.StatusCode);
        var envelope = JsonConvert.DeserializeObject<WebModels.ResponseDto>(await api.Content.ReadAsStringAsync())!;
        var cart = JsonConvert.DeserializeObject<WebModels.CartDto>(Convert.ToString(envelope.Result)!)!;
        Assert.True(envelope.IsSuccess);
        Assert.True(cart.CartHeader.CartHeaderId > 0);
        Assert.Equal(withDetails ? 15 : 0, cart.CartHeader.CartTotal);
        Assert.Equal(withDetails ? 5 : 0, cart.CartHeader.Discount);
        using var index = await host.Browser.GetAsync("/Cart/CartIndex");
        Assert.Equal(HttpStatusCode.OK, index.StatusCode);
        Assert.Contains(withDetails ? "Synthetic product" : "Please add items to cart.", await index.Content.ReadAsStringAsync());
        using var checkout = await host.Browser.GetAsync("/Cart/Checkout");
        Assert.Equal(HttpStatusCode.OK, checkout.StatusCode);
        using var ordered = await host.Browser.PostAsync("/Cart/Checkout", Contact());
        Assert.Equal(HttpStatusCode.SeeOther, ordered.StatusCode);
        Assert.Equal(1, host.Orders.Created);
        Assert.Equal(1, host.Orders.StripeCalls);
        using var email = await host.Browser.PostAsync("/Cart/EmailCart", new FormUrlEncodedContent([]));
        Assert.Equal(HttpStatusCode.Redirect, email.StatusCode);
        Assert.Equal(1, host.State.EmailRequests);
        Assert.Equal(1, host.Bus.Calls);
    }

    [Theory]
    [InlineData("checkout-get")]
    [InlineData("checkout-post")]
    [InlineData("email")]
    public async Task VirtualCartIsRejectedBeforeSensitiveOperations(string action)
    {
        await using var host = await Harness.StartAsync();
        using var response = action switch
        {
            "checkout-get" => await host.Browser.GetAsync("/Cart/Checkout"),
            "checkout-post" => await host.Browser.PostAsync("/Cart/Checkout", Contact()),
            _ => await host.Browser.PostAsync("/Cart/EmailCart", new FormUrlEncodedContent([]))
        };
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        host.AssertNoEffects();
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("invalid")]
    [InlineData("expired")]
    [InlineData("no-subject")]
    [InlineData("blank-subject")]
    public async Task InvalidBearerNeverProducesEmptySuccess(string kind)
    {
        await using var host = await Harness.StartAsync();
        var token = kind switch
        {
            "invalid" => "invalid",
            "expired" => host.SignedToken(host.Owner, true),
            "no-subject" => host.SignedToken(null),
            "blank-subject" => host.SignedToken(" "),
            _ => ""
        };
        host.Cookies.Add(host.Browser.BaseAddress!, new Cookie(SD.TokenCookie, token, "/"));
        using var response = await host.Browser.GetAsync("/Cart/CartIndex");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain("Please add items", await response.Content.ReadAsStringAsync());
        host.AssertNoEffects();
    }

    [Fact]
    public async Task CookieBearerMismatchRemainsRejectedInsteadOfEmptySuccess()
    {
        await using var host = await Harness.StartAsync();
        host.Cookies.Add(host.Browser.BaseAddress!, new Cookie(SD.TokenCookie, host.SignedToken(Guid.NewGuid().ToString()), "/"));
        using var response = await host.Browser.GetAsync("/Cart/CartIndex");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        host.AssertNoEffects();
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task UpstreamHttpErrorsRemainBrowserErrors(HttpStatusCode status)
    {
        await using var host = await Harness.StartAsync();
        host.State.ForcedStatus = status;
        host.State.ForcedBody = "{\"isSuccess\":true,\"result\":{}}";
        using var response = await host.Browser.GetAsync("/Cart/CartIndex");
        Assert.Equal(status, response.StatusCode);
        Assert.DoesNotContain("Please add items", await response.Content.ReadAsStringAsync());
        host.AssertNoEffects();
    }

    [Theory]
    [InlineData("")]
    [InlineData("{\"type\":\"about:blank\",\"status\":404,\"title\":\"Not Found\"}")]
    [InlineData("{\"isSuccess\":false,\"result\":null,\"message\":\"Cart not found\"}")]
    public async Task UnclassifiedNotFoundBodiesNeverBecomeEmptySuccess(string body)
    {
        await using var host = await Harness.StartAsync();
        host.State.ForcedStatus = HttpStatusCode.NotFound;
        host.State.ForcedBody = body;
        using var response = await host.Browser.GetAsync("/Cart/CartIndex");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("Please add items", await response.Content.ReadAsStringAsync());
        host.AssertNoEffects();
    }

    [Fact]
    public async Task MissingApiRouteRemainsNotFoundThroughMvc()
    {
        await using var host = await Harness.StartAsync();
        host.State.WrongRoute = true;
        using var response = await host.Browser.GetAsync("/Cart/CartIndex");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("Please add items", await response.Content.ReadAsStringAsync());
        host.AssertNoEffects();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TransportAndStorageFailuresRemainErrors(bool storage)
    {
        await using var host = await Harness.StartAsync();
        host.State.FailStorage = storage;
        host.State.FailTransport = !storage;
        using var response = await host.Browser.GetAsync("/Cart/CartIndex");
        Assert.Equal(storage ? HttpStatusCode.InternalServerError : HttpStatusCode.BadGateway, response.StatusCode);
        Assert.DoesNotContain("Please add items", await response.Content.ReadAsStringAsync());
        host.AssertNoEffects();
    }

    [Theory]
    [InlineData("Product", false)]
    [InlineData("Product", true)]
    [InlineData("Coupon", false)]
    [InlineData("Coupon", true)]
    public async Task DependencyFailuresRemainErrorsForRealHeaders(string dependency, bool withDetails)
    {
        await using var host = await Harness.StartAsync(true, withDetails);
        if (dependency == "Product") host.State.ProductStatus = HttpStatusCode.Unauthorized;
        else host.State.CouponStatus = HttpStatusCode.ServiceUnavailable;
        using var response = await host.Browser.GetAsync("/Cart/CartIndex");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("Please add items", await response.Content.ReadAsStringAsync());
        host.AssertNoEffects();
    }

    [Fact]
    public async Task UnresolvedProductsRemainUnsuccessfulAndMissingCouponRemainsUndiscounted()
    {
        await using var host = await Harness.StartAsync(true, true);
        host.State.EmptyProducts = true;
        using var api = await host.ApiClient.GetAsync("/api/cart/GetCart/" + host.Owner);
        var dto = JsonConvert.DeserializeObject<WebModels.ResponseDto>(await api.Content.ReadAsStringAsync())!;
        Assert.Equal(HttpStatusCode.OK, api.StatusCode);
        Assert.False(dto.IsSuccess);
        Assert.NotNull(dto.Result);
        using var failed = await host.Browser.GetAsync("/Cart/CartIndex");
        Assert.Equal(HttpStatusCode.BadGateway, failed.StatusCode);
        host.State.EmptyProducts = false;
        host.State.CouponStatus = HttpStatusCode.NotFound;
        using var undiscounted = await host.ApiClient.GetAsync("/api/cart/GetCart/" + host.Owner);
        var envelope = JsonConvert.DeserializeObject<WebModels.ResponseDto>(await undiscounted.Content.ReadAsStringAsync())!;
        Assert.True(envelope.IsSuccess);
        var cart = JsonConvert.DeserializeObject<WebModels.CartDto>(Convert.ToString(envelope.Result)!)!;
        Assert.Equal(20, cart.CartHeader.CartTotal);
        Assert.Equal(0, cart.CartHeader.Discount);
        using var page = await host.Browser.GetAsync("/Cart/CartIndex");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("Synthetic product", await page.Content.ReadAsStringAsync());
        host.AssertNoEffects();
    }

    [Fact]
    public async Task RejectedRemovalKeepsItsErrorAndDoesNotRedirect()
    {
        await using var host = await Harness.StartAsync();
        host.State.ForcedStatus = HttpStatusCode.NotFound;
        using var response = await host.Browser.GetAsync("/Cart/Remove?cartDetailsId=999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Null(response.Headers.Location);
        host.AssertNoEffects();
    }

    private static FormUrlEncodedContent Contact() => new(new Dictionary<string, string>
    {
        ["CartHeader.Name"] = "Synthetic owner", ["CartHeader.Phone"] = "12345", ["CartHeader.Email"] = "owner@example.invalid"
    });

    private sealed class Harness : IAsyncDisposable
    {
        private const string Secret = "empty-contract-test-only-key-32-bytes-long-12345";
        private readonly string previousApiBase = SD.ShoppingCartAPIBase;
        private readonly DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        private WebApplication api = null!;
        private WebApplication web = null!;
        public string Owner { get; } = Guid.NewGuid().ToString();
        public string Token { get; private set; } = "";
        public int DetailId { get; private set; }
        public State State { get; } = new();
        public OrderSpy Orders { get; } = new();
        public BusSpy Bus { get; } = new();
        public CookieContainer Cookies { get; } = new();
        public HttpClient Browser { get; private set; } = null!;
        public HttpClient ApiClient { get; private set; } = null!;
        public AppDbContext CreateDb() => new(options);

        public static async Task<Harness> StartAsync(bool persisted = false, bool withDetails = false)
        {
            var host = new Harness();
            try { await host.Start(persisted, withDetails); return host; }
            catch { await host.DisposeAsync(); throw; }
        }

        private async Task Start(bool persisted, bool withDetails)
        {
            Token = new JwtTokenGenerator(Options.Create(new JwtOptions { Secret = Secret, Issuer = "empty-test", Audience = "empty-test" }))
                .GenerateToken(new ApplicationUser { Id = Owner, Email = "owner@example.invalid", UserName = "synthetic" }, ["CUSTOMER"]);
            var apiBuilder = WebApplication.CreateBuilder();
            apiBuilder.Logging.ClearProviders();
            apiBuilder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
            apiBuilder.WebHost.UseUrls("http://127.0.0.1:0");
            apiBuilder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApiSettings:Secret"] = Secret, ["ApiSettings:Issuer"] = "empty-test", ["ApiSettings:Audience"] = "empty-test",
                ["TopicAndQueueNames:EmailShoppingCartQueue"] = "synthetic"
            });
            apiBuilder.AddAppAuthetication();
            apiBuilder.Services.AddAuthorization();
            apiBuilder.Services.AddControllers().AddApplicationPart(typeof(CartAPIController).Assembly);
            apiBuilder.Services.AddScoped<AppDbContext>(_ =>
            {
                var db = CreateDb();
                if (State.FailStorage) db.Dispose();
                return db;
            });
            apiBuilder.Services.AddSingleton(Mango.Services.ShoppingCartAPI.MappingConfig.RegisterMaps().CreateMapper());
            apiBuilder.Services.AddSingleton<IMessageBus>(Bus);
            apiBuilder.Services.AddScoped<Mango.Services.ShoppingCartAPI.Service.IService.IProductService, Mango.Services.ShoppingCartAPI.Service.ProductService>();
            apiBuilder.Services.AddScoped<Mango.Services.ShoppingCartAPI.Service.IService.ICouponService, Mango.Services.ShoppingCartAPI.Service.CouponService>();
            foreach (var dependency in new[] { "Product", "Coupon" })
                apiBuilder.Services.AddHttpClient(dependency, c => c.BaseAddress = new Uri("http://dependency.invalid"))
                    .ConfigurePrimaryHttpMessageHandler(() => new DependencyHandler(State, dependency));
            api = apiBuilder.Build();
            api.UseAuthentication();
            api.UseAuthorization();
            api.MapControllers();
            if (persisted)
            {
                await using var db = CreateDb();
                var header = new Mango.Services.ShoppingCartAPI.Models.CartHeader { UserId = Owner, CouponCode = "SAVE" };
                db.CartHeaders.Add(header);
                var detail = new Mango.Services.ShoppingCartAPI.Models.CartDetails { CartHeader = header, ProductId = 7, Count = 2 };
                if (withDetails) db.CartDetails.Add(detail);
                await db.SaveChangesAsync();
                DetailId = detail.CartDetailsId;
            }
            await api.StartAsync();
            SD.ShoppingCartAPIBase = Address(api);
            ApiClient = new HttpClient { BaseAddress = new Uri(Address(api)), Timeout = TimeSpan.FromSeconds(15) };
            ApiClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

            var webBuilder = WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName = typeof(Mango.Web.Controllers.CartController).Assembly.FullName });
            webBuilder.Logging.ClearProviders();
            webBuilder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
            webBuilder.WebHost.UseUrls("http://127.0.0.1:0");
            webBuilder.Services.AddControllersWithViews().AddApplicationPart(typeof(Mango.Web.Controllers.CartController).Assembly);
            webBuilder.Services.AddHttpContextAccessor();
            webBuilder.Services.AddHttpClient("MangoAPI").ConfigurePrimaryHttpMessageHandler(() => new GatewayHandler(this));
            webBuilder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o => o.LoginPath = "/Auth/Login");
            webBuilder.Services.AddAuthorization();
            webBuilder.Services.AddScoped<ITokenProvider, TokenProvider>();
            webBuilder.Services.AddScoped<IBaseService, BaseService>();
            webBuilder.Services.AddScoped<ICartService, CartService>();
            webBuilder.Services.AddSingleton<IOrderService>(Orders);
            webBuilder.Services.AddSingleton<IAuthService>(new LoginStub(Token));
            web = webBuilder.Build();
            web.UseRouting();
            web.UseAuthentication();
            web.UseAuthorization();
            web.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
            await web.StartAsync();
            Browser = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = Cookies })
                { BaseAddress = new Uri(Address(web)), Timeout = TimeSpan.FromSeconds(15) };
            using var login = await Browser.PostAsync("/Auth/Login", new FormUrlEncodedContent(new Dictionary<string, string>
                { ["UserName"] = "synthetic", ["Password"] = "synthetic" }));
            Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        }

        public string SignedToken(string? subject, bool expired = false)
        {
            var claims = subject is null ? Array.Empty<Claim>() : new[] { new Claim(JwtRegisteredClaimNames.Sub, subject) };
            return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("empty-test", "empty-test", claims,
                notBefore: DateTime.UtcNow.AddHours(-3), expires: DateTime.UtcNow.AddHours(expired ? -2 : 1),
                signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.ASCII.GetBytes(Secret)), SecurityAlgorithms.HmacSha256)));
        }

        public void AssertNoEffects()
        {
            Assert.Equal(0, Orders.Created);
            Assert.Equal(0, Orders.StripeCalls);
            Assert.Equal(0, State.EmailRequests);
            Assert.Equal(0, Bus.Calls);
        }

        public async ValueTask DisposeAsync()
        {
            Browser?.Dispose();
            ApiClient?.Dispose();
            if (web is not null) await web.DisposeAsync();
            if (api is not null) await api.DisposeAsync();
            SD.ShoppingCartAPIBase = previousApiBase;
        }

        private static string Address(WebApplication app) => app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single();
    }

    private sealed class State
    {
        public HttpStatusCode ProductStatus { get; set; } = HttpStatusCode.OK;
        public HttpStatusCode CouponStatus { get; set; } = HttpStatusCode.OK;
        public HttpStatusCode? ForcedStatus { get; set; }
        public string ForcedBody { get; set; } = "";
        public bool EmptyProducts { get; set; }
        public bool FailStorage { get; set; }
        public bool FailTransport { get; set; }
        public bool WrongRoute { get; set; }
        public bool FakeRemoval { get; set; }
        public int RemoveCalls { get; set; }
        public int EmailRequests { get; set; }
        public int ProductCalls { get; set; }
        public int CouponCalls { get; set; }
        public string? LastBearer { get; set; }
    }

    private sealed class GatewayHandler(Harness host) : DelegatingHandler(new HttpClientHandler())
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var state = host.State;
            state.LastBearer = request.Headers.Authorization?.ToString();
            if (state.FailTransport) throw new HttpRequestException("synthetic transport failure");
            if (state.ForcedStatus is { } status)
                return new HttpResponseMessage(status) { Content = new StringContent(state.ForcedBody, Encoding.UTF8, "application/json") };
            if (state.WrongRoute)
                request.RequestUri = new Uri(request.RequestUri!, "/api/cart/nonexistent-route");
            if (request.RequestUri!.AbsolutePath.EndsWith("/EmailCartRequest")) state.EmailRequests++;
            if (state.FakeRemoval && request.RequestUri.AbsolutePath.EndsWith("/RemoveCart"))
            {
                Assert.Equal(host.DetailId, JsonConvert.DeserializeObject<int>(await request.Content!.ReadAsStringAsync(cancellationToken)));
                state.RemoveCalls++;
                await using var db = host.CreateDb();
                db.CartDetails.RemoveRange(await db.CartDetails.ToListAsync(cancellationToken));
                db.CartHeaders.RemoveRange(await db.CartHeaders.ToListAsync(cancellationToken));
                await db.SaveChangesAsync(cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK)
                    { Content = new StringContent("{\"isSuccess\":true,\"result\":true}", Encoding.UTF8, "application/json") };
            }
            return await base.SendAsync(request, cancellationToken);
        }
    }

    private sealed class DependencyHandler(State state, string dependency) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var products = dependency == "Product";
            if (products) state.ProductCalls++; else state.CouponCalls++;
            object result = products
                ? state.EmptyProducts ? Array.Empty<Api.ProductDto>() : new[] { new Api.ProductDto { ProductId = 7, Name = "Synthetic product", Price = 10, Description = "Synthetic", CategoryName = "Synthetic", ImageUrl = "https://example.invalid/image" } }
                : new Api.CouponDto { CouponCode = "SAVE", MinAmount = 1, DiscountAmount = 5 };
            return Task.FromResult(new HttpResponseMessage(products ? state.ProductStatus : state.CouponStatus)
                { Content = new StringContent(JsonConvert.SerializeObject(new Api.ResponseDto { Result = result }), Encoding.UTF8, "application/json") });
        }
    }

    private sealed class LoginStub(string token) : IAuthService
    {
        public Task<WebModels.ResponseDto?> LoginAsync(WebModels.LoginRequestDto request) => Task.FromResult<WebModels.ResponseDto?>(new()
            { Result = JsonConvert.SerializeObject(new WebModels.LoginResponseDto { Token = token }) });
        public Task<WebModels.ResponseDto?> RegisterAsync(WebModels.RegistrationRequestDto request) => throw new NotImplementedException();
        public Task<WebModels.ResponseDto?> AssignRoleAsync(WebModels.RegistrationRequestDto request) => throw new NotImplementedException();
    }

    private sealed class OrderSpy : IOrderService
    {
        public int Created { get; private set; }
        public int StripeCalls { get; private set; }
        public Task<WebModels.ResponseDto?> CreateOrder(WebModels.CartDto cart)
        {
            Created++;
            Assert.True(cart.CartHeader.CartHeaderId > 0);
            return Task.FromResult<WebModels.ResponseDto?>(new() { Result = "{\"orderHeaderId\":7}" });
        }
        public Task<WebModels.ResponseDto?> CreateStripeSession(WebModels.StripeRequestDto request)
        {
            StripeCalls++;
            return Task.FromResult<WebModels.ResponseDto?>(new() { Result = "{\"stripeSessionUrl\":\"https://example.invalid/checkout\"}" });
        }
        public Task<WebModels.ResponseDto?> ValidateStripeSession(int id) => throw new NotImplementedException();
        public Task<WebModels.ResponseDto?> GetAllOrder(string? id) => throw new NotImplementedException();
        public Task<WebModels.ResponseDto?> GetOrder(int id) => throw new NotImplementedException();
        public Task<WebModels.ResponseDto?> UpdateOrderStatus(int id, string status) => throw new NotImplementedException();
    }

    private sealed class BusSpy : IMessageBus
    {
        public int Calls { get; private set; }
        public Task PublishMessage(object message, string destination) { Calls++; return Task.CompletedTask; }
    }
}
