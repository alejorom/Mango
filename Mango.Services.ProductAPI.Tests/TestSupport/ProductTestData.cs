using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Mango.Services.ProductAPI.Models;
using Mango.Services.ProductAPI.Models.Dto;
using Microsoft.IdentityModel.Tokens;

namespace Mango.Services.ProductAPI.Tests.TestSupport;

internal static class ProductTestData
{
    public const string Secret = "product-tests-only-signing-key-at-least-32-bytes";
    public const string Issuer = "product-tests-issuer";
    public const string Audience = "product-tests-audience";
    public const int ExistingId = 17;
    public const int MissingId = 2000000;

    public static ProductDto Dto(int id = ExistingId, double price = 42.5, string? name = "Synthetic product") => new()
    {
        ProductId = id, Name = name!, Price = price, Description = "Synthetic description",
        CategoryName = "Synthetic category", ImageUrl = "https://example.invalid/product.png"
    };

    public static Product Entity() => new()
    {
        ProductId = ExistingId, Name = "Synthetic product", Price = 42.5, Description = "Synthetic description",
        CategoryName = "Synthetic category", ImageUrl = "https://example.invalid/product.png"
    };

    public static Dictionary<string, string?> Settings() => new()
    {
        ["ApiSettings:Secret"] = Secret,
        ["ApiSettings:Issuer"] = Issuer,
        ["ApiSettings:Audience"] = Audience
    };

    public static string Token(string? role = "ADMIN", string secret = Secret, string issuer = Issuer,
        string audience = Audience, bool expired = false)
    {
        var now = DateTime.UtcNow;
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, "synthetic-catalog-caller") };
        if (role is not null) claims.Add(new Claim("role", role));
        var token = new JwtSecurityToken(issuer, audience, claims, now.AddHours(-2),
            expired ? now.AddHours(-1) : now.AddHours(1),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.ASCII.GetBytes(secret)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
