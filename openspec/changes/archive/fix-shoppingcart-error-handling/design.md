## Context

See proposal.md for motivation. Five related bugs in `CartAPIController` and its two downstream service dependencies. All fixes are isolated to `Mango.Services.ShoppingCartAPI` — no schema changes, no new packages, no changes to other services.

Current controller uses concrete return types (`ResponseDto`, `object`) and always emits HTTP 200. `CouponService` returns a default struct on failure making null-guards in the controller inert. `RemoveCart` throws on missing id, and `ApplyCoupon`/`EmailCartRequest` expose stack traces.

## Goals / Non-Goals

**Goals:**
- Fix all five bugs in a single coherent pass on `CartAPIController` and `CouponService`
- Leave the public route signatures unchanged (no breaking URL or payload changes)
- Make all five actions return `ActionResult<ResponseDto>` uniformly in one pass, since the controller needs touching for bugs 1–5 anyway

**Non-Goals:**
- Adding `[Authorize]` or userId/token validation (separate change: `harden-shoppingcart-authorization`)
- `ProductService` contract change — it still returns an empty list on failure; the fix is in how `GetCart` handles that list
- Any new EF migrations or DB schema changes

## Decisions

**Decision: `CouponService.GetCoupon` returns `CouponDto?` (nullable) instead of `CouponDto`.**
The interface `ICouponService` and its implementation change to `Task<CouponDto?>`. The null sentinel is cleaner than a default-valued struct: it is unambiguous (zero discount is a valid coupon value) and forces the caller to handle the absent case explicitly. All call sites are in `GetCart`, which gains a `coupon != null` guard that is now actually reachable.

*Alternative: keep returning `new CouponDto()` but add a sentinel flag.* Rejected — more surface area, same conceptual problem.

**Decision: `GetCart` skips unresolvable items and returns `IsSuccess = false` when any are skipped.**
The alternative — returning `IsSuccess = true` with incomplete totals silently — is worse for callers. A partial result with an explicit error signal lets callers decide what to do (show partial cart, show error, retry). The resolved items and totals are still in `Result` so the response is as useful as possible.

*Alternative: return HTTP 500 / IsSuccess=false with no Result when any product is missing.* Rejected — loses the partially-computed cart, which is unnecessary.

**Decision: All five actions change to `ActionResult<ResponseDto>` in one pass.**
Since four of the five actions need edits anyway (bugs 1–5), converting the return type for all five in the same commit keeps the controller consistent and avoids a half-migrated state. The fifth action (`CartUpsert`) has no bug but is refactored for uniformity.

**Decision: Generic fixed message `"An unexpected error occurred"` for all catch blocks.**
Consistent with the pattern seen in other Mango services (`CouponAPIController`, `ProductAPIController`). `ex.ToString()` was only in two actions; aligning all five to the same message removes the inconsistency and the accidental disclosure risk.

## Risks / Trade-offs

- **`GetCart` behavior change is visible to callers**: previously returned `IsSuccess = true` with a wrong total when ProductAPI was down; now returns `IsSuccess = false`. Any caller checking only `IsSuccess` without also checking for products will see a new failure mode. Low risk in practice since the old behavior (silent wrong total) was strictly worse.
- **`ICouponService` interface change**: if other services or tests depend on the interface returning a non-nullable `CouponDto`, they will break at compile time. Compile-time failure is preferable to the silent runtime bug.
- **HTTP 500 vs HTTP 200 for unhandled exceptions**: callers relying on the old "always 200, check IsSuccess" pattern will receive HTTP 500 instead of 200 on server errors. This is the correct behavior, but callers must be updated to not treat a non-200 as a hard crash.

## Migration Plan

No database migrations. No config changes. Deploy `Mango.Services.ShoppingCartAPI` as a normal service update. `Mango.Web`'s `CartService` uses `IBaseService.SendAsync`, which reads `ResponseDto.IsSuccess` — it already handles `IsSuccess = false` responses, so no Mango.Web changes are required for the bug fixes. The HTTP status code changes are transparent to `BaseService` since it does not call `EnsureSuccessStatusCode`.
