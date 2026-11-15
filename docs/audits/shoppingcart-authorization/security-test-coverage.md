# Matriz de cobertura de seguridad — tareas 7.1 y 7.2

## Corrección del fallo de ProductAPI con carrito vacío

La prueba `ProductFailureDoesNotSucceedForCartWithoutItems` se ejecutó antes de modificar el cliente y reprodujo el fallo. ProductService convertía el sobre HTTP 200 con `IsSuccess = false` en una lista vacía; GetCart no encontraba productos sin resolver cuando el carrito no tenía detalles y devolvía éxito. El cliente ahora propaga el fallo mediante una excepción de mensaje genérico; una respuesta válida con lista vacía sigue devolviendo lista vacía. El delta de ProductAPI refleja esa distinción.

Las pruebas posteriores comprueban el carrito vacío válido, el error con y sin detalles, fallos HTTP y de transporte, respuestas genéricas y ausencia de publicaciones en EmailCartRequest. La prueba antes fallida pasó. La batería funcional y de seguridad excluyendo concurrencia terminó con **64 aprobadas y 0 fallidas**; las pruebas filtradas de GetCart, correo y ProductFailure sumaron 17 aprobadas. No se utilizaron servicios externos ni mensajes reales. 8.1, 8.2 y 4.2 permanecen pendientes; la validación estructural no acredita las garantías concurrentes.

## Cobertura adicional de fallos de ProductAPI

`ProductFailureIntegrationTests` ejercita el cliente real con un HTTP 200 cuyo sobre indica `IsSuccess = false` y con una excepción de transporte sintética. Para un carrito con detalle, GetCart devuelve `IsSuccess = false`; EmailCartRequest devuelve 503 o 500 respectivamente, sin publicar, filtrar el detalle sintético ni modificar datos. Ambas pruebas pasaron.

**Defecto reproducido:** con una cabecera persistida sin detalles, ProductService transforma el sobre fallido en una lista vacía y GetCart solo marca fallo si hay identificadores de producto no resueltos. Al no existir detalles, devuelve `IsSuccess = true` aunque ProductAPI haya fallado. `ProductFailureDoesNotSucceedForCartWithoutItems` reproduce el resultado y permanece fallida. No se modificó código productivo. Resolverlo requiere decidir cómo distinguir un sobre fallido de una lista válida vacía, puesto que el delta actual documenta que el cliente devuelve una lista vacía para ese caso; cualquier cambio requiere autorización y alineación OpenSpec.

La batería funcional y de seguridad, excluyendo concurrencia 4.2, terminó con **62 aprobadas y 1 fallida**. La compilación, validación estricta OpenSpec y `git diff --check` pasaron. Las tareas 8.1 y 8.2 siguen pendientes; la validación estructural no verifica este comportamiento ni las garantías de 4.2.

## Verificación consolidada parcial de 8.1 y 8.2

Se ejecutó una batería filtrada de autenticación, los cinco controladores, integración HTTP MVC, clientes descendentes y JWT real: **60 aprobadas, 0 fallidas**. Las escrituras usaron exclusivamente el laboratorio SQL aislado y usuarios sintéticos; se excluyeron las pruebas de concurrencia. Compilación de los proyectos afectados, `openspec validate harden-shoppingcart-authorization --strict` y `git diff --check`: satisfactorios. La compilación mantiene advertencias NU1903 de AutoMapper.

- **GetCart:** pruebas de propietario, ADMIN, ajeno e inexistente, 400/404 indistinguibles y excepciones 500 genéricas; no hay llamadas descendentes en los rechazos verificados.
- **ApplyCoupon, CartUpsert y RemoveCart:** propietario y rechazos por propiedad/identificadores cubiertos con SQL aislado; las pruebas verifican estado sin cambios y ausencia de publicación cuando corresponde.
- **EmailCartRequest:** payload reconstruido, claim `email` del JWT validado, publicación del propietario y ausencia de publicaciones para rechazos y fallos de dependencias, con bus simulado.
- **JWT y clientes:** middleware real cubre 401; se verifican 400/404, JWT firmado y propagación, errores HTTP de Product/Coupon y 404 de cupón. La implementación de ProductAPI para HTTP 200 con `IsSuccess = false` se contrastó por inspección, pero no se identificó una prueba dedicada de ese caso ni de fallo de transporte real del cliente ProductService.
- **Delta:** los cuatro requisitos MODIFIED conservan los nombres de todos sus escenarios de la especificación principal; los escenarios de error se actualizaron en el delta. Las rutas, DTO y forma de los mensajes se conservaron. La especificación principal sigue sin sincronizar.

Las tareas **8.1 y 8.2 permanecen pendientes**: la verificación estructural no acredita concurrencia. En 4.2 siguen sin verificarse todas las intercalaciones necesarias para autorización, rollback y limpieza segura de cabecera; la prueba sincronizada falló con deadlock 1205 y una repetición terminó en timeout. La unicidad global y las mejoras avanzadas siguen diferidas. No se ejecutó ni modificó 4.2 en esta fase.

La matriz relaciona criterios con pruebas existentes y distingue las pruebas HTTP con JWT validado de las invocaciones directas al controlador. Los resultados de ejecución se registran al finalizar la batería; no cubre las carreras de 4.2.

| Criterio | Evidencia automatizada | Alcance |
|---|---|---|
| JWT ausente, inválido, vencido o sin subject en los cinco endpoints | `AuthenticationTests.CartEndpointsRejectInvalidIdentitiesBeforeControllerActivation` | HTTP y middleware real; rechazo previo a activación del controlador. |
| GetCart propio, ajeno, inexistente, malformado y ADMIN | `GetCartAuthorizationTests` | Controlador; composición, 400/404 y equivalencia, cero llamadas descendentes en rechazos. |
| ApplyCoupon propio, ajeno, inexistente, malformado y ADMIN | `ApplyCouponAuthorizationTests` | Controlador y SQL aislado; cambios y rechazos sin escrituras ni publicaciones. |
| CartUpsert propio, ajeno, ADMIN e identificadores anidados | `CartUpsertAuthorizationTests` | Controlador y SQL aislado; creación, incrementos, 400/404 y estado sin cambios ante rechazos. |
| RemoveCart propio, ajeno, inexistente, malformado y ADMIN | `RemoveCartAuthorizationTests` | Controlador y SQL aislado; preservación de registros y cero escrituras/publicaciones rechazadas. |
| EmailCart propio, ajeno, inexistente, malformado y ADMIN | `EmailCartAuthorizationTests` | Controlador; 400/404, payload reconstruido, destinatario JWT y bus falso. |
| Sin consultas externas antes de autorización de correo | `EmailCartAuthorizationTests.RejectedOwnershipAndMalformedEmailRequestsNeverCallDependencies` | Fakes con contadores, rechazos 400/404, estado sin cambios. |
| Fallos Product/Coupon y destinatario no disponible | `EmailCartAuthorizationTests`, `WebCompatibilityTests`, `RealJwtWebIntegrationTests` | Fakes y clientes controlados; no publicar en fallo. |
| JWT real y propagación | `AuthenticationTests`, `RealJwtWebIntegrationTests` | Generador AuthAPI y JWT Bearer real; Product/Coupon simulados capturan el token. |
| Integración HTTP de Web | `MvcHttpIntegrationTests` | Cookies, HTTP y SQL aislado; login de AuthAPI simulado, bus falso. |

Las pruebas de autenticación prueban rechazo antes de construir el controlador; las pruebas directas comprueban específicamente el bus y estado en rechazos del controlador. La matriz no afirma pruebas de todas las permutaciones posibles ni garantías de concurrencia.

## Ejecución y resultado

Se ejecutaron 58 pruebas filtradas de autenticación, autorización por endpoint, correo, clientes controlados y JWT real: 58 aprobadas, ninguna fallida. Las pruebas relacionales usaron únicamente la base SQL Server aislada con guardas y datos sintéticos; no se utilizó Azure Service Bus. Se añadió la prueba de contadores para rechazo de correo, sin duplicar las pruebas existentes. Compilación y validación estricta OpenSpec satisfactorias; `git diff --check` sin errores. Las advertencias NU1903 de AutoMapper ya existían. La tarea 4.2 y las pruebas de concurrencia permanecen fuera del alcance de esta ejecución.

## Revisión parcial de 8.1 y 8.2

La validación estructural estricta del cambio pasó. La revisión se detuvo antes de volver a ejecutar pruebas o compilar al encontrar un bloqueo de exposición de errores, conforme a las instrucciones de la intervención; las 58 pruebas anteriores no son una ejecución nueva. Las tareas 8.1 y 8.2 permanecen pendientes.

- **GetCart — bloqueo estático:** `Mango.Services.ShoppingCartAPI/Controllers/CartAPIController.cs:94-98` devuelve `ex.Message` en HTTP 500. La especificación principal `openspec/specs/shopping-cart-api/spec.md:206-220` exige un mensaje genérico sin detalles de excepciones; `AGENTS.md` también prohíbe exponer detalles internos. El texto de una excepción SQL, JSON o de comunicación podría llegar al cliente. No se ha reproducido una filtración concreta en esta intervención; requiere corrección y prueba autorizadas.
- **Diferencias entre especificación y clientes descendentes:** `openspec/specs/shopping-cart-api/spec.md:163-166` describe lista vacía sin excepción ante fallo de ProductAPI, y `:181-184` describe null sin excepción ante fallo de CouponAPI. `ProductService.GetProducts` y `CouponService.GetCoupon` ahora lanzan excepciones ante errores HTTP; el delta activo no modifica esos requisitos. Se requiere decidir y formalizar la semántica correcta mediante OpenSpec, conservando el tratamiento aprobado de errores y el contrato público.
- **Trazabilidad de destinatario:** `proposal.md` aún presenta la fuente del destinatario como decisión pendiente, mientras `design.md` registra la aprobación del claim email del JWT. Es una inconsistencia documental que requiere alineación antes del cierre.
- **Concurrencia 4.2:** siguen sin acreditarse rollback, autorización y limpieza segura en todas las intercalaciones necesarias; la última prueba sincronizada falló con deadlock 1205 y una repetición terminó en timeout. No se retomó esa investigación.

La matriz anterior proporciona evidencia para los cinco endpoints, JWT, propiedad, respuestas 400/404 y publicación controlada en condiciones no concurrentes. La revisión no certifica ausencia de información sensible en todos los fallos ni conformidad completa de todos los escenarios; quedan pendientes la batería y compilación de esta fase y una comparación final tras resolver los bloqueos.

## Corrección acotada de errores de GetCart

El bloqueo de `ex.Message` descrito arriba se corrigió: GetCart responde HTTP 500 con `IsSuccess = false` y `An unexpected error occurred`; registra el tipo de error con `ILogger`, sin registrar la excepción ni su mensaje. `GetCartAuthorizationTests.UnexpectedFailureReturnsGenericErrorAndLogsWithoutExceptionDetails` inyecta una excepción con detalle sintético y verifica respuesta y registro. Las pruebas filtradas de GetCart y autenticación pasaron: 4/4, incluyendo rechazos 400, 401 y 404. El primer intento en sandbox falló en el servidor de autenticación por permisos del registro de eventos; el intento fuera del sandbox pasó.

### Discrepancias documentales pendientes

- `openspec/specs/shopping-cart-api/spec.md`, requisito **Downstream integration with ProductAPI**, escenario **ProductAPI unavailable or returns failure**: afirma que toda indisponibilidad o respuesta fallida produce lista vacía sin excepción. `ProductService` devuelve lista vacía para un sobre exitoso con `IsSuccess = false`, pero lanza ante fallos HTTP y de comunicación. Propuesta mínima: delta MODIFIED del requisito que distinga ambos casos y describa el error controlado observado en el controlador; conservar los demás escenarios.
- Misma especificación, requisito **Downstream integration with CouponAPI**, escenario **CouponAPI unavailable or code not found**: agrupa indisponibilidad y cupón ausente como null sin excepción. `CouponService` devuelve null para HTTP 404 y lanza ante otros fallos HTTP, comunicación o respuestas inválidas. Propuesta mínima: delta MODIFIED que separe 404 de fallo de dependencia y preserve los demás escenarios.
- `openspec/changes/harden-shoppingcart-authorization/proposal.md`, **What Changes**, presenta la fuente de destinatario como pendiente; `design.md`, decisión **D05**, registra la aprobación del claim `email` del JWT validado. El delta activo **Request cart email via message bus** admite una fuente aprobada sin fijarla. Propuesta mínima: actualizar la propuesta para reflejar la decisión y mantener el delta coherente con D05 y la ausencia de publicación si el claim no es utilizable.

No se modificaron las especificaciones principales ni los artefactos del cambio. Las tareas 8.1 y 8.2 siguen pendientes, al igual que las garantías de 4.2 documentadas anteriormente.
