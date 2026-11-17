# Pruebas aisladas de AuthAPI

Incremento autorizado de `establish-api-test-foundations`: tareas 1.1, 1.2 y 2.1–2.5. Proyecto `net11.0` con referencia exclusiva a AuthAPI, registrado bajo Tests en `Mango.sln` para Debug/Release. Usa Microsoft.NET.Test.Sdk 17.14.1, xUnit 2.9.3, runner 3.1.4 y EF Core InMemory 11.0.0. No agrega un framework de mocking ni una biblioteca compartida.

## Ejecución

Desde la raíz del repositorio, con SDK compatible con .NET 11 y paquetes NuGet disponibles:

```powershell
dotnet --version
dotnet restore Mango.Services.AuthAPI.Tests/Mango.Services.AuthAPI.Tests.csproj
dotnet test Mango.Services.AuthAPI.Tests/Mango.Services.AuthAPI.Tests.csproj --no-restore --configuration Release --filter Category=Unit --logger 'trx;LogFileName=unit.trx' --results-directory Mango.Services.AuthAPI.Tests/TestResults
dotnet test Mango.Services.AuthAPI.Tests/Mango.Services.AuthAPI.Tests.csproj --no-restore --configuration Release --filter Category=Integration --logger 'trx;LogFileName=integration.trx' --results-directory Mango.Services.AuthAPI.Tests/TestResults
dotnet test Mango.Services.AuthAPI.Tests/Mango.Services.AuthAPI.Tests.csproj --no-restore --configuration Release --logger 'trx;LogFileName=all.trx' --results-directory Mango.Services.AuthAPI.Tests/TestResults
```

`Unit` cubre generador JWT y controlador con dobles. `Integration` cubre Identity/EF reales sobre InMemory y HTTP sobre loopback. Ningún caso requiere infraestructura; ejecutar el proyecto sin filtro es seguro. Los TRX y binarios quedan ignorados por Git. No ejecutar la solución completa ni pruebas del hardening de ShoppingCart para verificar este incremento.

## Composición y aislamiento

- `TestSupport/IdentityTestServices.cs` registra Identity, sus managers/stores y el hasher real. Un wrapper cuenta las verificaciones de password sin cambiar su resultado. Cada fixture tiene un nombre/root InMemory propio. La protección de datos usa un proveedor efímero, sin persistir claves.
- `RecordingBus` implementa IMessageBus y captura destino/payload; nunca construye MessageBus o ServiceBusClient. Las integraciones de registro invocan el controlador y AuthService reales, de modo que la comprobación de publicación está vinculada al resultado de Identity.
- `AuthHttpHost` compone controladores reales con AddControllers y la extensión productiva AddAppAuthetication. Conserva serialización MVC predeterminada, routing, validación automática, middleware JWT y autorización. Enlaza únicamente `127.0.0.1:0`, desactiva proxy/redirecciones del cliente y acota arranque, parada y peticiones a 30 segundos. Todos los hosts, clientes y scopes se liberan al terminar cada caso.
- La configuración es exclusivamente de fixtures: `ApiSettings:JwtOptions:Secret`, `Issuer`, `Audience` y `TopicAndQueueNames:RegisterUserQueue`. Se limpian las fuentes del builder antes de registrar servicios; no se consumen appsettings, variables de secretos ni credenciales reales. La firma de JWT usa una clave sintética local. No se altera la política de validación de tokens ni el clock skew productivo; el token expirado de prueba venció una hora antes.
- Registro y login se prueban sin Authorization. AssignRole usa JWT locales con trust settings controlados y también un token emitido por el login real tras asignar ADMIN. Los rechazos comparan snapshots de usuarios, roles y membresías, además del conteo del bus, para detectar efectos indebidos.

## Trazabilidad de la familia A

Fuentes: [spec auth-api](../openspec/specs/auth-api/spec.md) y [diseño D1–D7](../openspec/changes/establish-api-test-foundations/design.md). Los nombres siguientes identifican métodos; las teorías ejecutan cada combinación de datos.

| Requisito / escenario seleccionado | Pruebas | Evidencia y límite |
|---|---|---|
| JWT claims, signing, expiration; Token contains expected claims / Token expiration | `JwtTokenGeneratorTests.GeneratedTokenHasValidSignatureExpectedClaimsAndSevenDayLifetime`, `GeneratedTokenCannotBeValidatedWithDifferentTrustSettings` | Firma HS256, issuer/audience, sub/email/name, cero o múltiples roles y ventana de siete días; rechazo con trust settings distintos |
| User registration; Successful registration; Message bus publish on registration | `AuthControllerTests.RegisterPublishesOnlyAfterSuccessfulRegistrationCompletes`, `IdentityFlowTests.SuccessfulRegistrationPersistsIdentityAndPublishesEmailWithoutAssigningRequestedRole` | Espera del resultado antes de publicar; usuario persistido y password hasheado, email/destino capturados; Role de registro no asignado |
| Email/username uniqueness; Duplicate email rejected | `IdentityFlowTests.DuplicateRegistrationIsRejectedByIdentityWithoutAdditionalUserOrMessage` | Mismo casing y casing distinto, 400, una sola fila/publicación |
| Password validation; Registration fails Identity validation | `IdentityFlowTests.IdentityPasswordRulesRejectInvalidPasswordWithoutPersistenceOrPublication`, `AuthControllerTests.RegistrationFailureReturns400WithoutPublishing` | Longitud, mayúscula, minúscula, dígito y carácter especial; sin filas/publicaciones en fracaso |
| User login and JWT issuance; Successful login / Unknown username / Wrong password | `IdentityFlowTests.LoginUsesRealIdentityAndOnlyValidCredentialsReceiveSignedUserRoles`, `AuthControllerTests.LoginSuccessReturnsServiceUserAndTokenWithoutPublishing`, `LoginFailureReturnsGeneric400WithoutPublishing` | Login válido, búsqueda sin distinguir casing, usuario ausente sin verificar password, password incorrecto y token con rol almacenado |
| User registration; Registration throws an unexpected exception | `IdentityFlowTests.UnexpectedRegistrationFailureIsLoggedAndReturnedAsGeneric400WithoutPublication`, `AuthHttpTests.PersistenceFailureReturnsGeneric400OverHttpAndLogsInternalException` | Interceptor de guardado lanza una excepción sintética; logging del detalle y HTTP 400 genérico, sin persistencia/publicación |
| Role assignment; Successful assignment, create/reuse role, unknown email, missing Role | `IdentityFlowTests.RoleAssignmentCreatesThenReusesRoleAndLooksUpEmailWithoutCaseSensitivity`, `AuthControllerTests.MissingRoleReturns400WithoutCallingService`, `RoleAssignmentNormalizesRoleAndReflectsServiceResult`, `AuthHttpTests.AdminAssignmentWithMissingRoleOrUnknownUserReturns400WithoutEffects` | Normalización a mayúsculas, creación/reuso del rol, 400 y ausencia de efectos en rechazos; el test unitario verifica que falta de Role no llama al servicio |
| JWT authentication and endpoint authorization; Register and Login remain open | `AuthHttpTests.RegisterAndLoginRemainPublicAndReturnPersistedUserWithSignedJwt` | HTTP 200 sin token en registro/login, usuario real, envelope y JWT serializados |
| AssignRole requires ADMIN; Unauthenticated caller blocked with 401 | `AuthHttpTests.AssignRoleRejectsUntrustedJwtWith401AndNoIdentityOrBusEffects` | Sin token, JWT malformado, vencido, firma/issuer/audience incorrectos; HTTP 401 con challenge Bearer y estado intacto |
| AssignRole requires ADMIN; Non-admin caller blocked with 403; ADMIN accepted | `AuthHttpTests.AssignRoleRejectsAuthenticatedNonAdminWith403AndNoEffects`, `AdminCanAssignRolesAndAnActualLoginTokenCanAuthorizeTheNextAssignment` | Sin rol o CUSTOMER: 403 sin efectos. ADMIN: 200 y membresía CUSTOMER persistida, usando también JWT del login real |
| Binding MVC actual; ApiController y contratos JSON | `AuthHttpTests.MalformedJsonReturns400BeforeIdentityOrBusEffects` | JSON inválido en los tres endpoints: 400 problem+json sin efectos; no se agregan validaciones de DTO inexistentes |
| Límite de publicación: el fallo del bus se propaga fuera de AuthService.Register | `AuthControllerTests.PublishFailurePropagatesOutsideRegistrationValidation` | Bus simulado fallido; no se afirma atomicidad DB+bus ni manejo genérico de ese fallo en producción |

## Límites de cobertura

No existe infraestructura Azure Service Bus ni Stripe disponible. No se solicitan ni configuran tenants, cuentas, credenciales o secretos y no se hacen llamadas reales. Stripe no participa en esta suite. No se prueba entrega al broker, ack, reintentos, dead-letter ni consistencia distribuida.

El host no ejecuta Program.cs, migraciones, SQL Server, Swagger ni redirección HTTPS. Acredita la composición de prueba del controlador, Identity y middleware real, no el startup/DI completo del ejecutable. La base InMemory no prueba esquema SQL, migraciones, índices, collation, transacciones ni concurrencia. La excepción de guardado se inyecta; no representa una falla del servidor SQL real.

No se cubren todos los escenarios canónicos: quedan fuera el esquema/migraciones y la construcción/publicación del MessageBus real cuando falta su configuración. La propagación del fallo de publicación sí está caracterizada con un doble. No se certifica seguridad completa ni porcentajes globales de cobertura. Las demás APIs y el hardening de ShoppingCart permanecen fuera del incremento.

## Verificación del incremento

Verificación del 2026-11-17 con SDK 11.0.100 y configuración Release. Las primeras ejecuciones compilaron y aprobaron 15 casos Unit, 13 casos Identity y 17 casos HTTP. Las ejecuciones finales reutilizaron ese binario con `--no-build --no-restore`; cada caso se ejecutó correctamente al menos dos veces, sin reintentos para ocultar fallos.

| Ejecución final | Total | Aprobados | Fallos | Omitidos | TRX en TestResults |
|---|---:|---:|---:|---:|---|
| Category=Unit | 15 | 15 | 0 | 0 | `unit-final.trx` |
| Category=Integration | 30 | 30 | 0 | 0 | `integration.trx` |
| Proyecto completo sin filtro | 45 | 45 | 0 | 0 | `all.trx` |

Los tres comandos finalizaron con código 0. Las comprobaciones de alcance conservaron código productivo, especificaciones canónicas y hardening intactos; se agregó únicamente este proyecto a la solución y se actualizó el seguimiento del incremento. OpenSpec estricto y whitespace se verifican al entregar. Las tareas fuera de 1.1, 1.2 y 2.1–2.5 permanecen pendientes.

La restauración inicial dentro del sandbox falló con NU1301 por acceso a NuGet; se repitió con permiso del entorno y finalizó con código 0. No se cambiaron versiones ni configuración personal. NuGet reportó dos avisos NU1903 heredados de AutoMapper 16.1.1 y la compilación emitió advertencias de nullability en AuthAPI existente. El runner informa que usa un directorio temporal privado por ausencia de VSTEST_ARTIFACTS_DIRECTORY con este Test SDK; las ejecuciones y los TRX se generaron correctamente. Estos avisos se conservan sin modificar producción ni paquetes existentes.
