using System.Collections.Concurrent;
using Mango.MessageBus.Service.IService;
using Mango.Services.AuthAPI.Data;
using Mango.Services.AuthAPI.Models;
using Mango.Services.AuthAPI.Service;
using Mango.Services.AuthAPI.Service.IService;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mango.Services.AuthAPI.Tests.TestSupport;

internal static class IdentityTestServices
{
    public static void Add(IServiceCollection services, RecordingBus bus, TrackingPasswordHasher hasher,
        CapturedAuthLogger logger, SaveChangesInterceptor? interceptor = null)
    {
        var database = Guid.NewGuid().ToString();
        var root = new InMemoryDatabaseRoot();
        services.AddLogging();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseInMemoryDatabase(database, root);
            if (interceptor is not null) options.AddInterceptors(interceptor);
        });
        services.Configure<JwtOptions>(AuthTestData.Configuration().GetSection("ApiSettings:JwtOptions"));
        services.AddIdentity<ApplicationUser, IdentityRole>().AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddSingleton<IMessageBus>(bus);
        services.AddSingleton<IPasswordHasher<ApplicationUser>>(hasher);
        services.AddSingleton<ILogger<AuthService>>(logger);
    }
}

internal sealed class TrackingPasswordHasher : IPasswordHasher<ApplicationUser>
{
    private readonly PasswordHasher<ApplicationUser> _inner = new();
    private int _verifications;
    public int Verifications => Volatile.Read(ref _verifications);

    public string HashPassword(ApplicationUser user, string password) => _inner.HashPassword(user, password);

    public PasswordVerificationResult VerifyHashedPassword(ApplicationUser user, string hashedPassword, string providedPassword)
    {
        Interlocked.Increment(ref _verifications);
        return _inner.VerifyHashedPassword(user, hashedPassword, providedPassword);
    }
}

internal sealed record AuthLog(LogLevel Level, Exception? Exception, string Message);

internal sealed class CapturedAuthLogger : ILogger<AuthService>
{
    public ConcurrentQueue<AuthLog> Entries { get; } = new();
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter) => Entries.Enqueue(new AuthLog(logLevel, exception, formatter(state, exception)));
}

internal sealed class FailingSaveInterceptor(Exception failure) : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default) => throw failure;
}
