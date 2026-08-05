# coupon-api Specification

## Purpose

The Coupon API microservice manages discount coupon records (create, read, update, delete) used by other Mango services, and exposes them over a JWT-secured REST interface backed by SQL Server.

## Requirements

### Requirement: Coupon domain model and persistence
The system SHALL persist coupons as a `Coupon` entity with fields `CouponId` (int, identity primary key), `CouponCode` (string, required), `DiscountAmount` (double, required), and `MinAmount` (int, no default validation). Coupons SHALL be stored in a single `Coupons` table in a SQL Server database via an EF Core `DbContext` with one `DbSet<Coupon>`.

#### Scenario: Coupon table schema
- **WHEN** the database is migrated
- **THEN** a `Coupons` table exists with columns `CouponId` (identity PK), `CouponCode` (nvarchar(max), not null), `DiscountAmount` (float), `MinAmount` (int)

### Requirement: Retrieve all coupons
The system SHALL expose `GET /api/coupon` that returns every coupon row, mapped to `CouponDto`, wrapped in a `ResponseDto` with `IsSuccess = true` and `Result` set to the coupon list. The endpoint SHALL require a valid, authenticated JWT bearer token but no specific role.

#### Scenario: List all coupons
- **WHEN** an authenticated caller sends `GET /api/coupon`
- **THEN** the response body is a `ResponseDto` whose `Result` contains all coupons currently in the database

### Requirement: Retrieve coupon by id
The system SHALL expose `GET /api/coupon/{id:int}` that returns the coupon matching `CouponId`, mapped to `CouponDto`. The endpoint SHALL require a valid, authenticated JWT bearer token but no specific role. If no coupon matches the given id, the lookup SHALL throw, and the exception SHALL be caught and returned as `ResponseDto` with `IsSuccess = false` and `Message` set to the exception's message, using HTTP 200.

#### Scenario: Coupon found by id
- **WHEN** an authenticated caller sends `GET /api/coupon/{id}` for an existing `CouponId`
- **THEN** the response `Result` contains the matching coupon

#### Scenario: Coupon not found by id
- **WHEN** an authenticated caller sends `GET /api/coupon/{id}` for a `CouponId` that does not exist
- **THEN** the response is returned with HTTP 200, `IsSuccess = false`, and `Message` containing the underlying exception text

### Requirement: Retrieve coupon by code
The system SHALL expose `GET /api/coupon/GetByCode/{code}` that performs a case-insensitive match against `CouponCode` and returns the first matching coupon as `CouponDto`. The endpoint SHALL require a valid, authenticated JWT bearer token but no specific role. If no coupon matches, behavior SHALL follow the same not-found error handling as retrieve-by-id.

#### Scenario: Coupon found by code (case-insensitive)
- **WHEN** an authenticated caller sends `GET /api/coupon/GetByCode/{code}` where `{code}` matches an existing `CouponCode` in any letter casing
- **THEN** the response `Result` contains the matching coupon

#### Scenario: Coupon not found by code
- **WHEN** an authenticated caller sends `GET /api/coupon/GetByCode/{code}` for a code that does not exist
- **THEN** the response is returned with HTTP 200, `IsSuccess = false`, and `Message` containing the underlying exception text

### Requirement: Create coupon (admin only)
The system SHALL expose `POST /api/coupon` that accepts a `CouponDto`, maps it to a `Coupon`, persists it, and returns the created `CouponDto`. This endpoint SHALL require a valid JWT bearer token with role `ADMIN`.

#### Scenario: Admin creates a coupon
- **WHEN** a caller authenticated with role `ADMIN` sends `POST /api/coupon` with a valid coupon payload
- **THEN** a new `Coupon` row is persisted and the response `Result` contains the created coupon

#### Scenario: Non-admin blocked from creating a coupon
- **WHEN** a caller without the `ADMIN` role sends `POST /api/coupon`
- **THEN** the request is rejected by authorization before reaching the controller action

### Requirement: Update coupon (admin only)
The system SHALL expose `PUT /api/coupon` that accepts a `CouponDto`, maps it to a `Coupon`, updates the persisted row, and returns the updated `CouponDto`. This endpoint SHALL require a valid JWT bearer token with role `ADMIN`.

#### Scenario: Admin updates a coupon
- **WHEN** a caller authenticated with role `ADMIN` sends `PUT /api/coupon` with an existing `CouponId` and modified fields
- **THEN** the corresponding `Coupon` row is updated and the response `Result` contains the updated coupon

#### Scenario: Non-admin blocked from updating a coupon
- **WHEN** a caller without the `ADMIN` role sends `PUT /api/coupon`
- **THEN** the request is rejected by authorization before reaching the controller action

### Requirement: Delete coupon (admin only)
The system SHALL expose `DELETE /api/coupon/{id:int}` that removes the coupon matching `CouponId`. This endpoint SHALL require a valid JWT bearer token with role `ADMIN`. If no coupon matches the given id, the lookup SHALL throw, and the exception SHALL be caught and returned as `ResponseDto` with `IsSuccess = false`, using HTTP 200.

#### Scenario: Admin deletes an existing coupon
- **WHEN** a caller authenticated with role `ADMIN` sends `DELETE /api/coupon/{id}` for an existing `CouponId`
- **THEN** the coupon row is removed from the database

#### Scenario: Delete of non-existent coupon
- **WHEN** a caller authenticated with role `ADMIN` sends `DELETE /api/coupon/{id}` for a `CouponId` that does not exist
- **THEN** the response is returned with HTTP 200 and `IsSuccess = false`

### Requirement: JWT-based authentication and authorization
The system SHALL authenticate every request to `CouponAPIController` using JWT bearer tokens validated against `ApiSettings:Secret` (signing key), `ApiSettings:Issuer`, and `ApiSettings:Audience` from configuration. The system SHALL NOT use ASP.NET Core Identity (`UserManager`/`IdentityDbContext`) within this service; tokens are issued by a separate service and only validated here. Write operations (POST, PUT, DELETE) SHALL additionally require a `role` claim equal to `ADMIN`.

#### Scenario: Request without a token is rejected
- **WHEN** a caller sends any `/api/coupon` request without a bearer token
- **THEN** the request is rejected with an authentication failure before reaching the controller action

#### Scenario: Valid token without ADMIN role can read but not write
- **WHEN** a caller presents a valid JWT without the `ADMIN` role claim
- **THEN** GET endpoints succeed and POST/PUT/DELETE endpoints are rejected by authorization
