## Context

`Mango.Services.AuthAPI` issues JWTs but never validates them itself: `Program.cs` has no `AddAuthentication`/`AddJwtBearer` registration, and `AuthAPIController` has no `[Authorize]` attributes. `AssignRole` is therefore reachable by anyone and can grant `ADMIN` to any account. `CouponAPI`, `ProductAPI`, and `ShoppingCartAPI` already validate JWTs via a per-service `Extensions/WebApplicationBuilderExtensions.AddAppAuthetication()` method (note the existing typo, kept for consistency with those services), reading `ApiSettings:Secret/Issuer/Audience`. `AuthAPI`'s own JWT config lives one level deeper, under `ApiSettings:JwtOptions:Secret/Issuer/Audience` (bound to the `JwtOptions` class already used by `JwtTokenGenerator`), so the extension method for this service must read that nested section instead of copying `CouponAPI`'s path. See [proposal.md](proposal.md) for motivation.

## Goals / Non-Goals

**Goals:**
- Require a valid JWT with role `ADMIN` on `POST /api/auth/AssignRole`, matching the write-endpoint pattern already used in `CouponAPI`.
- Eliminate the two identified unhandled-exception paths (`Login` null-check ordering, `AssignRole` null `Role`) without changing their documented client-facing responses (still HTTP 400 with the existing messages).
- Make the previously silent `Register` exception observable server-side via logging.

**Non-Goals:**
- Fixing the `MessageBus` `ArgumentNullException` on missing `ServiceBusConnection` (separate change, per proposal).
- Adding authentication/authorization to `register` or `login` (they must stay open for unauthenticated credential issuance).
- Introducing refresh tokens, rate limiting, or any other hardening not explicitly requested.
- Changing HTTP status codes/response shapes beyond what's needed to prevent the two crash scenarios (both already return HTTP 400 on their respective handled failure paths - only the crash path is removed, not the contract).

## Decisions

- **Add JWT bearer validation to `AuthAPI` via a new `Extensions/WebApplicationBuilderExtensions.AddAppAuthetication()`**, mirroring `CouponAPI`'s structure and method name (including its existing "Authetication" typo, to stay consistent with the rest of the codebase per repo convention) but reading `ApiSettings:JwtOptions:Secret`, `ApiSettings:JwtOptions:Issuer`, `ApiSettings:JwtOptions:Audience` (the section this service already binds `JwtOptions` from), instead of introducing a second, differently-shaped config section.
  - Alternative considered: inline `AddAuthentication`/`AddJwtBearer` directly in `Program.cs`. Rejected to stay consistent with the extension-method pattern used by every other API in the solution.
- **Only protect `AssignRole` with `[Authorize(Roles = "ADMIN")]`**, leaving `register`/`login` unauthenticated, since those two endpoints are the only ones in this controller that must remain callable without a prior token.
- **`Login` fix**: move the `user == null` check before the `CheckPasswordAsync` call and short-circuit-return the existing "incorrect credentials" `LoginResponseDto` when null, rather than restructuring the method further. Keeps the change minimal and preserves the existing return shape/status code.
- **`AssignRole` fix**: add a guard clause in the controller (`string.IsNullOrWhiteSpace(model.Role)`) before calling `_authService.AssignRole(model.Email, model.Role.ToUpper())`, returning the existing `"Error encountered"` / HTTP 400 response. Chosen over pushing the check into `AuthService` because the controller already owns the `.ToUpper()` call that would throw.
- **`Register` logging**: inject `ILogger<AuthService>` into `AuthService` (standard ASP.NET Core DI, no new package) and call `_logger.LogError(ex, ...)` inside the existing `catch` block. The client-facing `"Error Encountered"` message is unchanged - only server-side observability is added.

## Risks / Trade-offs

- [Risk] Enabling JWT validation in `AuthAPI` for the first time could affect other endpoints if more are added later without an explicit `[Authorize]`/`[AllowAnonymous]` decision. → Mitigation: only `AssignRole` gets `[Authorize(Roles = "ADMIN")]` in this change; `register`/`login` are left explicitly open by omission (default `[ApiController]` actions are anonymous unless attributed), matching current behavior.
- [Risk] Any existing manual/bootstrap calls to `AssignRole` without a token (e.g., initial admin setup scripts) will start failing with 401. → Mitigation: called out as a **BREAKING** change in the proposal; an admin must already hold a valid `ADMIN`-role token (obtained by first registering a user and assigning `ADMIN` through some other trusted path, e.g., direct DB/Identity seeding) before this change ships.
- [Risk] `Program.cs` must call `app.UseAuthentication()` before `app.UseAuthorization()` (order matters). → Mitigation: verify existing middleware order during implementation; `UseAuthentication()` is already present but was a no-op without a registered authentication scheme - adding the scheme makes it active.
