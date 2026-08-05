## 1. Validate baseline accuracy

- [x] 1.1 Cross-check `specs/auth-api/spec.md` requirements against current `Mango.Services.AuthAPI` source (`Controllers/AuthAPIController.cs`, `Models/ApplicationUser.cs`, `Models/JwtOptions.cs`, `Data/AppDbContext.cs`, `Service/AuthService.cs`, `Service/JwtTokenGenerator.cs`, `Program.cs`)
- [x] 1.2 Confirm the `Mango.MessageBus` project reference and `IMessageBus` usage in `Program.cs`/`AuthAPIController.cs` match the documented registration-publish behavior
- [x] 1.3 Confirm `appsettings.json` and `appsettings.Development.json` for `Mango.Services.AuthAPI` do not define `ConnectionStrings:ServiceBusConnection`, reproducing the documented configuration gap

## 2. Review ambiguous/incomplete points

- [x] 2.1 Share the "ambiguous or incomplete" findings (unhandled `ArgumentNullException` from missing `ServiceBusConnection`, swallowed exception detail in `Register`, no HTTP 4xx on `AssignRole`/`Login` mapped consistently, unprotected `AssignRole` endpoint, no refresh-token support, `Role` field on `RegistrationRequestDto` accepted but unused during registration) with the team for triage
- [x] 2.1a Empty catch block in `AuthService.Register` swallows exceptions with no logging whatsoever — even server-side, the real error is lost, not just hidden from the caller
- [x] 2.1b `AssignRole` controller action calls `model.Role.ToUpper()` without a null check — a request omitting `Role` throws an unhandled `NullReferenceException` (HTTP 500) instead of the documented HTTP 400 "Error encountered" path
- [x] 2.1c `AuthService.Login` calls `_userManager.CheckPasswordAsync(user, ...)` BEFORE checking if `user` is null — logging in with a non-existent username likely throws an unhandled exception instead of returning the documented HTTP 400 "Username or password is incorrect"
- [x] 2.2 Decide whether any findings should become separate follow-up change proposals (out of scope for this baseline)

## 3. Publish baseline spec

- [x] 3.1 Run `openspec validate --change authapi-baseline-spec --strict` and fix any structural issues
- [x] 3.2 Sync `specs/auth-api/spec.md` into `openspec/specs/auth-api/spec.md` (via `/opsx-archive` or `/opsx-sync-specs`)
- [x] 3.3 Archive the change once the main spec reflects the documented baseline
