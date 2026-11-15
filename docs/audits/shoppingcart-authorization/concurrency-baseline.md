# Línea base de concurrencia de ShoppingCartAPI

## Entorno y método

Fecha: 2026-11-15. Instancia SQL Server local predeterminada del equipo `22LAP5CD1195RMS`; base exclusiva `Mango_ShoppingCart_Concurrency_Test`. Cada conexión comprobó `DB_NAME()`, equipo e instancia antes de ejecutar operaciones. Se verificaron las tablas y la migración `20260529203002_addShoppingCartTables`; las tablas de negocio estaban vacías al inicio. La conexión del harness especificó el laboratorio y autenticación integrada, sin usar la configuración habitual de ShoppingCartAPI.

Se usó un harness temporal en `%TEMP%\mango-concurrency-baseline-20261115`, con `sqlcmd`, datos sintéticos, dos sesiones independientes por escenario, aislamiento predeterminado `READ COMMITTED`, `LOCK_TIMEOUT` de 6 segundos y timeout de comando de 12 segundos. La base tenía `READ_COMMITTED_SNAPSHOT` desactivado y snapshot isolation desactivado. Las esperas artificiales de uno y tres segundos hicieron reproducible el orden; los tiempos incluyen esas esperas y el inicio del proceso. No son una medición de rendimiento ni una prueba de carga.

El harness reproduce la secuencia de lecturas y escrituras relevante, no ejecuta los controladores ni EF Core. En particular, simula explícitamente una transferencia de propiedad con SQL directo, operación que no se observó como endpoint de ShoppingCartAPI; muestra qué ocurriría si otro escritor cambiara ese estado. Las inserciones duplicadas reproducen la comprobación de ausencia seguida de inserción; la prueba de eliminación reproduce el conteo previo y la eliminación de detalle y cabecera en una transacción de escritura, no la implementación completa de `SaveChanges`.

## Resultados observados

| Escenario | Condición inicial y sincronización | Resultado | Tiempos de sesiones |
|---|---|---|---|
| ApplyCoupon y cambio de propietario | Una cabecera sintética; A lee su ID y espera 3 s. B espera 1 s y cambia el propietario. A actualiza el cupón usando solo el ID. | Ambas escrituras afectaron una fila. El cupón quedó actualizado en la cabecera cuyo propietario había cambiado. La anomalía se reprodujo en el harness SQL; no se ejecutó el endpoint. | A 3.285 s; B 1.286 s. |
| Creación simultánea de carrito | Dos sesiones comprueban que no existe cabecera para el mismo usuario y esperan 3 s antes de insertar. | Ambas terminaron correctamente; quedaron dos cabeceras para el mismo usuario. | 3.274 s y 3.277 s. |
| Inserción simultánea de producto | Una cabecera sintética; ambas sesiones comprueban ausencia del mismo producto y esperan 3 s antes de insertar. | Ambas terminaron correctamente; quedaron dos detalles con el mismo producto y cabecera. | 3.218 s y 3.217 s. |
| Último detalle frente a inserción | A cuenta un detalle y espera 3 s; B espera 1 s e inserta otro. A elimina el original y la cabecera en una transacción, basándose en el conteo previo. | B informó inserción exitosa; A eliminó la cabecera y el detalle recién insertado desapareció por cascada. El resultado final fue cero cabeceras y cero detalles para ese carrito. | A 3.271 s; B 1.284 s. |
| Bloqueo controlado | A actualiza una cabecera dentro de una transacción, espera 3 s y hace rollback. B espera 1 s e intenta actualizar la misma fila. | Ambas sesiones terminaron correctamente; B actualizó después de que A liberó el bloqueo. No hubo timeout ni deadlock en esta ejecución. La espera de B es consistente con bloqueo, pero no se capturaron eventos ni DMV de espera. | A 3.230 s; B 3.215 s. |

Los planes estimados con tablas vacías mostraron `Clustered Index Scan` para la búsqueda de cabecera por `UserId` y `Index Seek` sobre `IX_CartDetails_CartHeaderId` para contar detalles. Son planes estimados para esas consultas y condiciones; no demuestran el plan de producción ni la extensión de bloqueos bajo otras cargas.

## Limpieza y límites

Cada harness eliminó únicamente cabeceras identificadas por sus propios GUID sintéticos; la FK eliminó sus detalles por cascada. La verificación final encontró cero cabeceras y cero detalles; la base de laboratorio y `Mango_ShoppingCart` estaban `ONLINE`. No se dirigieron escrituras a la base original; su estado no descarta actividad de otros procesos.

Se confirmaron las anomalías descritas únicamente para las secuencias SQL controladas, con una ejecución por escenario. No se reprodujeron deadlocks, timeouts, incrementos perdidos, cambios de propietario durante inserción, ni se verificaron garantías bajo `Serializable`; siguen siendo riesgos o preguntas de pruebas posteriores. No se midieron planes con volumen real ni se inspeccionaron esperas en DMV. No se aplicó ninguna corrección y la tarea OpenSpec 4.2 permanece pendiente.
