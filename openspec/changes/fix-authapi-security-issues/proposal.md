## Why

The `authapi-baseline-spec` documentation effort surfaced one unauthenticated privilege-escalation endpoint and two unhandled-exception bugs in `Mango.Services.AuthAPI`, plus a swallowed server-side error that hides real failures. These are fixed independently of the still-open message-bus configuration gap, which needs a broader architecture decision and is tracked separately.

## What Changes

- **BREAKING**: `POST /api/auth/AssignRole` now requires a valid JWT bearer token with role `ADMIN` (previously reachable by anyone with no token). Callers without an `ADMIN`-role token will be rejected with 401/403 instead of reaching the controller action.
- Add JWT bearer authentication to `Mango.Services.AuthAPI` itself (`AddAuthentication`/`AddJwtBearer`), reading the existing `ApiSettings:JwtOptions:Secret/Issuer/Audience` configuration, so `[Authorize]` can be enforced on this service's own endpoints.
- Reorder `AuthService.Login` to check for a `null` user before calling `UserManager.CheckPasswordAsync`, returning the existing "credentials incorrect" result immediately when the user does not exist, instead of risking a `NullReferenceException`.
- Add a null/empty check for `Role` in `AuthAPIController.AssignRole` before calling `.ToUpper()`, returning the existing HTTP 400 "Error encountered" response instead of risking a `NullReferenceException`.
- Log the real exception in `AuthService.Register`'s previously empty `catch` block (server-side only; the client-facing `"Error Encountered"` message is unchanged).

Out of scope: the unhandled `ArgumentNullException` thrown by `Mango.MessageBus.Service.MessageBus` when `ConnectionStrings:ServiceBusConnection` is missing during registration. That requires a broader decision on messaging failure handling and will be addressed in a separate change.

## Capabilities

### New Capabilities
- None.

### Modified Capabilities
- `auth-api`: `AssignRole` now requires JWT bearer authentication with role `ADMIN`; `Login` no longer risks an unhandled exception when the user does not exist; `AssignRole` no longer risks an unhandled exception when `Role` is missing; `Register`'s previously silent exception is now logged server-side.

## Impact

- Affected code: `Mango.Services.AuthAPI/Program.cs` (register `AddAuthentication`/`AddJwtBearer`, `UseAuthentication`/`UseAuthorization` ordering if needed), a new `Extensions/WebApplicationBuilderExtensions.cs` (mirroring the `CouponAPI` pattern), `Controllers/AuthAPIController.cs` (`[Authorize(Roles = "ADMIN")]` on `AssignRole`, null check on `Role`), `Service/AuthService.cs` (`Login` null-check reorder, `Register` logging).
- Affected APIs: `POST /api/auth/AssignRole` now requires a bearer token with role `ADMIN` - existing unauthenticated callers (e.g., any admin bootstrap script/manual call) must be updated to send a valid token.
- New dependency: `ILogger<AuthService>` injected into `AuthService` for server-side error logging (no new NuGet package - `Microsoft.Extensions.Logging.Abstractions` is already transitively available via `Microsoft.AspNetCore.App`).
- No database schema changes.
