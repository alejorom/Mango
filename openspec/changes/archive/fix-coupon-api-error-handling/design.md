## Context

`CouponAPIController` currently returns every response as `ResponseDto` with HTTP 200, regardless of outcome. Not-found lookups use `.First()`, which throws `InvalidOperationException`, caught by a blanket `try/catch` that copies `ex.Message` into `ResponseDto.Message`. `CouponDto` carries no validation attributes. See proposal.md - Why/What Changes for the full list of defects being fixed.

## Goals / Non-Goals

**Goals:**
- Return correct HTTP status codes (404, 400, 500) from `CouponAPIController` while keeping the existing `ResponseDto` envelope shape (`Result`, `IsSuccess`, `Message`) for backward compatibility of the body shape.
- Replace exception-driven not-found flow with explicit `FirstOrDefault` + null checks.
- Add validation on `CouponDto` using standard ASP.NET Core model validation (data annotations + `ModelState`).
- Stop leaking `ex.Message` to callers.

**Non-Goals:**
- Not changing the `ResponseDto` envelope structure itself (no new fields, no versioned response contract).
- Not adding centralized/global exception-handling middleware; fixes stay local to `CouponAPIController` to keep the change small and scoped to this service.
- Not changing `Coupon`/database schema.

## Decisions

- **Validation approach**: Use `System.ComponentModel.DataAnnotations` on `CouponDto` (`[Required]` on `CouponCode`, `[Range]`/custom check on `DiscountAmount` and `MinAmount`) combined with an explicit `if (!ModelState.IsValid)` check in `Post`/`Put`, returning `BadRequest(ModelState)`. Alternative considered: FluentValidation - rejected to avoid adding a new dependency for a small, single-service fix.
- **Not-found signaling**: Replace `.First(...)` with `.FirstOrDefault(...)`; when `null`, return `NotFound(_response)` with `_response.IsSuccess = false` and a generic message (e.g., `"Coupon not found"`). Alternative considered: throwing a custom `NotFoundException` caught by middleware - rejected as out of scope (would require new middleware, a Non-Goal).
- **Error message exposure**: Keep the existing `try/catch` per action but stop assigning `ex.Message` to `_response.Message`; use a fixed generic message (e.g., `"An unexpected error occurred"`) and return `StatusCode(500, _response)`. Server-side logging of the real exception is implied but not specified here (logging infrastructure is out of scope for this fix).
- **HTTP status codes returned via `ActionResult<ResponseDto>`**: Controller actions change return type from `ResponseDto` to `ActionResult<ResponseDto>` (or equivalent) so `NotFound`/`BadRequest`/`StatusCode` helpers can be used while keeping `ResponseDto` as the body.

## Risks / Trade-offs

- [Breaking change for existing clients that branch only on `ResponseDto.IsSuccess` while assuming HTTP 200] → Call out as **BREAKING** in the proposal; downstream consumers (e.g., `Mango.Web`) must be checked for coupon-not-found handling that assumes 200.
- [Removing `ex.Message` from responses reduces debuggability for API consumers] → Acceptable trade-off for avoiding information disclosure; server-side logging (if added later) covers diagnostics.
- [Data annotation validation on `CouponDto` may reject payloads that were previously silently accepted (e.g., `MinAmount` negative)] → Intentional per proposal; documented as a behavior change in the modified spec.

## Migration Plan

- Update `CouponAPIController` and `CouponDto` in a single PR; no data migration needed (no schema change).
- No rollback complexity beyond reverting the code change; no persisted data format changes.
