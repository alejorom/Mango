## 1. Add JWT authentication to AuthAPI

- [ ] 1.1 Create `Mango.Services.AuthAPI/Extensions/WebApplicationBuilderExtensions.cs` with `AddAppAuthetication()`, reading `ApiSettings:JwtOptions:Secret/Issuer/Audience` and registering `AddAuthentication` + `AddJwtBearer`
- [ ] 1.2 Call `builder.AddAppAuthetication()` in `Program.cs` and confirm `app.UseAuthentication()` precedes `app.UseAuthorization()`

## 2. Protect AssignRole

- [ ] 2.1 Add `[Authorize(Roles = "ADMIN")]` to the `AssignRole` action in `AuthAPIController`
- [ ] 2.2 Add a guard in `AssignRole` returning the existing HTTP 400 `"Error encountered"` response when `model.Role` is null/empty, before calling `.ToUpper()` or the service

## 3. Fix Login null-check ordering

- [ ] 3.1 In `AuthService.Login`, check `user == null` before calling `CheckPasswordAsync`, returning the existing "incorrect credentials" `LoginResponseDto` immediately when null

## 4. Log the swallowed Register exception

- [ ] 4.1 Inject `ILogger<AuthService>` into `AuthService`
- [ ] 4.2 Log the caught exception in `Register`'s catch block (server-side only; client-facing `"Error Encountered"` message unchanged)

## 5. Verify

- [ ] 5.1 Build `Mango.Services.AuthAPI` and confirm no compile errors
- [ ] 5.2 Manually verify: `AssignRole` without a token is rejected (401), with a non-ADMIN token is rejected (403), with an ADMIN token succeeds
- [ ] 5.3 Manually verify: `Login` with a non-existent username returns HTTP 400 without throwing
- [ ] 5.4 Manually verify: `AssignRole` with missing `Role` returns HTTP 400 without throwing
- [ ] 5.5 Run `openspec validate --change fix-authapi-security-issues --strict` and fix any structural issues

## 6. Additional
- [ ] Nota adicional: se confirmó que el ArgumentNullException del MessageBus 
  ocurre en el CONSTRUCTOR de MessageBus, no en PublishMessage — por lo tanto 
  tumba TODOS los endpoints de AuthAPIController (no solo Register), ya que 
  ASP.NET Core debe resolver IMessageBus vía DI antes de instanciar el 
  controller para cualquier acción. Esto eleva la severidad del gap de 
  configuración del MessageBus: no es un fallo aislado a un endpoint, es una 
  falla total del servicio si ServiceBusConnection no está configurada.