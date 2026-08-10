# auth-api Specification

## Purpose

The Auth API microservice manages user identity (registration, login, role assignment) and issues the JWT bearer tokens that `Mango.Services.CouponAPI` and other Mango services validate for authentication and authorization.

## Requirements

### Requirement: User and role persistence via ASP.NET Core Identity
The system SHALL persist users as an `ApplicationUser` entity extending ASP.NET Core Identity's `IdentityUser`, adding a `Name` field. Users and roles SHALL be stored via ASP.NET Core Identity (`UserManager<ApplicationUser>`, `RoleManager<IdentityRole>`) backed by an `AppDbContext` that extends `IdentityDbContext<ApplicationUser>`, persisted in a SQL Server database using the standard ASP.NET Identity schema (`AspNetUsers`, `AspNetRoles`, etc.).

#### Scenario: ApplicationUser schema
- **WHEN** the database is migrated
- **THEN** the `AspNetUsers` table includes all standard Identity columns plus a `Name` column, and `AspNetRoles`/`AspNetUserRoles` tables exist for role storage

### Requirement: User registration
The system SHALL expose `POST /api/auth/register` accepting a `RegistrationRequestDto` (`Email`, `Name`, `PhoneNumber`, `Password`, optional `Role`). It SHALL create the user via `UserManager.CreateAsync`, using `Email` as both `UserName` and `Email`. On success, it SHALL publish the registered user's email to a message queue named by configuration key `TopicAndQueueNames:RegisterUserQueue` and return `ResponseDto` with `IsSuccess = true` and HTTP 200. On failure (e.g., Identity validation error), it SHALL return `ResponseDto` with `IsSuccess = false`, `Message` set to the first Identity error description, and HTTP 400. The optional `Role` field on the request is accepted but not used during registration (role assignment is a separate endpoint). If `UserManager.CreateAsync` succeeds but a subsequent step throws an unexpected exception, the system SHALL log the exception server-side (without exposing its detail to the caller) and return `ResponseDto` with `IsSuccess = false`, `Message` set to the literal string `"Error Encountered"`, and HTTP 400.

#### Scenario: Successful registration
- **WHEN** a caller sends `POST /api/auth/register` with a unique email and a password meeting ASP.NET Core Identity's default password rules
- **THEN** a new `ApplicationUser` row is persisted, a message with the user's email is published to the `RegisterUserQueue`-configured destination, and the response is HTTP 200 with `IsSuccess = true`

#### Scenario: Registration fails Identity validation
- **WHEN** a caller sends `POST /api/auth/register` with a password or email that fails ASP.NET Core Identity's user/password validators (e.g., duplicate email/username, password too short)
- **THEN** no user is created, no message is published, and the response is HTTP 400 with `IsSuccess = false` and `Message` set to the first validation error description

#### Scenario: Registration throws an unexpected exception
- **WHEN** `UserManager.CreateAsync` or the subsequent lookup throws an exception for a reason other than a validation failure
- **THEN** the exception is caught and logged server-side, no message is published, and the response is HTTP 400 with `IsSuccess = false` and `Message` set to the literal string `"Error Encountered"` (the original exception detail is not surfaced to the caller)

### Requirement: Email/username uniqueness
The system SHALL rely on ASP.NET Core Identity's default user store behavior to reject registrations with a `UserName`/`Email` that already exists (case-insensitively, via the normalized email/username columns). No additional custom uniqueness check is performed outside of Identity's built-in validation.

#### Scenario: Duplicate email rejected
- **WHEN** a caller registers with an email that already belongs to an existing user (in any letter casing)
- **THEN** `UserManager.CreateAsync` fails validation and registration returns HTTP 400 with `IsSuccess = false`

### Requirement: Password validation
The system SHALL validate passwords using ASP.NET Core Identity's default `PasswordOptions` (no custom password policy is configured in `Program.cs`): minimum length 6, requires at least one uppercase letter, one lowercase letter, one digit, and one non-alphanumeric character, with a maximum of 3 unique repeated characters not enforced by default.

#### Scenario: Password below default Identity requirements rejected
- **WHEN** a caller registers with a password that does not satisfy ASP.NET Core Identity's default `PasswordOptions`
- **THEN** `UserManager.CreateAsync` fails validation and registration returns HTTP 400 with `IsSuccess = false` and an Identity-generated error message

### Requirement: User login and JWT issuance
The system SHALL expose `POST /api/auth/login` accepting a `LoginRequestDto` (`UserName`, `Password`). It SHALL look up the user by `UserName` (case-insensitive comparison against the stored `UserName`). If no user matches, it SHALL immediately return `ResponseDto` with `IsSuccess = false`, `Message` = `"Username or password is incorrect"`, and HTTP 400, without calling password verification. If a user matches, it SHALL verify the password via `UserManager.CheckPasswordAsync`. On success, it SHALL generate a JWT via the token generator and return `ResponseDto` with `IsSuccess = true`, HTTP 200, and `Result` set to a `LoginResponseDto` containing a `UserDto` (`ID`, `Email`, `Name`, `PhoneNumber`) and the signed `Token`. On a matched user with a wrong password, it SHALL return `ResponseDto` with `IsSuccess = false`, `Message` = `"Username or password is incorrect"`, and HTTP 400.

#### Scenario: Successful login
- **WHEN** a caller sends `POST /api/auth/login` with a `UserName` that exists and the correct password
- **THEN** the response is HTTP 200 with `IsSuccess = true` and `Result` containing the user's `UserDto` and a signed JWT

#### Scenario: Login with unknown username
- **WHEN** a caller sends `POST /api/auth/login` with a `UserName` that does not match any user
- **THEN** the system does not call password verification, and the response is HTTP 400 with `IsSuccess = false` and `Message` = `"Username or password is incorrect"`

#### Scenario: Login with wrong password
- **WHEN** a caller sends `POST /api/auth/login` with a `UserName` that exists but an incorrect password
- **THEN** the response is HTTP 400 with `IsSuccess = false` and `Message` = `"Username or password is incorrect"`

### Requirement: JWT claims, signing, and expiration
The system SHALL issue JWTs signed with `HmacSha256Signature` using the symmetric key from configuration `ApiSettings:JwtOptions:Secret`, with `Issuer` and `Audience` set from `ApiSettings:JwtOptions:Issuer` and `ApiSettings:JwtOptions:Audience`. The token SHALL include claims: `sub` (user id), `email`, `name` (the user's `UserName`), and one `role` claim (`ClaimTypes.Role`) per role currently assigned to the user via `UserManager.GetRolesAsync`. Tokens SHALL expire 7 days after issuance (`DateTime.UtcNow.AddDays(7)`). No refresh token is issued or supported.

#### Scenario: Token contains expected claims
- **WHEN** a user with one or more assigned roles logs in successfully
- **THEN** the issued JWT contains `sub`, `email`, and `name` claims matching the user's data, and one `role` claim per assigned role

#### Scenario: Token expiration
- **WHEN** a JWT issued by this service is decoded
- **THEN** its `exp` claim corresponds to 7 days after the issuance time, with no shorter-lived access token or refresh token provided

### Requirement: Role assignment
The system SHALL expose `POST /api/auth/AssignRole` accepting a `RegistrationRequestDto`, using only its `Email` and `Role` fields. This endpoint SHALL require a valid JWT bearer token with role `ADMIN`. A request without a JWT bearer token, or with an invalid/expired token, SHALL be rejected with HTTP 401 Unauthorized. A request with a valid JWT bearer token that lacks the `ADMIN` role SHALL be rejected with HTTP 403 Forbidden, via the `JwtBearerEvents.OnForbidden` handler configured in `WebApplicationBuilderExtensions.AddAppAuthetication()`. If the caller has a valid ADMIN token and `Role` is null or empty, it SHALL return `ResponseDto` with `IsSuccess = false`, `Message` = `"Error encountered"`, and HTTP 400, without calling the service layer. Otherwise, it SHALL look up the user by `Email` (case-insensitive). If found, it SHALL create the role via `RoleManager.CreateAsync` if it does not already exist (using the role name upper-cased), then add the user to that role via `UserManager.AddToRoleAsync`, returning `ResponseDto` with `IsSuccess = true` and HTTP 200. If the user is not found, it SHALL return `ResponseDto` with `IsSuccess = false`, `Message` = `"Error encountered"`, and HTTP 400.

#### Scenario: Successful role assignment to existing user
- **WHEN** a caller authenticated with role `ADMIN` sends `POST /api/auth/AssignRole` with an existing user's email and a non-empty role name
- **THEN** the role is created if missing, the user is added to that role, and the response is HTTP 200 with `IsSuccess = true`

#### Scenario: Role assignment for unknown email
- **WHEN** a caller authenticated with role `ADMIN` sends `POST /api/auth/AssignRole` with an email that does not match any user
- **THEN** no role is created or assigned, and the response is HTTP 400 with `IsSuccess = false` and `Message` = `"Error encountered"`

#### Scenario: Role assignment with missing Role
- **WHEN** a caller authenticated with role `ADMIN` sends `POST /api/auth/AssignRole` with `Role` null or empty
- **THEN** the service layer is not invoked, and the response is HTTP 400 with `IsSuccess = false` and `Message` = `"Error encountered"`

#### Scenario: Unauthenticated caller blocked with 401
- **WHEN** a caller without a JWT bearer token, or with an invalid/expired token, sends `POST /api/auth/AssignRole`
- **THEN** the request is rejected before reaching the controller action with HTTP 401 Unauthorized

#### Scenario: Non-admin caller blocked with 403
- **WHEN** a caller with a valid JWT bearer token that lacks the `ADMIN` role sends `POST /api/auth/AssignRole`
- **THEN** the request is rejected before reaching the controller action with HTTP 403 Forbidden

### Requirement: No self-authentication or endpoint-level authorization
The system SHALL authenticate `POST /api/auth/AssignRole` using JWT bearer tokens validated against `ApiSettings:JwtOptions:Secret` (signing key), `ApiSettings:JwtOptions:Issuer`, and `ApiSettings:JwtOptions:Audience` from configuration, and SHALL require a `role` claim equal to `ADMIN` for that endpoint. `POST /api/auth/register` and `POST /api/auth/login` SHALL remain reachable without any bearer token, since they are the entry points for unauthenticated users to obtain credentials and a token.

#### Scenario: AssignRole requires an ADMIN token
- **WHEN** a caller sends `POST /api/auth/AssignRole` without any `Authorization` header
- **THEN** the request is rejected with an authentication failure before reaching the controller action

#### Scenario: Register and Login remain open
- **WHEN** a caller sends `POST /api/auth/register` or `POST /api/auth/login` without any `Authorization` header
- **THEN** the request reaches the controller action and is processed normally

### Requirement: Message bus publish on registration
The system SHALL publish the registered user's email to a message queue on successful registration, using `IMessageBus.PublishMessage`, where the destination name comes from configuration key `TopicAndQueueNames:RegisterUserQueue` (configured as `"registeruser"`). The underlying `IMessageBus` implementation (`Mango.MessageBus.Service.MessageBus`) requires a connection string at `ConnectionStrings:ServiceBusConnection`, which it reads via `IConfiguration.GetConnectionString("ServiceBusConnection")` and throws `ArgumentNullException` if absent.

#### Scenario: Registration fails at publish step due to missing Service Bus configuration
- **WHEN** a caller successfully registers (Identity validation passes and `UserManager.CreateAsync` succeeds) but `ConnectionStrings:ServiceBusConnection` is not configured for the running instance
- **THEN** `MessageBus.PublishMessage` throws `ArgumentNullException` when constructing the `ServiceBusClient`, and this exception is not caught by `AuthAPIController.Register` (it propagates as an unhandled exception, distinct from the try/catch inside `AuthService.Register`)

### Requirement: External dependencies
The system SHALL depend on: ASP.NET Core Identity (`Microsoft.AspNetCore.Identity.EntityFrameworkCore`), EF Core with SQL Server (`Microsoft.EntityFrameworkCore.SqlServer`), JWT Bearer token creation (`Microsoft.AspNetCore.Authentication.JwtBearer`, used only for its token-handling types, not for request validation middleware), AutoMapper, Swashbuckle for OpenAPI/Swagger, and a project reference to `Mango.MessageBus` for publishing registration events. Its SQL Server database is configured via `ConnectionStrings:DefaultConnection` pointing at a local database named `Mango_Auth`.

#### Scenario: Pending migrations applied at startup
- **WHEN** the application starts
- **THEN** any pending EF Core migrations against `AppDbContext` are applied automatically before the app begins serving requests
