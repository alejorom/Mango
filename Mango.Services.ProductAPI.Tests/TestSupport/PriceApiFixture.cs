using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AutoMapper;
using Mango.Services.ProductAPI;
using Mango.Services.ProductAPI.Controllers;
using Mango.Services.ProductAPI.Data;
using Mango.Services.ProductAPI.Models;
using Mango.Services.ProductAPI.Models.Dto;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mango.Services.ProductAPI.Tests.TestSupport;

internal sealed class PriceApiFixture : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly WebApplication _application;
    private readonly DbContextOptions<AppDbContext> _inspectionOptions;
    private readonly SaveAttemptCounter _saves;
    public HttpClient Client { get; }
    public int SaveAttempts => _saves.Attempts;

    private PriceApiFixture(WebApplication application, DbContextOptions<AppDbContext> inspectionOptions, SaveAttemptCounter saves)
    {
        _application = application;
        _inspectionOptions = inspectionOptions;
        _saves = saves;
        var addresses = application.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!;
        Client = new HttpClient(new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false })
        {
            BaseAddress = new Uri(addresses.Addresses.Single()), Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public static async Task<PriceApiFixture> StartAsync()
    {
        var database = "price-range-" + Guid.NewGuid();
        var root = new InMemoryDatabaseRoot();
        var inspectionOptions = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(database, root).Options;
        var saves = new SaveAttemptCounter();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(ProductAPIController).Assembly.GetName().Name,
            EnvironmentName = "Testing", Args = []
        });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(LocalAdminJwt.Settings());
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(database, root).AddInterceptors(saves));
        builder.Services.AddSingleton<IMapper>(MappingConfig.RegisterMaps().CreateMapper());
        builder.AddAppAuthetication();
        builder.Services.AddAuthorization();
        builder.Services.AddControllers().AddApplicationPart(typeof(ProductAPIController).Assembly);
        var application = builder.Build();
        application.UseAuthentication();
        application.UseAuthorization();
        application.MapControllers();
        try
        {
            await using (var seed = new AppDbContext(inspectionOptions))
            {
                seed.Products.AddRange(PriceBoundaryData.Entity(),
                    PriceBoundaryData.Entity(47.5, "Unrelated control product", PriceBoundaryData.UnrelatedId));
                await seed.SaveChangesAsync();
            }
            using var startup = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await application.StartAsync(startup.Token);
            return new PriceApiFixture(application, inspectionOptions, saves);
        }
        catch
        {
            await application.DisposeAsync();
            throw;
        }
    }

    public async Task<Product[]> RowsAsync()
    {
        await using var db = new AppDbContext(_inspectionOptions);
        return await db.Products.AsNoTracking().OrderBy(p => p.ProductId).ToArrayAsync();
    }

    public async Task<string[]> SnapshotAsync() => (await RowsAsync()).Select(p => JsonSerializer.Serialize(p)).ToArray();

    public async Task<HttpResponseMessage> WriteAsync(string method, ProductDto payload, bool authenticated = true)
    {
        var json = JsonSerializer.SerializeToElement(payload, JsonOptions);
        if (BitConverter.DoubleToInt64Bits(payload.Price) != BitConverter.DoubleToInt64Bits(json.GetProperty("price").GetDouble()))
            throw new InvalidOperationException("JSON changed the tested price");
        using var request = new HttpRequestMessage(new HttpMethod(method), "/api/product")
        {
            Content = new StringContent(json.GetRawText(), Encoding.UTF8, "application/json")
        };
        if (authenticated) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", LocalAdminJwt.Create());
        return await Client.SendAsync(request);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        try
        {
            using var shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await _application.StopAsync(shutdown.Token);
        }
        finally
        {
            await _application.DisposeAsync();
        }
    }
}
