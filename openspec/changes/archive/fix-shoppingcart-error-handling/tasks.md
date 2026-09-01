## 1. CouponService — fix null return on failure

- [x] 1.1 Change `ICouponService.GetCoupon` return type to `Task<CouponDto?>`
- [x] 1.2 Change `CouponService.GetCoupon` return type to `Task<CouponDto?>` and replace `return new CouponDto()` with `return null` in the failure branch

## 2. CartAPIController — fix GetCart

- [x] 2.1 In `GetCart`, after resolving each item's product, skip items where `item.Product == null` (ProductId not in ProductAPI response) instead of accessing `null.Price`
- [x] 2.2 After the loop, if any items were skipped, set `_response.IsSuccess = false` with a message listing the unresolvable product ids; still populate `_response.Result` with the partial cart
- [x] 2.3 In `GetCart`, change the coupon discount block to guard on `coupon != null` (CouponService now returns null on failure — the guard is now reachable) and skip coupon application entirely when `unresolvedIds` is non-empty (CartTotal is partial)

## 3. CartAPIController — fix RemoveCart

- [x] 3.1 Replace `.First(u => u.CartDetailsId == cartDetailsId)` with `.FirstOrDefault(...)` in `RemoveCart`
- [x] 3.2 If the result is null, return `NotFound(_response)` with `IsSuccess = false` and a not-found message (guard + message applied now; `NotFound(...)` call wired in task 4.3 when return type becomes `ActionResult<ResponseDto>`)

## 4. CartAPIController — migrate all actions to ActionResult<ResponseDto>

- [x] 4.1 Change return type of `GetCart` from `ResponseDto` to `ActionResult<ResponseDto>`; update all return sites (`return _response` → `return Ok(_response)` or direct return where implicit conversion applies; catch block → `return StatusCode(500, _response)`)
- [x] 4.2 Change return type of `CartUpsert` from `ResponseDto` to `ActionResult<ResponseDto>`; catch block → `return StatusCode(500, _response)`
- [x] 4.3 Change return type of `RemoveCart` from `ResponseDto` to `ActionResult<ResponseDto>`; not-found → `return NotFound(_response)`; catch block → `return StatusCode(500, _response)`
- [x] 4.4 Change return type of `ApplyCoupon` from `object` to `ActionResult<ResponseDto>`; catch block → `return StatusCode(500, _response)`
- [x] 4.5 Change return type of `EmailCartRequest` from `object` to `ActionResult<ResponseDto>`; catch block → `return StatusCode(500, _response)`

## 5. CartAPIController — fix exception message exposure

- [x] 5.1 In `ApplyCoupon` catch, replace `ex.ToString()` with `"An unexpected error occurred"`
- [x] 5.2 In `EmailCartRequest` catch, replace `ex.ToString()` with `"An unexpected error occurred"`

## 6. Program.cs — remove redundant AddAuthentication call

- [x] 6.1 Remove the `builder.Services.AddAuthentication()` call that follows `builder.AddAppAuthetication()` in `Program.cs`

## 7. Validate and verify

- [x] 7.1 Build `Mango.Services.ShoppingCartAPI` — confirm zero compile errors
- [x] 7.2 Run `openspec validate fix-shoppingcart-error-handling --strict`
