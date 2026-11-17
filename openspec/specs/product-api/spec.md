# product-api Specification

## Purpose

The Product API microservice manages the food/product catalog (create, read, update, delete) consumed directly by end users (via `Mango.Web`) and by other services such as `Mango.Services.ShoppingCartAPI`, exposing it over a partially JWT-secured REST interface backed by SQL Server.

## Requirements

### Requirement: Product domain model and persistence
The system SHALL persist products as a `Product` entity with fields `ProductId` (int, identity primary key), `Name` (string, required), `Price` (double, with an inclusive 1-1000 range annotation that validates the original double value without integer conversion or rounding), `Description` (string), `CategoryName` (string), and `ImageUrl` (string).

#### Scenario: Product storage and migration contract
- **WHEN** product storage and migration seeding are configured
- **THEN** Products SHALL be stored in a single `Products` table in a SQL Server database via an EF Core `AppDbContext` with one `DbSet<Product>`. The table SHALL be seeded at migration time with 4 fixed rows (`Samosa`, `Paneer Tikka`, `Sweet Pie`, `Pav Bhaji`) via `HasData` in `OnModelCreating`.

#### Scenario: Product table schema
- **WHEN** the database is migrated
- **THEN** a `Products` table exists with columns `ProductId` (identity PK), `Name`, `Price` (float), `Description`, `CategoryName`, `ImageUrl`, and contains the 4 seeded rows

#### Scenario: Entity price validation rejects fractional values outside the interval
- **WHEN** a product entity is validated with a price of 0.99 or 1000.01
- **THEN** price validation fails without rounding the value to an integer

#### Scenario: Entity price validation includes both bounds
- **WHEN** a product entity with otherwise valid fields is validated with a price of 1 or 1000
- **THEN** price validation succeeds and the original price is unchanged

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

### Requirement: Product payload validation rules
`ProductDto` payloads submitted to `POST /api/product` and `PUT /api/product` SHALL be validated server-side before any persistence occurs: `Name` SHALL be required and non-empty, and `Price` SHALL be within the range `1-1000` inclusive.

#### Scenario: Original price and invalid payload handling
- **WHEN** ProductAPI validates a submitted product payload
- **THEN** Price validation SHALL compare the original double value without rounding, truncating, clamping or converting it to an integer, consistently with the product entity's range validation. A payload violating either rule SHALL be rejected with HTTP 400 Bad Request describing the validation failure(s) and SHALL NOT reach the database.

#### Scenario: Missing product name
- **WHEN** a `ProductDto` payload has an empty or missing `Name`
- **THEN** the request is rejected with HTTP 400 Bad Request and no `Product` row is persisted or updated

#### Scenario: Price out of range
- **WHEN** a `ProductDto` payload has a `Price` less than 1 or greater than 1000
- **THEN** the request is rejected with HTTP 400 Bad Request and no `Product` row is persisted or updated

#### Scenario: Fractional price below the minimum is rejected on create and update
- **WHEN** an ADMIN caller submits an otherwise valid POST or PUT payload with Price equal to 0.99, using an existing product id for PUT
- **THEN** the response is HTTP 400 with a price validation error, no save operation is attempted, and persisted products remain unchanged

#### Scenario: Fractional price above the maximum is rejected on create and update
- **WHEN** an ADMIN caller submits an otherwise valid POST or PUT payload with Price equal to 1000.01, using an existing product id for PUT
- **THEN** the response is HTTP 400 with a price validation error, no save operation is attempted, and persisted products remain unchanged

#### Scenario: Inclusive bounds are accepted on create and update
- **WHEN** an ADMIN caller submits an otherwise valid POST or PUT payload with Price equal to 1 or 1000, using an existing product id for PUT
- **THEN** the request succeeds with HTTP 200, the requested product is created or updated, and its returned and persisted price equals the submitted value

#### Scenario: Valid fractional prices are preserved
- **WHEN** an ADMIN caller submits an otherwise valid POST or PUT payload with Price equal to 1.01 or 999.99, using an existing product id for PUT
- **THEN** the request succeeds with HTTP 200 and its returned and persisted price equals the submitted double value without rounding to an integer

#### Scenario: Adjacent representable values outside the bounds are rejected
- **WHEN** an ADMIN caller submits an otherwise valid POST or PUT payload whose double price is the immediately preceding representable value below 1 or the immediately succeeding representable value above 1000, using an existing product id for PUT
- **THEN** the response is HTTP 400 with a price validation error, no save operation is attempted, and persisted products remain unchanged

### Requirement: Create product (admin only)
The system SHALL expose `POST /api/product` that accepts a `ProductDto`, validates it server-side, maps it to a `Product` via AutoMapper, persists it via `Products.Add` + `SaveChangesAsync`, and returns the created `ProductDto` via `ActionResult<ResponseDto>`.

#### Scenario: Create response and validation contract
- **WHEN** a caller submits a product creation request
- **THEN** This endpoint SHALL require a valid JWT bearer token with role `ADMIN`. If the payload fails validation (see "Product payload validation rules"), the system SHALL return HTTP 400 Bad Request with validation error details instead of persisting anything.

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
The system SHALL expose `PUT /api/product` that accepts a `ProductDto`, validates it server-side, and looks up the existing `Product` by `ProductId` via `FirstOrDefaultAsync` before making any change.

#### Scenario: Update authorization validation and persistence contract
- **WHEN** a caller submits a product update request
- **THEN** This endpoint SHALL require a valid JWT bearer token with role `ADMIN`. If the payload fails validation, the system SHALL return HTTP 400 Bad Request instead of updating anything. If no `Product` matches the given `ProductId`, the system SHALL return HTTP 404 Not Found with `IsSuccess = false`, without attempting the update. If the product exists and the payload is valid, the system SHALL update its fields from the `ProductDto`, persist via `SaveChangesAsync`, and return the updated `ProductDto` via `ActionResult<ResponseDto>`.

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
The system SHALL validate JWT bearer tokens for `POST`, `PUT`, and `DELETE` actions on `ProductAPIController` using `builder.AddAppAuthetication()`, which reads `ApiSettings:Secret` (signing key), `ApiSettings:Issuer`, and `ApiSettings:Audience` directly from flat configuration keys - the same pattern used by `Mango.Services.CouponAPI`, and distinct from `Mango.Services.AuthAPI`, which reads the nested `ApiSettings:JwtOptions` section.

#### Scenario: Token roles public access and startup contract
- **WHEN** ProductAPI authentication and endpoint access are configured
- **THEN** The system SHALL NOT use ASP.NET Core Identity (`UserManager`/`IdentityDbContext`) within this service; tokens are issued by `AuthAPI` and only validated here. `POST`, `PUT`, and `DELETE` SHALL additionally require a `role` claim equal to `ADMIN` via `[Authorize(Roles = "ADMIN")]` on each action. Unlike `CouponAPIController`, `ProductAPIController` SHALL NOT carry a class-level `[Authorize]` attribute, and its `GET` actions SHALL carry no `[Authorize]` attribute at all, so both `GET /api/product` and `GET /api/product/{id}` SHALL be reachable without any bearer token. This is an intentional design decision - the product catalog is meant to be publicly browsable, like a storefront - and SHALL NOT be changed to require authentication as part of error-handling or validation fixes. `Program.cs` also calls the parameterless `builder.Services.AddAuthentication()` after `builder.AddAppAuthetication()` has already configured the JWT Bearer scheme; this second call is redundant (not present in `CouponAPI` or `AuthAPI`) and remains documented here as an implementation detail only, out of scope for this change.

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

### Requirement: Synchronous consumption by ShoppingCartAPI
`Mango.Services.ShoppingCartAPI` SHALL consume this API synchronously over HTTP - via a named `HttpClient` ("Product") pointed at `ServiceUrls:ProductAPI`/`ProductAPI` (configuration key `"ProductAPI"`) - calling `GET /api/product` to resolve the `ProductDto` for each cart line item.

#### Scenario: Product messaging dependency contract
- **WHEN** product data integrations and project dependencies are configured
- **THEN** The system SHALL NOT expose or consume any message-bus-based (Azure Service Bus / `Mango.MessageBus`) integration for product data; `Mango.Services.ProductAPI.csproj` SHALL carry no NuGet package reference or `ProjectReference` to a message bus library.

#### Scenario: Cart resolves product data via HTTP
- **WHEN** `Mango.Services.ShoppingCartAPI` builds a cart response
- **THEN** it calls `GET /api/product` on this service over HTTP and matches returned products to cart items by `ProductId`

#### Scenario: No message bus dependency
- **WHEN** `Mango.Services.ProductAPI.csproj` is inspected
- **THEN** it contains no reference to `Mango.MessageBus` or `Azure.Messaging.ServiceBus`
