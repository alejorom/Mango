## MODIFIED Requirements

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
- **THEN** their cart is returned under the existing cart composition contract

#### Scenario: Foreign or nonexistent cart read or coupon update

- **WHEN** an authenticated user requests a foreign or nonexistent cart through `GetCart` or `ApplyCoupon`
- **THEN** HTTP 404 is returned with indistinguishable responses without modifying data

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

#### Scenario: Foreign or nonexistent detail

- **WHEN** `RemoveCart` receives a detail belonging to another user or a nonexistent detail
- **THEN** HTTP 404 is returned without deleting any row and without distinguishing the two cases

#### Scenario: Foreign cart email

- **WHEN** an authenticated user requests `EmailCartRequest` for another user's cart
- **THEN** HTTP 404 is returned without publishing a message or disclosing the cart's existence
