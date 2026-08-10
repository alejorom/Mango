## 1. Add JWT authentication to AuthAPI

- [x] 1.1 Create `Mango.Services.AuthAPI/Extensions/WebApplicationBuilderExtensions.cs` with `AddAppAuthetication()`, reading `ApiSettings:JwtOptions:Secret/Issuer/Audience` and registering `AddAuthentication` + `AddJwtBearer`
- [x] 1.2 Call `builder.AddAppAuthetication()` in `Program.cs` and confirm `app.UseAuthentication()` precedes `app.UseAuthorization()`

## 2. Protect AssignRole

- [x] 2.1 Add `[Authorize(Roles = "ADMIN")]` to the `AssignRole` action in `AuthAPIController`
- [x] 2.2 Add a guard in `AssignRole` returning the existing HTTP 400 `"Error encountered"` response when `model.Role` is null/empty, before calling `.ToUpper()` or the service
- [x] 2.3 Configurar JwtBearerEvents en WebApplicationBuilderExtensions.AddAppAuthetication() 
  para que un token válido con rol incorrecto devuelva HTTP 403 (Forbidden), 
  en vez del 401 (Unauthorized) que devuelve JwtBearer por defecto para 
  cualquier fallo de autorización

## 3. Fix Login null-check ordering

- [x] 3.1 In `AuthService.Login`, check `user == null` before calling `CheckPasswordAsync`, returning the existing "incorrect credentials" `LoginResponseDto` immediately when null

## 4. Log the swallowed Register exception

- [x] 4.1 Inject `ILogger<AuthService>` into `AuthService`
- [x] 4.2 Log the caught exception in `Register`'s catch block (server-side only; the client-facing "Error Encountered" message is unchanged)

## 5. Verify

- [x] 5.1 Build `Mango.Services.AuthAPI` and confirm no compile errors
- [x] 5.1a Se detectó que agregar autenticación JWT real (bloque 1) no incluía 
  la configuración de Swagger (AddSecurityDefinition/AddSecurityRequirement) 
  para poder probar endpoints protegidos desde la UI. Se agregó ese bloque a 
  Program.cs, siguiendo el mismo patrón ya usado en CouponAPI.
- [x] 5.2 Manually verify: `AssignRole` without a token is rejected (401), with a non-ADMIN token is rejected (403), with an ADMIN token succeeds
(NOTA: se agregó JwtBearerEvents.OnForbidden en WebApplicationBuilderExtensions 
  para forzar 403 en vez del 401 por defecto de ASP.NET Core cuando el token 
  es válido pero el rol es incorrecto — no estaba en el plan original, se 
  descubrió durante la verificación manual y se corrigió como tarea 2.3)
- [x] 5.3 Manually verify: `Login` with a non-existent username returns HTTP 400 without throwing
- [x] 5.4 Manually verify: `AssignRole` with missing `Role` returns HTTP 400 without throwing
- [x] 5.5 Run `openspec validate --change fix-authapi-security-issues --strict` and fix any structural issues

## 6. Additional
- [x] Nota adicional (informativa, no accionable en este change): se confirmó que 
  el ArgumentNullException del MessageBus ocurre en el CONSTRUCTOR de 
  MessageBus, no en PublishMessage — por lo tanto tumba TODOS los endpoints de 
  AuthAPIController (no solo Register), ya que ASP.NET Core debe resolver 
  IMessageBus vía DI antes de instanciar el controller para cualquier acción. 
  Esto eleva la severidad del gap de configuración del MessageBus: no es un 
  fallo aislado a un endpoint, es una falla total del servicio si 
  ServiceBusConnection no está configurada. Explícitamente fuera de alcance de 
  este change (ver proposal.md); queda documentado para un posible change de 
  seguimiento.