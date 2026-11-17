using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Mango.Services.ProductAPI.Tests.TestSupport;

internal static class LocalAdminJwt
{
    private const string Key = "price-range-regression-only-signature-key-32-bytes";
    private const string Issuer = "price-range-regression";
    private const string Audience = "isolated-product-api";

    public static Dictionary<string, string?> Settings() => new()
    {
        ["ApiSettings:Secret"] = Key,
        ["ApiSettings:Issuer"] = Issuer,
        ["ApiSettings:Audience"] = Audience
    };

    public static string Create()
    {
        var now = DateTime.UtcNow;
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(Issuer, Audience,
            [new Claim("sub", "range-test-admin"), new Claim("role", "ADMIN")],
            now.AddMinutes(-1), now.AddMinutes(30), credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
