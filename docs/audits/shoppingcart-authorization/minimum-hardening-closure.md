# Propuesta de cierre mínimo del hardening de ShoppingCartAPI

## Estado y decisión solicitada

**Propuesta para aprobación; no modifica los criterios actuales ni completa la tarea 4.2.** Recomiendo conservar en este cambio las garantías de autorización, atomicidad y limpieza segura, acreditarlas con una regresión SQL acotada y diferir el fortalecimiento integral de concurrencia a `harden-shoppingcart-concurrency-integrity`. Ese cambio posterior aún no existe.

**No recomiendo cerrar 4.2 todavía.** Hay una prueba sincronizada fallida, un deadlock 1205 y un timeout históricos cuya repercusión sobre el resultado completo no quedó acreditada. La última carrera del mismo producto también falló y el resumen disponible no identifica la causa. Estos hechos impiden aceptar como demostrados el tratamiento seguro de conflictos y la limpieza concurrente; no prueban por sí solos una escritura ajena ni pérdida de datos en el controlador actual.

Fuentes: [diseño del cambio](../../../openspec/changes/harden-shoppingcart-authorization/design.md), [tarea 4.2](../../../openspec/changes/harden-shoppingcart-authorization/tasks.md), [delta de seguridad](../../../openspec/changes/harden-shoppingcart-authorization/specs/shopping-cart-api/spec.md), [especificación principal](../../../openspec/specs/shopping-cart-api/spec.md), [diagnóstico final](concurrency-final-diagnosis.md) y [evidencia de concurrencia](concurrency-verification.md). Esta propuesta usa evidencia existente; no ejecuta nuevas pruebas ni SQL.

## Garantías que permanecen en el cambio actual

| Garantía indispensable | Estado de la evidencia |
| --- | --- |
| Escrituras de ApplyCoupon, CartUpsert y RemoveCart condicionadas al propietario en el momento efectivo; control de filas afectadas y 404 indistinguibles cuando corresponde. | Hay predicados y pruebas de rechazo; un cambio de propietario antes del UPDATE de cupón produjo 404 sin modificación. Falta cobertura de todas las intercalaciones necesarias. |
| Propiedad protegida hasta insertar un detalle y referencias anidadas válidas hasta la escritura. | Serializable selectivo y una prueba de transferencia bloqueada ofrecen evidencia parcial; no acreditan todos los casos concurrentes. |
| Operaciones compuestas atómicas y rollback ante fallo conocido antes del commit. | Pasaron dos casos con fallo inducido antes del commit: upsert no dejó cabecera ni detalle; remove restauró ambos. Esto no cubre todos los fallos ni confirma el resultado de un commit cuya respuesta se perdió. |
| Eliminar el último detalle y limpiar la cabecera sin borrar por cascada una inserción concurrente confirmada. | Hay predicado de cabecera vacía y comprobación de resultado. La prueba sincronizada relevante falló; una prueba anterior no sincronizada pasó. No está acreditada la intercalación crítica. |
| Fallos y conflictos con respuesta genérica, sin exponer propiedad ni detalles internos, sin reintentos automáticos de operaciones ambiguas. | El controlador devuelve 500 genérico y registra código SQL o tipo de error; falta acreditar el resultado y la liberación ante conflictos reales. |
| Detectar cabeceras ambiguas al aplicar cupón sin modificar varias filas. | Implementación y prueba de duplicados existentes aprobada; no equivale a una restricción de unicidad global. |

La autorización de propietario, la integridad de recursos ajenos, el rollback de fallos conocidos y la prevención de cascadas inseguras no son candidatos a diferimiento. Las tareas y escenarios de autenticación, correo y compatibilidad existentes tampoco se eliminan mediante esta propuesta.

## Bloqueos y defectos confirmados

1. **Fallo de verificación confirmado:** `ConcurrentSameProductPreservesCommittedWrites` falló una vez. No se dispone del detalle de la aserción ni del código SQL; no puede atribuirse a deadlock, timeout o pérdida de incremento. No se ha demostrado que este fallo sea un defecto productivo específico.
2. **Conflictos confirmados en una prueba anterior:** SQL Server notificó 1205 en la carrera de eliminación e inserción y hubo un timeout posterior durante liberación o rollback. Sin grafo ni estado completo no se puede determinar la secuencia exacta ni descartar una regresión en el manejo de conflictos. El deadlock por sí solo no demuestra corrupción.
3. **Verificación incompleta del harness:** la cancelación de operaciones del controlador y la finalización de tareas que exceden el límite no están acreditadas. Sin ese control la limpieza de pruebas y su interpretación pueden ser inciertas.

No se ha confirmado una escritura sobre un carrito ajeno ni una pérdida por cascada en el controlador actual. Las anomalías de la línea base se reprodujeron con secuencias SQL controladas, no con la implementación actual completa. La ausencia de evidencia suficiente es un bloqueo de aceptación, no un defecto productivo confirmado.

## Mejoras diferidas y consecuencias

| Tema | Tratamiento propuesto | Consecuencia residual |
| --- | --- | --- |
| Incremento calculado en memoria y escrito después | Diferir garantía global de incremento atómico y análisis de todos los escritores. Serializable protege las operaciones participantes, pero no se ha demostrado una garantía global. | Pueden perderse incrementos en escenarios no cubiertos o con escritores que no respeten el mismo protocolo; también pueden aparecer errores por contención. No hay pérdida de incremento reproducida en el controlador actual. |
| Ausencia de unicidad por `UserId` | Diferir restricción, migración e investigación y reparación de duplicados. La detección en ApplyCoupon no impone unicidad. | Pueden existir varios carritos propios y lecturas o upserts pueden seleccionar uno; el cupón ambiguo se rechaza. Los duplicados se reprodujeron en la línea base SQL, no se ha demostrado unicidad del sistema completo. |
| Unicidad por producto y coordinación integral de inserciones | Diferir restricciones, coordinación universal y reparación de duplicados. | Pueden aparecer detalles duplicados o resultados inconsistentes con otros escritores; la carrera reciente fallida no establece su causa ni demuestra duplicados actuales. |
| Bloqueos, deadlocks y reintentos avanzados | Diferir prevención exhaustiva, optimización e ingeniería de reintentos. Mantener manejo seguro de conflictos como requisito actual. | Puede haber esperas, errores y menor disponibilidad; un commit con confirmación perdida deja resultado ambiguo. No reintentar automáticamente incrementos no idempotentes. |
| `UserId` sin índice y cascada | Diferir optimización y cambios de esquema. Conservar la protección contra cascadas inseguras ahora. | Bloqueos amplios y contención; si la protección de limpieza falla, una inserción confirmada podría perderse por cascada, como mostró el harness de línea base. |

La [especificación principal](../../../openspec/specs/shopping-cart-api/spec.md) describe un carrito por usuario y un detalle por producto. La decisión de diferir su garantía global bajo concurrencia no la elimina ni acredita su cumplimiento universal: requiere aceptación explícita del riesgo y trazabilidad al cambio posterior, sin presentar la conducta secuencial existente como garantía global.

## Regresión mínima propuesta

Reutilizar el proyecto y las pruebas existentes; no añadir pruebas de estrés. Ejecutar solo después de corregir la identificación del fallo y garantizar que el harness libere barreras, observe y espere las tareas antes de limpiar. Un timeout que deje una tarea o rollback inciertos detiene la campaña.

1. Ejecutar las pruebas existentes de autorización y rechazo de ApplyCoupon, CartUpsert y RemoveCart: propietario, ajeno, inexistente, ADMIN, referencias anidadas y estado ajeno inalterado; incluir `DuplicateOwnedHeadersRejectCouponWithoutMutation`.
2. Reutilizar `CouponUpdateRejectsOwnershipChangedAfterRead` y `OwnershipChangeWaitsUntilUpsertCompletes` para los puntos de propiedad; comprobar respuesta y estado persistido, no solo que una tarea espere.
3. Reutilizar `UpsertFailureBeforeCommitRollsBackPersistedWrites` y `RemovalFailureBeforeCommitRestoresHeaderAndDetail` para fallo después de escrituras y antes de commit, con verificación desde otro contexto.
4. Resolver primero el resultado desconocido de `ConcurrentSameProductPreservesCommittedWrites`; comprobar los efectos correspondientes a operaciones confirmadas y que las abortadas no dejen escritura parcial. Un 500, deadlock o timeout no es aprobación automática ni prueba de unicidad global.
5. Reutilizar la prueba sincronizada de último detalle frente a inserción y la carrera entre controladores, comprobando ambos órdenes relevantes: ninguna inserción confirmada desaparece, y un conflicto deja respuesta genérica y estado atómico verificable. La prueba con inserción directa también debe observar el resultado real de ambas operaciones.
6. Ejecutar compilación, pruebas pertinentes, validación estricta OpenSpec y `git diff --check`; registrar resultados reales y límites. Comprobar ausencia de exposición de mensajes SQL y equivalencia de rechazos.

Las pruebas relacionales deben usar exclusivamente `Mango_ShoppingCart_Concurrency_Test`, con validación de equipo, instancia y base por conexión, datos sintéticos y limpieza delimitada; no iniciar el `Program` productivo ni migrar. Aplicar los límites del [diagnóstico final](concurrency-final-diagnosis.md): máximo dos ejecuciones por caso y una regresión final, barreras de 5 segundos, comandos de 10 segundos y 30 segundos por caso; detener ante tareas persistentes, rollback incierto o pérdida de integridad. Una ejecución aprobada no demuestra ausencia universal de deadlocks.

## Ajuste de alcance OpenSpec para aprobación

La modificación siguiente es **una propuesta**, no una decisión ni una edición de artefactos:

- En `design.md`, conservar expresamente D06 y todas las obligaciones de propiedad en la escritura, inserción protegida, atomicidad, limpieza segura, respuestas genéricas y tratamiento de conflictos. Aclarar que la acreditación de este cambio consiste en los escenarios críticos y la regresión acotada anteriores, sin prometer pruebas exhaustivas de todas las planificaciones ni ausencia universal de deadlocks.
- En `tasks.md`, solo después de aprobar ese encuadre, precisar que 4.2 verifica esos escenarios, incluidos el resultado de las pruebas actualmente fallidas y el cierre seguro de tareas y transacciones. No marcarla hasta que exista evidencia satisfactoria. Registrar separadamente los riesgos diferidos y la decisión explícita de aceptación.
- En `proposal.md`, si se aprueba la modificación de alcance, expresar que la unicidad global, la coordinación integral entre escritores, los incrementos atómicos globales y los reintentos avanzados quedan fuera de este cambio, sin reducir las obligaciones de seguridad e integridad que permanecen.
- Mantener intacto el delta de seguridad y no debilitar la especificación principal mediante una simple declaración de cierre. Crear y aprobar por separado el cambio posterior antes de atribuirle una solución o fecha.

La aprobación de esta propuesta autorizaría acordar el encuadre; las modificaciones de OpenSpec, la ejecución de pruebas, la aceptación formal de riesgos y la eventual sincronización o archivo deben seguir sus autorizaciones y verificaciones correspondientes. Los cambios actuales sin commit se conservan.
