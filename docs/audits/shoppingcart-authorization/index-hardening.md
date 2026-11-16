# Índice histórico y técnico del hardening de ShoppingCartAPI

## Propósito y alcance del hardening

Este índice reúne la trazabilidad del cambio `harden-shoppingcart-authorization` en la rama `feature/harden-shoppingcart-authorization` de Mango (.NET 11). El objetivo es exigir JWT válido, autorizar por propietario las cinco acciones del carrito, proteger escrituras y referencias anidadas, reconstruir el correo con datos confiables y preservar integraciones y respuestas seguras.

**Estado actualizado tras implementar D11:** alternativa B verificada con 77 pruebas aprobadas, 0 fallidas y 0 omitidas en la selección final sin SQL. 9.1–9.9 completadas; 4.2, 8.2 y 8.3 permanecen pendientes. Los cambios siguen sin commit. El informe final conserva por separado la revisión histórica de 69 pruebas y la nueva regresión; ninguna certifica el cierre del hardening completo. `concurrency-final-diagnosis.md` sigue siendo un diagnóstico de concurrencia y sus pendientes no se retomaron.

Se distingue entre **hechos documentados** (inspecciones y resultados registrados), **decisiones aprobadas** (políticas y alcance autorizados) y **pendientes** (garantías o riesgos sin resolución o aceptación). Los resultados históricos citados no son ejecuciones realizadas para crear este índice.

## Línea de tiempo de intervenciones

Las fechas y horas de commits provienen de la fecha de autor de Git, con desplazamiento `-05:00`. Git confirma el orden de integración de esos cinco commits, pero no fecha cada investigación contenida en ellos. Los documentos acumulan secciones de intervenciones distintas, a menudo con lo más reciente primero: sus títulos antiguos de «estado actual» o «pendiente» no describen necesariamente el estado presente.

Las etapas sin commit siguen el orden documentado y el de las intervenciones de esta conversación. Su secuencia es aproximada donde los documentos o Git no permiten mayor precisión; no se asignan fechas u horas nuevas. En particular, la etapa 1 y las investigaciones de concurrencia incluidas en la etapa 2 se incorporaron juntas en el primer commit listado, sin commits independientes para sus pasos internos.

| Etapa | Orden verificable o referencia | Intervención y resultado |
| --- | --- | --- |
| 1 | Previa a la incorporación en `d762bfb`; orden interno aproximado | Auditoría estática SC-01–SC-05; preparación del laboratorio aislado y baseline SQL. Los documentos de laboratorio y baseline describen fases diferentes: preparar tablas no equivale a verificar concurrencia. |
| 2 | 2026-11-15 09:07:10, `d762bfb` | Incorporación del hardening de autorización y sus artefactos. Incluye evidencia de concurrencia parcial: escrituras condicionadas, transacciones selectivas, adaptación de pruebas relacionales, deadlock 1205 y timeout posteriores; 4.2 queda pendiente. |
| 3 | 2026-11-15 09:34:27, `689abd0` | Correcciones de compatibilidad de Mango.Web y tratamiento HTTP; evolución de pruebas y clientes descendentes documentada en la integración Web. |
| 4 | 2026-11-15 10:23:49, `a80968a` | Verificación de integración Web con JWT, acciones MVC y después pipeline HTTP/cookies. Las secciones distinguen los alcances y las limitaciones de cada ejecución. |
| 5 | 2026-11-15 10:37:15, `c36dbbf` | Consolidación de cobertura de autorización y publicación. Las pruebas funcionales excluyen las carreras pendientes de 4.2. |
| 6 | 2026-11-15 14:58:46, `f1551d5` | Corrección y pruebas de manejo de errores descendentes, incluido el fallo de ProductAPI con carrito vacío; evidencia funcional de 64 aprobadas sin concurrencia. |
| 7 | Sin commit; posterior según la conversación | Diagnóstico final de 4.2 y conservación del informe: separación entre obligaciones actuales y concurrencia avanzada, propuesta de corrección mínima y pruebas limitadas. |
| 8 | Sin commit; posterior a etapa 7 | Implementación parcial: rechazo de cabeceras ambiguas en ApplyCoupon, comprobación de limpieza en RemoveCart y prueba de ambigüedad. Compilación y validación estructural, sin ejecución SQL en esa intervención. |
| 9 | Sin commit; posterior a etapa 8 | Registro seguro, cambios iniciales del harness y pruebas de fallo antes del commit. Pasaron dos casos de rollback y el rechazo de duplicados; la carrera sincronizada del mismo producto falló sin detalle suficiente y se detuvo la batería. |
| 10 | Sin commit; posterior a etapa 9 | Propuesta de cierre mínimo y aprobación de su encuadre: conservar seguridad, atomicidad e integridad y diferir mejoras globales. La aceptación de riesgos no se concede automáticamente. |
| 11 | Sin commit; posterior a etapa 10 | Actualización autorizada de proposal, design y tasks: regresión crítica acotada, sin exigencia de ausencia universal de deadlocks; tareas pendientes conservadas. |
| 12 | Sin commit; posterior a etapa 11 | Inspección previa a repetir un único caso: se detectó que Drain podía vencer sin finalización real de tareas y que faltaba capturar el logger SQL. La prueba no se ejecutó. |
| 13 | Sin commit; posterior a etapa 12 | Corrección autorizada del ciclo de vida del harness: registro de tareas/contextos, barreras, cancelación cooperativa, cuarentena y diagnóstico xUnit. Pasaron dos comprobaciones sin SQL; no se ejecutaron carreras y el aislamiento supervisado sigue propuesto. |
| 14 | Sin commit; posterior a etapa 13 | Diagnóstico del carrito vacío: API 404 propio inexistente propagado por MVC, frente a vista 200 en main. Se documentaron los consumidores y límites del fallback basado en 404. |
| 15 | Sin commit; posterior a etapa 14 | Decisión B aprobada y formalizada en proposal, design D11, tasks y delta: estado propio vacío exitoso y guardas de Checkout/EmailCart. La autorización documental no se interpretó como permiso para implementar. |
| 16 | Sin commit; posterior a etapa 15 | Implementación autorizada de GetCart virtual y tres guardas MVC; regresión final de 77 casos sin SQL aprobada. El fallo inicial del host de autenticación en el sandbox se resolvió repitiendo sin cambios fuera de él. No se modificó ni ejecutó concurrencia. |

## Índice de documentos con enlaces relativos

Once documentos de auditoría y cuatro artefactos OpenSpec enlazados; este índice no se cuenta a sí mismo.

| Documento | Uso y límite |
| --- | --- |
| [evidence.md](evidence.md) | Auditoría inicial SC-01–SC-05. Inspección estática y decisiones entonces pendientes, no explotación ejecutada ni descripción actual del controlador. |
| [sqlserver-lab.md](sqlserver-lab.md) | Preparación histórica, destino aislado, guardas y límites. No autoriza repetir migraciones ni confirma disponibilidad actual. |
| [concurrency-baseline.md](concurrency-baseline.md) | Reproducciones SQL controladas anteriores; no ejecuta los controladores ni demuestra defectos del código actual completo. |
| [concurrency-verification.md](concurrency-verification.md) | Registro acumulado de pruebas, fallos, diagnósticos y correcciones del harness. Consultar primero sus secciones recientes. |
| [web-integration.md](web-integration.md) | Evolución desde incompatibilidades estáticas hasta integraciones controladas, JWT y pipeline HTTP MVC. |
| [security-test-coverage.md](security-test-coverage.md) | Matriz de seguridad, fallos de dependencias, correcciones y resultados funcionales; no acredita 4.2. |
| [concurrency-final-diagnosis.md](concurrency-final-diagnosis.md) | Diagnóstico de 4.2 en el momento de su redacción. Sus pendientes deben contrastarse con resultados posteriores del registro de verificación. |
| [minimum-hardening-closure.md](minimum-hardening-closure.md) | Propuesta cuyo encuadre fue aprobado; conserva su carácter histórico de propuesta y no constituye aceptación de riesgos ni cierre de tareas. |
| [hardening-final-review.md](hardening-final-review.md) | Resultado actual D11: 77 pruebas sin SQL aprobadas, filtro, incidencia inicial y reintentos, matriz de 44 escenarios y límites. Conserva la revisión histórica de 69 pruebas; 8.2 sigue limitada por D06/4.2. |
| [cart-empty-regression-diagnosis.md](cart-empty-regression-diagnosis.md) | Diagnóstico original y actualización verificada de la corrección B: propio inexistente 200, guardas virtuales 404 y navegación controlada, sin acreditar limpieza SQL. |
| [cart-empty-contract-decision.md](cart-empty-contract-decision.md) | Análisis histórico A/B que recomendó B; su aprobación posterior consta en D11 y el contrato formal. No equivale por sí mismo a implementación ni resultados de pruebas. |
| [proposal.md](../../../openspec/changes/harden-shoppingcart-authorization/proposal.md) | Propósito, impacto y exclusiones del cambio activo. |
| [design.md](../../../openspec/changes/harden-shoppingcart-authorization/design.md) | D01-D06, D11 de carrito lógico vacío, consumidores seguros, alcance de concurrencia y riesgos diferidos. |
| [tasks.md](../../../openspec/changes/harden-shoppingcart-authorization/tasks.md) | Fuente actual del estado de las tareas. |
| [Delta shopping-cart-api](../../../openspec/changes/harden-shoppingcart-authorization/specs/shopping-cart-api/spec.md) | Requisitos y escenarios de seguridad del cambio; sigue sin sincronizar. |

## Decisiones arquitectónicas relevantes

**Aprobadas y recogidas en el diseño:** JWT obligatorio con identidad usable; autorización contra propiedad persistida; ADMIN sin excepción; 400 por sintaxis y 404 indistinguible para recursos ajenos o ausentes cuando corresponde. Los DTO y rutas se preservan y los identificadores del cliente no conceden autorización.

D11 distingue la ausencia propia confirmada mediante consulta autorizada: GetCart devuelve 200 con cabecera virtual ID 0, UserId del JWT, detalles vacíos y totales cero, sin escrituras ni dependencias. Toda identidad distinta conserva 404 exista o no su carrito, sin consultar esa existencia. Checkout GET/POST y EmailCart rechazan el virtual con 404 antes de efectos; una cabecera real sin detalles conserva su comportamiento. No se interpreta ningún error de ruta, autenticación, almacenamiento o dependencias como vacío exitoso.

El correo se reconstruye desde fuentes confiables, con el claim `email` del JWT validado como destinatario aprobado; Name y Phone carecen de fuente autorizada y permanecen sin establecer. Se conserva la forma requerida por EmailAPI y se falla sin publicar ante reconstrucción insegura o destinatario inutilizable.

D06 conserva como obligatorias las escrituras condicionadas al propietario, inserción protegida, atomicidad, rollback, limpieza segura de cabecera y errores genéricos. Se mantienen transacciones Serializable selectivas; no se aprueban aislamiento global, rediseño ni reintentos automáticos de incrementos o commits ambiguos.

## Defectos detectados y correcciones

- **Hallazgos iniciales:** acceso anónimo, confianza en identidad e identificadores del cliente y publicación de payload no confiable. Se incorporaron controles y pruebas; la auditoría inicial era estática.
- **Compatibilidad Web:** se reprodujeron caminos de error y deserialización inadecuada, y se corrigieron respuestas y accesos a cabeceras ausentes. Consultar el alcance de cada prueba en la integración Web.
- **Dependencias y errores:** se corrigieron errores con cuerpos vacíos y fallos de Product/Coupon, exposición potencial de `ex.Message` en GetCart y falso éxito de ProductAPI para carrito vacío. La matriz conserva las fases anteriores al arreglo para trazabilidad.
- **Regresión del carrito vacío:** GetCart propio sin cabecera retorna ahora la proyección exitosa acordada; CartIndex usa su vista existente y las tres operaciones sensibles rechazan el ID 0. Se conservaron BaseService, ResponseDto, DTO, cargador, RemoveCart y transacciones. La redirección posterior al último borrado se verificó con un borrado simulado y GET real sobre InMemory.
- **Corrección mínima sin commit:** detección de cabeceras ambiguas y comprobación del resultado de limpieza. La prueba de ambigüedad pasó; no implica unicidad global.
- **Harness:** se confirmó que el timeout de una espera no garantizaba finalización de SQL y faltaba captura del logger. Se incorporaron controles y diagnósticos; detener efectivamente SQL y acreditar rollback del servidor siguen pendientes.

No se ha confirmado una escritura ajena o una pérdida por cascada en el controlador actual. El baseline sí reprodujo esas anomalías en sus secuencias SQL; no deben confundirse las dos evidencias.

## Pruebas y evidencias

Los documentos registran grupos distintos de pruebas con filtros y versiones diferentes. Sus conteos no se suman ni sustituyen una regresión final. Entre los hitos constan 28 pruebas antes de un fallo sincronizado, 64 funcionales sin concurrencia tras corregir ProductAPI y, después, dos pruebas de rollback antes del commit y una de cabeceras ambiguas aprobadas por separado.

La evidencia Web distingue invocaciones directas de acciones, JWT firmado y middleware real, y hosts MVC con cookies y vistas. Product/Coupon y el bus fueron controlados o simulados; el login real de AuthAPI no queda acreditado por un stub que entrega su JWT.

La intervención del harness registrada en la etapa 13 solo compiló y ejecutó dos comprobaciones sin SQL, con resultado satisfactorio, además de OpenSpec estricto y `git diff --check`. Eso valida mecanismos sintéticos del harness, no concurrencia ni transacciones SQL reales. Las advertencias NU1903 de AutoMapper permanecen documentadas y no se resolvieron en estas intervenciones.

La regresión final D11 seleccionó seis clases explícitas: CartEmptyContractTests (29), GetCartAuthorizationTests (9), WebCompatibilityTests (24), ProductFailureIntegrationTests (4), EmailCartAuthorizationTests (10) y AuthenticationTests (1): **77/77, sin fallos ni skips**. El primer pase conjunto falló solo en el host existente de AuthenticationTests por DPAPI/EventLog del sandbox; pasó sin cambios fuera de él y luego se repitió la selección completa con éxito. Compilación final y OpenSpec estricto satisfactorios, sin cambios de paquetes; los TRX y limitaciones figuran en la revisión final. Las clases SQL, integración mixta y concurrencia se excluyeron por filtro, no se contaron como skips y no aportan evidencia nueva.

## Problemas e investigaciones de concurrencia

El baseline documentó duplicados, una actualización tras cambio de propietario y pérdida por cascada mediante SQL controlado. En pruebas posteriores del controlador hubo 1205 y timeout sin grafo que permitiera fijar su cadena exacta. Más tarde falló `ConcurrentSameProductPreservesCommittedWrites` sin aserción o código SQL suficiente en el resumen disponible: su causa permanece indeterminada.

Las pruebas de fallo antes del commit aportan evidencia concreta de rollback, pero no acreditan por sí solas el manejo de todos los conflictos. La comprobación histórica de cero cabeceras, detalles y solicitudes activas tras una ejecución tampoco demuestra el estado de todas las sesiones o el rollback de cada operación, ni es una consulta actual.

El harness ahora libera barreras y observa tareas; si persisten operaciones o transacciones locales, bloquea limpieza y disposición y pone contextos en cuarentena. Sigue siendo posible que una tarea permanezca activa: se propone un host hijo supervisado por caso y comprobación posterior del servidor antes de limpiar. Ese supervisor no está implementado ni verificado. No se debe declarar resuelta la cancelación efectiva.

## Cambios de alcance aprobados

El encuadre mínimo aprobado se documentó primero como propuesta y después se incorporó a los tres artefactos de planificación, aún sin commit. Mantiene las garantías no negociables y una regresión de escenarios críticos con límites; no exige comprobar todas las planificaciones ni demostrar ausencia universal de deadlocks.

Se difieren las garantías globales de unicidad e incrementos, coordinación entre todos los escritores, índices y bloqueos, reintentos avanzados e idempotencia. Aprobar ese encuadre no acepta automáticamente sus riesgos, no elimina requisitos de la especificación principal y no permite ignorar las pruebas fallidas ni cascadas inseguras.

## Estado actual de las tareas OpenSpec

Según el archivo de tareas tras D11: **23 de 26 completas; 3 pendientes**.

| Grupo | Estado registrado |
| --- | --- |
| 1.1–1.2, 2.1, 3.1–3.3 y 4.1 | Marcadas completas: autenticación, identidad, lectura y escrituras por propietario y eliminación autorizada. |
| 4.2 | Pendiente: concurrencia crítica, resolución de fallos, conflictos, limpieza e integridad verificables. |
| 5.1–5.2, 6.1–6.2 y 7.1–7.2 | Marcadas completas: correo, compatibilidad Web y cobertura de seguridad. |
| 8.1 | Completada: regresión y revisión final en el alcance no concurrente documentado en el informe final. |
| 8.2–8.3 | Pendientes: conformidad integral de seguridad limitada por D06/4.2 y sincronización autorizada. Una validación estructural aprobada no completa estos trabajos. |
| 9.1-9.9 | Completadas: implementación de B, guardas, pruebas controladas, compilación y documentación de resultados; sin SQL y sin acreditar concurrencia. |

No hay sincronización ni archivo del cambio en esta secuencia. Este índice no altera estados ni concede aceptación de riesgos.

## Riesgos residuales y trabajo futuro

Permanecen abiertos duplicados de carritos y productos, posibles incrementos perdidos, contención, fallos por deadlock/timeout, commits ambiguos y reintentos no idempotentes. No se ha acreditado la garantía global frente a todos los escritores. La limpieza insegura podría perder una inserción confirmada: prevenirla sigue siendo obligatorio ahora, no trabajo diferible.

`harden-shoppingcart-concurrency-integrity` es el nombre previsto para el trabajo avanzado; el diseño indica que aún no se ha creado. La aceptación de riesgos requiere decisión explícita. La regresión final no concurrente está registrada en el informe final; antes del cierre siguen pendientes la verificación segura de los casos críticos y la resolución del resultado desconocido. No se asignan fechas de solución ni se presume que la demo autorice el cierre.

## Referencias a commits relevantes

Los enlaces usan el remoto GitHub configurado en el repositorio. Las fechas identifican commits, no necesariamente el momento de cada prueba o aprobación.

| Commit | Asunto registrado |
| --- | --- |
| [d762bfb](https://github.com/alejorom/mango/commit/d762bfb23aa57c49f0295c445471c0a45facd319) | feat(shopping-cart): harden cart authorization |
| [689abd0](https://github.com/alejorom/mango/commit/689abd0857f20c0f9aa9caf5d83372a4a19397bc) | fix(shopping-cart): improve web integration and HTTP error handling |
| [a80968a](https://github.com/alejorom/mango/commit/a80968a96a347439a9ad904110d650d0b353b6fb) | test(shopping-cart): complete web integration verification |
| [c36dbbf](https://github.com/alejorom/mango/commit/c36dbbf0fafcb9bca3f2640daa31abfccf6a0b3d) | test(shopping-cart): complete authorization security coverage |
| [f1551d5](https://github.com/alejorom/mango/commit/f1551d5a5681b94ec10d93c4d4df7231e731d894) | fix(shopping-cart): harden dependency error handling |

Las etapas posteriores no tienen commit propio. Se conservan los cambios previos del controlador, pruebas, evidencia y artefactos. La creación inicial de este índice fue documental; la actualización D11 registra la implementación autorizada y su evidencia sin alterar el trabajo pendiente de concurrencia.
