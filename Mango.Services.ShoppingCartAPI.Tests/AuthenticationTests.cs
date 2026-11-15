using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Mango.Services.ShoppingCartAPI.Controllers;
using Mango.Services.ShoppingCartAPI.Extensions;
using Mango.Services.AuthAPI.Models;
using Mango.Services.AuthAPI.Service;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using Xunit;

namespace Mango.Services.ShoppingCartAPI.Tests;

public class AuthenticationTests
{
    private const string Secret = "test-only-signing-key-32-bytes-long-12345";
    private const string Issuer = "test-issuer";
    private const string Audience = "test-audience";

    [Fact]
    public async Task CartEndpointsRejectInvalidIdentitiesBeforeControllerActivation()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ApiSettings:Secret"] = Secret,
            ["ApiSettings:Issuer"] = Issuer,
            ["ApiSettings:Audience"] = Audience
        });
        builder.AddAppAuthetication();
        builder.Services.AddAuthorization();
        builder.Services.AddControllers().AddApplicationPart(typeof(CartAPIController).Assembly);
        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.MapGet("/subject", (HttpContext context) => context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value).RequireAuthorization();
        app.MapGet("/email", (HttpContext context) => context.User.FindFirst(JwtRegisteredClaimNames.Email)?.Value).RequireAuthorization();
        await app.StartAsync();
        try
        {
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient { BaseAddress = new Uri(address) };
            var endpoints = new[] { (HttpMethod.Get, "/api/cart/GetCart/user"), (HttpMethod.Post, "/api/cart/ApplyCoupon"), (HttpMethod.Post, "/api/cart/CartUpsert"), (HttpMethod.Post, "/api/cart/RemoveCart"), (HttpMethod.Post, "/api/cart/EmailCartRequest") };
            var invalidTokens = new string?[] { null, "invalid", Token("user", DateTime.UtcNow.AddHours(-2)), Token(null, DateTime.UtcNow.AddHours(1)), Token(" ", DateTime.UtcNow.AddHours(1)) };
            foreach (var (method, path) in endpoints)
            foreach (var token in invalidTokens)
            {
                using var request = new HttpRequestMessage(method, path);
                if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                using var response = await client.SendAsync(request);
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            }

            using var valid = new HttpRequestMessage(HttpMethod.Get, "/api/cart/GetCart/user");
            valid.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token("user", DateTime.UtcNow.AddHours(1)));
            using var validResponse = await client.SendAsync(valid);
            Assert.NotEqual(HttpStatusCode.Unauthorized, validResponse.StatusCode);
            var issuer = new JwtTokenGenerator(Options.Create(new JwtOptions { Secret = Secret, Issuer = Issuer, Audience = Audience }));
            var issuedToken = issuer.GenerateToken(new ApplicationUser { Id = "user", Email = "user@example.invalid", UserName = "user" }, Array.Empty<string>());
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", issuedToken);
            Assert.Equal("user", await client.GetStringAsync("/subject"));
            Assert.Equal("user@example.invalid", await client.GetStringAsync("/email"));
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    private static string Token(string? subject, DateTime expires)
    {
        var claims = subject is null ? Array.Empty<Claim>() : new[] { new Claim(JwtRegisteredClaimNames.Sub, subject) };
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.ASCII.GetBytes(Secret)), SecurityAlgorithms.HmacSha256);
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(Issuer, Audience, claims, expires: expires, signingCredentials: credentials));
    }
}
