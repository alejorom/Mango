using Mango.Services.AuthAPI.Models.Dto;
using Mango.Services.AuthAPI.Service.IService;

namespace Mango.Services.AuthAPI.Tests.TestSupport;

internal sealed class AuthServiceStub : IAuthService
{
    public Task<string> Registration { get; set; } = Task.FromResult("");
    public LoginResponseDto LoginResponse { get; set; } = new() { User = null!, Token = "" };
    public bool AssignmentSucceeds { get; set; } = true;
    public RegistrationRequestDto? RegisteredRequest { get; private set; }
    public LoginRequestDto? LoginRequest { get; private set; }
    public List<(string Email, string Role)> Assignments { get; } = [];

    public Task<string> Register(RegistrationRequestDto request)
    {
        RegisteredRequest = request;
        return Registration;
    }

    public Task<LoginResponseDto> Login(LoginRequestDto request)
    {
        LoginRequest = request;
        return Task.FromResult(LoginResponse);
    }

    public Task<bool> AssignRole(string email, string roleName)
    {
        Assignments.Add((email, roleName));
        return Task.FromResult(AssignmentSucceeds);
    }
}
