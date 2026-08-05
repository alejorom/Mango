## Why

`Mango.Services.CouponAPI` is existing, running code with no formal specification. There is no documented record of its domain model, endpoints, business rules, auth model, or dependencies, which makes onboarding, review, and future changes error-prone. This change documents the **current, as-built** state of the service as a baseline spec, without changing any behavior.

## What Changes

- Document the `Coupon` domain entity and `AppDbContext` (EF Core, SQL Server, single `Coupons` table).
- Document the 6 existing REST endpoints on `CouponAPIController` (GET all, GET by id, GET by code, POST, PUT, DELETE).
- Document the business rules currently enforced (or not enforced) in the controller.
- Document the authentication/authorization model actually in place: JWT Bearer validation, with `[Authorize]` on the controller and `[Authorize(Roles = "ADMIN")]` on write operations. No ASP.NET Core Identity (`UserManager`/`IdentityDbContext`) is used inside this service.
- Document external dependencies: NuGet packages, SQL Server database, and confirm no message bus (RabbitMQ/MassTransit) is referenced.
- Call out ambiguous or incomplete points found in the code (unhandled-lookup exceptions, no HTTP status codes on error, no server-side validation beyond data annotations, no uniqueness constraint on `CouponCode`, etc.).

This is a documentation-only change. **No code is modified.**

## Capabilities

### New Capabilities
- `coupon-api`: Baseline specification of the current `Mango.Services.CouponAPI` microservice - domain model, endpoints, business rules, auth, and dependencies as they exist today.

### Modified Capabilities
- None.

## Impact

- Affected code: none (read-only documentation of `Mango.Services.CouponAPI`).
- Affected artifacts: adds `openspec/specs/coupon-api/spec.md` as the baseline spec for this capability.
- No API, schema, or dependency changes.
