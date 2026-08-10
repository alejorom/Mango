## MODIFIED Requirements

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

### Requirement: Role assignment
The system SHALL expose `POST /api/auth/AssignRole` accepting a `RegistrationRequestDto`, using only its `Email` and `Role` fields. This endpoint SHALL require a valid JWT bearer token with role `ADMIN`. If `Role` is null or empty, it SHALL return `ResponseDto` with `IsSuccess = false`, `Message` = `"Error encountered"`, and HTTP 400, without calling the service layer. Otherwise, it SHALL look up the user by `Email` (case-insensitive). If found, it SHALL create the role via `RoleManager.CreateAsync` if it does not already exist (using the role name upper-cased), then add the user to that role via `UserManager.AddToRoleAsync`, returning `ResponseDto` with `IsSuccess = true` and HTTP 200. If the user is not found, it SHALL return `ResponseDto` with `IsSuccess = false`, `Message` = `"Error encountered"`, and HTTP 400.

#### Scenario: Successful role assignment to existing user
- **WHEN** a caller authenticated with role `ADMIN` sends `POST /api/auth/AssignRole` with an existing user's email and a non-empty role name
- **THEN** the role is created if missing, the user is added to that role, and the response is HTTP 200 with `IsSuccess = true`

#### Scenario: Role assignment for unknown email
- **WHEN** a caller authenticated with role `ADMIN` sends `POST /api/auth/AssignRole` with an email that does not match any user
- **THEN** no role is created or assigned, and the response is HTTP 400 with `IsSuccess = false` and `Message` = `"Error encountered"`

#### Scenario: Role assignment with missing Role
- **WHEN** a caller authenticated with role `ADMIN` sends `POST /api/auth/AssignRole` with `Role` null or empty
- **THEN** the service layer is not invoked, and the response is HTTP 400 with `IsSuccess = false` and `Message` = `"Error encountered"`

#### Scenario: Non-admin or unauthenticated caller blocked
- **WHEN** a caller without a valid JWT bearer token, or with a token lacking the `ADMIN` role, sends `POST /api/auth/AssignRole`
- **THEN** the request is rejected by authentication/authorization before reaching the controller action

### Requirement: No self-authentication or endpoint-level authorization
The system SHALL authenticate `POST /api/auth/AssignRole` using JWT bearer tokens validated against `ApiSettings:JwtOptions:Secret` (signing key), `ApiSettings:JwtOptions:Issuer`, and `ApiSettings:JwtOptions:Audience` from configuration, and SHALL require a `role` claim equal to `ADMIN` for that endpoint. `POST /api/auth/register` and `POST /api/auth/login` SHALL remain reachable without any bearer token, since they are the entry points for unauthenticated users to obtain credentials and a token.

#### Scenario: AssignRole requires an ADMIN token
- **WHEN** a caller sends `POST /api/auth/AssignRole` without any `Authorization` header
- **THEN** the request is rejected with an authentication failure before reaching the controller action

#### Scenario: Register and Login remain open
- **WHEN** a caller sends `POST /api/auth/register` or `POST /api/auth/login` without any `Authorization` header
- **THEN** the request reaches the controller action and is processed normally

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
