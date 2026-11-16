# Revisión final funcional y de seguridad de ShoppingCartAPI

## Actualización D11: implementación y regresión sin SQL

La alternativa B aprobada en [cart-empty-contract-decision.md](cart-empty-contract-decision.md) y formalizada en el delta ya se implementó con autorización separada. GetCart produce un carrito lógico vacío HTTP 200 únicamente tras confirmar que no existe cabecera para el JWT propietario; no crea filas ni consulta Product/Coupon. El modelo contiene cabecera ID 0, UserId del subject validado, total/descuento cero y detalles vacíos. Checkout GET/POST y EmailCart rechazan esa cabecera con 404 antes de efectos. CartIndex, BaseService, ResponseDto, DTO compartidos, cargador, rutas, vistas, RemoveCart y transacciones no se modificaron.

### Ejecuciones y resultados reales

| Ejecución | Aprobadas | Fallidas | Omitidas por ejecutor | Resultado |
| --- | ---: | ---: | ---: | --- |
| Casos nuevos CartEmptyContractTests, sandbox | 29 | 0 | 0 | Contrato HTTP, cookies, JWT y navegación controlada |
| Primer pase conjunto de seis clases, sandbox | 76 | 1 | 0 | AuthenticationTests terminó con ResponseEnded; el log registra activación sin AppDbContext y errores de acceso a DPAPI/EventLog |
| Repetición aislada de AuthenticationTests, sandbox | 0 | 1 | 0 | Confirma el fallo del host existente y del entorno; no se cambió la prueba |
| AuthenticationTests fuera del sandbox | 1 | 0 | 0 | Mismo ensamblado y aserciones, sin SQL |
| **Regresión final conjunta fuera del sandbox** | **77** | **0** | **0** | **Resultado final de esta intervención; no se suman los pases anteriores** |

El reintento fuera del sandbox se autorizó para resolver las restricciones de DPAPI/EventLog del host de autenticación existente. La aplicación de producción y sus claves/configuración no se modificaron. Los hosts nuevos usan protección de datos efímera y proveedores de log controlados. Las pruebas no arrancan el Program de ShoppingCartAPI ni su migración de inicio.

| Clase de la regresión final | Aprobadas |
| --- | ---: |
| CartEmptyContractTests | 29 |
| GetCartAuthorizationTests | 9 |
| WebCompatibilityTests | 24 |
| ProductFailureIntegrationTests | 4 |
| EmailCartAuthorizationTests | 10 |
| AuthenticationTests | 1 |
| **Total** | **77** |

Se ejecutó `dotnet test Mango.Services.ShoppingCartAPI.Tests/Mango.Services.ShoppingCartAPI.Tests.csproj --no-build --no-restore` con este único filtro explícito:

```text
FullyQualifiedName~CartEmptyContractTests|FullyQualifiedName~GetCartAuthorizationTests|FullyQualifiedName~WebCompatibilityTests|FullyQualifiedName~ProductFailureIntegrationTests|FullyQualifiedName~EmailCartAuthorizationTests|FullyQualifiedName~AuthenticationTests
```

TRX final: `%TEMP%/mango-cart-empty-contract/cart-empty-regression-final.trx`, con total=77, executed=77, passed=77, failed=0 y notExecuted=0. Las ejecuciones previas se conservan en el mismo directorio como cart-empty-initial.trx, cart-empty-regression.trx, authentication-diagnosis.trx y authentication-unsandboxed.trx. Son evidencia temporal y pueden desaparecer.

**Exclusiones por filtro, no skips:** ApplyCouponAuthorizationTests, CartUpsertAuthorizationTests y RemoveCartAuthorizationTests usan SQL; también se excluyeron las dos variantes de RealJwtWebIntegrationTests, MvcHttpIntegrationTests y todos los casos de SqlServerConcurrencyTests. No se seleccionaron esas clases ni siquiera para sus casos sin SQL. No se ejecutaron bases habituales, laboratorio SQL, migraciones, carreras ni validación de transacciones. Los rechazos HTTP de ApplyCoupon/Remove MVC sí se probaron con transporte controlado, y sus cuerpos productivos y pruebas relacionales existentes se conservaron.

La primera compilación detectó una colisión entre el alias del fixture y el namespace Mango.Web; se corrigió en el archivo nuevo. La compilación final `dotnet build Mango.Services.ShoppingCartAPI.Tests/Mango.Services.ShoppingCartAPI.Tests.csproj --no-restore --verbosity quiet` pasó con cero errores y seis advertencias NU1903 de AutoMapper 16.1.1 en los proyectos referenciados. Compila ShoppingCartAPI, Mango.Web, AuthAPI y MessageBus; no se actualizaron paquetes. El aviso del ejecutor sobre VSTEST_ARTIFACTS_DIRECTORY/Test.Sdk también permanece sin cambio de dependencias.

### Contraste con los 44 escenarios del delta vigente

| Requisito | Escenarios | Evidencia y límites |
| --- | ---: | --- |
| Retrieve cart with computed totals | 9 | GetCartAuthorizationTests y CartEmptyContractTests verifican forma virtual, repetición sin SaveChanges, dependencias no consultadas en ausencia, carrito real, real sin detalles, descuento, productos no resueltos, cupón ausente y error de almacenamiento. WriteGuard detecta intentos de persistencia; un contexto dispuesto distingue acceso a almacenamiento de rechazo ajeno previo |
| Error handling and response envelope | 3 | Éxito 200, fallos 500 genéricos y errores HTTP preservados. Los not-found de escrituras no cambian; regresión de clientes con fakes y revisión estática, sin nueva ejecución relacional |
| JWT authentication configuration | 2 | AuthenticationTests mantiene los cinco endpoints y tokens ausentes/invalidos/vencidos/subject inutilizable; los nuevos hosts verifican JWT y sesiones reales para lectura y consumidor MVC |
| Request cart email via message bus | 3 | Diez pruebas EmailCartAuthorizationTests mantienen reconstrucción confiable, recipient aprobado, integridad y ausencia de publicación en rechazo; los casos virtuales MVC añaden cero solicitudes de correo |
| Downstream integration with ProductAPI | 3 | Clientes reales con handlers controlados, errores HTTP/transporte/cuerpo, lista válida vacía y fallos con cabecera real vacía. La ausencia de cabecera no requiere llamar al catálogo |
| Downstream integration with CouponAPI | 2 | Error propagado conserva 500; 404 conserva lectura sin descuento. WebCompatibilityTests y la nueva integración no convierten fallos en carritos virtuales |
| Cart ownership and client identifiers | 13 | Se separan propio ausente 200 y ajeno existente/inexistente 404 idéntico, también ADMIN. Un contexto dispuesto prueba rechazo ajeno sin acceso a datos. Se verifica discordancia cookie/bearer. Se revisan los bloques de escrituras, IDs anidados, creación y limpieza sin alterarlos; las pruebas SQL que los ejercitan no se repiten |
| Mango.Web logical empty-cart consumers | 9 | Vista vacía HTTP 200, redirección seguida con sesión válida, tres rechazos virtuales 404 sin OrderService/correo/bus, carritos persistidos con y sin detalles, ruta realmente inexistente y cuerpos 404 vacíos/ProblemDetails/genéricos, 400/401/403/500/502 y borrado rechazado sin redirección |

El borrado de la prueba de navegación se sustituye por un handler que simula el éxito y prepara la ausencia en InMemory; el siguiente GetCart pasa por el controlador real y serialización MVC. No prueba ExecuteDelete, transacción, cascada ni limpieza SQL. El 500 de almacenamiento se induce con un contexto dispuesto, no con una caída real de SQL Server. Product/Coupon, bus y OrderService son controlados; no se contactan servicios externos ni se realizan pagos/correos reales. El login Web usa un stub que entrega un JWT del generador AuthAPI; no valida credenciales contra AuthAPI desplegada.

OpenSpec estricto y `git diff --check` pasan. El delta no se modificó en esta implementación ni presentó contradicciones con D11. Las tareas 9.1–9.9 quedan completadas con esta evidencia y estos límites; **4.2, 8.2 y 8.3 continúan pendientes**. La comparación funcional de 44 escenarios no acredita D06 frente a intercalaciones SQL ni cierra la conformidad integral del hardening. El sucesor propuesto `harden-shoppingcart-concurrency-integrity` conserva su trazabilidad, sin creación ni ejecución.

**Registro histórico:** las secciones siguientes corresponden a la revisión anterior a D11, sus 69 pruebas y su alcance SQL entonces autorizado. No describen una nueva ejecución ni la selección actual; se conservan sin sumar sus resultados al total de 77.

## Alcance y conclusión

Revisión de 8.1 y 8.2 del cambio `harden-shoppingcart-authorization`, en `feature/harden-shoppingcart-authorization`. Se conservaron los cambios existentes sin commit y se detuvo el trabajo de concurrencia conforme a la decisión del usuario. No se modificaron código productivo, pruebas, dependencias, esquema ni criterios de aceptación.

**8.1 completada en su alcance de regresión y revisión:** compilación y 69 pruebas pertinentes aprobadas, con revisión de equivalencia de respuestas, exposición de errores y los cinco endpoints frente al delta. Su texto exige ejecutar y revisar; no exige completar 4.2. Esto no equivale a certificar las garantías concurrentes excluidas ni a cerrar el hardening.

**8.2 pendiente:** la validación estricta y la comparación funcional del delta son satisfactorias, pero no se declara conformidad integral con todos los escenarios de seguridad mientras D06 conserve garantías de escrituras, inserción y limpieza bajo intercalaciones críticas sin acreditar. La dependencia es de evidencia de comportamiento, no de estructura OpenSpec.

**4.2 y 8.3 pendientes.** Los riesgos residuales no se consideran aceptados. Este informe finaliza esta revisión acotada; no es un acta de aceptación del cambio completo.

Fuentes: [tareas vigentes](../../../openspec/changes/harden-shoppingcart-authorization/tasks.md), [diseño y D06](../../../openspec/changes/harden-shoppingcart-authorization/design.md), [delta de seguridad](../../../openspec/changes/harden-shoppingcart-authorization/specs/shopping-cart-api/spec.md), [matriz de cobertura](security-test-coverage.md), [integración Web](web-integration.md), [verificación de concurrencia](concurrency-verification.md) e [índice histórico](index-hardening.md).

## Entorno y selección

Se confirmó mediante una consulta explícita de solo lectura: `Mango_ShoppingCart_Concurrency_Test|22LAP5CD1195RMS|MSSQLSERVER|ONLINE`. La comprobación valida base, equipo e instancia predeterminada antes de continuar. Los fixtures SQL usan exclusivamente ese destino, autenticación integrada, usuarios sintéticos y limpieza delimitada. No se consultó ni escribió la base habitual.

Se inspeccionaron los fixtures y no se identificaron llamadas a `Migrate` o `EnsureCreated`. Los hosts HTTP se construyen expresamente con los controladores y dependencias controladas; no ejecutan el Program productivo ni su migración de arranque. El ensamblado desactiva el paralelismo de pruebas. La verificación previa y la regresión requirieron ejecución fuera del sandbox para acceder a SQL Server y a los hosts locales.

El filtro ejecutado fue:

```text
FullyQualifiedName!~SqlServerConcurrencyTests
|FullyQualifiedName~UpsertFailureBeforeCommitRollsBackPersistedWrites
|FullyQualifiedName~RemovalFailureBeforeCommitRestoresHeaderAndDetail
|FullyQualifiedName~RollbackOfFailedUpsertLeavesNoHeaderOrDetail
|FullyQualifiedName~OwnerCanCreateIncrementApplyCouponAndRemoveLastItem
```

Las líneas se concatenaron con `|` en un único argumento de filtro. Se ejecutó una sola regresión, con `dotnet test --no-build --no-restore`, salida detallada de consola y TRX. No se ejecutó ninguna carrera ni se investigó nuevamente su causa.

## Resultados reales

El TRX de esta ejecución registra **69 totales, 69 ejecutadas, 69 aprobadas, 0 fallidas y 0 no ejecutadas dentro de la selección**. No hubo pruebas omitidas por el ejecutor. Los casos de teoría se cuentan como ejecuciones individuales, sin multiplicar las aserciones o bucles internos.

| Clase | Aprobadas | Cobertura principal |
| --- | ---: | --- |
| AuthenticationTests | 1 | JWT ausente, inválido, expirado o identidad inutilizable; los cinco endpoints son comprobados dentro de la prueba. |
| GetCartAuthorizationTests | 3 | Lectura propia, rechazo ajeno/ausente, equivalencia y 500 genérico sin detalle sensible. |
| ApplyCouponAuthorizationTests | 3 | Aplicar/quitar cupón, rechazos y cabeceras ambiguas sin modificación. |
| CartUpsertAuthorizationTests | 14 | Creación, incremento secuencial, propietario, ADMIN y referencias anidadas o inconsistentes. |
| RemoveCartAuthorizationTests | 5 | Eliminación propia, última cabecera, recursos ajenos/inexistentes e identificadores inválidos. |
| EmailCartAuthorizationTests | 10 | Reconstrucción confiable, destinatario JWT, rechazos y ausencia de publicación o llamadas indebidas. |
| WebCompatibilityTests | 22 | Clientes de los cinco endpoints, bearer, errores HTTP, Product/Coupon y respuestas inválidas. |
| ProductFailureIntegrationTests | 4 | Fallos de Product con/sin detalles, transporte, no publicación/no filtración y lista vacía válida. |
| RealJwtWebIntegrationTests | 2 | JWT firmado validado, propagación a Product/Coupon, flujos y fallos controlados, con variantes InMemory/SQL. |
| MvcHttpIntegrationTests | 1 | Cookies, vistas y flujos HTTP MVC, JWT real y persistencia autorizada en SQL aislado. |
| SqlServerConcurrencyTests: solo cuatro casos secuenciales | 4 | Persistencia y rollback sin operaciones competidoras. |
| **Total** | **69** | **0 fallidas; 0 omitidas en la selección.** |

Los cuatro casos secuenciales fueron `UpsertFailureBeforeCommitRollsBackPersistedWrites`, `RemovalFailureBeforeCommitRestoresHeaderAndDetail`, `RollbackOfFailedUpsertLeavesNoHeaderOrDetail` y `OwnerCanCreateIncrementApplyCouponAndRemoveLastItem`. El primero y el segundo inducen fallo antes del commit después de escrituras; los otros comprueban fallo de inserción y el flujo funcional de persistencia. No se emplean sus resultados como evidencia de rollback concurrente.

**Ocho pruebas del catálogo no se ejecutaron por filtro**, sin contarlas como aprobadas u omitidas por xUnit:

- Carreras excluidas: `ConcurrentSameProductPreservesCommittedWrites`, `LastItemDeletionAndConcurrentInsertNeverLoseSuccessfulInsert`, `OwnershipChangeWaitsUntilUpsertCompletes`, `LastItemRemovalRacingInsertDoesNotCascadeDeleteSuccessfulInsert`, `ConcurrentCartCreationUsesSerializableTransactions` y `CouponUpdateRejectsOwnershipChangedAfterRead`.
- Comprobaciones auxiliares del harness fuera del objetivo funcional: `HarnessDrainReleasesBarrierAndWaitsForCompletion` y `HarnessReportsSecondaryFaultInsteadOfSilentlyPassing`.

La evidencia bruta se guardó en `%TEMP%/mango-hardening-final-review/hardening-final-review.trx`; es un artefacto temporal y puede desaparecer. Los conteos anteriores se extrajeron de sus contadores y definiciones de pruebas. No se agregan resultados históricos al total de esta ejecución.

## Contraste con requisitos y escenarios

El delta contiene cuatro requisitos MODIFIED y uno ADDED, con **20 escenarios**. Se revisó la implementación de autenticación, los controladores y los clientes descendentes y se relacionó con las pruebas existentes:

| Requisito y escenarios | Implementación y evidencia de esta regresión | Límite |
| --- | --- | --- |
| JWT authentication configuration: 2 | JwtBearer valida firma/emisor/audiencia y desactiva mapeo entrante; identidad unusable falla. AuthenticationTests, RealJwtWebIntegrationTests y MvcHttpIntegrationTests cubren aceptación y rechazo. | Entorno de prueba con claves sintéticas; no valida configuración del despliegue real. |
| Request cart email via message bus: 3 | Reconstrucción desde carrito y servicios, claim email aprobado, Name/Phone sin establecer, fallo cerrado. EmailCartAuthorizationTests y las integraciones comprueban payload y no publicación en rechazo. | Bus simulado; no se envían mensajes a Azure Service Bus real. |
| Downstream integration with ProductAPI: 3 | Cliente Product propaga fallos HTTP/transporte/sobre fallido; GetCart devuelve 500 genérico y email no publica. Productos válidos y lista vacía tienen cobertura. | Clientes HTTP controlados; no se prueba disponibilidad de ProductAPI desplegada. |
| Downstream integration with CouponAPI: 2 | Cliente Coupon distingue 404 de otros fallos y rechaza respuestas inválidas. Se verifica descuento, ausencia de cupón, bearer y fallo seguro en GetCart/email. | Dependencia simulada; no se acredita un servicio desplegado. |
| Cart ownership and client identifiers: 10 | Lectura propia; cupón y upsert autorizados; identidad malformada o discrepante; referencias anidadas; ADMIN sin bypass; eliminación propia; rechazo de detalle ajeno/ausente y correo ajeno. Las clases por endpoint y las integraciones comprueban 400/401/404 y persistencia sin cambios ante rechazo. | Cobertura funcional sin escritores competidores; no extiende los resultados a las carreras de D06. |

Se revisaron las respuestas 400, 401, 404 y 500 y, en correo sin datos confiables, 503; las pruebas correspondientes pasaron. Las pruebas comparan cuerpos de rechazo ajeno/inexistente, comprueban ausencia de escrituras/publicaciones y usan detalles sintéticos para verificar que no lleguen a respuestas de error. Los catches productivos revisados devuelven mensajes genéricos. Estos resultados no son una prueba universal contra canales temporales ni contra toda entrada o excepción posible.

La integración MVC usa cookies, vistas y middleware real, pero el login de AuthAPI se sustituye por un stub con JWT de su generador. No se verifican credenciales reales, entrega real de correo ni integraciones externas desplegadas.

## Verificaciones estructurales y pendientes

- Compilación: `dotnet build Mango.Services.ShoppingCartAPI.Tests/Mango.Services.ShoppingCartAPI.Tests.csproj --no-restore -v:q` satisfactoria, cero errores. Incluye sus proyectos referenciados ShoppingCartAPI, Mango.Web, AuthAPI y MessageBus; no modifica esos servicios.
- Persisten seis advertencias NU1903 de AutoMapper en esta compilación. No se actualizaron paquetes ni se declara resuelta esa vulnerabilidad.
- `openspec validate harden-shoppingcart-authorization --strict`: satisfactorio.
- `git diff --check`: satisfactorio; la comprobación se repite tras documentar la revisión.
- Proposal, design y tasks distinguen expresamente garantías obligatorias y mejoras diferidas; el diseño no presenta como resueltas las carreras fallidas. El delta de seguridad y la especificación principal permanecen sin modificar en esta intervención.

El gate técnico de D06/4.2 sigue sin acreditar el conjunto crítico de autorización en la escritura, inserción protegida y limpieza sin pérdida de inserciones confirmadas ante escritores concurrentes, rollback y respuestas seguras ante sus conflictos. Los fallos históricos y la causa indeterminada de la carrera del mismo producto permanecen en la evidencia. La cuarentena del harness no demuestra cancelación SQL efectiva ni cierre del rollback en el servidor. Ninguna de esas investigaciones se retomó aquí.

## Riesgos residuales y estado de tareas

Se mantienen los riesgos documentados de duplicados por usuario/producto, posibles incrementos perdidos, contención, deadlocks/timeouts, commits ambiguos, reintentos no idempotentes y límites del aislamiento del harness. No se consideran aceptados por ejecutar una regresión satisfactoria. Prevenir escrituras ajenas, operaciones parciales y cascadas inseguras sigue siendo obligatorio; no se difiere por denominarlo riesgo.

| Tarea | Decisión de esta revisión |
| --- | --- |
| 8.1 | Completada: regresión pertinente, build y revisión funcional/seguridad efectuadas, con exclusiones y límites explícitos. No es cierre de 4.2. |
| 8.2 | Pendiente: estructura válida y comparación funcional realizadas; conformidad integral limitada por evidencia pendiente de D06/4.2. |
| 4.2 | Pendiente, sin nuevas ejecuciones concurrentes y sin aceptación automática de riesgos. |
| 8.3 | Pendiente y sin ejecutar. |

## Recomendación para preparar 8.3

Preparar únicamente la trazabilidad de requisitos, los límites de esta revisión y la lista de diferencias del delta respecto a la especificación principal. No sincronizar ni archivar mientras 4.2 y la conformidad integral de 8.2 permanezcan pendientes sin una decisión explícita que habilite otro flujo. La decisión de detener la investigación de concurrencia no equivale a aprobar sus garantías ni aceptar los riesgos. Toda sincronización requiere autorización separada; no se realizó aquí.
