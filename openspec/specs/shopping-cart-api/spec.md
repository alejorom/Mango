# shopping-cart-api Specification

## Purpose

Defines the observable behavior of the Shopping Cart API microservice: managing per-user shopping carts (add, update, remove items, apply coupons), computing cart totals by compositing live product data from ProductAPI, applying coupon discounts from CouponAPI, and publishing cart data to an Azure Service Bus queue for downstream email processing.

## Requirements

### Requirement: Cart domain model and persistence

The system SHALL persist shopping cart data as two entities in a dedicated SQL Server database (`Mango_ShoppingCart`):

- `CartHeader`: `CartHeaderId` (int, identity PK), `UserId` (string, nullable), `CouponCode` (string, nullable). The fields `Discount` (double) and `CartTotal` (double) are NOT persisted — they are computed at read time and marked `[NotMapped]`.
- `CartDetails`: `CartDetailsId` (int, identity PK), `CartHeaderId` (int, FK to `CartHeader`), `ProductId` (int), `Count` (int). The field `Product` (`ProductDto`) is NOT persisted — it is populated at read time and marked `[NotMapped]`.

One `CartHeader` per user. One `CartDetails` row per distinct `ProductId` within that header.

#### Scenario: Cart tables exist after migration

- **WHEN** the database is migrated
- **THEN** a `CartHeaders` table exists with columns `CartHeaderId` (identity PK), `UserId` (nvarchar, nullable), `CouponCode` (nvarchar, nullable), and a `CartDetails` table exists with columns `CartDetailsId` (identity PK), `CartHeaderId` (int, FK), `ProductId` (int), `Count` (int)

---

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

### Requirement: Upsert cart item

The system SHALL expose `POST /api/cart/CartUpsert` that accepts a `CartDto` and applies upsert semantics:

- If no `CartHeader` exists for the user: create a new `CartHeader` and a new `CartDetails` row.
- If a `CartHeader` exists but the given `ProductId` is not yet in the cart: add a new `CartDetails` row.
- If a `CartHeader` exists and the `ProductId` is already in the cart: increment the existing `Count` by the incoming `Count` value and update the row.

The endpoint accepts exactly one `CartDetails` item per call.

The response is a `ResponseDto` with `IsSuccess = true` and `Result` set to the submitted `CartDto` (reflecting the merged count on update).

#### Scenario: New cart created on first item add

- **WHEN** no cart exists for the user and `POST /api/cart/CartUpsert` is called with one detail row
- **THEN** a new `CartHeader` and one `CartDetails` row are persisted; response `Result` contains the submitted `CartDto`

#### Scenario: New product added to existing cart

- **WHEN** a cart exists for the user and the submitted `ProductId` is not yet in that cart
- **THEN** a new `CartDetails` row is added; the existing `CartHeader` is not duplicated

#### Scenario: Existing product count incremented

- **WHEN** a cart exists for the user and the submitted `ProductId` is already in that cart
- **THEN** the existing `CartDetails.Count` is incremented by the submitted `Count`; no duplicate row is created

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

### Requirement: Apply or clear coupon code

The system SHALL expose `POST /api/cart/ApplyCoupon` that accepts a `CartDto` and persists the `CouponCode` value from `cartDto.CartHeader` onto the stored `CartHeader` for that user. Passing an empty string or null effectively clears the coupon.

No coupon validation is performed at this step. Validation happens at read time inside `GetCart`.

The response is a `ResponseDto` with `IsSuccess = true` and `Result = true` on success.

#### Scenario: Coupon code set on cart

- **WHEN** `POST /api/cart/ApplyCoupon` is called with a `CartDto` where `CartHeader.CouponCode` is a non-empty string
- **THEN** the stored `CartHeader.CouponCode` is updated to that value

#### Scenario: Coupon code cleared

- **WHEN** `POST /api/cart/ApplyCoupon` is called with a `CartDto` where `CartHeader.CouponCode` is null or empty
- **THEN** the stored `CartHeader.CouponCode` is set to null/empty and subsequent `GetCart` calls apply no discount

---

### Requirement: Request cart email via message bus

The system SHALL expose `POST /api/cart/EmailCartRequest` that accepts a `CartDto` and publishes it as-is to the Azure Service Bus queue named by `TopicAndQueueNames:EmailShoppingCartQueue` (default: `emailshoppingcart`). No transformation is applied to the payload before publishing.

The response is a `ResponseDto` with `IsSuccess = true` and `Result = true` on success.

#### Scenario: Cart email message published

- **WHEN** `POST /api/cart/EmailCartRequest` is called with a valid `CartDto`
- **THEN** a message containing the full `CartDto` is published to the configured Service Bus queue

---

### Requirement: Downstream integration with ProductAPI

The system SHALL call ProductAPI using a named `HttpClient` (`"Product"`) with base address from `ServiceUrls:ProductAPI`. The outgoing request carries the caller's JWT bearer token, propagated by `BackendApiAuthenticationHttpClientHandler`.

The call is `GET /api/product`. The response is deserialized as `ResponseDto`; on `IsSuccess = true`, the `Result` array is deserialized as `IEnumerable<ProductDto>`. On failure (`IsSuccess = false`, unreachable host, or any non-success HTTP status), the system SHALL return an empty product list without throwing. `EnsureSuccessStatusCode` is NOT called.

#### Scenario: ProductAPI returns products

- **WHEN** ProductAPI responds with `IsSuccess = true` and a non-empty product list
- **THEN** `GetCart` resolves each cart item's `Product` field from that list by `ProductId`

#### Scenario: ProductAPI unavailable or returns failure

- **WHEN** ProductAPI is unreachable or responds with `IsSuccess = false`
- **THEN** `ProductService.GetProducts` returns an empty list; no exception propagates from the service layer

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

### Requirement: JWT authentication configuration

The system SHALL validate JWT bearer tokens using symmetric key signing with the secret from `ApiSettings:Secret`, issuer from `ApiSettings:Issuer`, and audience from `ApiSettings:Audience`. The secret is stored as a plain string under the `ApiSettings` section (not nested under a sub-key). This is the same flat `ApiSettings` pattern used by CouponAPI and ProductAPI.

No `[Authorize]` attributes are present on `CartAPIController` or any of its actions. Authentication infrastructure is configured but authorization is not enforced at the action level in the current codebase.

#### Scenario: Valid JWT accepted by token validation middleware

- **WHEN** a request carries a valid JWT with the configured issuer, audience, and signature
- **THEN** the request proceeds to the controller action

#### Scenario: No authorization enforcement on cart actions

- **WHEN** any cart action (`GetCart`, `CartUpsert`, `RemoveCart`, `ApplyCoupon`, `EmailCartRequest`) is called
- **THEN** no `[Authorize]` check is applied at the action or controller level; the JWT middleware validates the token if present but does not reject unauthenticated requests

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
