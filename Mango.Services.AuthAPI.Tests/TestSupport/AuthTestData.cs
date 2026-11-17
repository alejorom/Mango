using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Mango.Services.AuthAPI.Models;
using Mango.Services.AuthAPI.Models.Dto;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Mango.Services.AuthAPI.Tests.TestSupport;

internal static class AuthTestData
{
    public const string Secret = "auth-tests-only-signing-key-at-least-32-bytes";
    public const string Issuer = "auth-tests-issuer";
    public const string Audience = "auth-tests-audience";
    public const string Queue = "auth-tests-register-user";
    public const string Password = "Synthetic123!";

    public static JwtOptions JwtOptions => new() { Secret = Secret, Issuer = Issuer, Audience = Audience };

    public static RegistrationRequestDto Registration(string email = "member@example.invalid", string? role = null,
        string password = Password) => new()
    {
        Email = email, Name = "Synthetic member", PhoneNumber = "0000000000", Password = password, Role = role
    };

    public static IConfiguration Configuration() => new ConfigurationBuilder().AddInMemoryCollection(Settings()).Build();

    public static Dictionary<string, string?> Settings() => new()
    {
        ["ApiSettings:JwtOptions:Secret"] = Secret,
        ["ApiSettings:JwtOptions:Issuer"] = Issuer,
        ["ApiSettings:JwtOptions:Audience"] = Audience,
        ["TopicAndQueueNames:RegisterUserQueue"] = Queue
    };

    public static string Token(string? role = "ADMIN", string secret = Secret, string issuer = Issuer,
        string audience = Audience, bool expired = false)
    {
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, "synthetic-caller") };
        if (role is not null) claims.Add(new Claim("role", role));
        var now = DateTime.UtcNow;
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer, audience, claims, now.AddHours(-2), expired ? now.AddHours(-1) : now.AddHours(1),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.ASCII.GetBytes(secret)), SecurityAlgorithms.HmacSha256)));
    }

    public static ClaimsPrincipal ValidateToken(string token, string secret = Secret, string issuer = Issuer,
        string audience = Audience)
    {
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        return handler.ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(secret)),
            ValidateIssuer = true, ValidIssuer = issuer,
            ValidateAudience = true, ValidAudience = audience,
            ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true,
            ClockSkew = TimeSpan.Zero
        }, out _);
    }
}
