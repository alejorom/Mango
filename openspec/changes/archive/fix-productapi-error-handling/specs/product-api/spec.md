## ADDED Requirements

### Requirement: Product payload validation rules
`ProductDto` payloads submitted to `POST /api/product` and `PUT /api/product` SHALL be validated server-side before any persistence occurs: `Name` SHALL be required and non-empty, and `Price` SHALL be within the range `1-1000` inclusive (mirroring the existing, previously-unenforced `[Required]`/`[Range(1, 1000)]` annotations on the `Product` entity). A payload violating either rule SHALL be rejected with HTTP 400 Bad Request describing the validation failure(s) and SHALL NOT reach the database.

#### Scenario: Missing product name
- **WHEN** a `ProductDto` payload has an empty or missing `Name`
- **THEN** the request is rejected with HTTP 400 Bad Request and no `Product` row is persisted or updated

#### Scenario: Price out of range
- **WHEN** a `ProductDto` payload has a `Price` less than 1 or greater than 1000
- **THEN** the request is rejected with HTTP 400 Bad Request and no `Product` row is persisted or updated

## MODIFIED Requirements

### Requirement: Retrieve all products
The system SHALL expose `GET /api/product` that returns every product row, mapped to `ProductDto`, wrapped in a `ResponseDto` returned via `ActionResult<ResponseDto>`. This endpoint SHALL NOT require authentication - there is no `[Authorize]` attribute on the action or on the controller class.

#### Scenario: List all products without authentication
- **WHEN** any caller, authenticated or not, sends `GET /api/product`
- **THEN** the response body is a `ResponseDto` (HTTP 200) whose `Result` contains all products currently in the database

### Requirement: Retrieve product by id
The system SHALL expose `GET /api/product/{id:int}` that returns the product matching `ProductId`, mapped to `ProductDto`, via `ActionResult<ResponseDto>`. This endpoint SHALL NOT require authentication. The lookup SHALL use `Products.FirstOrDefaultAsync(u => u.ProductId == id)`. If no product matches the given id, the system SHALL return HTTP 404 Not Found with a `ResponseDto` where `IsSuccess = false` and a generic not-found `Message`, without relying on an unhandled exception.

#### Scenario: Product found by id
- **WHEN** any caller sends `GET /api/product/{id}` for an existing `ProductId`
- **THEN** the response is HTTP 200 with `IsSuccess = true` and `Result` containing the matching product

#### Scenario: Product not found by id
- **WHEN** any caller sends `GET /api/product/{id}` for a `ProductId` that does not exist
- **THEN** the response is HTTP 404 Not Found with `IsSuccess = false` and a generic "product not found" message

### Requirement: Create product (admin only)
The system SHALL expose `POST /api/product` that accepts a `ProductDto`, validates it server-side, maps it to a `Product` via AutoMapper, persists it via `Products.Add` + `SaveChangesAsync`, and returns the created `ProductDto` via `ActionResult<ResponseDto>`. This endpoint SHALL require a valid JWT bearer token with role `ADMIN`. If the payload fails validation (see "Product payload validation rules"), the system SHALL return HTTP 400 Bad Request with validation error details instead of persisting anything.

#### Scenario: Admin creates a product
- **WHEN** a caller authenticated with role `ADMIN` sends `POST /api/product` with a valid product payload
- **THEN** a new `Product` row is persisted and the response is HTTP 200 with `Result` containing the created product

#### Scenario: Non-admin blocked from creating a product
- **WHEN** a caller without the `ADMIN` role (or without a token) sends `POST /api/product`
- **THEN** the request is rejected by authorization/authentication before reaching the controller action

#### Scenario: Invalid payload rejected on create
- **WHEN** a caller authenticated with role `ADMIN` sends `POST /api/product` with an empty `Name` or a `Price` outside `1-1000`
- **THEN** the response is HTTP 400 Bad Request describing the validation failure(s) and no `Product` row is persisted

#### Scenario: Out-of-range payload accepted on create
- **WHEN** a caller authenticated with role `ADMIN` sends `POST /api/product` with an empty `Name` or an out-of-range `Price`
- **THEN** this previously-observed behavior (persisting the payload as-is with no validation) no longer occurs; see "Invalid payload rejected on create" above for the corrected behavior

### Requirement: Update product (admin only)
The system SHALL expose `PUT /api/product` that accepts a `ProductDto`, validates it server-side, and looks up the existing `Product` by `ProductId` via `FirstOrDefaultAsync` before making any change. This endpoint SHALL require a valid JWT bearer token with role `ADMIN`. If the payload fails validation, the system SHALL return HTTP 400 Bad Request instead of updating anything. If no `Product` matches the given `ProductId`, the system SHALL return HTTP 404 Not Found with `IsSuccess = false`, without attempting the update. If the product exists and the payload is valid, the system SHALL update its fields from the `ProductDto`, persist via `SaveChangesAsync`, and return the updated `ProductDto` via `ActionResult<ResponseDto>`.

#### Scenario: Admin updates an existing product
- **WHEN** a caller authenticated with role `ADMIN` sends `PUT /api/product` with an existing `ProductId` and modified fields
- **THEN** the corresponding `Product` row is updated and the response `Result` contains the updated product

#### Scenario: Non-admin blocked from updating a product
- **WHEN** a caller without the `ADMIN` role (or without a token) sends `PUT /api/product`
- **THEN** the request is rejected by authorization/authentication before reaching the controller action

#### Scenario: Invalid payload rejected on update
- **WHEN** a caller authenticated with role `ADMIN` sends `PUT /api/product` with an empty `Name` or a `Price` outside `1-1000`
- **THEN** the response is HTTP 400 Bad Request describing the validation failure(s) and no `Product` row is updated

#### Scenario: Update of non-existent product returns 404
- **WHEN** a caller authenticated with role `ADMIN` sends `PUT /api/product` with a `ProductId` that does not exist
- **THEN** the response is HTTP 404 Not Found with `IsSuccess = false`, and no update is attempted

#### Scenario: Update of non-existent product fails at save time
- **WHEN** a caller authenticated with role `ADMIN` sends `PUT /api/product` with a `ProductId` that does not exist
- **THEN** this previously-observed behavior (an unhandled `DbUpdateConcurrencyException` surfacing as HTTP 200 with the raw exception message) no longer occurs; see "Update of non-existent product returns 404" above for the corrected behavior

### Requirement: Delete product (admin only)
The system SHALL expose `DELETE /api/product/{id:int}` that removes the product matching `ProductId`, via `ActionResult<ResponseDto>`. This endpoint SHALL require a valid JWT bearer token with role `ADMIN`. The lookup SHALL use `Products.FirstOrDefaultAsync(u => u.ProductId == id)`. If no product matches the given id, the system SHALL return HTTP 404 Not Found with a `ResponseDto` where `IsSuccess = false`, without relying on an unhandled exception.

#### Scenario: Admin deletes an existing product
- **WHEN** a caller authenticated with role `ADMIN` sends `DELETE /api/product/{id}` for an existing `ProductId`
- **THEN** the product row is removed from the database and the response is HTTP 200 with `IsSuccess = true`

#### Scenario: Non-admin blocked from deleting a product
- **WHEN** a caller without the `ADMIN` role (or without a token) sends `DELETE /api/product/{id}`
- **THEN** the request is rejected by authorization/authentication before reaching the controller action

#### Scenario: Delete of non-existent product
- **WHEN** a caller authenticated with role `ADMIN` sends `DELETE /api/product/{id}` for a `ProductId` that does not exist
- **THEN** the response is HTTP 404 Not Found with `IsSuccess = false`

### Requirement: Unhandled errors surface as HTTP 200 with exception detail
When an unexpected server-side error occurs on any `/api/product` endpoint, the controller's generic `try/catch` SHALL set `IsSuccess = false` and a fixed, generic `Message` (not the raw exception message) on the shared `ResponseDto`, and SHALL return it via `StatusCode(500, _response)`. The raw exception message or stack trace SHALL NOT be included in the response body.

#### Scenario: Unexpected error on any endpoint
- **WHEN** an unhandled exception occurs while processing a `/api/product` request
- **THEN** the response is HTTP 500 Internal Server Error with a generic `Message` and no exception detail is present in the response body

### Requirement: JWT-based authentication and authorization
The system SHALL validate JWT bearer tokens for `POST`, `PUT`, and `DELETE` actions on `ProductAPIController` using `builder.AddAppAuthetication()`, which reads `ApiSettings:Secret` (signing key), `ApiSettings:Issuer`, and `ApiSettings:Audience` directly from flat configuration keys - the same pattern used by `Mango.Services.CouponAPI`, and distinct from `Mango.Services.AuthAPI`, which reads the nested `ApiSettings:JwtOptions` section. The system SHALL NOT use ASP.NET Core Identity (`UserManager`/`IdentityDbContext`) within this service; tokens are issued by `AuthAPI` and only validated here. `POST`, `PUT`, and `DELETE` SHALL additionally require a `role` claim equal to `ADMIN` via `[Authorize(Roles = "ADMIN")]` on each action. Unlike `CouponAPIController`, `ProductAPIController` SHALL NOT carry a class-level `[Authorize]` attribute, and its `GET` actions SHALL carry no `[Authorize]` attribute at all, so both `GET /api/product` and `GET /api/product/{id}` SHALL be reachable without any bearer token. This is an intentional design decision - the product catalog is meant to be publicly browsable, like a storefront - and SHALL NOT be changed to require authentication as part of error-handling or validation fixes. `Program.cs` also calls the parameterless `builder.Services.AddAuthentication()` after `builder.AddAppAuthetication()` has already configured the JWT Bearer scheme; this second call is redundant (not present in `CouponAPI` or `AuthAPI`) and remains documented here as an implementation detail only, out of scope for this change.

#### Scenario: Write request without a token is rejected
- **WHEN** a caller sends `POST`, `PUT`, or `DELETE` to `/api/product` without a bearer token
- **THEN** the request is rejected with an authentication failure before reaching the controller action

#### Scenario: Valid token without ADMIN role can read but not write
- **WHEN** a caller presents a valid JWT without the `ADMIN` role claim
- **THEN** `GET` endpoints succeed and `POST`/`PUT`/`DELETE` endpoints are rejected by authorization

#### Scenario: Read endpoints reachable without any token
- **WHEN** a caller sends `GET /api/product` or `GET /api/product/{id}` with no `Authorization` header at all
- **THEN** the request succeeds and returns product data

#### Scenario: Public read access is deliberate, not a gap
- **WHEN** the authentication requirements of `ProductAPIController` are reviewed
- **THEN** the absence of `[Authorize]` on the controller class and on both `GET` actions is confirmed as an intentional design decision for a public product catalog, not an omission to be fixed
