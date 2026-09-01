## MODIFIED Requirements

### Requirement: Retrieve cart with computed totals

The system SHALL expose `GET /api/cart/GetCart/{userId}` that returns the full cart for the given user as a `CartDto` with all totals computed.

Total computation:
1. Fetch all `CartDetails` rows for the user's `CartHeader`.
2. Fetch all products from ProductAPI (`GET /api/product`). For each detail row, resolve the matching product by `ProductId`. Items whose `ProductId` is not present in the ProductAPI response SHALL be skipped — they SHALL NOT contribute to `CartTotal` and their `Product` field SHALL remain null.
3. `CartTotal` = sum of (`item.Count × item.Product.Price`) for all **resolved** detail rows only.
4. If any detail rows could not be resolved (product not found in ProductAPI response), the response SHALL set `IsSuccess = false` with a descriptive message identifying the unresolvable items. `Result` SHALL still contain the partially composed `CartDto` with the resolved items' totals.
5. If `CouponCode` is not null/empty and all products resolved: fetch the coupon from CouponAPI (`GET /api/coupon/GetByCode/{code}`). If the coupon is not `null` AND `CartTotal > coupon.MinAmount`, subtract `coupon.DiscountAmount` from `CartTotal` and set `CartHeader.Discount = coupon.DiscountAmount`. If the coupon is `null` (CouponAPI failed or code not found), no discount SHALL be applied.

The response is wrapped in `ResponseDto` with `IsSuccess = true` and `Result` set to the composed `CartDto`, unless unresolved products are detected (step 4).

#### Scenario: Cart retrieved with product prices and no coupon

- **WHEN** an authenticated caller sends `GET /api/cart/GetCart/{userId}` for a user with existing cart items and no `CouponCode`
- **THEN** the response `Result` contains a `CartDto` where each `CartDetailsDto.Product` is populated from ProductAPI and `CartHeader.CartTotal` equals the sum of all `(Count × Product.Price)` values

#### Scenario: Coupon discount applied when total exceeds minimum

- **WHEN** a cart has a `CouponCode` set, CouponAPI returns a non-null coupon, and `CartTotal > coupon.MinAmount`
- **THEN** `CartHeader.CartTotal` is reduced by `coupon.DiscountAmount` and `CartHeader.Discount` equals `coupon.DiscountAmount`

#### Scenario: Coupon not applied when total does not exceed minimum

- **WHEN** a cart has a `CouponCode` set but `CartTotal <= coupon.MinAmount`
- **THEN** `CartHeader.CartTotal` and `CartHeader.Discount` remain unchanged (no discount applied)

#### Scenario: Coupon lookup fails or returns no match

- **WHEN** CouponAPI returns `IsSuccess = false` for the given coupon code (coupon is `null`)
- **THEN** no discount is applied; `CartTotal` and `Discount` reflect the raw product total

#### Scenario: One or more products not resolvable from ProductAPI

- **WHEN** one or more `CartDetails` rows have a `ProductId` that is not present in the ProductAPI response
- **THEN** those items are excluded from `CartTotal`, and the response sets `IsSuccess = false` with a message identifying the unresolved product ids; resolved items' totals are still returned in `Result`

---

### Requirement: Remove cart item

The system SHALL expose `POST /api/cart/RemoveCart` that accepts a `cartDetailsId` (int) and removes the matching `CartDetails` row. If the removed row was the only item in the cart, the associated `CartHeader` SHALL also be removed.

If no `CartDetails` row with the given `cartDetailsId` exists, the system SHALL return HTTP 404 Not Found with `IsSuccess = false` and a descriptive not-found message, without attempting any deletion.

The response is a `ResponseDto` with `IsSuccess = true` and `Result = true` on success.

#### Scenario: Item removed, cart header retained

- **WHEN** `POST /api/cart/RemoveCart` is called with a `cartDetailsId` that belongs to a cart with more than one item
- **THEN** only that `CartDetails` row is deleted; the `CartHeader` and remaining items are retained

#### Scenario: Last item removed, header also deleted

- **WHEN** `POST /api/cart/RemoveCart` is called with the only remaining `cartDetailsId` for a given cart
- **THEN** both the `CartDetails` row and its parent `CartHeader` are deleted

#### Scenario: cartDetailsId not found

- **WHEN** `POST /api/cart/RemoveCart` is called with a `cartDetailsId` that does not exist in the database
- **THEN** the response is HTTP 404 Not Found with `IsSuccess = false` and a not-found message; no rows are deleted

---

### Requirement: Downstream integration with CouponAPI

The system SHALL call CouponAPI using a named `HttpClient` (`"Coupon"`) with base address from `ServiceUrls:CouponAPI`. The outgoing request carries the caller's JWT bearer token, propagated by `BackendApiAuthenticationHttpClientHandler`.

The call is `GET /api/coupon/GetByCode/{code}`. The response is deserialized as `ResponseDto`; on `IsSuccess = true`, the `Result` is deserialized as `CouponDto`. On failure (`IsSuccess = false`, null response, unreachable host, or any non-success HTTP status), the system SHALL return `null` without throwing. `EnsureSuccessStatusCode` is NOT called.

#### Scenario: CouponAPI returns a valid coupon

- **WHEN** CouponAPI responds with `IsSuccess = true` and coupon data
- **THEN** the coupon's `DiscountAmount` and `MinAmount` are used in `GetCart` discount calculation

#### Scenario: CouponAPI unavailable or code not found

- **WHEN** CouponAPI is unreachable or responds with `IsSuccess = false`
- **THEN** `CouponService.GetCoupon` returns `null`; no exception propagates from the service layer; `GetCart` applies no discount

---

### Requirement: Error handling and response envelope

All controller actions SHALL return `ActionResult<ResponseDto>`. Successful responses SHALL use the implicit 200 OK with the `ResponseDto` result. On an unhandled exception, any action SHALL return `StatusCode(500, _response)` with `IsSuccess = false` and `Message` set to `"An unexpected error occurred"`. On a not-found condition (e.g., `RemoveCart` with unknown `cartDetailsId`), the action SHALL return `NotFound(_response)` with `IsSuccess = false` and a descriptive message.

No DTO validation attributes (`[Required]`, `[Range]`, etc.) are present on any request model.

#### Scenario: Successful action

- **WHEN** a cart action completes without error
- **THEN** the response is HTTP 200 with `ResponseDto.IsSuccess = true`

#### Scenario: Unhandled exception in cart action

- **WHEN** an unhandled exception occurs inside a cart action
- **THEN** the response is HTTP 500 with `ResponseDto.IsSuccess = false` and `ResponseDto.Message = "An unexpected error occurred"`; no exception details or stack trace are exposed to the caller

#### Scenario: Not-found condition in cart action

- **WHEN** a cart action is called with an identifier that does not exist (e.g., unknown `cartDetailsId`)
- **THEN** the response is HTTP 404 Not Found with `ResponseDto.IsSuccess = false` and a descriptive message
