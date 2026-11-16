# Diagnóstico final de concurrencia: tarea 4.2

## Alcance y estado

Este documento conserva el diagnóstico realizado en la conversación, sin nuevas pruebas ni acceso a SQL Server. La tarea 4.2 permanece pendiente: la evidencia disponible no acredita todas las garantías obligatorias. Las 28 pruebas históricas anteriores al último caso sincronizado y las 64 pruebas funcionales posteriores que excluyeron concurrencia no resuelven ese caso fallido. Las tareas 8.1–8.3 también siguen pendientes.

Las fuentes de este diagnóstico son [la propuesta](../../../openspec/changes/harden-shoppingcart-authorization/proposal.md), [el diseño](../../../openspec/changes/harden-shoppingcart-authorization/design.md), [las tareas](../../../openspec/changes/harden-shoppingcart-authorization/tasks.md), [el delta de especificación](../../../openspec/changes/harden-shoppingcart-authorization/specs/shopping-cart-api/spec.md), la [línea base de concurrencia](concurrency-baseline.md), la [verificación parcial](concurrency-verification.md), la [cobertura de seguridad](security-test-coverage.md) y el [laboratorio SQL Server](sqlserver-lab.md). También se inspeccionaron [el controlador](../../../Mango.Services.ShoppingCartAPI/Controllers/CartAPIController.cs), [las pruebas de concurrencia](../../../Mango.Services.ShoppingCartAPI.Tests/SqlServerConcurrencyTests.cs) y la configuración EF Core.

## Garantías obligatorias y evidencia

El diseño exige condicionar las escrituras efectivas al propietario autenticado y comprobar las filas afectadas; proteger la inserción de detalles frente a cambios concurrentes de propietario; mantener atómicas las operaciones compuestas y revertir sus fallos; impedir que la limpieza de una cabecera elimine por cascada una inserción concurrente confirmada; y devolver errores seguros sin revelar propiedad ni detalles SQL.

La evidencia acredita casos concretos, no todas las intercalaciones: ApplyCoupon devuelve 404 sin cambiar el cupón cuando el propietario cambia antes del UPDATE; una prueba sincronizada observó que una transferencia esperaba mientras un upsert mantenía protegida la lectura; y pasaron pruebas de flujos secuenciales y rechazo por propiedad. La prueba de eliminación frente a inserción que pasó no forzó el punto crítico.

Quedan pendientes la verificación de rollback después de una escritura efectiva, la limpieza segura en las intercalaciones relevantes, la inserción concurrente determinista del mismo producto y el tratamiento completo de conflictos y liberación. La falla sintética de inserción existente puede impedir todo el lote antes de la primera escritura y no demuestra rollback parcial.

## Defectos y observaciones

La línea base reprodujo mediante SQL controlado una actualización posterior a un cambio de propietario, duplicados de cabeceras y detalles, y la pérdida por cascada de un detalle insertado concurrentemente. El harness no ejecutaba el controlador ni EF Core; estos resultados no prueban que los mismos defectos persistan en el controlador actual.

Por inspección del controlador actual:

- ApplyCoupon selecciona la primera cabecera sin detectar duplicados ambiguos, aunque su UPDATE se limita a una cabecera y al propietario.
- RemoveCart no comprueba las filas afectadas en la limpieza de la cabecera. Cero puede ser correcto si quedan detalles; esta omisión no demuestra por sí sola pérdida de datos.
- CartUpsert y RemoveCart utilizan transacciones Serializable selectivas. Las escrituras existentes de detalle incluyen predicados de propietario y comprobación de filas afectadas; la inserción se apoya en la comprobación de propiedad protegida por la transacción.

No existe evidencia de pérdida por cascada ni de escritura ajena en el controlador actual. La batería afectada por incompatibilidad de EF Core InMemory se migró a SQL Server; sus resultados anteriores no sustituyen la verificación de concurrencia pendiente.

## Deadlock 1205 y timeout

**Hechos documentados:** una prueba sincronizada detuvo RemoveCart después de eliminar el detalle mientras otra sesión insertaba directamente con EF Core. SQL Server notificó deadlock 1205 y seleccionó la inserción competidora como víctima. Una repetición terminó con timeout durante liberación o rollback. No se capturó el grafo de deadlock ni hay detalle suficiente para establecer la cadena exacta o atribuir el timeout a un mecanismo concreto. La prueba no permitió acreditar completamente el estado final de ambas operaciones. SQL Server revierte la transacción de la víctima de un 1205; el error por sí solo no demuestra corrupción. Véase la [guía de deadlocks de Microsoft](https://learn.microsoft.com/en-us/sql/relational-databases/sql-server-deadlocks-guide?view=sql-server-ver17).

**Hipótesis:** un ciclo entre bloqueos de detalles o rangos y cabecera o clave foránea durante la limpieza es compatible con el escenario, pero no está demostrado. La prueba usa inserción directa, no CartUpsert. Ante ciertas excepciones puede iniciar la limpieza sin haber esperado ambas tareas; `WaitAsync` limita la espera y no cancela por sí mismo la operación original. Esto podría interferir con la liberación, pero no demuestra la causa del timeout observado. Véase la [documentación de WaitAsync](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.waitasync).

## Corrección mínima recomendada

Conservar el mecanismo transaccional selectivo y completar sus comprobaciones y su verificación:

1. Detectar cabeceras ambiguas en ApplyCoupon y rechazar antes de escribir con error genérico.
2. Comprobar la limpieza de RemoveCart: una cabecera eliminada, o cero con cabecera propia conservada y detalles restantes. Abortar la operación ante un resultado incoherente.
3. Mantener los predicados de propietario, la condición de ausencia de detalles, el commit único y el rollback al salir sin commit.
4. Registrar de forma segura el código SQL y la fase del fallo, incluida la liberación; conservar HTTP 500 genérico y evitar reintentos automáticos.
5. Corregir la sincronización, las aserciones y el ciclo de vida del harness antes de atribuir sus fallos al controlador.

Mantener Serializable en CartUpsert y RemoveCart protege la comprobación de propiedad hasta insertar y la secuencia de eliminación y limpieza. Rebajar el aislamiento exigiría otra protección equivalente. Sin el grafo no hay evidencia suficiente para prescribir un cambio del orden de bloqueos. Las operaciones relacionales múltiples requieren una transacción común; véase [EF Core ExecuteUpdate y ExecuteDelete](https://learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete).

La estrategia es defendible sin cambios de esquema, migraciones ni dependencias, condicionada a que supere las verificaciones. El rollback implícito existente no se ha demostrado defectuoso.

## Archivos previstos para una implementación posterior

- `Mango.Services.ShoppingCartAPI/Controllers/CartAPIController.cs`: comprobaciones y diagnóstico seguro.
- `Mango.Services.ShoppingCartAPI.Tests/SqlServerConcurrencyTests.cs`: sincronización, rollback parcial y espera de tareas antes de limpiar.
- `Mango.Services.ShoppingCartAPI.Tests/ApplyCouponAuthorizationTests.cs`: rechazo de cabeceras ambiguas.
- `docs/audits/shoppingcart-authorization/concurrency-verification.md`: resultados posteriores y sus límites.

No se prevén cambios de DbContext, esquema o dependencias. Los artefactos OpenSpec se actualizarían posteriormente mediante el flujo autorizado.

## Batería mínima propuesta

| Caso | Evidencia exigida |
| --- | --- |
| Propiedad concurrente | En cupón, inserción o incremento y eliminación, una transferencia anterior produce rechazo; una posterior espera o produce conflicto controlado, sin escritura ajena. |
| Mismo producto concurrente | Intercalación sincronizada; efectos persistidos correspondientes a operaciones confirmadas y ninguna escritura parcial de operaciones abortadas. No se acredita unicidad global. |
| Cabecera frente a inserción | Ambos órdenes con controladores reales y el escritor directo del caso fallido; ninguna inserción confirmada desaparece por cascada. |
| Rollback parcial | Inyectar fallo después de una escritura ejecutada y antes del commit en upsert y después de eliminar un detalle; comprobar restauración completa desde otro contexto. |
| Recursos ajenos y ambigüedad | Estado ajeno intacto, 404 equivalentes y rechazo de cabeceras duplicadas sin modificación. |
| Deadlock y timeout | Conflictos controlados, respuesta genérica, rollback comprobado y sesiones liberadas; no aceptar cualquier excepción como éxito. |

Usar exclusivamente `Mango_ShoppingCart_Concurrency_Test`, verificando equipo e instancia por conexión, con datos sintéticos y limpieza por identificadores. Evitar iniciar el `Program` productivo, que contiene migración automática.

**Límites propuestos:** máximo dos ejecuciones por caso y una regresión final; barreras de 5 segundos, comandos de 10 segundos y límite total de 30 segundos por caso. Liberar barreras, cancelar y esperar ambas tareas antes de limpiar. Detener inmediatamente ante tareas persistentes, rollback incierto o pérdida de integridad; no repetir sin diagnóstico. Una segunda falla equivalente detiene la campaña. Una ejecución aprobada no demuestra ausencia universal de deadlocks.

## Riesgos residuales y decisión

El diseño difiere a `harden-shoppingcart-concurrency-integrity` la unicidad global, investigación y remediación de duplicados, incrementos atómicos entre escritores, coordinación avanzada de inserciones y estrategias avanzadas de reintento. Persisten riesgos de contención por `UserId` sin índice, deadlocks y resultados ambiguos si se pierde la confirmación de commit; su aceptación requiere decisión explícita. El diferimiento no autoriza escrituras ajenas ni cascadas inseguras.

**Decisión recomendada:** implementar la corrección acotada tras autorización. No se ha demostrado un bloqueo técnico que obligue a rediseñar o modificar el esquema. Si la verificación vuelve a dejar rollback o integridad indeterminados, detener y conservar 4.2 pendiente. Completar 4.2 no completa automáticamente las tareas 8.1–8.3.
