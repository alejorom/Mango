using System.IdentityModel.Tokens.Jwt;
using Mango.Services.AuthAPI.Models;
using Mango.Services.AuthAPI.Service;
using Mango.Services.AuthAPI.Tests.TestSupport;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Mango.Services.AuthAPI.Tests.Unit;

[Trait("Category", "Unit")]
public class JwtTokenGeneratorTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GeneratedTokenHasValidSignatureExpectedClaimsAndSevenDayLifetime(bool withRoles)
    {
        var user = new ApplicationUser
        {
            Id = "synthetic-user", Email = "member@example.invalid", UserName = "member@example.invalid", Name = "Member"
        };
        string[] roles = withRoles ? ["ADMIN", "CUSTOMER"] : [];
        var generator = new JwtTokenGenerator(Options.Create(AuthTestData.JwtOptions));
        var before = DateTime.UtcNow;

        var token = generator.GenerateToken(user, roles);

        var after = DateTime.UtcNow;
        var principal = AuthTestData.ValidateToken(token);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal(SecurityAlgorithms.HmacSha256, jwt.Header.Alg);
        Assert.Equal(AuthTestData.Issuer, jwt.Issuer);
        Assert.Equal(AuthTestData.Audience, Assert.Single(jwt.Audiences));
        Assert.Equal(user.Id, principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value);
        Assert.Equal(user.Email, principal.FindFirst(JwtRegisteredClaimNames.Email)?.Value);
        Assert.Equal(user.UserName, principal.FindFirst(JwtRegisteredClaimNames.Name)?.Value);
        Assert.Equal(roles, principal.FindAll("role").Select(c => c.Value).ToArray());
        Assert.InRange(jwt.ValidTo, before.AddDays(7).AddSeconds(-1), after.AddDays(7));
    }

    [Theory]
    [InlineData("signature")]
    [InlineData("issuer")]
    [InlineData("audience")]
    public void GeneratedTokenCannotBeValidatedWithDifferentTrustSettings(string mismatch)
    {
        var generator = new JwtTokenGenerator(Options.Create(AuthTestData.JwtOptions));
        var token = generator.GenerateToken(new ApplicationUser
        {
            Id = "synthetic-user", Email = "member@example.invalid", UserName = "member@example.invalid", Name = "Member"
        }, []);

        Assert.ThrowsAny<SecurityTokenException>(() => AuthTestData.ValidateToken(token,
            secret: mismatch == "signature" ? "another-test-only-signing-key-at-least-32-bytes" : AuthTestData.Secret,
            issuer: mismatch == "issuer" ? "other-issuer" : AuthTestData.Issuer,
            audience: mismatch == "audience" ? "other-audience" : AuthTestData.Audience));
    }
}
