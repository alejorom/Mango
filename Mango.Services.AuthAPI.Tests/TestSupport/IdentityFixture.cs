using Mango.Services.AuthAPI.Controllers;
using Mango.Services.AuthAPI.Data;
using Mango.Services.AuthAPI.Models;
using Mango.Services.AuthAPI.Service.IService;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Mango.Services.AuthAPI.Tests.TestSupport;

internal sealed class IdentityFixture : IAsyncDisposable
{
    private readonly ServiceProvider _provider;
    private readonly AsyncServiceScope _scope;
    public RecordingBus Bus { get; } = new();
    public TrackingPasswordHasher Hasher { get; } = new();
    public CapturedAuthLogger Logger { get; } = new();
    public AppDbContext Db => _scope.ServiceProvider.GetRequiredService<AppDbContext>();
    public UserManager<ApplicationUser> Users => _scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    public RoleManager<IdentityRole> Roles => _scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    public AuthAPIController Controller => new(_scope.ServiceProvider.GetRequiredService<IAuthService>(), Bus,
        AuthTestData.Configuration());

    public IdentityFixture(SaveChangesInterceptor? interceptor = null)
    {
        var services = new ServiceCollection();
        IdentityTestServices.Add(services, Bus, Hasher, Logger, interceptor);
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        _scope = _provider.CreateAsyncScope();
    }

    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
        await _provider.DisposeAsync();
    }
}
