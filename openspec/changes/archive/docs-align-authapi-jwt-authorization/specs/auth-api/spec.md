## RENAMED Requirements

- FROM: `### Requirement: No self-authentication or endpoint-level authorization`
- TO: `### Requirement: JWT authentication and endpoint authorization`

## MODIFIED Requirements

### Requirement: External dependencies
The system SHALL depend on: ASP.NET Core Identity (`Microsoft.AspNetCore.Identity.EntityFrameworkCore`), EF Core with SQL Server (`Microsoft.EntityFrameworkCore.SqlServer`), JWT Bearer authentication (`Microsoft.AspNetCore.Authentication.JwtBearer`, used for token handling and request validation middleware), AutoMapper, Swashbuckle for OpenAPI/Swagger, and a project reference to `Mango.MessageBus` for publishing registration events. Its SQL Server database is configured via `ConnectionStrings:DefaultConnection` pointing at a local database named `Mango_Auth`.

#### Scenario: Pending migrations applied at startup
- **WHEN** the application starts
- **THEN** any pending EF Core migrations against `AppDbContext` are applied automatically before the app begins serving requests
