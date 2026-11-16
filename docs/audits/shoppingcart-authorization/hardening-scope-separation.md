# Propuesta formal de separación del alcance del hardening

## Decisión propuesta y límites

Propuesta para revisión del responsable de Mango, sobre `feature/harden-shoppingcart-authorization` y el cambio `harden-shoppingcart-authorization`. No aprueba riesgos, no modifica artefactos OpenSpec y no completa 4.2 ni 8.2. No se ejecutaron pruebas, SQL ni nuevas investigaciones de concurrencia para prepararla.

**Recomendación:** separar la entrega funcional y su evidencia de la verificación de concurrencia pendiente, preservando íntegramente el código de protección existente y las invariantes de seguridad del sistema. La transferencia debe quedar registrada en un cambio posterior real y aprobado; no basta con quitar una dependencia de 8.2 o llamar mejora opcional a una garantía no acreditada.

**Viabilidad:** existe evidencia suficiente para proponer conformidad funcional acotada, con 69 pruebas aprobadas y 0 fallidas en la regresión existente sin carreras. No existe hoy base para afirmar conformidad integral o autorizar integración a main. Antes deben aprobarse la separación y la disposición de cada riesgo, formalizarse el trabajo sucesor y reevaluarse 8.2 contra el alcance revisado. Si no se permite diferir la evidencia de autorización e integridad concurrentes, persiste el bloqueo de D06/4.2 y la separación no habilita el cierre.

## Fuentes y estado vigente

- [Diseño actual](../../../openspec/changes/harden-shoppingcart-authorization/design.md): D06 conserva propiedad en la escritura efectiva, inserción protegida, atomicidad, rollback, limpieza segura y conflictos sin filtración.
- [Tareas actuales](../../../openspec/changes/harden-shoppingcart-authorization/tasks.md): 8.1 marcada completa; 4.2, 8.2 y 8.3 pendientes.
- [Delta de seguridad](../../../openspec/changes/harden-shoppingcart-authorization/specs/shopping-cart-api/spec.md): cuatro requisitos MODIFIED y uno ADDED, 20 escenarios funcionales.
- [Revisión final](hardening-final-review.md): regresión y comparación funcional satisfactorias; conformidad integral limitada por D06/4.2.
- [Verificación concurrente](concurrency-verification.md): resultados parciales, fallos históricos y límites del harness.
- [Encuadre mínimo aprobado](minimum-hardening-closure.md) e [índice histórico](index-hardening.md): trazabilidad del alcance y sus aprobaciones. Aprobar el encuadre anterior no aceptó riesgos ni trasladó todavía sus garantías obligatorias.

8.2 exige validación estricta y contraste con todos los escenarios de seguridad. La estructura y la comparación funcional ya se documentaron satisfactoriamente; la revisión actual mantuvo pendiente la conformidad integral por D06. 8.3 exige autorización separada para sincronizar, conservar requisitos y escenarios no relacionados y comprobar que no se modificaron archivos históricos. Esta propuesta no autoriza esa operación ni elimina sus controles.

## Garantías implementadas, verificadas y pendientes

| Garantía | Implementación o protección existente | Evidencia disponible | Pendiente exacto |
| --- | --- | --- | --- |
| JWT y propiedad en las cinco acciones | Autenticación obligatoria, identidad validada, filtros de propietario y ADMIN sin excepción. | Regresión funcional, JWT real de prueba, rechazos y equivalencia de respuestas aprobados. | No extender esa evidencia a todas las intercalaciones de lectura/escritura. |
| ApplyCoupon bajo cambio de propietario | UPDATE por ID y propietario, comprobación de filas y rechazo de cabeceras ambiguas. | Caso histórico de transferencia antes del UPDATE con 404 sin cambio; ambigüedad incluida en regresión aprobada. | Conformidad concurrente completa dentro del conjunto crítico y resultado de conflictos, sin afirmar unicidad global. |
| CartUpsert sobre detalles existentes | Predicado de detalle y padre propietario; comprobación de filas; transacción Serializable selectiva. | Creación e incremento secuenciales, referencias y rechazos aprobados. | Propiedad y referencias válidas hasta la escritura ante cambios concurrentes y consistencia de los efectos confirmados. |
| Inserción en una cabecera existente | Comprobación de propiedad dentro de la transacción selectiva. | Una prueba histórica observó espera de transferencia; no acredita todos los puntos críticos. | Verificación controlada de inserción protegida hasta su ejecución frente a transferencias, borrados y otros escritores relevantes. |
| Atomicidad y rollback | Commit común y disposición de transacciones; no hay reintentos automáticos añadidos. | Dos fallos inducidos antes del commit comprobaron ausencia/restauración de datos; regresión secuencial aprobada. | Rollback y resultado persistido bajo conflicto, timeout, cancelación o confirmación ambigua; no confundir fallo antes del commit con commit de resultado desconocido. |
| Limpieza de última cabecera | Borrado condicionado al propietario y a ausencia de detalles, con comprobación del resultado. | Eliminación secuencial aprobada; una carrera anterior no sincronizada pasó. | Intercalaciones críticas de eliminación e inserción sin pérdida por cascada de una inserción confirmada; estado completo de ambas operaciones. |
| Respuestas seguras ante conflictos | Mensajes públicos genéricos y diagnóstico interno. | Fallos funcionales y datos sensibles sintéticos cubiertos. | Asociar cada respuesta concurrente con commit/rollback verificable, sin tratar 500 como éxito ni filtrar detalles SQL. |
| Ciclo de vida del harness | Registro de tareas/contextos, barreras, observación de fallos y cuarentena. | Dos comprobaciones sin SQL aprobadas en una intervención anterior. | Aislamiento supervisado y cierre efectivo de operaciones/rollback en el servidor; la cuarentena no termina una tarea SQL. |

El deadlock 1205 y el timeout históricos no tienen cadena exacta documentada. `ConcurrentSameProductPreservesCommittedWrites` falló sin detalle suficiente para determinar causa o aserción. No hay evidencia para convertir ese fallo en prueba de incremento perdido, duplicado o defecto específico del controlador. Tampoco puede ignorarse como aprobado.

## Requisitos que permanecerían en el hardening actual

Permanecerían íntegros los cinco requisitos del delta y sus 20 escenarios:

| Requisito OpenSpec | Escenarios que permanecen |
| --- | --- |
| JWT authentication configuration | Token válido y rechazo sin token válido o identidad usable: 2 escenarios. |
| Request cart email via message bus | Publicación autorizada, manipulación de contenido/destinatario y ausencia de fuente aprobada: 3 escenarios. |
| Downstream integration with ProductAPI | Productos válidos, indisponibilidad/fallo y lista vacía válida: 3 escenarios. |
| Downstream integration with CouponAPI | Cupón válido e indisponibilidad/código no encontrado: 2 escenarios. |
| Cart ownership and client identifiers | Lectura propia; lectura/cupón ajeno o ausente; cupón/upsert propio; identidad malformada; identidad discrepante; referencias anidadas; ADMIN ajeno; eliminación propia; detalle ajeno/ausente; correo ajeno: 10 escenarios. |

Se mantienen las rutas y DTO, respuestas 400/401/404 y errores genéricos aplicables, propiedad persistida, ausencia de mutación o publicación en rechazo, destinatario JWT y reconstrucción confiable del mensaje. Se conserva el comportamiento secuencial de upsert y eliminación del último detalle y la evidencia de rollback antes del commit.

Las invariantes de no escribir sobre recursos ajenos, no dejar operaciones parciales conocidas y no provocar cascadas inseguras no se eliminan del sistema. Se propone trasladar la responsabilidad de completar su evidencia concurrente, no autorizar su incumplimiento. Se conservarían todos los predicados, comprobaciones, transacciones selectivas y diagnósticos actuales; esta propuesta no habilita retirar código.

## Alcance que pasaría al cambio posterior

Nombre propuesto: `harden-shoppingcart-concurrency-integrity`; no se crea mediante este documento. Debe tener propietario, planificación y trazabilidad aprobados antes de considerar resuelta documentalmente la transferencia.

| Origen actual | Contenido propuesto para el sucesor | Tratamiento en el cambio actual después de aprobación |
| --- | --- | --- |
| D06: autorización en la escritura e inserción protegida | Escenarios concurrentes de transferencia de propietario y referencias que cambian durante upsert, inserción y eliminación. | Conservar la invariante y las protecciones; describir el límite de evidencia y enlazar el sucesor. |
| D06: atomicidad, rollback y conflictos | Conflictos reales, fallos de liberación, cancelación y commits ambiguos, con resultado verificable de cada operación. | Mantener atomicidad y errores seguros implementados, sin afirmar cobertura concurrente completa. |
| D06: limpieza segura | Ambos órdenes de eliminación/inserción, incluida inserción directa relevante, sin cascada sobre inserción confirmada. | Mantener limpieza actual y su prueba secuencial; no declarar garantizada la carrera pendiente. |
| 4.2 y sus pruebas fallidas | Investigación y acreditación pendiente del conjunto crítico, captura de causa y control del host del harness. | Registrar transferencia explícita, conservando fallo y estado pendiente/diferido con referencia; nunca marcar 4.2 como implementada o aprobada por el mero traslado. |
| Mejoras ya diferidas en el diseño | Unicidad global por usuario/producto, investigación y reparación de duplicados, incrementos atómicos globales, coordinación entre escritores, optimización de índices/bloqueos, reintentos avanzados e idempotencia. | Mantener sus riesgos abiertos y su aceptación separada; no presentarlas como resueltas. |

No hay escenarios concurrentes `#### Scenario` separados en el delta actual que puedan simplemente moverse: su formulación detallada está en D06 y 4.2. El sucesor debería expresar esos escenarios explícitamente en sus propios requisitos/deltas. No se deben eliminar escenarios funcionales de propiedad para simular que se transfirió concurrencia.

La especificación principal describe cardinalidades y comportamiento del carrito. El traslado de evidencia no modifica esa especificación ni demuestra unicidad frente a todos los escritores. Si se propone limitar normativamente un requisito existente, hace falta una revisión explícita de especificación y aprobación distinta de una aceptación genérica de riesgos.

## Criterios propuestos de aceptación del cambio posterior

1. Ejecutar únicamente en laboratorio aislado verificado, con datos sintéticos y un host supervisado que limite el caso y observe todas las tareas. No limpiar hasta comprobar cierre de operaciones y transacciones; detener ante incertidumbre.
2. Demostrar en intercalaciones controladas que un cambio de propietario anterior a una escritura impide la mutación, y que una operación iniciada con propiedad protegida no escribe después sobre un recurso ya ajeno. Cubrir cupón, detalle existente, inserción y eliminación; verificar referencias anidadas hasta la escritura.
3. Acreditar en ambos órdenes de eliminación/inserción que ninguna inserción confirmada desaparece por cascada y que una operación abortada no deja eliminación parcial. Verificar por conexión independiente el resultado de ambas operaciones.
4. Resolver y registrar la causa y resultado de los casos históricos fallidos. Una operación competidora puede terminar en un conflicto seguro según el contrato que se apruebe; no exigir éxito simultáneo como sustituto de integridad ni aceptar cualquier 500 sin comprobar su estado persistido.
5. Ante deadlock, timeout o cancelación, verificar mensaje público seguro, diagnóstico interno útil y cierre o aislamiento efectivo. Ante confirmación de commit perdida, tratar el resultado como ambiguo y no reintentar ciegamente operaciones no idempotentes.
6. Establecer y verificar garantías globales de cardinalidad e incremento respecto a los escritores definidos en ese alcance, con plan explícito para datos duplicados existentes. Los cambios de esquema, si se eligieran, requerirán su autorización y verificación propias; no se autorizan aquí.
7. Mantener la regresión funcional y de seguridad del hardening, con trazabilidad de requisitos a resultados y sin omitir casos fallidos como aprobados. No exigir demostrar ausencia universal de deadlocks; sí su tratamiento seguro en los escenarios definidos.

Estos son criterios propuestos, no resultados obtenidos ni autorización de implementación.

## Impacto y riesgos que requieren decisión explícita antes de main

| Riesgo | Consecuencia | Decisión necesaria |
| --- | --- | --- |
| Propiedad efectiva concurrente sin evidencia completa | Posible escritura ajena si alguna intercalación vulnera la protección; no se ha demostrado ese defecto en el controlador actual. | Aprobar o rechazar el diferimiento de evidencia de una invariante de seguridad. Si no se admite esa incertidumbre, bloquea integración. |
| Limpieza y rollback concurrentes sin acreditar | Posible pérdida de inserciones confirmadas u operaciones parciales; las secuencias SQL de baseline mostraron pérdida por cascada, no el controlador actual completo. | No tratarlo como mero rendimiento; decidir si se permite integrar con esa incertidumbre o se mantiene el bloqueo. |
| Duplicados y falta de garantía global de incremento | Carritos/detalles múltiples, cantidades incorrectas y selección ambigua. | Aceptación explícita y delimitada de consecuencias y escritores, o resolución previa mediante el sucesor. |
| Deadlocks, timeouts y commit ambiguo | Errores, esperas, indisponibilidad o doble efecto si se reintenta sin idempotencia. | Política explícita de respuesta operativa y restricción de reintentos; sin presumir éxito ni rollback universal. |
| Harness y cancelación SQL incompletos | Tareas activas, recursos retenidos y resultados de prueba inciertos. | Aprobar un mecanismo de aislamiento antes de futuras carreras; no usar ausencia de solicitudes en una consulta histórica como garantía actual. |
| Advertencias NU1903 de AutoMapper documentadas | Riesgo de dependencia señalado por la compilación; esta propuesta no evalúa explotabilidad ni lo corrige. | Disposición separada de ese riesgo antes de integrar, sin mezclarlo con aceptación de concurrencia. |

Una aceptación válida debe identificar responsable, alcance de uso y exposición, consecuencias admitidas, límites, seguimiento y condición de revisión. Este documento no contiene esa aceptación ni impone una fecha inventada. Ninguna aprobación de separación debe describir como segura o verificada una garantía desconocida.

## Ruta formal propuesta para la separación

1. Aprobar o rechazar esta división y decidir expresamente si se permite diferir la evidencia concurrente de las invariantes de seguridad e integridad. La aprobación anterior de verificación acotada no implica esa nueva decisión.
2. Si se aprueba, crear por el flujo OpenSpec el sucesor y registrar allí requisitos, escenarios, pruebas fallidas y dependencias, sin retirar implementaciones ni evidencias.
3. Actualizar después proposal/design/tasks del cambio actual con referencias recíprocas y un límite claro de conformidad funcional. Conservar 4.2 como trabajo pendiente transferido/diferido, no como éxito técnico. Cualquier excepción al gate de cierre debe ser explícita y compatible con la política del repositorio.
4. Reevaluar 8.2 contra el alcance formalmente revisado y el delta funcional preservado. Los 69 resultados existentes sostienen esa propuesta, pero no completan automáticamente 8.2 ni certifican concurrencia; valorar también si cambios posteriores invalidaron evidencia.
5. Resolver formalmente los riesgos y los requisitos obligatorios antes de preparar integración a main. Un traslado documental no sustituye esa resolución. 8.3 seguirá requiriendo autorización separada de sincronización, revisión de diferencias y preservación de requisitos no relacionados; no se ejecuta por esta propuesta.

**Estado al terminar este análisis:** 8.1 completada según su revisión existente; 4.2, 8.2 y 8.3 pendientes. La conformidad funcional tras separación es viable de forma condicionada y limitada; el cierre integral e integración a main siguen bloqueados por decisiones y evidencia que este documento no reemplaza.
