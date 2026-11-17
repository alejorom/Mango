# Spec Delta

## MODIFIED Requirements

### Requirement: Product domain model and persistence
The system SHALL persist products as a `Product` entity with fields `ProductId` (int, identity primary key), `Name` (string, required), `Price` (double, with an inclusive 1-1000 range annotation that validates the original double value without integer conversion or rounding), `Description` (string), `CategoryName` (string), and `ImageUrl` (string). Products SHALL be stored in a single `Products` table in a SQL Server database via an EF Core `AppDbContext` with one `DbSet<Product>`. The table SHALL be seeded at migration time with 4 fixed rows (`Samosa`, `Paneer Tikka`, `Sweet Pie`, `Pav Bhaji`) via `HasData` in `OnModelCreating`.

#### Scenario: Product table schema
- **WHEN** the database is migrated
- **THEN** a `Products` table exists with columns `ProductId` (identity PK), `Name`, `Price` (float), `Description`, `CategoryName`, `ImageUrl`, and contains the 4 seeded rows

#### Scenario: Entity price validation rejects fractional values outside the interval
- **WHEN** a product entity is validated with a price of 0.99 or 1000.01
- **THEN** price validation fails without rounding the value to an integer

#### Scenario: Entity price validation includes both bounds
- **WHEN** a product entity with otherwise valid fields is validated with a price of 1 or 1000
- **THEN** price validation succeeds and the original price is unchanged

### Requirement: Product payload validation rules
`ProductDto` payloads submitted to `POST /api/product` and `PUT /api/product` SHALL be validated server-side before any persistence occurs: `Name` SHALL be required and non-empty, and `Price` SHALL be within the range `1-1000` inclusive. Price validation SHALL compare the original double value without rounding, truncating, clamping or converting it to an integer, consistently with the product entity's range validation. A payload violating either rule SHALL be rejected with HTTP 400 Bad Request describing the validation failure(s) and SHALL NOT reach the database.

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
