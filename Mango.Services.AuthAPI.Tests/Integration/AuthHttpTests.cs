using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Mango.Services.AuthAPI.Models.Dto;
using Mango.Services.AuthAPI.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Mango.Services.AuthAPI.Tests.Integration;

[Trait("Category", "Integration")]
public class AuthHttpTests
{
    [Fact]
    public async Task RegisterAndLoginRemainPublicAndReturnPersistedUserWithSignedJwt()
    {
        await using var host = await AuthHttpHost.StartAsync();
        var registration = AuthTestData.Registration(role: "ADMIN");
        Assert.Null(host.Client.DefaultRequestHeaders.Authorization);

        using var registered = await host.Client.PostAsJsonAsync("/api/auth/register", registration);

        Assert.Equal(HttpStatusCode.OK, registered.StatusCode);
        Assert.True((await Envelope(registered)).IsSuccess);
        Assert.Equal(1, await host.InspectAsync(db => db.Users.CountAsync()));
        Assert.Equal(0, await host.InspectAsync(db => db.UserRoles.CountAsync()));
        var publication = Assert.Single(host.Bus.Publications);
        Assert.Equal(registration.Email, publication.Payload);
        Assert.Equal(AuthTestData.Queue, publication.Destination);

        using var loggedIn = await host.Client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto
        {
            UserName = registration.Email, Password = registration.Password
        });

        Assert.Equal(HttpStatusCode.OK, loggedIn.StatusCode);
        var envelope = await Envelope(loggedIn);
        Assert.True(envelope.IsSuccess);
        var login = LoginResult(envelope);
        var stored = await host.InspectAsync(db => db.Users.AsNoTracking().SingleAsync());
        Assert.Equal(stored.Id, login.User.ID);
        Assert.Equal(registration.Email, login.User.Email);
        Assert.Equal(registration.Name, login.User.Name);
        Assert.Equal(registration.PhoneNumber, login.User.PhoneNumber);
        var principal = AuthTestData.ValidateToken(login.Token);
        Assert.Equal(stored.Id, principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value);
        Assert.Empty(principal.FindAll("role"));
        Assert.Single(host.Bus.Publications);
    }

    [Theory]
    [InlineData("absent")]
    [InlineData("malformed")]
    [InlineData("expired")]
    [InlineData("signature")]
    [InlineData("issuer")]
    [InlineData("audience")]
    public async Task AssignRoleRejectsUntrustedJwtWith401AndNoIdentityOrBusEffects(string scenario)
    {
        await using var host = await AuthHttpHost.StartAsync();
        await Register(host);
        var before = await host.SnapshotAsync();
        var token = scenario switch
        {
            "absent" => null,
            "malformed" => "not-a-jwt",
            "expired" => AuthTestData.Token(expired: true),
            "signature" => AuthTestData.Token(secret: "different-test-signing-key-at-least-32-bytes"),
            "issuer" => AuthTestData.Token(issuer: "untrusted-issuer"),
            "audience" => AuthTestData.Token(audience: "untrusted-audience"),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };
        using var request = Assignment(AuthTestData.Registration(role: "ADMIN"), token);

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(response.Headers.WwwAuthenticate, h => h.Scheme == "Bearer");
        Assert.Equal(before, await host.SnapshotAsync());
        Assert.Single(host.Bus.Publications);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("CUSTOMER")]
    public async Task AssignRoleRejectsAuthenticatedNonAdminWith403AndNoEffects(string? role)
    {
        await using var host = await AuthHttpHost.StartAsync();
        await Register(host);
        var before = await host.SnapshotAsync();
        using var request = Assignment(AuthTestData.Registration(role: "ADMIN"), AuthTestData.Token(role));

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(before, await host.SnapshotAsync());
        Assert.Single(host.Bus.Publications);
    }

    [Fact]
    public async Task AdminCanAssignRolesAndAnActualLoginTokenCanAuthorizeTheNextAssignment()
    {
        await using var host = await AuthHttpHost.StartAsync();
        await Register(host, "admin@example.invalid");
        await Register(host);
        using var bootstrap = Assignment(AuthTestData.Registration("admin@example.invalid", "admin"), AuthTestData.Token());
        using var bootstrapped = await host.Client.SendAsync(bootstrap);
        Assert.Equal(HttpStatusCode.OK, bootstrapped.StatusCode);
        Assert.True((await Envelope(bootstrapped)).IsSuccess);
        using var loginResponse = await host.Client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto
        {
            UserName = "admin@example.invalid", Password = AuthTestData.Password
        });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var login = LoginResult(await Envelope(loginResponse));
        Assert.Equal("ADMIN", AuthTestData.ValidateToken(login.Token).FindFirst("role")?.Value);
        using var assignment = Assignment(AuthTestData.Registration("MEMBER@EXAMPLE.INVALID", "customer"), login.Token);

        using var assigned = await host.Client.SendAsync(assignment);

        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
        Assert.True((await Envelope(assigned)).IsSuccess);
        var roles = await host.InspectAsync(async db =>
            await (from user in db.Users join membership in db.UserRoles on user.Id equals membership.UserId
                   join role in db.Roles on membership.RoleId equals role.Id
                   where user.Email == "member@example.invalid" select role.Name).ToListAsync());
        Assert.Equal("CUSTOMER", Assert.Single(roles));
        Assert.Equal(2, await host.InspectAsync(db => db.UserRoles.CountAsync()));
        Assert.Equal(2, await host.InspectAsync(db => db.Roles.CountAsync()));
        Assert.Equal(2, host.Bus.Publications.Count);
    }

    [Theory]
    [InlineData("member@example.invalid", null)]
    [InlineData("member@example.invalid", "")]
    [InlineData("absent@example.invalid", "CUSTOMER")]
    public async Task AdminAssignmentWithMissingRoleOrUnknownUserReturns400WithoutEffects(string email, string? role)
    {
        await using var host = await AuthHttpHost.StartAsync();
        await Register(host);
        var before = await host.SnapshotAsync();
        using var request = Assignment(AuthTestData.Registration(email, role), AuthTestData.Token());

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var envelope = await Envelope(response);
        Assert.False(envelope.IsSuccess);
        Assert.Equal("Error encountered", envelope.Message);
        Assert.Equal(before, await host.SnapshotAsync());
        Assert.Single(host.Bus.Publications);
    }

    [Theory]
    [InlineData("/api/auth/register")]
    [InlineData("/api/auth/login")]
    [InlineData("/api/auth/AssignRole")]
    public async Task MalformedJsonReturns400BeforeIdentityOrBusEffects(string path)
    {
        await using var host = await AuthHttpHost.StartAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent("{invalid-json", Encoding.UTF8, "application/json")
        };
        if (path.EndsWith("AssignRole", StringComparison.Ordinal))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AuthTestData.Token());

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Empty(await host.SnapshotAsync());
        Assert.Empty(host.Bus.Publications);
    }

    [Fact]
    public async Task PersistenceFailureReturnsGeneric400OverHttpAndLogsInternalException()
    {
        var failure = new InvalidOperationException("synthetic-private-database-detail");
        await using var host = await AuthHttpHost.StartAsync(new FailingSaveInterceptor(failure));

        using var response = await host.Client.PostAsJsonAsync("/api/auth/register", AuthTestData.Registration());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(failure.Message, body);
        var envelope = await Envelope(response);
        Assert.False(envelope.IsSuccess);
        Assert.Equal("Error Encountered", envelope.Message);
        Assert.Same(failure, Assert.Single(host.Logger.Entries).Exception);
        Assert.Empty(await host.SnapshotAsync());
        Assert.Empty(host.Bus.Publications);
    }

    private static async Task Register(AuthHttpHost host, string email = "member@example.invalid")
    {
        using var response = await host.Client.PostAsJsonAsync("/api/auth/register", AuthTestData.Registration(email));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await Envelope(response)).IsSuccess);
    }

    private static HttpRequestMessage Assignment(RegistrationRequestDto registration, string? token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/AssignRole")
        {
            Content = JsonContent.Create(registration)
        };
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static async Task<ResponseDto> Envelope(HttpResponseMessage response)
    {
        var envelope = await response.Content.ReadFromJsonAsync<ResponseDto>();
        Assert.NotNull(envelope);
        return envelope;
    }

    private static LoginResponseDto LoginResult(ResponseDto envelope)
    {
        var result = Assert.IsType<JsonElement>(envelope.Result);
        var login = result.Deserialize<LoginResponseDto>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(login);
        return login;
    }
}
