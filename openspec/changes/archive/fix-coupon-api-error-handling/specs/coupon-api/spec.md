## MODIFIED Requirements

### Requirement: Retrieve coupon by id
The system SHALL expose `GET /api/coupon/{id:int}` that returns the coupon matching `CouponId`, mapped to `CouponDto`. The endpoint SHALL require a valid, authenticated JWT bearer token but no specific role. The lookup SHALL use a null-safe query (no exception-driven control flow). If no coupon matches the given id, the system SHALL return HTTP 404 Not Found with a `ResponseDto` where `IsSuccess = false` and a generic not-found `Message`, without exposing any internal exception detail.

#### Scenario: Coupon found by id
- **WHEN** an authenticated caller sends `GET /api/coupon/{id}` for an existing `CouponId`
- **THEN** the response is HTTP 200 and `Result` contains the matching coupon

#### Scenario: Coupon not found by id
- **WHEN** an authenticated caller sends `GET /api/coupon/{id}` for a `CouponId` that does not exist
- **THEN** the response is HTTP 404 Not Found with `IsSuccess = false` and a generic "coupon not found" message

### Requirement: Retrieve coupon by code
The system SHALL expose `GET /api/coupon/GetByCode/{code}` that performs a case-insensitive match against `CouponCode` and returns the first matching coupon as `CouponDto`. The endpoint SHALL require a valid, authenticated JWT bearer token but no specific role. The lookup SHALL use a null-safe query (no exception-driven control flow). If no coupon matches, the system SHALL return HTTP 404 Not Found with a `ResponseDto` where `IsSuccess = false` and a generic not-found `Message`, without exposing any internal exception detail.

#### Scenario: Coupon found by code (case-insensitive)
- **WHEN** an authenticated caller sends `GET /api/coupon/GetByCode/{code}` where `{code}` matches an existing `CouponCode` in any letter casing
- **THEN** the response is HTTP 200 and `Result` contains the matching coupon

#### Scenario: Coupon not found by code
- **WHEN** an authenticated caller sends `GET /api/coupon/GetByCode/{code}` for a code that does not exist
- **THEN** the response is HTTP 404 Not Found with `IsSuccess = false` and a generic "coupon not found" message

### Requirement: Create coupon (admin only)
The system SHALL expose `POST /api/coupon` that accepts a `CouponDto`, validates it server-side, maps it to a `Coupon`, persists it, and returns the created `CouponDto`. This endpoint SHALL require a valid JWT bearer token with role `ADMIN`. If the payload fails validation, the system SHALL return HTTP 400 Bad Request with validation error details instead of persisting anything.

#### Scenario: Admin creates a coupon
- **WHEN** a caller authenticated with role `ADMIN` sends `POST /api/coupon` with a valid coupon payload
- **THEN** a new `Coupon` row is persisted and the response is HTTP 200 (or 201) with `Result` containing the created coupon

#### Scenario: Non-admin blocked from creating a coupon
- **WHEN** a caller without the `ADMIN` role sends `POST /api/coupon`
- **THEN** the request is rejected by authorization before reaching the controller action

#### Scenario: Invalid payload rejected on create
- **WHEN** a caller authenticated with role `ADMIN` sends `POST /api/coupon` with an empty `CouponCode`, a `DiscountAmount` less than or equal to 0, or a negative `MinAmount`
- **THEN** the response is HTTP 400 Bad Request describing the validation failure(s) and no `Coupon` row is persisted

### Requirement: Update coupon (admin only)
The system SHALL expose `PUT /api/coupon` that accepts a `CouponDto`, validates it server-side, maps it to a `Coupon`, updates the persisted row, and returns the updated `CouponDto`. This endpoint SHALL require a valid JWT bearer token with role `ADMIN`. If the payload fails validation, the system SHALL return HTTP 400 Bad Request with validation error details instead of updating anything.

#### Scenario: Admin updates a coupon
- **WHEN** a caller authenticated with role `ADMIN` sends `PUT /api/coupon` with an existing `CouponId` and modified fields
- **THEN** the corresponding `Coupon` row is updated and the response `Result` contains the updated coupon

#### Scenario: Non-admin blocked from updating a coupon
- **WHEN** a caller without the `ADMIN` role sends `PUT /api/coupon`
- **THEN** the request is rejected by authorization before reaching the controller action

#### Scenario: Invalid payload rejected on update
- **WHEN** a caller authenticated with role `ADMIN` sends `PUT /api/coupon` with an empty `CouponCode`, a `DiscountAmount` less than or equal to 0, or a negative `MinAmount`
- **THEN** the response is HTTP 400 Bad Request describing the validation failure(s) and no `Coupon` row is updated

### Requirement: Delete coupon (admin only)
The system SHALL expose `DELETE /api/coupon/{id:int}` that removes the coupon matching `CouponId`. This endpoint SHALL require a valid JWT bearer token with role `ADMIN`. The lookup SHALL use a null-safe query (no exception-driven control flow). If no coupon matches the given id, the system SHALL return HTTP 404 Not Found with a `ResponseDto` where `IsSuccess = false`, without exposing any internal exception detail.

#### Scenario: Admin deletes an existing coupon
- **WHEN** a caller authenticated with role `ADMIN` sends `DELETE /api/coupon/{id}` for an existing `CouponId`
- **THEN** the coupon row is removed from the database and the response is HTTP 200

#### Scenario: Delete of non-existent coupon
- **WHEN** a caller authenticated with role `ADMIN` sends `DELETE /api/coupon/{id}` for a `CouponId` that does not exist
- **THEN** the response is HTTP 404 Not Found with `IsSuccess = false`

## ADDED Requirements

### Requirement: Coupon payload validation rules
`CouponDto` payloads submitted to `POST /api/coupon` and `PUT /api/coupon` SHALL be validated server-side before any persistence occurs: `CouponCode` SHALL be required and non-empty, `DiscountAmount` SHALL be greater than 0, and `MinAmount` SHALL be greater than or equal to 0. A payload violating any of these rules SHALL be rejected with HTTP 400 Bad Request and SHALL NOT reach the database.

#### Scenario: Missing coupon code
- **WHEN** a `CouponDto` payload has an empty or missing `CouponCode`
- **THEN** the request is rejected with HTTP 400 Bad Request

#### Scenario: Non-positive discount amount
- **WHEN** a `CouponDto` payload has `DiscountAmount` equal to 0 or negative
- **THEN** the request is rejected with HTTP 400 Bad Request

#### Scenario: Negative minimum amount
- **WHEN** a `CouponDto` payload has a negative `MinAmount`
- **THEN** the request is rejected with HTTP 400 Bad Request

### Requirement: Unhandled errors do not leak exception details
When an unexpected server-side error occurs on any `/api/coupon` endpoint, the system SHALL return HTTP 500 Internal Server Error with a `ResponseDto` where `IsSuccess = false` and a generic `Message`, and SHALL NOT include the raw exception message or stack trace in the response body.

#### Scenario: Unexpected error on any endpoint
- **WHEN** an unhandled exception occurs while processing a `/api/coupon` request
- **THEN** the response is HTTP 500 Internal Server Error with a generic `Message` and no exception detail is present in the response body
