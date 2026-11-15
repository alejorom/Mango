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
