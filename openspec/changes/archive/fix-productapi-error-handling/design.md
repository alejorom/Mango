## Context

`ProductAPIController` currently returns every response as `ResponseDto` with HTTP 200, regardless of outcome. Not-found lookups (`Get(id)`, `Delete(id)`) use `.FirstAsync(...)`, which throws `InvalidOperationException`, caught by a blanket `try/catch` that copies `ex.Message` into `ResponseDto.Message`. `PUT` maps the payload straight into a new `Product` and calls `Update()` without checking the row exists. `ProductDto` carries no validation attributes even though the `Product` entity already declares `[Required]`/`[Range(1, 1000)]`. See proposal.md - Why/What Changes for the full list of defects being fixed. `Mango.Services.CouponAPI` was fixed for the equivalent set of issues in `fix-coupon-api-error-handling`; this change follows the same approach for consistency across services.

## Goals / Non-Goals

**Goals:**
- Return correct HTTP status codes (404, 400, 500) from `ProductAPIController` while keeping the existing `ResponseDto` envelope shape (`Result`, `IsSuccess`, `Message`).
- Replace exception-driven not-found flow in `Get(id)`/`Delete(id)` with explicit `FirstOrDefaultAsync` + null checks.
- Make `PUT` existence-safe: look the product up first, return 404 if missing, otherwise update its fields in place.
- Add validation on `ProductDto` using standard ASP.NET Core model validation (data annotations + `ModelState`), matching the range already declared on the `Product` entity.
- Stop leaking `ex.Message` to callers.

**Non-Goals:**
- Not changing the `ResponseDto` envelope structure itself (no new fields, no versioned response contract).
- Not adding centralized/global exception-handling middleware; fixes stay local to `ProductAPIController`.
- Not changing the `Product`/database schema, migrations, or seed data.
- Not adding authentication/authorization to `GET /api/product` or `GET /api/product/{id}` - these remain intentionally public (see proposal.md and the updated `product-api` spec).
- Not addressing pagination, `Name` uniqueness, or the redundant `AddAuthentication()` call in `Program.cs` - explicitly deferred per the proposal.

## Decisions

- **Validation approach**: Use `System.ComponentModel.DataAnnotations` on `ProductDto` (`[Required]` on `Name`, `[Range(1, 1000)]` on `Price`, mirroring the entity) combined with an explicit `if (!ModelState.IsValid)` check in `Post`/`Put`, returning `BadRequest(ModelState)`. Same approach used in `fix-coupon-api-error-handling`; rejected FluentValidation to avoid a new dependency for a small, single-service fix.
- **Not-found signaling**: Replace `.FirstAsync(...)` with `.FirstOrDefaultAsync(...)` in `Get(id)` and `Delete(id)`; when `null`, return `NotFound(_response)` with `_response.IsSuccess = false` and a generic message (`"Product not found"`).
- **`PUT` existence check**: Fetch the tracked `Product` via `FirstOrDefaultAsync(u => u.ProductId == productDto.ProductId)` first. If `null`, return `NotFound(_response)` without calling `SaveChangesAsync`. If found, copy the DTO's fields onto the tracked entity (instead of mapping a detached instance and calling `Update()`), then `SaveChangesAsync()`. Alternative considered: keep `_mapper.Map<Product>(productDto)` + `Update()` guarded by a prior existence check - rejected because it still risks overwriting concurrency/columns not present in the DTO; updating the tracked entity's properties is the more conventional EF Core pattern and avoids a second, redundant `Update()` call.
- **Error message exposure**: Keep the existing `try/catch` per action but stop assigning `ex.Message` to `_response.Message`; use a fixed generic message (`"An unexpected error occurred"`) and return `StatusCode(500, _response)`.
- **HTTP status codes returned via `ActionResult<ResponseDto>`**: All five controller actions change return type from `ResponseDto` to `ActionResult<ResponseDto>` so `NotFound`/`BadRequest`/`StatusCode` helpers can be used while keeping `ResponseDto` as the body. `GET` actions keep no `[Authorize]` attribute; only the return type changes.

## Risks / Trade-offs

- [Breaking change for existing clients that branch only on `ResponseDto.IsSuccess` while assuming HTTP 200] → Call out as **BREAKING** in the proposal; `Mango.Web` and `Mango.Services.ShoppingCartAPI` (`ProductService.GetProducts()`) must be checked for product-not-found/validation handling that assumed 200.
- [Removing `ex.Message` from responses reduces debuggability for API consumers] → Acceptable trade-off for avoiding information disclosure; matches the precedent set in `CouponAPI`.
- [Data annotation validation on `ProductDto` may reject payloads that were previously silently accepted (e.g., `Price` of 0 or empty `Name`)] → Intentional per proposal; documented as a behavior change in the modified spec.
- [Changing `PUT` to update the tracked entity's fields instead of mapping a new detached instance changes AutoMapper usage for this action only] → Scoped to `Put`; `Post` keeps `_mapper.Map<Product>(productDto)` since it creates a new row.

## Migration Plan

- Update `ProductAPIController` and `ProductDto` in a single PR; no data migration needed (no schema change).
- No rollback complexity beyond reverting the code change; no persisted data format changes.
