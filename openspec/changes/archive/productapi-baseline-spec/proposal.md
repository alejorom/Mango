## Why

`Mango.Services.ProductAPI` is existing, running code with no formal specification. There is no documented record of its domain model, endpoints, business rules, auth model, or dependencies, which makes onboarding, review, and future changes error-prone. This change documents the **current, as-built** state of the service as a baseline spec, without changing any behavior.

## What Changes

- Document the `Product` domain entity and `AppDbContext` (EF Core, SQL Server, single `Products` table, seeded with 4 rows via `HasData`).
- Document the 5 existing REST endpoints on `ProductAPIController` (GET all, GET by id, POST, PUT, DELETE).
- Document the business rules currently enforced (or not enforced) in the controller, including the fact that `ProductDto` carries no server-side validation attributes even though the `Product` entity does.
- Document the authentication/authorization model actually in place: JWT Bearer validation via the same flat `ApiSettings:Secret`/`Issuer`/`Audience` pattern as `CouponAPI` (not the nested `ApiSettings:JwtOptions` pattern used by `AuthAPI`), with `[Authorize(Roles = "ADMIN")]` only on POST/PUT/DELETE and **no** `[Authorize]` at all on the controller or on the GET actions (unauthenticated read access).
- Document external dependencies: NuGet packages, SQL Server database, and confirm no message bus (`Mango.MessageBus`/Azure Service Bus) package or project reference exists in `Mango.Services.ProductAPI.csproj`. Document that `Mango.Services.ShoppingCartAPI` consumes this API synchronously over HTTP (named `HttpClient` "Product", `GET /api/product`) to resolve product data for cart items - not via message bus.
- Call out ambiguous or incomplete points found in the code (`.FirstAsync()` on lookups instead of `FirstOrDefaultAsync`, no HTTP status codes differentiation - endpoints return bare `ResponseDto` instead of `ActionResult<ResponseDto>`, no 404 on missing product, exception messages leaked in `Message`, no DTO-level validation, unauthenticated GET endpoints, blind `Update()` on PUT without existence check).

This is a documentation-only change. **No code is modified.**

## Capabilities

### New Capabilities
- `product-api`: Baseline specification of the current `Mango.Services.ProductAPI` microservice - domain model, endpoints, business rules, auth, and dependencies as they exist today.

### Modified Capabilities
- None.

## Impact

- Affected code: none (read-only documentation of `Mango.Services.ProductAPI`).
- Affected artifacts: adds `openspec/specs/product-api/spec.md` as the baseline spec for this capability.
- No API, schema, or dependency changes.
