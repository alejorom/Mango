## MODIFIED Requirements

### Requirement: Retrieve cart with computed totals

The system SHALL expose `GET /api/cart/GetCart/{userId}` that returns the authenticated user's logical cart as a `CartDto` with all totals computed. Authentication, identifier validation and owner authorization SHALL precede the read. A successful lookup for the authenticated subject that finds no persisted header SHALL return the successful logical empty representation defined below; it SHALL NOT create data or convert a storage or dependency failure into an empty cart.

For an existing owned header, total computation SHALL remain:
1. Fetch all `CartDetails` rows for the user's `CartHeader`.
2. Fetch all products from ProductAPI (`GET /api/product`). For each detail row, resolve the matching product by `ProductId`. Items whose `ProductId` is not present in the ProductAPI response SHALL be skipped - they SHALL NOT contribute to `CartTotal` and their `Product` field SHALL remain null.
3. `CartTotal` = sum of (`item.Count * item.Product.Price`) for all **resolved** detail rows only.
4. If any detail rows could not be resolved (product not found in ProductAPI response), the response SHALL set `IsSuccess = false` with a descriptive message identifying the unresolvable items. `Result` SHALL still contain the partially composed `CartDto` with the resolved items' totals.
5. If `CouponCode` is not null/empty and all products resolved: fetch the coupon from CouponAPI (`GET /api/coupon/GetByCode/{code}`). If the coupon is not `null` AND `CartTotal > coupon.MinAmount`, subtract `coupon.DiscountAmount` from `CartTotal` and set `CartHeader.Discount = coupon.DiscountAmount`. If the coupon is `null` because CouponAPI returned HTTP 404, no discount SHALL be applied. Other propagated dependency failures SHALL remain generic HTTP 500 errors.

The response SHALL be wrapped in `ResponseDto` with `IsSuccess = true` and `Result` set to the composed `CartDto`, unless unresolved products are detected (step 4) or a failure occurs. A real header without details SHALL retain its existing composition and dependency-error behavior; it SHALL NOT be replaced by the virtual-header absence path.

#### Scenario: Cart retrieved with product prices and no coupon

- **WHEN** an authenticated caller sends `GET /api/cart/GetCart/{userId}` for their own user with existing cart items and no `CouponCode`
- **THEN** the response `Result` contains a `CartDto` where each `CartDetailsDto.Product` is populated from ProductAPI and `CartHeader.CartTotal` equals the sum of all `(Count * Product.Price)` values

#### Scenario: Coupon discount applied when total exceeds minimum

- **WHEN** an owned cart has a `CouponCode` set, CouponAPI returns a non-null coupon, and `CartTotal > coupon.MinAmount`
- **THEN** `CartHeader.CartTotal` is reduced by `coupon.DiscountAmount` and `CartHeader.Discount` equals `coupon.DiscountAmount`

#### Scenario: Coupon not applied when total does not exceed minimum

- **WHEN** an owned cart has a `CouponCode` set but `CartTotal <= coupon.MinAmount`
- **THEN** `CartHeader.CartTotal` and `Discount` remain unchanged (no discount applied)

#### Scenario: Coupon lookup fails or returns no match

- **WHEN** CouponAPI returns HTTP 404 for the owned cart's coupon code and the coupon is `null`
- **THEN** no discount is applied; `CartTotal` and `Discount` reflect the raw product total
- **WHEN** coupon lookup instead propagates a transport, non-404 HTTP or invalid-response failure
- **THEN** GetCart returns generic HTTP 500 without substituting a successful logical empty cart

#### Scenario: One or more products not resolvable from ProductAPI

- **WHEN** one or more `CartDetails` rows of an owned cart have a `ProductId` that is not present in the ProductAPI response
- **THEN** those items are excluded from `CartTotal`, and the HTTP 200 response sets `IsSuccess = false` with a message identifying the unresolved product ids; resolved items' totals are still returned in `Result`, and the unsuccessful response is not a logical empty success

#### Scenario: Own cart has no persisted header

- **WHEN** an authenticated caller requests their own identity and the owner-scoped storage lookup succeeds without finding a CartHeader
- **THEN** HTTP 200 is returned with `IsSuccess = true` and a non-null `Result` containing a CartDto with non-null CartHeader, `CartHeaderId = 0`, `UserId` equal to the validated JWT subject, `CartTotal = 0`, `Discount = 0` and a non-null empty CartDetails collection
- **AND** the virtual header has no persisted identifier, coupon or invented contact data; no records are written or created, no Product/Coupon composition calls are made and no message is published

#### Scenario: Repeated own empty-cart reads

- **WHEN** an authenticated caller repeatedly requests their own cart while no header exists
- **THEN** every successful read returns the same logical empty shape with HTTP 200, without creating a header or details; initial persistence remains the responsibility of an authorized item upsert

#### Scenario: Real owned header has no details

- **WHEN** an authenticated owner requests a persisted header with no details and valid downstream responses
- **THEN** the existing HTTP 200 composition behavior is retained with the real header identifier and empty details, rather than returning a virtual header
- **WHEN** composition for that real empty header encounters a propagated dependency failure
- **THEN** GetCart returns generic HTTP 500 and does not turn the failure into a successful logical empty response

#### Scenario: Own cart read fails in storage

- **WHEN** the authenticated subject's header lookup fails because of a storage error rather than succeeding with no rows
- **THEN** GetCart returns HTTP 500 with `IsSuccess = false` and the generic error message, without returning a successful virtual cart or exposing exception details

### Requirement: Error handling and response envelope

All controller actions SHALL return `ActionResult<ResponseDto>`. Successful responses SHALL use the implicit 200 OK with the `ResponseDto` result, including the successful own-header absence defined by `Retrieve cart with computed totals`. On an unhandled exception, any action SHALL return `StatusCode(500, _response)` with `IsSuccess = false` and `Message` set to `"An unexpected error occurred"`. On a not-found condition requiring a persisted resource (e.g., RemoveCart with unknown cartDetailsId or ApplyCoupon without a header), the action SHALL return HTTP 404 with `IsSuccess = false` and a descriptive message. Foreign identities and references SHALL retain the indistinguishable rejections required by the ownership contract. Own-header absence in GetCart SHALL be a successful logical state, not a not-found condition.

No DTO validation attributes (`[Required]`, `[Range]`, etc.) are present on any request model.

#### Scenario: Successful action

- **WHEN** a cart action completes without error
- **THEN** the response is HTTP 200 with `ResponseDto.IsSuccess = true`

#### Scenario: Unhandled exception in cart action

- **WHEN** an unhandled exception occurs inside a cart action
- **THEN** the response is HTTP 500 with `ResponseDto.IsSuccess = false` and `ResponseDto.Message = "An unexpected error occurred"`; no exception details or stack trace are exposed to the caller

#### Scenario: Not-found condition in cart action

- **WHEN** an action requires a persisted resource and receives a nonexistent required reference, such as an unknown cartDetailsId or ApplyCoupon for an absent owned header
- **THEN** the response is HTTP 404 Not Found with `ResponseDto.IsSuccess = false` and a descriptive message; the explicit successful own GetCart absence contract is not applied to these operations

### Requirement: JWT authentication configuration

The system SHALL validate JWT bearer tokens using symmetric key signing with the secret from `ApiSettings:Secret`, issuer from `ApiSettings:Issuer`, and audience from `ApiSettings:Audience`. The secret is stored as a plain string under the `ApiSettings` section (not nested under a sub-key). This is the same flat `ApiSettings` pattern used by CouponAPI and ProductAPI.

All five cart actions SHALL require a valid JWT and a usable authenticated user identity derived from the validated token subject. Missing or unusable identity SHALL be rejected as authentication failure, not as a resource ownership failure.

#### Scenario: Valid JWT accepted by token validation middleware

- **WHEN** a request carries a valid JWT with the configured issuer, audience, and signature and a usable subject
- **THEN** authentication succeeds and the request proceeds to resource authorization

#### Scenario: No authorization enforcement on cart actions

- **WHEN** any cart action (`GetCart`, `CartUpsert`, `RemoveCart`, `ApplyCoupon`, `EmailCartRequest`) is called without a valid JWT or with a missing or unusable subject
- **THEN** the request is rejected with HTTP 401 before reaching the action or performing a mutation or publication

### Requirement: Request cart email via message bus

The system SHALL expose `POST /api/cart/EmailCartRequest` accepting a `CartDto`. It SHALL verify the authenticated user's ownership and publish a reconstructed cart message to the Azure Service Bus queue named by `TopicAndQueueNames:EmailShoppingCartQueue` (default: `emailshoppingcart`). Message content SHALL come from trusted data and its recipient from an approved authorized source. The client-provided content and recipient SHALL NOT be trusted. Until a recipient source is approved and available, the system SHALL fail closed without publication.

The response is a `ResponseDto` with `IsSuccess = true` and `Result = true` on success.

#### Scenario: Cart email message published

- **WHEN** an authenticated owner requests email for their cart and a recipient from the approved authorized source is available
- **THEN** a full `CartDto` reconstructed from trusted data and the authorized recipient is published to the configured Service Bus queue

#### Scenario: Email content or recipient manipulated

- **WHEN** a request supplies manipulated cart content or recipient
- **THEN** the system does not publish those client-supplied values and uses only trusted reconstructed content and an authorized recipient, or rejects without publishing

#### Scenario: Recipient source unavailable or unapproved

- **WHEN** no approved authorized recipient source is available
- **THEN** the request fails closed and no message is published

### Requirement: Downstream integration with ProductAPI

The system SHALL call ProductAPI using the named `HttpClient` (`"Product"`) with base address from `ServiceUrls:ProductAPI`, propagating the caller's JWT bearer token through `BackendApiAuthenticationHttpClientHandler`.

The call is `GET /api/product`. A successful HTTP response is deserialized as `ResponseDto`; when `IsSuccess = true`, `Result` is deserialized as `IEnumerable<ProductDto>`, including a valid empty list. A successful HTTP response with `IsSuccess = false`, a non-success HTTP status, or a communication error propagates an exception; an empty response or JSON deserialization error also fails. GetCart handles propagated exceptions with its existing generic HTTP 500 response. EmailCartRequest fails closed without publication when products cannot be resolved.

#### Scenario: ProductAPI returns products

- **WHEN** ProductAPI responds with `IsSuccess = true` and a non-empty product list
- **THEN** `GetCart` resolves each cart item's `Product` field from that list by `ProductId`

#### Scenario: ProductAPI unavailable or returns failure

- **WHEN** ProductAPI is unreachable or returns a non-success HTTP status
- **THEN** the client propagates an exception and GetCart returns HTTP 500 without exposing exception details
- **WHEN** ProductAPI returns a successful HTTP response with `IsSuccess = false`
- **THEN** the client propagates an exception and GetCart returns HTTP 500 without exposing exception details

#### Scenario: ProductAPI returns a valid empty list

- **WHEN** ProductAPI returns a successful response with `IsSuccess = true` and an empty product list
- **THEN** the client returns an empty list without treating it as a dependency failure

### Requirement: Downstream integration with CouponAPI

The system SHALL call CouponAPI using the named `HttpClient` (`"Coupon"`) with base address from `ServiceUrls:CouponAPI`, propagating the caller's JWT bearer token through `BackendApiAuthenticationHttpClientHandler`.

The call is `GET /api/coupon/GetByCode/{code}`. A valid successful HTTP response is deserialized as `ResponseDto` and its `Result` as `CouponDto`. HTTP 404 returns `null`. Other non-success HTTP statuses, communication failures and invalid successful responses propagate an exception. GetCart handles propagated exceptions with its existing generic HTTP 500 response. EmailCartRequest fails closed without publication if the coupon cannot be resolved.

#### Scenario: CouponAPI returns a valid coupon

- **WHEN** CouponAPI responds with `IsSuccess = true` and coupon data
- **THEN** the coupon's `DiscountAmount` and `MinAmount` are used in `GetCart` discount calculation

#### Scenario: CouponAPI unavailable or code not found

- **WHEN** CouponAPI responds with HTTP 404
- **THEN** `CouponService.GetCoupon` returns `null` and GetCart applies no discount
- **WHEN** CouponAPI is unreachable, returns another non-success HTTP status, or returns an invalid successful response
- **THEN** the client propagates an exception and GetCart returns HTTP 500 without exposing exception details

## ADDED Requirements

### Requirement: Cart ownership and client identifiers

The system SHALL authorize all cart actions against the authenticated subject. Foreign carts or details SHALL return HTTP 404 without disclosing their existence. ADMIN SHALL have no ownership bypass. New carts SHALL be owned by the authenticated subject; client identifiers SHALL never establish authorization. Malformed identifiers SHALL return HTTP 400 based on syntax alone.

#### Scenario: Owner reads own cart

- **WHEN** the authenticated owner requests `GET /api/cart/GetCart/{userId}` with their own identity
- **THEN** their cart is returned under the cart composition contract, including the successful logical empty representation when their own header is absent

#### Scenario: Foreign cart read regardless of existence

- **WHEN** an authenticated user requests GetCart with a well-formed identity different from the validated JWT subject, whether or not that identity has a persisted cart
- **THEN** HTTP 404 is returned with indistinguishable status, body and observable response details, without querying the foreign resource's existence, modifying data or publishing a message; ADMIN receives the same rejection

#### Scenario: Foreign or nonexistent required cart for coupon update

- **WHEN** an authenticated user requests ApplyCoupon for a foreign or nonexistent required cart
- **THEN** HTTP 404 is returned with indistinguishable responses without modifying data or publishing a message; own-header absence is not converted to coupon-update success

#### Scenario: MVC and bearer identities disagree on cart read

- **WHEN** the well-formed identity requested by Mango.Web differs from the validated bearer subject, including a mismatch between its MVC session and bearer identity
- **THEN** GetCart returns the foreign-identity HTTP 404 rejection without querying that identity's cart existence; Mango.Web does not substitute an empty view

#### Scenario: Owner applies coupon or upserts

- **WHEN** an authenticated owner supplies consistent identifiers to `ApplyCoupon` or `CartUpsert`
- **THEN** the operation follows the existing coupon or single-item upsert contract and new cart ownership is assigned from the authenticated subject

#### Scenario: Malformed client identity

- **WHEN** a supplied UserId is missing or malformed
- **THEN** the request returns HTTP 400 without substituting the identifier, changing data, or publishing a message

#### Scenario: Well-formed mismatched user identity

- **WHEN** `CartUpsert` receives a well-formed UserId different from the authenticated subject
- **THEN** HTTP 404 is returned without creating or changing data, with the same response whether that other user's cart exists or not

#### Scenario: Foreign or inconsistent nested identifiers

- **WHEN** a supplied header, detail, or nested resource identifier references another user's existing resource or conflicts with the resource being operated on
- **THEN** malformed identifiers are rejected with HTTP 400 based on syntax alone; well-formed references to foreign or nonexistent required resources are rejected with indistinguishable HTTP 404 responses, without mutation or publication

#### Scenario: Administrator accesses a foreign cart

- **WHEN** an authenticated user with role `ADMIN` requests a cart or detail owned by another user
- **THEN** the same ownership rule applies and HTTP 404 is returned without mutation or publication

#### Scenario: Owner removes an item

- **WHEN** an authenticated owner calls `RemoveCart` with a detail belonging to their persisted cart
- **THEN** the existing removal and last-item header cleanup behavior applies

#### Scenario: Owner reads cart after successful last-item removal

- **WHEN** an authenticated owner successfully removes their last detail and its header is deleted, then requests their own cart again
- **THEN** the subsequent GetCart returns the successful HTTP 200 logical empty representation without recreating the header; the removal's own success and authorization behavior is unchanged

#### Scenario: Foreign or nonexistent detail

- **WHEN** `RemoveCart` receives a detail belonging to another user or a nonexistent detail
- **THEN** HTTP 404 is returned without deleting any row and without distinguishing the two cases

#### Scenario: Foreign cart email

- **WHEN** an authenticated user requests `EmailCartRequest` for another user's cart
- **THEN** HTTP 404 is returned without publishing a message or disclosing the cart's existence

### Requirement: Mango.Web logical empty-cart consumers

Mango.Web SHALL render a successful own logical empty-cart result in CartIndex with HTTP 200. Checkout GET, Checkout POST and EmailCart SHALL reject the virtual `CartHeaderId = 0` result with HTTP 404 before order creation or email dispatch. Existing real-header behavior and HTTP error handling SHALL be preserved; errors SHALL NOT be reinterpreted as empty success or classified by message text.

#### Scenario: CartIndex displays own logical empty cart

- **WHEN** Mango.Web requests GetCart for its authenticated user and receives the successful logical empty representation
- **THEN** CartIndex renders its existing empty-cart view with a non-null model and final browser HTTP 200

#### Scenario: Last-item removal redirect reaches the empty view

- **WHEN** the owner's last-item removal succeeds and Mango.Web redirects to CartIndex with the same valid session
- **THEN** the subsequent successful own empty-cart read produces final browser HTTP 200 with the empty view, without recreating a persisted header

#### Scenario: Checkout GET rejects a virtual cart

- **WHEN** Checkout GET loads the own logical empty result with CartHeaderId 0
- **THEN** it returns HTTP 404 before rendering checkout, calling order creation or requesting email; no order or message is created

#### Scenario: Checkout POST rejects a virtual cart

- **WHEN** Checkout POST reloads the own cart and receives the logical empty result with CartHeaderId 0
- **THEN** it returns HTTP 404 before calling OrderService, creating an order or requesting payment; no message is published

#### Scenario: EmailCart rejects a virtual cart

- **WHEN** EmailCart reloads the own cart and receives the logical empty result with CartHeaderId 0
- **THEN** it returns HTTP 404 before requesting cart email; no order is created or message published

#### Scenario: Real-cart presentation and operations are preserved

- **WHEN** the authenticated owner has a real persisted header and valid cart data
- **THEN** CartIndex, Checkout and EmailCart retain their existing authorized behavior; a real header without details is not reclassified as a virtual header

#### Scenario: Route or configuration not found is not an empty cart

- **WHEN** the GetCart HTTP request returns 404 because of a wrong route or configuration, with an empty, ProblemDetails or generic error body
- **THEN** Mango.Web preserves the 404 rejection instead of rendering a successful empty-cart view, without using message text to identify the response

#### Scenario: Authentication and upstream failures remain failures

- **WHEN** Mango.Web receives HTTP 400, 401, 403 or 500 from the cart request, encounters its existing 502 transport-failure mapping, or receives an unsuccessful product-composition response
- **THEN** the existing rejection behavior is preserved and no successful empty-cart view, order creation or cart-email request occurs; dependent-service failures are not treated as header absence

#### Scenario: Rejected removal is not successful emptying

- **WHEN** RemoveCart rejects a foreign or nonexistent detail or reports another failure
- **THEN** Mango.Web preserves its error response rather than treating the operation as successful emptying and redirecting to a successful empty view
