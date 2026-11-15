# Matriz de cobertura de seguridad — tareas 7.1 y 7.2

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
