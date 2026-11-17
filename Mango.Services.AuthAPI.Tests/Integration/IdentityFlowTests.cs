using System.IdentityModel.Tokens.Jwt;
using Mango.Services.AuthAPI.Models.Dto;
using Mango.Services.AuthAPI.Tests.TestSupport;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Mango.Services.AuthAPI.Tests.Integration;

[Trait("Category", "Integration")]
public class IdentityFlowTests
{
    [Fact]
    public async Task SuccessfulRegistrationPersistsIdentityAndPublishesEmailWithoutAssigningRequestedRole()
    {
        await using var fixture = new IdentityFixture();
        var request = AuthTestData.Registration(role: "ADMIN");

        var result = Assert.IsType<OkObjectResult>(await fixture.Controller.Register(request));

        Assert.True(Assert.IsType<ResponseDto>(result.Value).IsSuccess);
        var user = Assert.Single(await fixture.Db.ApplicationUsers.AsNoTracking().ToListAsync());
        Assert.Equal(request.Email, user.UserName);
        Assert.Equal(request.Email, user.Email);
        Assert.Equal(request.Email.ToUpperInvariant(), user.NormalizedEmail);
        Assert.Equal(request.Name, user.Name);
        Assert.Equal(request.PhoneNumber, user.PhoneNumber);
        Assert.False(string.IsNullOrEmpty(user.PasswordHash));
        Assert.NotEqual(request.Password, user.PasswordHash);
        Assert.Empty(await fixture.Users.GetRolesAsync(user));
        Assert.Empty(await fixture.Db.Roles.ToListAsync());
        var publication = Assert.Single(fixture.Bus.Publications);
        Assert.Equal(request.Email, publication.Payload);
        Assert.Equal(AuthTestData.Queue, publication.Destination);
    }

    [Theory]
    [InlineData("member@example.invalid")]
    [InlineData("MEMBER@EXAMPLE.INVALID")]
    public async Task DuplicateRegistrationIsRejectedByIdentityWithoutAdditionalUserOrMessage(string duplicateEmail)
    {
        await using var fixture = new IdentityFixture();
        Assert.IsType<OkObjectResult>(await fixture.Controller.Register(AuthTestData.Registration()));

        var result = Assert.IsType<BadRequestObjectResult>(await fixture.Controller.Register(AuthTestData.Registration(duplicateEmail)));

        var response = Assert.IsType<ResponseDto>(result.Value);
        Assert.False(response.IsSuccess);
        Assert.NotEmpty(response.Message);
        Assert.Single(await fixture.Db.Users.AsNoTracking().ToListAsync());
        Assert.Single(fixture.Bus.Publications);
    }

    [Theory]
    [InlineData("Ab1!")]
    [InlineData("synthetic123!")]
    [InlineData("SYNTHETIC123!")]
    [InlineData("Synthetic!")]
    [InlineData("Synthetic123")]
    public async Task IdentityPasswordRulesRejectInvalidPasswordWithoutPersistenceOrPublication(string password)
    {
        await using var fixture = new IdentityFixture();

        var result = Assert.IsType<BadRequestObjectResult>(await fixture.Controller.Register(
            AuthTestData.Registration(password: password)));

        var response = Assert.IsType<ResponseDto>(result.Value);
        Assert.False(response.IsSuccess);
        Assert.NotEmpty(response.Message);
        Assert.Empty(await fixture.Db.Users.AsNoTracking().ToListAsync());
        Assert.Empty(fixture.Bus.Publications);
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("unknown")]
    [InlineData("wrong-password")]
    public async Task LoginUsesRealIdentityAndOnlyValidCredentialsReceiveSignedUserRoles(string scenario)
    {
        await using var fixture = new IdentityFixture();
        var registration = AuthTestData.Registration();
        Assert.IsType<OkObjectResult>(await fixture.Controller.Register(registration));
        var storedUser = await fixture.Db.Users.SingleAsync();
        Assert.True((await fixture.Roles.CreateAsync(new IdentityRole("CUSTOMER"))).Succeeded);
        Assert.True((await fixture.Users.AddToRoleAsync(storedUser, "CUSTOMER")).Succeeded);
        var request = new LoginRequestDto
        {
            UserName = scenario == "unknown" ? "absent@example.invalid" : registration.Email.ToUpperInvariant(),
            Password = scenario == "wrong-password" ? "WrongPassword123!" : registration.Password
        };

        var result = await fixture.Controller.Login(request);

        if (scenario == "valid")
        {
            var response = Assert.IsType<ResponseDto>(Assert.IsType<OkObjectResult>(result).Value);
            Assert.True(response.IsSuccess);
            var login = Assert.IsType<LoginResponseDto>(response.Result);
            Assert.Equal(storedUser.Id, login.User.ID);
            Assert.Equal(registration.Email, login.User.Email);
            Assert.Equal(registration.Name, login.User.Name);
            Assert.Equal(registration.PhoneNumber, login.User.PhoneNumber);
            var principal = AuthTestData.ValidateToken(login.Token);
            Assert.Equal(storedUser.Id, principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value);
            Assert.Equal("CUSTOMER", principal.FindFirst("role")?.Value);
        }
        else
        {
            var response = Assert.IsType<ResponseDto>(Assert.IsType<BadRequestObjectResult>(result).Value);
            Assert.False(response.IsSuccess);
            Assert.Equal("Username or password is incorrect", response.Message);
            Assert.Null(response.Result);
        }
        Assert.Equal(scenario == "unknown" ? 0 : 1, fixture.Hasher.Verifications);
        Assert.Single(await fixture.Db.Users.AsNoTracking().ToListAsync());
        Assert.Single(fixture.Bus.Publications);
    }

    [Fact]
    public async Task UnexpectedRegistrationFailureIsLoggedAndReturnedAsGeneric400WithoutPublication()
    {
        var failure = new InvalidOperationException("synthetic-internal-persistence-detail");
        await using var fixture = new IdentityFixture(new FailingSaveInterceptor(failure));

        var result = Assert.IsType<BadRequestObjectResult>(await fixture.Controller.Register(AuthTestData.Registration()));

        var response = Assert.IsType<ResponseDto>(result.Value);
        Assert.False(response.IsSuccess);
        Assert.Equal("Error Encountered", response.Message);
        Assert.DoesNotContain(failure.Message, response.Message);
        var log = Assert.Single(fixture.Logger.Entries);
        Assert.Equal(LogLevel.Error, log.Level);
        Assert.Same(failure, log.Exception);
        Assert.Empty(await fixture.Db.Users.AsNoTracking().ToListAsync());
        Assert.Empty(fixture.Bus.Publications);
    }

    [Fact]
    public async Task RoleAssignmentCreatesThenReusesRoleAndLooksUpEmailWithoutCaseSensitivity()
    {
        await using var fixture = new IdentityFixture();
        Assert.IsType<OkObjectResult>(await fixture.Controller.Register(AuthTestData.Registration()));
        Assert.IsType<OkObjectResult>(await fixture.Controller.Register(AuthTestData.Registration("second@example.invalid")));

        var first = await fixture.Controller.AssignRole(AuthTestData.Registration("MEMBER@EXAMPLE.INVALID", "customer"));
        var second = await fixture.Controller.AssignRole(AuthTestData.Registration("second@example.invalid", "CUSTOMER"));

        Assert.True(Assert.IsType<ResponseDto>(Assert.IsType<OkObjectResult>(first).Value).IsSuccess);
        Assert.True(Assert.IsType<ResponseDto>(Assert.IsType<OkObjectResult>(second).Value).IsSuccess);
        Assert.Equal("CUSTOMER", Assert.Single(await fixture.Db.Roles.AsNoTracking().ToListAsync()).Name);
        Assert.Equal(2, await fixture.Db.UserRoles.CountAsync());
        Assert.Equal(2, fixture.Bus.Publications.Count);
    }
}
