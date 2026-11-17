# Design

## Context

Ver `proposal.md` para la motivación y el límite de esta intervención. Inventario leído el 2026-11-17 desde `main` en `b560b8b3d1e47698580cac9f9bd980c333d25b61`; el hardening se consultó únicamente mediante Git, sin ejecutar sus servicios o pruebas.

### Inventario y referencias actuales

Cada ruta siguiente es un `.csproj` real; todos declaran `net11.0` y están incluidos en `Mango.sln` con configuraciones Debug/Release.

| Servicio | Proyecto relativo | Dependencias y comportamiento relevantes | Proyecto individual de pruebas |
|---|---|---|---|
| Auth | `Mango.Services.AuthAPI/Mango.Services.AuthAPI.csproj` | Identity, JWT, EF/SQL Server; publica registro mediante MessageBus | Falta |
| Product | `Mango.Services.ProductAPI/Mango.Services.ProductAPI.csproj` | EF/SQL Server, AutoMapper, JWT; catálogo sin mensajería | Falta |
| Coupon | `Mango.Services.CouponAPI/Mango.Services.CouponAPI.csproj` | EF/SQL Server, AutoMapper, JWT; Stripe en creación y eliminación | Falta |
| ShoppingCart | `Mango.Services.ShoppingCartAPI/Mango.Services.ShoppingCartAPI.csproj` | EF/SQL Server, JWT, HTTP a Product/Coupon, MessageBus | Existe solo en la rama de hardening; excluido |
| Order | `Mango.Services.OrderAPI/Mango.Services.OrderAPI.csproj` | EF/SQL Server, JWT, AutoMapper, cliente HTTP Product, Stripe, MessageBus | Falta |
| EMail | `Mango.Services.EMailAPI/Mango.Services.EMailAPI.csproj` | Consumidor Azure Service Bus, EF/SQL Server; registra notificaciones, sin envío SMTP implementado | Falta |
| Reward | `Mango.Services.RewardAPI/Mango.Services.RewardAPI.csproj` | Consumidor Azure Service Bus, EF/SQL Server; registra recompensas | Falta |

EMail y Reward son proyectos web de la solución, pero no contienen controladores ni endpoints de negocio. No se les asignan artificialmente pruebas REST o JWT. Los otros dos proyectos son `Mango.Web/Mango.Web.csproj` (MVC) y `Mango.MessageBus/Mango.MessageBus.csproj` (biblioteca); no forman parte de los seis proyectos nuevos.

Referencias entre proyectos: Auth, ShoppingCart y Order referencian `Mango.MessageBus`; los otros servicios no referencian proyectos de dominio ajenos. `Mango.Web` y MessageBus tampoco declaran referencias a otros proyectos. La carpeta lógica `Gateway` de la solución no contiene un proyecto ejecutable. `dotnet sln Mango.sln list` enumera nueve proyectos, todos con ruta existente; no hay proyectos de pruebas versionados en esta base.

El proyecto `Mango.Services.ShoppingCartAPI.Tests/Mango.Services.ShoppingCartAPI.Tests.csproj` existe en `feature/harden-shoppingcart-authorization` en `f434fed0013ab8454a074017b78a20b3015abe38` y está referenciado en su solución, pero no en `main`. Usa Microsoft.NET.Test.Sdk 17.14.1, xunit 2.9.3, xunit.runner.visualstudio 3.1.4 y EF Core InMemory 11.0.0; referencia ShoppingCart, Auth y Web. Las clases están en la raíz del proyecto, usan `Assert` de xUnit, fakes/stubs/spies manuales y hosts HTTP sobre loopback. No se observaron Moq, NSubstitute, FluentAssertions ni Shouldly. Mezcla pruebas aisladas con pruebas SQL; no se reutiliza su ejecución global. Posibles carpetas ignoradas `bin`/`obj` que quedan al cambiar de rama no representan proyectos presentes en `main`.

Paquetes productivos observados: ASP.NET Core/EF Core 11.0.0, AutoMapper 16.1.1, Azure.Messaging.ServiceBus 7.20.1, Newtonsoft.Json 13.0.4 donde se usa, Stripe.net 52.4.2 en Coupon y 52.4.0 en Order. El SDK 11.0.100 está instalado; no hay `global.json` versionado ni convenciones centrales de testing. Estos datos no acreditan compilación ni ejecución: aquí no se ejecuta `dotnet test`.

### Matriz de autorización, validación y propiedad observada

| API | Acceso actual que se probará | Validación/propiedad y límites |
|---|---|---|
| Auth | Registro/login públicos; AssignRole requiere ADMIN | Role vacío devuelve 400; Identity valida usuario/contraseña. Los DTO no tienen validadores explícitos de email/password; no inventarlos |
| Product | GET públicos; POST/PUT/DELETE requieren ADMIN | Name requerido, Price 1..1000; inexistentes 404; errores genéricos 500. Catálogo sin dueño individual |
| Coupon | GET requieren autenticación; POST/PUT/DELETE requieren ADMIN | Code requerido, descuento mínimo anotado 0.01, mínimo de compra >= 0; GET/DELETE ausentes 404. Sin propiedad individual |
| Order | Las seis acciones llevan Authorize, sin restricción de rol en atributos | GetOrders permite listado global a ADMIN; para otros filtra por userId recibido. GetOrder y escrituras no verifican dueño contra claims. No afirmar aislamiento entre usuarios ni exigir 403 ADMIN en estas acciones |
| EMail / Reward | Sin endpoints propios ni autenticación HTTP configurada | Probar servicios y DTO de mensajes; no agregar reglas de entrada, idempotencia o reintentos que hoy no existen |

Fuentes: controladores y DTO de cada API, `Service/AuthService.cs`, extensiones `AddAppAuthetication`, `Program.cs`, servicios/consumidores EMail y Reward. Auth configura JWT en `ApiSettings:JwtOptions`; Product, Coupon y Order usan claves planas en `ApiSettings`. No aplicar una política uniforme ni importar la política futura de ShoppingCart.

## Goals / Non-Goals

**Goals:** seis suites individuales ejecutables sin infraestructura, resultados con assertions de contrato y efectos, helpers locales pequeños, clasificación explícita y trazabilidad de cada caso al contrato o código vigente. Separar pruebas puras de la integración entre componentes reales.

**Non-Goals:** porcentajes globales de cobertura, certificación de seguridad completa, pruebas end-to-end distribuidas, cambios de arranque productivo, refactorizar SDKs o consumidores, cerrar defectos existentes, ejecutar SQL/concurrencia, introducir bibliotecas compartidas o modificar el hardening. Las garantías del proveedor SQL y del broker requieren otra autorización y evidencia.

## Decisions

### D1. Seis proyectos independientes y estructura local

Crear posteriormente `<API>.Tests/<API>.Tests.csproj`, como hermanos de sus APIs, con namespaces del mismo nombre y clases `*Tests`. Cada proyecto referencia solo su API; las interfaces MessageBus transitivas pueden usarse para dobles. No referenciar `ShoppingCartAPI.Tests`, Web ni otro DbContext. Estructura propuesta:

```text
Mango.Services.<API>.Tests/
  Unit/                  # validadores, JWT, mappings y lógica pura existente
  Integration/           # HTTP/composición y servicios con persistencia aislada
  TestSupport/           # host, datos sintéticos, fakes y fixtures de esta API
  README.md              # comandos, trazabilidad y límites de esta suite
```

Usar solo carpetas con contenido. Una futura suite con infraestructura tendrá `Infrastructure/` dentro del mismo proyecto y opt-in explícito; esta iniciativa no necesita implementarla. La estructura plana de ShoppingCart se conserva intacta. Se descarta una biblioteca compartida porque seis helpers pequeños aún no justifican acoplar ciclos de evolución ni exponer tipos entre dominios.

El orden de implementación es AuthAPI → ProductAPI → CouponAPI → OrderAPI → EMailAPI → RewardAPI. AuthAPI es prioritario por la criticidad de autenticación, JWT, Identity y roles. Esta prioridad organiza el trabajo; no crea una dependencia de ejecución: ninguna suite ajena a Auth debe iniciar AuthAPI, llamar a su login, usar su generador JWT ni referenciar su proyecto o sus pruebas para autenticarse.

### D2. Herramientas mínimas, sin actualizaciones a proyectos existentes

Usar las mismas versiones observadas de Test SDK, xUnit y runner. Agregar EF Core InMemory 11.0.0 solo en nuevos proyectos que lo necesiten; `FrameworkReference` a `Microsoft.AspNetCore.App` cuando sea necesario para el host de pruebas. Usar assertions de xUnit y dobles manuales de interfaces, HttpMessageHandler e Identity stores. No incorporar Moq ni otra biblioteca de assertions. Los helpers de Identity pueden registrar managers/stores reales sobre persistencia aislada para probar sus reglas, sin mockear su lógica.

Alternativas: MSTest/NUnit crearían dos convenciones; SQLite añadiría un proveedor distinto sin demostrar semántica SQL Server. InMemory es suficiente para esta base funcional y ya se usa; nunca será evidencia de FKs, transacciones, índices, collation SQL, traducción relacional o concurrencia. Una prueba que use DbContext y almacenamiento aislado es de integración de componentes, no una prueba unitaria pura.

### D3. Hosts de pruebas por composición, sin arrancar Program.cs

En Auth, Product, Coupon y Order construir un `WebApplication` local en `TestSupport`, enlazar `127.0.0.1:0`, registrar controladores reales mediante `AddApplicationPart`, `AddControllers`, la extensión real de JWT, autorización y dependencias locales. Usar `UseAuthentication`, `UseAuthorization` y `MapControllers`. `AddControllers` mantiene el serializador MVC predeterminado. Ejercitar validación automática, routing, serialización y códigos HTTP reales; una llamada directa al controlador no verifica esos filtros.

Conservar exactamente el acceso público existente: `GET /api/product` y `GET /api/product/{id}` se prueban sin Authorization ni JWT. No agregar políticas globales de autorización ni RequireAuthorization que protejan estas consultas en el host de pruebas. Registro/login de Auth también permanecen públicos; EMail/Reward no requieren autenticación HTTP porque no exponen endpoints propios.

No usar `WebApplicationFactory<Program>` como primer camino: Auth/Product/Order invocan migraciones incondicionales; EMail/Reward además activan consumidores. Coupon define ApplyMigration pero no lo invoca. Un simple reemplazo de DbContext por InMemory no vuelve seguras las APIs relacionales de migración. Tampoco agregar un entorno Testing al código productivo bajo este alcance.

El host compuesto evita ejecutar migraciones, Swagger, redirección HTTPS y arranque productivo. Se documentan sus registros y diferencias contra Program; no acredita el cableado completo ni el startup del ejecutable real. Limpiar las fuentes de configuración del builder antes de registrar dependencias y usar solo configuración en memoria con claves y URLs sintéticas, sin consumir appsettings o secretos reales. Cada fixture libera host, clientes y persistencia; imponer tiempos máximos acotados (por ejemplo 30 segundos por petición/operación HTTP), sin sleeps ni puertos fijos. Alternativa TestServer exigiría un paquete adicional: loopback sigue el precedente existente y no requiere servicios externos.

### D4. Simular en límites existentes

No existe infraestructura disponible de Azure Service Bus ni Stripe para pruebas. No solicitar ni configurar tenants, cuentas, credenciales o secretos, ni efectuar llamadas reales a ninguno de esos servicios. Las claves de firma sintéticas de JWT y los payloads de las fixtures son datos locales de prueba, no credenciales de servicios externos. No buscar ni consumir secretos reales del repositorio, del entorno o del usuario.

- **Datos:** DbContext de la API real con nombre InMemory único por caso/fixture y limpieza/dispose. En Email/Reward compartir explícitamente las mismas opciones y raíz de base aislada con el contexto de inspección, pues los servicios crean sus propios contextos. Asertar contenido persistido y cantidades, no solo ausencia de excepción.
- **Identity/JWT:** stores/managers reales aislados para registro/login/roles de Auth; fake IAuthService para ramas puras de su controlador y spy IMessageBus para publicación. Las otras APIs generan JWT sintéticos con helpers locales de su propio TestSupport, configurados con firma, issuer, audience y claims de prueba para su validador real, sin ejecutar AuthAPI ni reutilizar su generador. Este mecanismo permite ejecutar sus suites de forma independiente. Comprobar firma, issuer, audience, roles, expiración y rechazos; acotar expiración entre instantes capturados antes/después, sin igualdad exacta del reloj. No reemplazar JWT por un handler que siempre autentica en las pruebas de seguridad ni exigir tokens en endpoints públicos.
- **HTTP:** HttpMessageHandler e IHttpClientFactory locales para ProductService de Order y su propagación de bearer. Respuestas JSON sintéticas, falla de transporte y JSON inválido según comportamiento actual; nunca iniciar ProductAPI. Conservar Newtonsoft.Json en clientes y mensajería, sin trasladarlo a MVC.
- **MessageBus:** spy/fake IMessageBus captura destino y payload, éxito/fallo. No construir MessageBus real. Probar servicios Email/Reward directamente con mensajes sintéticos y verificar persistencia; probar deserialización Newtonsoft por separado no demuestra que el callback del broker la ejecute correctamente.
- **Stripe:** Coupon/Order construyen servicios del SDK directamente. Usar el punto de extensión del SDK existente `StripeConfiguration.StripeClient` con `StripeClient` e implementación controlada de `Stripe.IHttpClient`, que devuelve respuestas sintéticas y falla ante solicitudes inesperadas. La documentación XML local de Stripe.net 52.4.2 y 52.4.0 confirma el setter global y el constructor que acepta IHttpClient. Guardar/restaurar el cliente global en finally, serializar todos los casos Stripe de cada assembly, impedir envíos reales y no actualizar SDK ni refactorizar producción. Probar primero una solicitud controlada del SDK antes de ejecutar acciones que guardan datos y luego llaman a Stripe.

Los callbacks privados de consumidores EMail/Reward construyen ServiceBusClient/Processor y dependen de servicios concretos; su ack, abandono, reintento, dead-letter y ciclo de vida no se pueden atribuir a las pruebas directas de servicios. Quedan fuera de esta base; no usar reflection sobre handlers privados para fingir cobertura del transporte. Separar posteriormente una propuesta de seams si se necesita esa garantía.

| Servicio | Pruebas aisladas sin modificar producción | Limitaciones sin infraestructura externa |
|---|---|---|
| Order | JWT local, DbContext aislado, fake de transporte Stripe, spy IMessageBus y dobles HTTP para Product | No acredita aceptación de peticiones por Stripe, pagos/reembolsos reales ni entrega de recompensas por Azure; solo respuestas y efectos locales simulados |
| EMail | EmailService real con opciones de persistencia aislada y fallos controlados; payloads sintéticos sin construir el consumidor | No acredita recepción desde colas/suscripciones, ack, reintentos, dead-letter ni ciclo de vida del broker |
| Reward | RewardService real con persistencia aislada, payloads sintéticos y fallo de guardado controlado; no construir el consumidor | No acredita entrega del tópico, ack, reintentos ni integridad del flujo Order → Azure → Reward |

Estos escenarios externos se registran como cobertura no disponible, no como pruebas ejecutadas o aprobadas. Si un caso adicional no admite aislamiento a través de los puntos existentes del SDK o de interfaces, documentar el límite y detener ese caso sin solicitar infraestructura, modificar código productivo o sustituirlo por una prueba vacía.

### D5. Cobertura inicial y trazabilidad por servicio

Las familias de casos siguientes son obligatorias para esta base; no equivalen a cubrir todas las especificaciones. Registrar en cada README requisito/escenario o símbolo de código, nombre de prueba y límite de evidencia. Un caso parametrizado cuenta por cada combinación ejecutada en TRX.

| ID / orden | Familias iniciales requeridas | Fuente y límite |
|---|---|---|
| A / 1 Auth | JWT firmado con claims/roles y ventana de 7 días; registro válido, duplicado y password inválido usando Identity real aislado; publicación solo tras éxito; login válido, usuario ausente y contraseña errónea; AssignRole ADMIN, sin role, usuario ausente, 401 y 403; error controlado del servicio sin filtrar detalles | `auth-api`: registration/login/roles/JWT y validación Identity. No inventar email annotations ni transacción DB+bus |
| P / 2 Product | GET lista y por id públicos; 404 GET/PUT/DELETE; ADMIN CRUD con lectura de estado; POST/PUT inválidos y bordes Price; error inyectado 500 genérico; 401 sin token/inválido/expirado y 403 sin ADMIN en cada escritura | `product-api`: Retrieve*, payload validation, CRUD, JWT y escenario de error; no verificar SQL |
| C / 3 Coupon | GET lista/id/code y código con distinta capitalización; 404 GET/DELETE; ADMIN CRUD con Stripe fake donde aplique; validación POST/PUT (code, descuento 0/negativo y 0.01, mínimo negativo y 0); 401 para todos los endpoints; 403 de escrituras; error 500 genérico | `coupon-api`: lectura, CRUD, validación y JWT. La comparación sin distinción de casing en InMemory no prueba collation SQL |
| O / 4 Order | Mapping y CreateOrder con redondeo/estado pending y detalles persistidos; GetOrders ADMIN y filtro userId tal como existe; GetOrder existente; 401 en las seis acciones con entradas válidas; petición autenticada representativa; binding inválido 400; ProductService HTTP/bearer; CreateStripeSession, ValidateStripeSession y cancelación con Stripe fake y spy de rewards | Controlador, MappingConfig, ProductService, handler y SD en `Mango.Services.OrderAPI`; sin spec canónico. No afirmar dueño vinculado a claims, rol ADMIN para escrituras ni errores 404/500 que hoy no se devuelven |
| E / 5 EMail | Mensajes cart/registro/orden; carrito con productos nulos y contenido parcial; contenido/destinatario/timestamp de EmailLogger; falla de guardado con logging y propagación; contratos JSON de DTO | `email-api`: notificaciones y Audit log persistence; servicio real. Sin HTTP, SMTP ni evidencia de CompleteMessageAsync |
| R / 6 Reward | Contrato JSON de RewardsMessage; UpdateRewards persiste OrderId/UserId/actividad/fecha; eventos separados con datos aislados; falla de persistencia caracterizada como excepción actualmente absorbida y sin fila creada | `Services/RewardService.cs`, `Message/RewardsMessage.cs`, modelos/Data; sin spec canónico. No declarar rethrow, idempotencia ni ack seguro |

La ausencia de controles de propiedad en Order es un hallazgo de seguridad, no un criterio de aceptación que convierta acceso ajeno en un contrato deseado. Probar el filtro explícito de consulta con datos propios no acredita aislamiento de propietario. No añadir pruebas verdes que llamen “seguro” al acceso ajeno ni arreglarlo aquí.

Discrepancias conocidas de specs: el encabezado de errores de Product menciona 200 pero el texto/escenario y código exigen 500; usar el escenario y registrar el título desactualizado. Email especifica el literal `[Some products could not be included]`, mientras el servicio incluye un contador. Verificar omisión de productos nulos, contenido parcial y presencia del aviso; registrar que el literal exacto no queda cubierto, sin cambiar la spec ni afirmar cumplimiento completo. Coupon anota mínimo 0.01 frente al texto general > 0; probar bordes implementados y registrar el intervalo 0..0.01 como discrepancia. Cualquier nueva divergencia que afecte un caso seleccionado se informa como fallo/bloqueo, sin alterar producción ni relajar silenciosamente assertions.

### D6. Ejecución reproducible y separación de infraestructura

Todos los nuevos casos tienen exactamente `Trait("Category", "Unit")` o `Trait("Category", "Integration")`. Reservar `Category=Infrastructure` para SQL Server/broker/Stripe reales en un alcance posterior. En esta base no habrá casos Infrastructure, pruebas de concurrencia ni casos omitidos para ocultar carencias. `dotnet test` sin filtro sobre cualquiera de los seis proyectos debe ser seguro, porque todos sus casos son aislados.

Comandos propuestos para implementación, NO ejecutados durante planificación:

```powershell
dotnet --version
dotnet restore Mango.sln
$apiTestNames = @('AuthAPI', 'ProductAPI', 'CouponAPI', 'OrderAPI', 'EMailAPI', 'RewardAPI')
foreach ($apiTestName in $apiTestNames) {
    $apiTestProject = "Mango.Services.$apiTestName.Tests/Mango.Services.$apiTestName.Tests.csproj"
    dotnet test $apiTestProject --no-restore --configuration Release --filter 'Category=Unit|Category=Integration' --logger 'trx;LogFileName=isolated.trx' --results-directory "TestResults/$apiTestName"
    if ($LASTEXITCODE -ne 0) { throw "Suite fallida: $apiTestName" }
}
```

Requisito previo: SDK compatible `11.0` y fuentes NuGet accesibles o caché disponible; no editar configuración personal ni paquetes existentes para resolver restore. Registrar SDK, comando, total y passed/failed/skipped por proyecto. Ejecutar dos veces las suites aisladas tras completarlas para detectar contaminación de estado, no para hacer pasar fallos mediante reintentos. Se puede filtrar una suite rápida con `--filter Category=Unit` y las de componentes/HTTP con `--filter Category=Integration`.

La lista refleja la prioridad acordada, pero cada `dotnet test <proyecto>` debe poder ejecutarse por separado sin AuthAPI activo, sin su proyecto de pruebas y sin obtener tokens de su endpoint de login. Azure Service Bus y Stripe no son prerrequisitos disponibles ni se aprovisionarán para esta iniciativa.

En la base actual más los seis proyectos, `dotnet test Mango.sln` sería seguro y debe descubrirlos todos. Si después se integra ShoppingCart, no ejecutar la solución completa ni un filtro negativo como única protección sin auditar primero su suite mixta: casos existentes sin Trait pueden escapar de la clasificación. La lista positiva de seis proyectos mantiene esta iniciativa independiente. El comando de infraestructura futuro será por proyecto y filtro positivo `--filter Category=Infrastructure`, solo con autorización específica y configuración de laboratorio; no se ejecuta ni implementa aquí.

### D7. Criterios de aceptación medibles para la implementación posterior

1. **AC1:** existen exactamente seis proyectos nuevos con nombres D1, TFM net11.0, IsTestProject y referencia a su API; `dotnet sln list` los enumera una vez, con configuraciones Debug/Release. Ninguna referencia a proyectos de prueba o DbContext ajenos.
2. **AC2:** todos los casos de las seis familias D5 tienen assertions de resultado y, cuando corresponda, estado o ausencia de efectos. Cada suite tiene al menos un caso Unit y uno Integration; cada fila queda trazada en README. Sin tests vacíos, skips ni afirmaciones de cobertura completa.
3. **AC3:** las cuatro APIs HTTP verifican en pipeline real su matriz de acceso D5; toda escritura protegida seleccionada incluye un rechazo y ausencia de efectos, y Product mantiene ambas lecturas públicas. JWT erróneo incluye firma/issuer/audience/expiración en cada host protegido. Las peticiones ADMIN válidas alcanzan y ejercitan la acción, no se aceptan respuestas 500 como éxito.
4. **AC4:** datos/configuración sintéticos, dobles de red/bus/Stripe y contextos independientes permiten ejecutar los seis proyectos dos veces con cero fallos y cero skips, sin SQL Server, Azure, Stripe ni otras APIs; cero migraciones y cero peticiones externas. Las suites ajenas a Auth se ejecutan individualmente sin AuthAPI activo, referencias a su proyecto o su suite, ni tokens obtenidos de su login. Cada caso está categorizado; casos que usan Stripe restauran estado global incluso al fallar. No se solicitan/configuran tenants, cuentas, credenciales o secretos; los escenarios externos de D4 permanecen expresamente fuera de la evidencia obtenida.
5. **AC5:** restore y `dotnet test` de cada proyecto finalizan con exit code 0 y descubren casos (un total cero es fallo). Conservar conteos y comandos, sin secretos ni artefactos binarios versionados. No fijar metas de porcentaje de cobertura sin un baseline.
6. **AC6:** además de los artefactos de planificación y su seguimiento, diff final de implementación limitado a seis proyectos y referencias de solución; no hay cambios de comportamiento, paquetes existentes, specs canónicas, archivos archivados ni hardening. Cualquier bloqueo o cobertura diferida se documenta y requiere aceptación explícita antes de reclamar cierre; no se transforma en tarea completada automáticamente.

## Risks / Trade-offs

- [Arranque con efectos externos] → Hosts de composición; startup/migraciones reales quedan expresamente fuera de evidencia.
- [InMemory difiere de SQL Server] → No usar resultados para demostrar integridad relacional o concurrencia; futura verificación de proveedor requiere laboratorio independiente.
- [Estado Stripe global y SDKs de versiones distintas] → Dobles del transporte local, restauración finally y colección serial por assembly; validar firma local de cada SDK y abortar ante red inesperada.
- [Callbacks del broker acoplados a tipos concretos] → Cubrir servicios/contratos públicos, declarar límites de ack/lifecycle y posponer cambios de seams fuera del alcance.
- [Azure Service Bus y Stripe no disponibles] → Usar exclusivamente dobles locales y documentar los límites D4; no solicitar ni configurar infraestructura, cuentas, credenciales o secretos ni ejecutar llamadas reales.
- [Reward absorbe errores; Order tiene brechas de propiedad y filtra excepciones] → Caracterizar límites, sin normalizar seguridad ni errores entre APIs; no atribuir garantías ausentes.
- [Divergencias entre specs y código] → Registro explícito D5, pruebas del comportamiento seleccionado, sin tocar el canónico ni ocultar fallos nuevos.
- [Referencia local origin/main podría estar desactualizada] → Base identificada por hash; no se hizo fetch/pull por restricción. Las seis APIs no difieren entre main y hardening, por lo que esta planificación no depende de sus cambios.
- [Restore o ejecución no disponibles] → Informar el fallo real y detener el bloque afectado; disponer de SDK instalado no equivale a pruebas aprobadas. No modificar NuGet para eludirlo.

## Migration Plan

No hay despliegue ni migración de datos. Tras autorización independiente, implementar en orden Auth → Product → Coupon → Order → EMail → Reward por la criticidad de autenticación, JWT, Identity y roles, verificando cada incremento. La prioridad no convierte AuthAPI en un requisito para ejecutar las demás suites. Completar referencias de solución y documentación dentro de cada incremento. Ante un defecto que necesite código productivo, detener ese incremento y presentar el bloqueo; no ampliar el alcance ni solicitar infraestructura externa.

Si la rama base cambia o se integra el hardening antes de implementar, repetir inventario y revisar referencias de solución sin copiar ni ejecutar su suite; no hacer merge/rebase sin autorización. La reversión de una futura implementación consiste en revisar y retirar únicamente sus seis proyectos/referencias mediante el flujo Git autorizado, sin modificar bases ni otros servicios. En esta intervención no se crean proyectos, se ejecutan pruebas ni se sincroniza o archiva OpenSpec.

## Planning Validation

Evidencia de esta intervención, 2026-11-17:

- Se partió de árbol limpio en el hardening `f434fed`; se creó la rama autorizada directamente desde main `b560b8b`, sin integrar cambios. La escritura de Git requirió permiso del entorno y se autorizó; no hubo commits ni operaciones de red.
- `openspec.cmd validate establish-api-test-foundations --strict` pasó. El CLI acepta cero deltas por `skip_specs: true`; proposal, design y tasks están completos como planificación, con 38 tareas de implementación pendientes (0 completadas).
- `git diff --check` pasó. También se verificaron los cuatro archivos nuevos con diff `--no-index --check` contra NUL, porque aún no están en el índice; sin errores de whitespace. No se cambió la configuración de Git.
- `git diff HEAD` y el índice no muestran cambios versionados. El estado contiene únicamente cuatro archivos sin seguimiento bajo este cambio: `.openspec.yaml`, `proposal.md`, `design.md` y `tasks.md`. El inventario físico conserva los nueve csproj de la base y ningún proyecto de pruebas nuevo.
- main y su referencia local origin/main conservan `b560b8b3d1e47698580cac9f9bd980c333d25b61`. El hardening conserva `f434fed0013ab8454a074017b78a20b3015abe38` y su árbol OpenSpec `5512f0acec195b2354a1423b7cff618e4a17e697`; sus tareas 4.2, 8.2 y 8.3 siguen pendientes. Su ausencia del working tree actual se debe al cambio de rama, no a una eliminación en esa rama.
- No se compiló, restauró ni ejecutó ninguna suite; no se iniciaron SQL Server, migraciones, broker, servicios externos o pruebas de concurrencia. AC1-AC6 son criterios futuros, no resultados ya obtenidos.
