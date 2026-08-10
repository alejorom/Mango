# product-api Specification

## Purpose

The Product API microservice manages the food/product catalog (create, read, update, delete) consumed directly by end users (via `Mango.Web`) and by other services such as `Mango.Services.ShoppingCartAPI`, exposing it over a partially JWT-secured REST interface backed by SQL Server.

## Requirements

### Requirement: Product domain model and persistence
The system SHALL persist products as a `Product` entity with fields `ProductId` (int, identity primary key), `Name` (string, required), `Price` (double, with a `[Range(1, 1000)]` data annotation on the entity), `Description` (string), `CategoryName` (string), and `ImageUrl` (string). Products SHALL be stored in a single `Products` table in a SQL Server database via an EF Core `AppDbContext` with one `DbSet<Product>`. The table SHALL be seeded at migration time with 4 fixed rows (`Samosa`, `Paneer Tikka`, `Sweet Pie`, `Pav Bhaji`) via `HasData` in `OnModelCreating`.

#### Scenario: Product table schema
- **WHEN** the database is migrated
- **THEN** a `Products` table exists with columns `ProductId` (identity PK), `Name`, `Price` (float), `Description`, `CategoryName`, `ImageUrl`, and contains the 4 seeded rows

### Requirement: Retrieve all products
The system SHALL expose `GET /api/product` that returns every product row, mapped to `ProductDto`, wrapped in a `ResponseDto` returned directly (not via `ActionResult<ResponseDto>`). This endpoint SHALL NOT require authentication - there is no `[Authorize]` attribute on the action or on the controller class.

#### Scenario: List all products without authentication
- **WHEN** any caller, authenticated or not, sends `GET /api/product`
- **THEN** the response body is a `ResponseDto` (HTTP 200) whose `Result` contains all products currently in the database

### Requirement: Retrieve product by id
The system SHALL expose `GET /api/product/{id:int}` that returns the product matching `ProductId`, mapped to `ProductDto`. This endpoint SHALL NOT require authentication. The lookup SHALL use `Products.FirstAsync(u => u.ProductId == id)`. If no product matches the given id, `FirstAsync` SHALL throw an `InvalidOperationException`, which the controller SHALL catch generically, setting `IsSuccess = false` and `Message` to the raw exception message, and SHALL still return HTTP 200 OK (no distinct not-found status code).

#### Scenario: Product found by id
- **WHEN** any caller sends `GET /api/product/{id}` for an existing `ProductId`
- **THEN** the response is HTTP 200 with `IsSuccess = true` and `Result` containing the matching product

#### Scenario: Product not found by id
- **WHEN** any caller sends `GET /api/product/{id}` for a `ProductId` that does not exist
- **THEN** the response is still HTTP 200 OK, with `IsSuccess = false` and `Message` set to the raw `InvalidOperationException` message

### Requirement: Create product (admin only)
The system SHALL expose `POST /api/product` that accepts a `ProductDto`, maps it to a `Product` via AutoMapper, persists it via `Products.Add` + `SaveChangesAsync`, and returns the created `ProductDto`. This endpoint SHALL require a valid JWT bearer token with role `ADMIN`. `ProductDto` carries no data-validation attributes, so ASP.NET Core's automatic model validation SHALL NOT enforce the `[Required]` on `Name` or the `[Range(1, 1000)]` on `Price` that exist on the `Product` entity - a payload with an empty `Name` or an out-of-range `Price` SHALL be accepted and persisted without a 400 response.

#### Scenario: Admin creates a product
- **WHEN** a caller authenticated with role `ADMIN` sends `POST /api/product` with a product payload
- **THEN** a new `Product` row is persisted and the response is HTTP 200 with `Result` containing the created product

#### Scenario: Non-admin blocked from creating a product
- **WHEN** a caller without the `ADMIN` role (or without a token) sends `POST /api/product`
- **THEN** the request is rejected by authorization/authentication before reaching the controller action

#### Scenario: Out-of-range payload accepted on create
- **WHEN** a caller authenticated with role `ADMIN` sends `POST /api/product` with an empty `Name` or a `Price` outside `1-1000`
- **THEN** the product is persisted as-is; no 400 Bad Request is returned

### Requirement: Update product (admin only)
The system SHALL expose `PUT /api/product` that accepts a `ProductDto`, maps it to a new `Product` instance, and calls `Products.Update(obj)` followed by `SaveChangesAsync`, without first fetching the existing row by `ProductId`. This endpoint SHALL require a valid JWT bearer token with role `ADMIN`. If the `ProductId` in the payload does not correspond to an existing row, `SaveChangesAsync` SHALL throw (e.g. `DbUpdateConcurrencyException`), which the controller SHALL catch generically, setting `IsSuccess = false` and `Message` to the raw exception message, and SHALL still return HTTP 200 OK.

#### Scenario: Admin updates an existing product
- **WHEN** a caller authenticated with role `ADMIN` sends `PUT /api/product` with an existing `ProductId` and modified fields
- **THEN** the corresponding `Product` row is updated and the response `Result` contains the updated product

#### Scenario: Non-admin blocked from updating a product
- **WHEN** a caller without the `ADMIN` role (or without a token) sends `PUT /api/product`
- **THEN** the request is rejected by authorization/authentication before reaching the controller action

#### Scenario: Update of non-existent product fails at save time
- **WHEN** a caller authenticated with role `ADMIN` sends `PUT /api/product` with a `ProductId` that does not exist
- **THEN** `SaveChangesAsync` throws, the response is still HTTP 200 OK with `IsSuccess = false` and the raw exception message in `Message`

### Requirement: Delete product (admin only)
The system SHALL expose `DELETE /api/product/{id:int}` that removes the product matching `ProductId`. This endpoint SHALL require a valid JWT bearer token with role `ADMIN`. The lookup SHALL use `Products.FirstAsync(u => u.ProductId == id)`. If no product matches the given id, `FirstAsync` SHALL throw an `InvalidOperationException`, which the controller SHALL catch generically, setting `IsSuccess = false` and `Message` to the raw exception message, and SHALL still return HTTP 200 OK.

#### Scenario: Admin deletes an existing product
- **WHEN** a caller authenticated with role `ADMIN` sends `DELETE /api/product/{id}` for an existing `ProductId`
- **THEN** the product row is removed from the database and the response is HTTP 200 with `IsSuccess = true`

#### Scenario: Non-admin blocked from deleting a product
- **WHEN** a caller without the `ADMIN` role (or without a token) sends `DELETE /api/product/{id}`
- **THEN** the request is rejected by authorization/authentication before reaching the controller action

#### Scenario: Delete of non-existent product
- **WHEN** a caller authenticated with role `ADMIN` sends `DELETE /api/product/{id}` for a `ProductId` that does not exist
- **THEN** the response is still HTTP 200 OK, with `IsSuccess = false` and `Message` set to the raw exception message

### Requirement: Unhandled errors surface as HTTP 200 with exception detail
When an unexpected server-side error occurs on any `/api/product` endpoint, the controller's generic `try/catch` SHALL set `IsSuccess = false` and `Message` to `ex.Message` on the shared `ResponseDto`, and SHALL return it with the default HTTP 200 OK status - no `StatusCode(...)` call is made in any catch block, so the raw exception message is exposed to the caller without a corresponding non-2xx status.

#### Scenario: Unexpected error on any endpoint
- **WHEN** an unhandled exception occurs while processing a `/api/product` request
- **THEN** the response is HTTP 200 OK with `IsSuccess = false` and the raw exception message present in `Message`

### Requirement: JWT-based authentication and authorization
The system SHALL validate JWT bearer tokens for `POST`, `PUT`, and `DELETE` actions on `ProductAPIController` using `builder.AddAppAuthetication()`, which reads `ApiSettings:Secret` (signing key), `ApiSettings:Issuer`, and `ApiSettings:Audience` directly from flat configuration keys - the same pattern used by `Mango.Services.CouponAPI`, and distinct from `Mango.Services.AuthAPI`, which reads the nested `ApiSettings:JwtOptions` section. The system SHALL NOT use ASP.NET Core Identity (`UserManager`/`IdentityDbContext`) within this service; tokens are issued by `AuthAPI` and only validated here. `POST`, `PUT`, and `DELETE` SHALL additionally require a `role` claim equal to `ADMIN` via `[Authorize(Roles = "ADMIN")]` on each action. Unlike `CouponAPIController`, `ProductAPIController` SHALL NOT carry a class-level `[Authorize]` attribute, and its `GET` actions SHALL carry no `[Authorize]` attribute at all, so both `GET /api/product` and `GET /api/product/{id}` SHALL be reachable without any bearer token. `Program.cs` also calls the parameterless `builder.Services.AddAuthentication()` after `builder.AddAppAuthetication()` has already configured the JWT Bearer scheme; this second call is redundant (not present in `CouponAPI` or `AuthAPI`) and is documented here as an implementation detail only, since it has not been confirmed to change observable behavior.

#### Scenario: Write request without a token is rejected
- **WHEN** a caller sends `POST`, `PUT`, or `DELETE` to `/api/product` without a bearer token
- **THEN** the request is rejected with an authentication failure before reaching the controller action

#### Scenario: Valid token without ADMIN role can read but not write
- **WHEN** a caller presents a valid JWT without the `ADMIN` role claim
- **THEN** `GET` endpoints succeed and `POST`/`PUT`/`DELETE` endpoints are rejected by authorization

#### Scenario: Read endpoints reachable without any token
- **WHEN** a caller sends `GET /api/product` or `GET /api/product/{id}` with no `Authorization` header at all
- **THEN** the request succeeds and returns product data

### Requirement: Synchronous consumption by ShoppingCartAPI
`Mango.Services.ShoppingCartAPI` SHALL consume this API synchronously over HTTP - via a named `HttpClient` ("Product") pointed at `ServiceUrls:ProductAPI`/`ProductAPI` (configuration key `"ProductAPI"`) - calling `GET /api/product` to resolve the `ProductDto` for each cart line item. The system SHALL NOT expose or consume any message-bus-based (Azure Service Bus / `Mango.MessageBus`) integration for product data; `Mango.Services.ProductAPI.csproj` SHALL carry no NuGet package reference or `ProjectReference` to a message bus library.

#### Scenario: Cart resolves product data via HTTP
- **WHEN** `Mango.Services.ShoppingCartAPI` builds a cart response
- **THEN** it calls `GET /api/product` on this service over HTTP and matches returned products to cart items by `ProductId`

#### Scenario: No message bus dependency
- **WHEN** `Mango.Services.ProductAPI.csproj` is inspected
- **THEN** it contains no reference to `Mango.MessageBus` or `Azure.Messaging.ServiceBus`
