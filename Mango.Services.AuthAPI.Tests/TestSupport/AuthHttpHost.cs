using System.Text.Json;
using Mango.Services.AuthAPI.Controllers;
using Mango.Services.AuthAPI.Data;
using Mango.Services.AuthAPI.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mango.Services.AuthAPI.Tests.TestSupport;

internal sealed class AuthHttpHost : IAsyncDisposable
{
    private readonly WebApplication _app;
    public HttpClient Client { get; }
    public RecordingBus Bus { get; }
    public CapturedAuthLogger Logger { get; }

    private AuthHttpHost(WebApplication app, RecordingBus bus, CapturedAuthLogger logger)
    {
        _app = app;
        Bus = bus;
        Logger = logger;
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        Client = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false })
        {
            BaseAddress = new Uri(address), Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public static async Task<AuthHttpHost> StartAsync(SaveChangesInterceptor? interceptor = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(AuthAPIController).Assembly.FullName, EnvironmentName = "Testing", Args = []
        });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(AuthTestData.Settings());
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var bus = new RecordingBus();
        var logger = new CapturedAuthLogger();
        IdentityTestServices.Add(builder.Services, bus, new TrackingPasswordHasher(), logger, interceptor);
        builder.AddAppAuthetication();
        builder.Services.AddAuthorization();
        builder.Services.AddControllers().AddApplicationPart(typeof(AuthAPIController).Assembly);
        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await app.StartAsync(timeout.Token);
            return new AuthHttpHost(app, bus, logger);
        }
        catch
        {
            await app.DisposeAsync();
            throw;
        }
    }

    public async Task<T> InspectAsync<T>(Func<AppDbContext, Task<T>> inspect)
    {
        await using var scope = _app.Services.CreateAsyncScope();
        return await inspect(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public Task<string[]> SnapshotAsync() => InspectAsync(async db =>
    {
        var users = await db.Users.AsNoTracking().ToListAsync();
        var roles = await db.Roles.AsNoTracking().ToListAsync();
        var memberships = await db.UserRoles.AsNoTracking().ToListAsync();
        return users.Select(u => "user:" + JsonSerializer.Serialize(u))
            .Concat(roles.Select(r => "role:" + JsonSerializer.Serialize(r)))
            .Concat(memberships.Select(r => "membership:" + JsonSerializer.Serialize(r)))
            .Order(StringComparer.Ordinal).ToArray();
    });

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await _app.StopAsync(timeout.Token);
        }
        finally
        {
            await _app.DisposeAsync();
        }
    }
}
