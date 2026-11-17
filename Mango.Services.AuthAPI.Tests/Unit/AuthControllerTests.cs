using Mango.Services.AuthAPI.Controllers;
using Mango.Services.AuthAPI.Models.Dto;
using Mango.Services.AuthAPI.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Mango.Services.AuthAPI.Tests.Unit;

[Trait("Category", "Unit")]
public class AuthControllerTests
{
    [Fact]
    public async Task RegisterPublishesOnlyAfterSuccessfulRegistrationCompletes()
    {
        var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new AuthServiceStub { Registration = completion.Task };
        var bus = new RecordingBus();
        var controller = new AuthAPIController(service, bus, AuthTestData.Configuration());
        var request = AuthTestData.Registration();

        var pending = controller.Register(request);

        Assert.Same(request, service.RegisteredRequest);
        Assert.False(pending.IsCompleted);
        Assert.Empty(bus.Publications);
        completion.SetResult("");
        var result = Assert.IsType<OkObjectResult>(await pending.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.True(Assert.IsType<ResponseDto>(result.Value).IsSuccess);
        var publication = Assert.Single(bus.Publications);
        Assert.Equal(request.Email, publication.Payload);
        Assert.Equal(AuthTestData.Queue, publication.Destination);
    }

    [Theory]
    [InlineData("Password validation failed")]
    [InlineData("Error Encountered")]
    public async Task RegistrationFailureReturns400WithoutPublishing(string error)
    {
        var service = new AuthServiceStub { Registration = Task.FromResult(error) };
        var bus = new RecordingBus();
        var controller = new AuthAPIController(service, bus, AuthTestData.Configuration());

        var result = Assert.IsType<BadRequestObjectResult>(await controller.Register(AuthTestData.Registration()));

        var response = Assert.IsType<ResponseDto>(result.Value);
        Assert.False(response.IsSuccess);
        Assert.Equal(error, response.Message);
        Assert.Empty(bus.Publications);
    }

    [Fact]
    public async Task LoginSuccessReturnsServiceUserAndTokenWithoutPublishing()
    {
        var service = new AuthServiceStub
        {
            LoginResponse = new LoginResponseDto
            {
                User = new UserDto { ID = "member", Email = "member@example.invalid", Name = "Member", PhoneNumber = "000" },
                Token = "synthetic-token"
            }
        };
        var bus = new RecordingBus();
        var controller = new AuthAPIController(service, bus, AuthTestData.Configuration());
        var request = new LoginRequestDto { UserName = "member@example.invalid", Password = AuthTestData.Password };

        var result = Assert.IsType<OkObjectResult>(await controller.Login(request));

        var response = Assert.IsType<ResponseDto>(result.Value);
        Assert.True(response.IsSuccess);
        Assert.Same(service.LoginResponse, response.Result);
        Assert.Same(request, service.LoginRequest);
        Assert.Empty(bus.Publications);
    }

    [Fact]
    public async Task LoginFailureReturnsGeneric400WithoutPublishing()
    {
        var bus = new RecordingBus();
        var controller = new AuthAPIController(new AuthServiceStub(), bus, AuthTestData.Configuration());

        var result = Assert.IsType<BadRequestObjectResult>(await controller.Login(
            new LoginRequestDto { UserName = "missing@example.invalid", Password = "wrong" }));

        var response = Assert.IsType<ResponseDto>(result.Value);
        Assert.False(response.IsSuccess);
        Assert.Equal("Username or password is incorrect", response.Message);
        Assert.Null(response.Result);
        Assert.Empty(bus.Publications);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task MissingRoleReturns400WithoutCallingService(string? role)
    {
        var service = new AuthServiceStub();
        var bus = new RecordingBus();
        var controller = new AuthAPIController(service, bus, AuthTestData.Configuration());

        var result = Assert.IsType<BadRequestObjectResult>(await controller.AssignRole(AuthTestData.Registration(role: role)));

        var response = Assert.IsType<ResponseDto>(result.Value);
        Assert.False(response.IsSuccess);
        Assert.Equal("Error encountered", response.Message);
        Assert.Empty(service.Assignments);
        Assert.Empty(bus.Publications);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RoleAssignmentNormalizesRoleAndReflectsServiceResult(bool succeeds)
    {
        var service = new AuthServiceStub { AssignmentSucceeds = succeeds };
        var bus = new RecordingBus();
        var controller = new AuthAPIController(service, bus, AuthTestData.Configuration());

        var result = await controller.AssignRole(AuthTestData.Registration(role: "customer"));

        var response = succeeds
            ? Assert.IsType<ResponseDto>(Assert.IsType<OkObjectResult>(result).Value)
            : Assert.IsType<ResponseDto>(Assert.IsType<BadRequestObjectResult>(result).Value);
        Assert.Equal(succeeds, response.IsSuccess);
        Assert.Equal(("member@example.invalid", "CUSTOMER"), Assert.Single(service.Assignments));
        if (!succeeds) Assert.Equal("Error encountered", response.Message);
        Assert.Empty(bus.Publications);
    }

    [Fact]
    public async Task PublishFailurePropagatesOutsideRegistrationValidation()
    {
        var failure = new InvalidOperationException("synthetic-bus-failure");
        var bus = new RecordingBus { Failure = failure };
        var controller = new AuthAPIController(new AuthServiceStub(), bus, AuthTestData.Configuration());

        var observed = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.Register(AuthTestData.Registration()));

        Assert.Same(failure, observed);
        Assert.Single(bus.Publications);
    }
}
