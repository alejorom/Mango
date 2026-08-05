## Why

`Mango.Services.AuthAPI` is existing, running code with no formal specification. There is no documented record of its domain model, endpoints, business rules, JWT issuance model, or dependencies, which makes onboarding, review, and future changes error-prone. This is also the service that issues the JWTs consumed by `Mango.Services.CouponAPI` and other services, so an accurate baseline is needed before any auth-related change is proposed. This change documents the **current, as-built** state of the service as a baseline spec, without changing any behavior.

## What Changes

- Document the domain model: `ApplicationUser` (extends `IdentityUser` with a `Name` field), `AppDbContext` (extends `IdentityDbContext<ApplicationUser>`, SQL Server), and confirm ASP.NET Core Identity (`UserManager`, `RoleManager`, `IdentityRole`) is used for user/role storage.
- Document the 3 existing REST endpoints on `AuthAPIController` (`POST /api/auth/register`, `POST /api/auth/login`, `POST /api/auth/AssignRole`).
- Document the business rules currently enforced (or not enforced): default ASP.NET Identity password rules (no custom `PasswordOptions` configured), email/username uniqueness via Identity's default user store behavior, and case-insensitive lookups performed manually in `AuthService`.
- Document how JWTs are issued: claims (`sub` = user id, `email`, `name` = `UserName`, one `role` claim per assigned role), signing via HMAC-SHA256 with `ApiSettings:JwtOptions:Secret`, `Issuer`/`Audience` from configuration, 7-day expiration, no refresh token mechanism.
- Document that `AuthAPIController` itself has no `[Authorize]` attributes and that the service does not register `AddAuthentication`/`AddJwtBearer` - it issues tokens but does not validate them on its own endpoints.
- Document external dependencies: NuGet packages (Identity, EF Core SqlServer, JwtBearer, AutoMapper, Swashbuckle), SQL Server database, and the `Mango.MessageBus` project reference used to publish a message on successful registration.
- **Call out the message bus configuration gap**: `Mango.MessageBus.Service.MessageBus` requires a `ConnectionStrings:ServiceBusConnection` value, but neither `appsettings.json` nor `appsettings.Development.json` in `Mango.Services.AuthAPI` define it, so `POST /api/auth/register` throws once it reaches the publish step (not login, which does not use the message bus).
- Call out other ambiguous or incomplete points found in the code (swallowed exception in `Register`, no HTTP 4xx status codes returned on business failures, unprotected `AssignRole` endpoint, no explicit password/email validation beyond Identity defaults, no refresh-token/expired-token handling).

This is a documentation-only change. **No code is modified.**

## Capabilities

### New Capabilities
- `auth-api`: Baseline specification of the current `Mango.Services.AuthAPI` microservice - domain model, endpoints, business rules, JWT issuance, and dependencies as they exist today.

### Modified Capabilities
- None.

## Impact

- Affected code: none (read-only documentation of `Mango.Services.AuthAPI`).
- Affected artifacts: adds `openspec/specs/auth-api/spec.md` as the baseline spec for this capability.
- No API, schema, or dependency changes.
