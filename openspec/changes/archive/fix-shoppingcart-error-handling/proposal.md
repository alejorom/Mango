## Why

The `shoppingcart-api` baseline surfaced critical runtime bugs: a nil-guard that is never actually exercised (coupon), a silent crash path when ProductAPI is unavailable, and an exception-dependent not-found response on remove. All five actions also always return HTTP 200, making error detection impossible for callers. These are not theoretical risks — they produce wrong totals or opaque failures under normal degraded-service conditions.

## What Changes

- **CRITICAL FIX**: `CouponService.GetCoupon` returns `null` (not `new CouponDto()`) when CouponAPI fails or returns `IsSuccess = false`. `GetCart` skips discount when the returned coupon is `null`.
- **CRITICAL FIX**: `GetCart` skips cart items whose `ProductId` is not found in the ProductAPI response instead of accessing `null.Price`. If any items are unresolved, `GetCart` returns `IsSuccess = false` with a descriptive message instead of silently producing a wrong total.
- `RemoveCart` replaces `.First()` with `.FirstOrDefault()` and returns HTTP 404 when `cartDetailsId` is not found.
- All five controller actions (`GetCart`, `CartUpsert`, `RemoveCart`, `ApplyCoupon`, `EmailCartRequest`) change return type from `ResponseDto`/`object` to `ActionResult<ResponseDto>`. Unhandled exceptions return `StatusCode(500, _response)`; not-found cases return `NotFound(_response)`.
- `ApplyCoupon` and `EmailCartRequest` replace `ex.ToString()` with a fixed generic message (`"An unexpected error occurred"`) — consistent with the pattern used by the other three actions, and no longer leaking stack traces.
- Refactor: remove redundant `builder.Services.AddAuthentication()` call in `Program.cs` (already configured by `builder.AddAppAuthetication()`). No observable behavior change.

**Not in scope:** adding `[Authorize]` to `CartAPIController`, validating `userId` against the token sub claim, `CartUpsert` multi-item support, `CartHeaderDto.Name/Phone/Email` server-side population. Those are tracked in separate changes.

## Capabilities

### New Capabilities

*(none)*

### Modified Capabilities

- `shopping-cart-api`: Multiple requirements changing observable behavior — coupon null contract, unresolved-product handling, RemoveCart not-found response, HTTP status codes on all actions, and error message exposure.

## Impact

- `Mango.Services.ShoppingCartAPI/Service/CouponService.cs` — return type contract change
- `Mango.Services.ShoppingCartAPI/Service/IService/ICouponService.cs` — interface return type
- `Mango.Services.ShoppingCartAPI/Controllers/CartAPIController.cs` — all five actions
- `Mango.Services.ShoppingCartAPI/Program.cs` — remove duplicate `AddAuthentication()` call
- No database schema changes. No new migrations.
- Callers of `GET /api/cart/GetCart/{userId}` will now receive `IsSuccess = false` (HTTP 200 with new envelope, or HTTP 500 via `ActionResult`) when products cannot be resolved, instead of a silent wrong total. This is a behavioral change callers should handle.
