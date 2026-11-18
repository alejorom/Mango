# Reconciliación de ProductAPI.Tests con main

Un único proyecto conserva sin cambios ProductContractTests/ProductTestData del checkpoint d35db64 y la suite de regresión de precios de main (448578e, 93bec07). Se mantienen las versiones y el GUID de solución de foundations, incluida su entrada AuthAPI.Tests. La integración solo incorpora el cambio productivo ya aprobado en main; no añade comportamiento productivo.

Las secciones siguientes conservan evidencia histórica de ambas iniciativas. Sus referencias a ramas, HEAD, ausencia de integración y bloqueos describen las ejecuciones originales. El resultado actual de la reconciliación se registra por separado: no se modifica ni se reclama completada ninguna tarea 3.2–3.5. La matriz completa de foundations sigue pendiente de /opsx:apply, incluso si las pruebas conservadas pasan tras integrar el arreglo.

## Resultado de esta reconciliación

SDK 11.0.100. Restore correcto tras repetir con permiso del entorno el intento bloqueado por sockets de NuGet (NU1301), sin cambiar paquetes ni configuración personal. Comando desde la raíz:

```powershell
dotnet test Mango.Services.ProductAPI.Tests/Mango.Services.ProductAPI.Tests.csproj --no-restore --configuration Release --logger 'trx;LogFileName=reconciled-product.trx' --results-directory Mango.Services.ProductAPI.Tests/TestResults
```

Release, código 0: 72/72 aprobados, cero fallos y cero omitidos. El TRX local ignorado descubre los 12 casos originales de ProductContractTests y los 60 de la regresión integrada: 40 Unit y 32 Integration. Las assertions originales de 0.99 y 1000.01 permanecen intactas y ahora pasan con el arreglo incorporado. Este resultado procede de una ejecución sin filtro; no representa nuevas ejecuciones separadas por categoría ni una repetición de la suite reconciliada.

Se verificó la igualdad de todos los archivos de prueba con sus ramas de origen y la entrada única del proyecto en solución, preservando AuthAPI.Tests y los GUID de foundations. OpenSpec estricto de foundations y de ProductAPI, y git diff --cached --check satisfactorios. El [delta archivado](../openspec/changes/archive/fix-product-price-range-validation/specs/product-api/spec.md) coincide con la especificación canónica incorporada desde main. Tasks de foundations queda intacto: 3.2–3.5 continúan pendientes de /opsx:apply.

Persisten los avisos heredados NU1903 de AutoMapper 16.1.1, nullability y directorio temporal del runner. No se ejecutaron SQL, migraciones, infraestructura externa, otras APIs ni suites del hardening. Los límites del host compuesto/InMemory y la cobertura HTTP pendiente de foundations siguen vigentes.

## Evidencia original del arreglo integrado

# Regresión del rango de precios de ProductAPI

Suite creada independientemente desde main para `fix-product-price-range-validation`; no se trasladaron archivos o commits de foundations/hardening. Referencia exclusiva a ProductAPI, net11.0, configuración de solución Debug/Release y versiones de testing aprobadas en D2.

## Evidencia previa a la corrección

SDK 11.0.100. Antes de cambiar producción se ejecutó:

```powershell
dotnet test Mango.Services.ProductAPI.Tests/Mango.Services.ProductAPI.Tests.csproj --no-restore --configuration Release --filter 'FullyQualifiedName~ObservedFractionalPricesAreRejected' --logger 'trx;LogFileName=price-range-before.trx' --results-directory Mango.Services.ProductAPI.Tests/TestResults
```

Resultado esperado: código 1, 4 casos ejecutados, 0 aprobados, 4 fallidos y 0 omitidos. Tanto ProductDto como Product aceptaban indebidamente 0.99 y 1000.01 en Validator.TryValidateObject; las cuatro assertions de rechazo fallaron. Evidencia local: `TestResults/price-range-before.trx`, ignorada por Git. La corrección no cambia las assertions ni los valores de entrada.

La restauración inicial falló por bloqueo de NuGet en el sandbox (NU1301); su repetición con permiso del entorno terminó con código 0. No se actualizaron paquetes ni configuración personal. Persisten avisos heredados NU1903 de AutoMapper 16.1.1, advertencias de nullability de ProductAPI y un aviso informativo del runner sobre su directorio temporal con este Test SDK.

## Validación unitaria posterior

Se cambiaron únicamente las dos anotaciones a `[Range(1d, 1000d)]`, manteniendo double y las assertions originales. `dotnet test` con `--filter Category=Unit`, Release y `--no-restore` finalizó con código 0: 28 aprobados, 0 fallidos y 0 omitidos. TRX local: `TestResults/price-range-unit.trx`. No se alteró cultura, tolerancias, validadores ni datos para hacer pasar la regresión.

| Grupo | Datos | Método Unit / resultado |
|---|---|---|
| B1 | -1, 0, 0.99, 1000.01, 1001 | `ObservedFractionalPricesAreRejected` para 0.99/1000.01 y `RangeMatrixValidatesOriginalValue` para los demás: rechazo por Price en DTO y entidad |
| B2 | Math.BitDecrement(1d), Math.BitIncrement(1000d) | `RangeMatrixValidatesOriginalValue`: vecinos exteriores rechazados |
| B3 | 1, 1000 | `RangeMatrixValidatesOriginalValue`: extremos inclusivos aceptados |
| B4 | 1.01, 999.99 | `RangeMatrixValidatesOriginalValue`: fracciones válidas aceptadas sin alterar precio |
| B5 | Name null, vacío, espacio; Price 30.25 | `RequiredNameRemainsEnforced`: rechazo por Name y precio válido conservado |

Cada fila se verifica sobre ProductDto y Product mediante Validator.TryValidateObject; todas comprueban que el precio original no cambia. Esto cubre los escenarios de entidad y la validación de payload del [delta product-api](../openspec/changes/fix-product-price-range-validation/specs/product-api/spec.md), con los límites de la [spec canónica](../openspec/specs/product-api/spec.md). No demuestra esquema, migraciones ni validación automática al guardar por EF Core. Las respuestas HTTP se verifican por separado en Integration.

## HTTP y trazabilidad de los límites

El host de `TestSupport/PriceApiFixture.cs` compone controladores, mapping, extensión JWT y autorización productivos con AddControllers, su serializador MVC predeterminado y MapControllers. Se ejecuta solo en `127.0.0.1:0`, con cliente sin proxy/redirecciones y límites de 30 segundos para arranque, parada y peticiones. Cada fixture libera host, cliente y contextos al terminar, incluso al fallar.

La configuración del builder se limpia antes de registrar dependencias y se reemplaza por ApiSettings:Secret/Issuer/Audience sintéticos de `LocalAdminJwt`. El helper emite JWT ADMIN locales sin invocar AuthAPI ni consumir sus archivos o pruebas. Se conservan el validador JWT y las opciones de ModelState productivos; ambos GET permanecen públicos.

Cada fixture dispone de nombre y raíz InMemory propios. Un contexto de inspección sin interceptor siembra dos productos de prueba y lee los resultados; los contextos del servidor comparten ese almacenamiento e incluyen `SaveAttemptCounter`, que cuenta intentos síncronos y asíncronos de guardar. Los rechazos comparan snapshots completos y exigen cero saves. Los casos válidos verifican una escritura, todos los campos devueltos/persistidos y conservación de productos ajenos al caso.

El helper HTTP comprueba el roundtrip exacto del double al número JSON antes de enviarlo. Las aceptaciones comparan los bits del precio recibido/devuelto/persistido, sin tolerancias. Para PUT se utiliza un producto existente; para POST un id nuevo. Los rechazos validan el error Price/Name en ValidationProblemDetails, sin exigir ResponseDto en los errores automáticos de ApiController.

| Escenarios / matriz | Método Integration | Casos |
|---|---|---:|
| Ambos GET públicos; requisito JWT vigente | `PriceApiAccessTests.CatalogReadsStayPublic` sin Authorization; `AnonymousWritesStillRequireAuthentication` para POST/PUT 401 sin efectos | 4 |
| DTO: fracciones fuera de rango, valores exteriores y vecinos representables; B1/B2 | `PriceApiBoundaryTests.OutOfRangePriceIsRejectedBeforeAnySave`: 400 con error de Price, estado idéntico y cero saves en POST/PUT | 14 |
| Missing product name; B5 | `RequiredNameIsRejectedBeforeAnySave`: null, vacío y espacio, 400 por Name, estado idéntico/cero saves en POST/PUT | 6 |
| Inclusive bounds / Valid fractional prices; B3/B4 | `AcceptedPriceIsPersistedWithoutRounding`: 200 para 1, 1000, 1.01, 999.99 en POST/PUT con JWT ADMIN, sin cambios ajenos | 8 |

Las ejecuciones dirigidas aprobaron 4/4 de acceso, 20/20 de rechazos y 8/8 de aceptaciones, con cero fallos/omisiones. TRX locales: `price-range-access.trx`, `price-range-rejections.trx` y `price-range-accepted.trx`. Category=Integration aprobó después los 32 casos juntos, con código 0, cero fallos y cero omisiones (`price-range-integration.trx`).

## Comandos reproducibles

Desde la raíz, con SDK compatible con .NET 11 y paquetes disponibles:

```powershell
dotnet --version
dotnet restore Mango.Services.ProductAPI.Tests/Mango.Services.ProductAPI.Tests.csproj
dotnet test Mango.Services.ProductAPI.Tests/Mango.Services.ProductAPI.Tests.csproj --no-restore --configuration Release --filter Category=Unit --logger 'trx;LogFileName=price-range-unit-final.trx' --results-directory Mango.Services.ProductAPI.Tests/TestResults
dotnet test Mango.Services.ProductAPI.Tests/Mango.Services.ProductAPI.Tests.csproj --no-restore --configuration Release --filter Category=Integration --logger 'trx;LogFileName=price-range-integration.trx' --results-directory Mango.Services.ProductAPI.Tests/TestResults
dotnet test Mango.Services.ProductAPI.Tests/Mango.Services.ProductAPI.Tests.csproj --no-restore --configuration Release --logger 'trx;LogFileName=price-range-all.trx' --results-directory Mango.Services.ProductAPI.Tests/TestResults
dotnet test Mango.Services.ProductAPI.Tests/Mango.Services.ProductAPI.Tests.csproj --no-restore --configuration Release --logger 'trx;LogFileName=price-range-repeat.trx' --results-directory Mango.Services.ProductAPI.Tests/TestResults
```

Las verificaciones finales pueden usar `--no-build` después de compilar esta misma versión en Release. Unit e Integration son traits explícitos de las clases descubiertas; el proyecto sin filtro solo contiene pruebas aisladas. Los TRX, binarios y obj permanecen ignorados por Git. No ejecutar la solución completa o suites de otras ramas para verificar este arreglo.

## Resultado final y criterios de aceptación

Verificación realizada con SDK 11.0.100 en Release. Las últimas ejecuciones usaron `--no-build --no-restore` sobre la misma compilación; todas terminaron con código 0. Se revisaron los contadores TRX, sin fallos ni omisiones:

| Ejecución | Aprobados / total | TRX local en TestResults |
|---|---:|---|
| Unit final | 28 / 28 | `price-range-unit-final.trx` |
| Integration | 32 / 32 | `price-range-integration.trx` |
| Proyecto completo | 60 / 60 | `price-range-all.trx` |
| Repetición del proyecto completo | 60 / 60 | `price-range-repeat.trx` |

| Criterio | Evidencia verificada |
|---|---|
| AC1 | Cuatro fallos esperados antes del arreglo, sobre ambos modelos; el archivo Unit conserva el mismo SHA256 después de la corrección: `F79F0D04F6F2BA9BB9E9A8CAD5EB2B13C1CA72460B2A8295E4CEDC30596C6F35`. |
| AC2 | Los 28 casos Unit cubren B1–B5 en DTO y entidad, sin cambiar el valor original. |
| AC3 | Los 32 casos Integration cubren acceso público, 401 anónimo y B1–B5 por POST/PUT; rechazos sin saves/cambios y aceptaciones sin redondeo. |
| AC4 | Categorías y suite completa con conteos positivos; repetición 60/60 sin contaminación observable. Las fixtures liberan host, cliente y contextos. |
| AC5 | Diff productivo limitado a dos atributos; proyecto independiente, referencia única a ProductAPI y registro en solución. Ningún canónico, archivo generado o cambio ajeno incluido; sin infraestructura externa. |
| AC6 | SDK, comandos, matrices y contadores registrados aquí y en tasks; `openspec.cmd validate fix-product-price-range-validation --strict` y `git diff --check` satisfactorios. Los archivos nuevos también pasaron revisión de whitespace. |

Al cerrar el incremento, la rama sigue en `b560b8b3d1e47698580cac9f9bd980c333d25b61`, igual que main. Se comprobaron intactas las referencias foundations `d35db642e949f7a12c2feb1684b23c5006e7d058` y hardening `f434fed0013ab8454a074017b78a20b3015abe38`; sus commits no son ancestros de esta rama. No se modifican ni se reclaman tareas de esas iniciativas. Los cambios quedan locales, sin staging, commits, push, integración, sync o archive.

## Alcance y límites de evidencia

Los requisitos modificados por el delta son Product domain model and persistence y Product payload validation rules. Se cubren sus escenarios de validación de entidad y precios del payload; el escenario histórico de esquema SQL/migración queda conservado en la spec pero fuera de esta verificación, que no ejecuta migraciones. La prueba explícita del atributo de entidad no implica que EF valide DataAnnotations automáticamente al guardar.

No se ejecutan Program.cs, SQL Server, EnsureCreated/Migrate, Swagger, redirección HTTPS, Azure Service Bus, Stripe ni otras APIs. No se solicitan tenants, cuentas, credenciales o secretos. No se configuran Newtonsoft.Json ni políticas MVC nuevas. El host acredita validación/pipeline reales sobre composición aislada, no el startup/DI completo, esquema SQL, collation, transacciones o concurrencia.

El arreglo no sanea datos históricos ni cambia double a decimal. El proyecto coincide por convención en nombre con el checkpoint de foundations, pero fue creado independientemente desde main; su futura reconciliación requerirá autorización y revisión, sin cerrar tareas de la otra rama aquí.

## Evidencia original del checkpoint foundations

# Pruebas de ProductAPI — incremento bloqueado

Alcance autorizado: tareas 3.1–3.5 de `establish-api-test-foundations`. Se creó este proyecto `net11.0`, con referencia exclusiva a ProductAPI y entrada única bajo Tests en `Mango.sln`, para Debug/Release. Usa las mismas versiones de Test SDK, xUnit, runner y EF InMemory que AuthAPI.Tests, sin referenciar ese proyecto ni cambiar paquetes existentes.

## Ejecución realizada

Desde la raíz del repositorio, SDK 11.0.100:

```powershell
dotnet restore Mango.Services.ProductAPI.Tests/Mango.Services.ProductAPI.Tests.csproj --verbosity minimal
dotnet test Mango.Services.ProductAPI.Tests/Mango.Services.ProductAPI.Tests.csproj --no-restore --configuration Release --filter Category=Unit --logger 'trx;LogFileName=unit.trx' --results-directory Mango.Services.ProductAPI.Tests/TestResults
```

La restauración finalizó con código 0 después de repetir con permiso del entorno el intento bloqueado por el sandbox (NU1301). La compilación Release fue correcta. El runner descubrió 12 casos Unit: 10 aprobados, 2 fallidos y 0 omitidos; `dotnet test` finalizó con código 1. El TRX local `TestResults/unit.trx` y los directorios bin/obj están ignorados por Git.

## Bloqueo de validación

La [spec product-api](../openspec/specs/product-api/spec.md), requisito Product payload validation rules y escenario Price out of range, exige rechazar precios inferiores a 1 o superiores a 1000. El DTO productivo declara `[Range(1, 1000)]` con límites enteros. Las pruebas usan Validator.TryValidateObject sobre el DTO real, sin sustituir sus atributos ni modificar cultura, opciones MVC o validadores:

| Caso Unit | Esperado por la spec | Observado |
|---|---|---|
| `ProductPriceOutsideRangeIsRejected(0.99)` | Validación falsa | Validación verdadera |
| `ProductPriceOutsideRangeIsRejected(1000.01)` | Validación falsa | Validación verdadera |

Se mantienen las assertions de rechazo: no se eliminan casos ni se cambia su expectativa para obtener una suite verde. La tarea 3.2 no se considera completada. Resolver la discrepancia entre la regla existente y la spec requiere una decisión/autorización fuera del alcance actual, que prohíbe modificar producción. No se ha comprobado el resultado HTTP de estos payloads; el hallazgo se limita a la validación del DTO ejercitada directamente.

## Trazabilidad implementada

| Requisito / decisión | Pruebas y resultado |
|---|---|
| Name requerido | `ProductNameIsRequired`: null, vacío y espacio rechazados; 3/3 aprobados |
| Price 1..1000 inclusive | `ValidProductAcceptsInclusivePriceLimits`: 1, 1000 y 42.5 aceptados; 3/3 aprobados |
| Price fuera de rango | `ProductPriceOutsideRangeIsRejected`: -1 y 0 rechazados; 0.99 y 1000.01 aceptados indebidamente; 2/4 aprobados |
| Mapping real y preservación de campos (D2 / tarea 3.2) | Configuración AutoMapper válida, DTO → entidad → DTO y actualización de entidad existente preservan identidad, nombre, precio, descripción, categoría e imagen; 2/2 aprobados |
| Proyecto individual y descubrimiento (D1 / tarea 3.1) | Una referencia a ProductAPI y una entrada de solución Debug/Release; 12 casos descubiertos |

## Pendientes y límites

Las tareas 3.2–3.5 permanecen pendientes. No se implementaron ni ejecutaron integraciones HTTP; no existen todavía casos Category=Integration ni evidencia de ejecución del proyecto sin filtro. Este README documenta el resultado parcial, no acredita la finalización de 3.5.

`TestSupport/ProductTestData.cs` contiene fixtures de producto y un helper JWT local para el host previsto por D3/D4; no invoca AuthAPI ni necesita su generador, login o suite. El helper aún no tiene verificación HTTP. La implementación posterior deberá conservar ambos GET del catálogo públicos, sin Authorization, y verificar ADMIN/401/403 en las escrituras con el validador productivo real; no se ha demostrado todavía esa matriz.

No se modificó código productivo, AuthAPI.Tests ni ShoppingCart/hardening. No se ejecutaron Program.cs, SQL Server, migraciones, Azure Service Bus, Stripe o pruebas de concurrencia. No se solicitaron ni configuraron cuentas, tenants, credenciales o secretos. Las claves y datos incluidos son sintéticos locales; no se hicieron llamadas reales a servicios externos.

Avisos heredados observados: NU1903 para AutoMapper 16.1.1 y advertencias de nullability en ProductAPI. El runner informa el uso de un directorio temporal privado por ausencia de VSTEST_ARTIFACTS_DIRECTORY con este Test SDK. No se actualizaron paquetes ni se alteró configuración personal para resolver esos avisos.
