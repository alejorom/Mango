# Decisión propuesta sobre el contrato del carrito vacío

Fecha: 2026-11-15. Rama: `feature/harden-shoppingcart-authorization`. Change: `harden-shoppingcart-authorization`.

Estado: **análisis terminado; se recomienda B, pendiente de autorización**. Este documento no aprueba ni implementa el cambio. No se modificaron código, pruebas, especificaciones ni otros documentos. No se ejecutaron aplicaciones, pruebas, compilación, SQL Server, migraciones o concurrencia.

## 1. Comportamiento anterior y actual

La comparación usa `b560b8b3d1e47698580cac9f9bd980c333d25b61`, base anterior al hardening y merge-base de las referencias locales `main` y HEAD. HEAD es `f1551d5a5681b94ec10d93c4d4df7231e731d894`. No se actualizó ni modificó main, ni se hizo fetch. Se inspeccionó el árbol de trabajo, incluyendo sus cambios sin commit.

| Caso, con identidad válida y dependencias sanas | main: API y navegador | Hardening actual: API y navegador |
| --- | --- | --- |
| No existe cabecera propia | GetCart usa `First`, captura la excepción y devuelve 500; MVC sustituye el fallo por `new CartDto()` y muestra la vista vacía con 200 | GetCart devuelve 404 explícito; BaseService conserva ese estado internamente; MVC devuelve 404 sin renderizar la vista |
| Existe cabecera propia sin detalles | GetCart 200 y vista vacía 200 | GetCart 200 y vista vacía 200 |
| Existen productos resueltos | GetCart 200 y vista con productos 200 | Mismo resultado |
| Borrado correcto del último detalle | RemoveCart 200 y borra también cabecera; Remove MVC devuelve 302; la siguiente consulta API devuelve 500 y la vista final 200 | RemoveCart 200 y borra también cabecera; mismo 302; la siguiente consulta API devuelve 404 y la respuesta MVC final 404 |
| Borrado correcto conservando otros detalles | 302 seguido de vista 200 con productos | Mismo resultado |

`d762bfb` introdujo el 404 de GetCart sin cabecera. `689abd0` cambió `CartIndex`, su cargador y BaseService para devolver los errores al navegador. Antes de ese segundo commit el fallback MVC todavía mostraba vacío ante el nuevo 404. La eliminación de la última cabecera ya existía en main.

Los cambios pendientes de CartAPIController afectan ApplyCoupon, comprobación de limpieza de RemoveCart y registro seguro de fallos; no cambian GetCart. Los cambios pendientes de pruebas y OpenSpec también se tuvieron presentes, sin alterarlos ni investigar concurrencia 4.2.

### Flujo y origen del estado

`CartIndex` → `LoadCartDtoBasedOnLoggedInUser` → `CartService.GetCartByUserIdAsnyc` → `BaseService.SendAsync` → `GET /api/cart/GetCart/{sub}`.

La ruta MVC `{controller=Home}/{action=Index}/{id?}`, las URL de CartService y la vista CartIndex siguen presentes y sin diferencias respecto de la base. El enlace de eliminación llama a `Remove(cartDetailsId)`; solo si la API informa éxito esa acción redirige a CartIndex. Un 404 del propio RemoveCart es un rechazo diferente y no debe transformarse en vista vacía.

El objeto de error de GetCart sin cabecera tiene `Result = null`, `IsSuccess = false` y `Message = "Cart not found"`; el rechazo de identidad ajena construye el mismo objeto. El ResponseDto de la API solo contiene esos tres campos. Se usa `AddControllers()` sin configuración explícita de Newtonsoft para MVC; el JSON esperado bajo la configuración estándar usa nombres camelCase. No se capturó tráfico real ni se reprodujo el incidente en esta intervención.

BaseService descarta el cuerpo de errores `/api/cart/` y genera un ResponseDto con mensaje genérico y `HttpStatusCode` interno. El cargador devuelve null y asigna `Response.StatusCode`. Preservar el cuerpo actual no basta: su forma genérica no identifica una causa específica y reconocer el mensaje supondría usar texto libre como protocolo.

Los proyectos inspeccionados apuntan a `net11.0`; ShoppingCartAPI usa EF Core/SQL Server 11.0.0. Se mantienen los clientes Newtonsoft.Json existentes.

## 2. Requisitos OpenSpec aplicables

Se leyeron la [especificación principal](../../../openspec/specs/shopping-cart-api/spec.md), el [delta del change](../../../openspec/changes/harden-shoppingcart-authorization/specs/shopping-cart-api/spec.md), proposal, design y tasks actuales. La especificación principal aún describe comportamientos anteriores; el delta no está sincronizado. No debe interpretarse esa falta de sincronización como autorización para retirar el hardening.

| Requisito o escenario | Efecto sobre la decisión |
| --- | --- |
| Principal: `Retrieve cart with computed totals` | Devuelve CartDto y totales; productos no resueltos producen `IsSuccess = false` con resultado parcial. No define explícitamente un GET propio exitoso sin cabecera |
| Principal: `Error handling and response envelope` y `Not-found condition in cart action` | Éxito 200; errores inesperados 500 genéricos; condición not-found 404. B necesita una excepción expresa para la consulta lógica del carrito propio vacío |
| Delta: `JWT authentication configuration` | Las cinco acciones exigen JWT válido e identidad utilizable. Conservar los rechazos antes de la lectura, sin sustituir identidad |
| Delta: `Cart ownership and client identifiers` y `Administrator accesses a foreign cart` | Consulta por propietario autenticado; referencias ajenas rechazadas; ADMIN sin bypass; identificadores malformados 400 |
| Delta: `Foreign or nonexistent cart read or coupon update` | Actualmente exige 404 indistinguibles también para GetCart propio sin cabecera. B cambia formalmente este escenario; no se puede implementar como simple ajuste de presentación |
| Principal y delta: `Owner removes an item` / `Last item removed, header also deleted` | Mantener borrado de última cabecera y autorización de RemoveCart. El GET posterior puede representar un estado vacío sin recrear filas |
| Delta: integración ProductAPI y CouponAPI | Fallos propagados llegan a GetCart como 500 genérico; CouponAPI 404 permite lectura sin descuento. No convertir excepciones, rechazos ni respuestas inválidas en vacío |
| Tasks 2.1 y 6.1; design D02 y riesgos | Deben reconciliarse con la decisión aprobada y con regresiones específicas; los checks existentes no demuestran cobertura del recorrido vacío |

No hay un escenario dedicado que defina conjuntamente cabecera ausente, cabecera persistida sin detalles y navegación después del último borrado. Esa distinción debe quedar explícita antes de implementar B. ApplyCoupon propio inexistente, referencias ajenas y detalles inexistentes siguen devolviendo 404.

## 3. Consumidores afectados y expectativas verificables

La búsqueda en el repositorio identifica un cliente productivo directo, Mango.Web. No se encontró otro servicio que invoque GetCart. Esto no acredita la inexistencia de clientes externos o desplegados fuera del repositorio.

| Consumidor | Expectativa observada y efecto |
| --- | --- |
| CartService e ICartService | GET con userId en ruta y bearer predeterminado desde ITokenProvider; espera ResponseDto. Con B el procesamiento exitoso existente ya transporta CartDto |
| CartController.CartIndex y su vista | El cargador requiere éxito y Result no nulo; la vista muestra `Please add items to cart.` si no hay detalles. B entra por ese camino normal sin interceptar ningún 404 |
| Checkout GET y POST | Usan el mismo cargador. Solo comprueban cabecera nula, no detalles ni cabecera no persistida. Un DTO exitoso con cabecera vacía no nula pasaría esas guardas; es obligatorio adaptarlas antes de implementar B |
| EmailCart MVC | También solo comprueba cabecera nula. B no debe habilitar solicitud de correo para un carrito virtual. La API de correo conserva su verificación de cabecera persistida y no debe modificarse |
| Remove MVC | Consume RemoveCart, no GetCart directamente; su 302 termina en CartIndex. Deben mantenerse tanto el 302 de éxito como los rechazos del borrado |
| OrderAPI, a través de Checkout | No consulta GetCart: recibe CartDto desde Web. Su CreateOrder compone y guarda el pedido; no se debe confiar en que rechace automáticamente el nuevo DTO vacío. Bloquear su envío desde Web sin modificar OrderAPI |
| GetCartAuthorizationTests | `ForeignAndMissingCartsHaveIdenticalResponsesWithoutExternalCallsOrWrites` compara actualmente ajeno con propio inexistente. B exige separar esos casos y mantener equivalencia entre identidades ajenas con y sin carrito |
| WebCompatibilityTests | Verifica transporte bearer, rechazos 400/401/404 y que una carga fallida no envíe correo. El 404 genérico debe seguir rechazado bajo B; añadir vacío exitoso y guardas de Checkout/EmailCart |
| ProductFailureIntegrationTests | Cabecera real sin detalles con fallo de ProductAPI espera 500; con lista válida vacía espera éxito. Mantener ambas expectativas, sin confundirlas con ausencia de cabecera |
| RealJwtWebIntegrationTests y AuthenticationTests | Verifican JWT, recursos ajenos, 400/401 y fallos de dependencias. No deben perder cobertura; la teoría de integración tiene variantes InMemory y SQL, que deben distinguirse al planear su ejecución |
| MvcHttpIntegrationTests | Tras el último borrado verifica 302, pero no sigue Location con token válido; cambia antes el bearer a inválido. Usa laboratorio SQL, por lo que no sirve ejecutarla indiscriminadamente bajo una autorización sin SQL |

AuthController crea la identidad MVC y guarda el bearer durante el login; TokenProvider lo recupera de otra cookie. Son fuentes separadas durante solicitudes posteriores. No asumir coherencia perpetua: B conserva el rechazo de GetCart cuando la ruta derivada de la cookie no coincide con el JWT validado. No se necesita modificar login o TokenProvider para la propuesta.

## 4. Comparación técnica: exactamente A frente a B

**A: mantener 404 con discriminador estructurado.** Añadir al sobre de error un código estable específico del dominio, documentado, y propagarlo internamente hacia CartIndex conservando HTTP. Para conservar el contrato actual de indistinguibilidad, el código debe ser igual en el rechazo ajeno y en el carrito inexistente; no emitir un código que permita reconocer existencia ajena. Web solo podría tratarlo como vacío tras verificar que consulta su propia identidad y que esta coincide con el bearer enviado. Los 404 sin código reconocido seguirían siendo errores. Ninguna decisión dependería de Message.

**B: devolver 200 con CartDto vacío válido para la consulta propia sin cabecera.** Tras autenticación, validación del identificador, igualdad con subject y consulta satisfactoria al almacenamiento, representar el estado lógico vacío. Conservar 404 para cualquier identidad ajena, exista o no su carrito. No modificar BaseService ni interpretar errores como éxitos.

| Criterio | A | B |
| --- | --- | --- |
| Seguridad y aislamiento | Mantiene 404, pero requiere código idéntico en ajeno/inexistente y verificación adicional de coherencia de identidad en Web | La API decide usando JWT validado; la rama de identidad ajena termina antes de leer su existencia. El 200 solo informa del estado propio |
| Compatibilidad OpenSpec | Conserva estados actuales; debe formalizar nuevo código, alcance y comportamiento MVC. Un código diferente para propio inexistente rompería la equivalencia actual | Contradice explícitamente el 404 propio inexistente vigente; requiere revisión formal previa, conservando 404 ajenos indistinguibles |
| Compatibilidad Mango.Web | Necesita transporte del código y tratamiento especial en CartIndex, sin relajar el cargador compartido | CartIndex y vista ya aceptan éxito vacío; requiere guardas de consumidores Checkout/EmailCart |
| Otros consumidores | Conserva 404; agrega propiedad pública y exige actualización para recuperar la experiencia vacía | Cambia estado/semántica de GetCart propio inexistente; clientes externos que dependan de 404 necesitan revisión |
| Archivos productivos mínimos previstos | Cinco: CartAPIController, ResponseDto de API y Web, BaseService y CartController; la coherencia de identidad añade lógica/dependencias en este último | Dos: CartAPIController y CartController. Las clases DTO, CartService, BaseService y vistas pueden conservarse con la representación definida abajo |
| Riesgo de regresión | Aumenta lógica de errores compartida y riesgo de aceptar un marcador fuera de contexto; exige probar degradación con cuerpos inválidos o códigos desconocidos | Riesgo concentrado en nuevo éxito vacío, pedidos/correo y consumidores que esperaban 404; no altera clasificación de errores HTTP |
| Mantenimiento | Dos representaciones para mostrar el mismo estado vacío: éxito con cabecera real y error reconocido sin cabecera | Un resultado de lectura exitoso para mostrar el estado propio vacío; las guardas distinguen representación no persistida |
| DTO y contrato público | Nueva propiedad/código de error y su estabilidad pública | Sin campos ni tipos nuevos; sí cambia el contrato público de estado y significado del DTO sin cabecera persistida |
| Pruebas adicionales específicas | Código reconocido/desconocido/ausente, cuerpo malformado, identidad discordante, 404 de ruta y aislamiento de otros endpoints | Forma completa del vacío 200, ausencia de escrituras, aislamiento ajeno y rechazo de Checkout/EmailCart para la representación virtual |

Los recuentos productivos no incluyen pruebas, planificación ni documentación. Ambas alternativas necesitan una modificación de contrato documentada; A no elimina ese trabajo.

## 5. Recomendación: B y contrato propuesto

**Se recomienda B.** Para Mango, GetCart alimenta la pantalla del estado actual de compra del usuario; la ausencia física de una cabecera tras vaciarlo es parte de su ciclo normal. Un estado lógico vacío exitoso encaja con la vista existente y evita extender el protocolo de errores y BaseService para una condición normal. El estado HTTP deja de depender del detalle de persistencia, sin ocultar errores reales.

HTTP permite expresar éxito con representación mediante 200 y ausencia o no divulgación mediante 404; no decide si el recurso de dominio debe ser una fila o una vista lógica. Elegir aquí la segunda interpretación es una decisión de diseño para Mango, no una obligación de HTTP. Véanse [RFC 9110, 200 OK](https://www.rfc-editor.org/rfc/rfc9110.html#section-15.3.1) y [404 Not Found](https://www.rfc-editor.org/rfc/rfc9110.html#section-15.5.5).

### Representación mínima propuesta

- Sobre ResponseDto existente: HTTP 200, IsSuccess verdadero y Result no nulo.
- CartDto con CartHeader no nulo, UserId igual al subject autenticado y CartHeaderId igual a 0, valor sin identificador persistido; CartTotal y Discount iguales a 0; sin cupón ni datos de contacto inventados.
- CartDetails como colección vacía no nula. La vista existente entra en su rama vacía.
- Esta cabecera es exclusivamente una proyección de lectura; no se guarda ni se crea durante GET. CartUpsert continúa siendo quien crea datos al agregar el primer producto.

Esto evita introducir null en una propiedad CartHeader que hoy está declarada no nullable en los DTO de API y Web. Reutiliza los valores predeterminados de los campos existentes; no añade una propiedad de estado. La semántica de CartHeaderId 0 en este resultado debe aprobarse y quedar especificada, no inferirse silenciosamente en futuros consumidores.

### Límites obligatorios del nuevo éxito

| Situación | Resultado propuesto |
| --- | --- |
| Sin JWT válido o identidad utilizable | Mantener rechazo de autenticación; sin lectura, escritura ni publicación autorizada |
| Identificador de ruta malformado | Mantener 400 |
| Identificador bien formado distinto del subject | Mantener 404 idéntico exista o no carrito para ese identificador; también para ADMIN |
| Identificador propio y consulta correcta sin cabecera | Nuevo 200 vacío, sin escrituras ni llamadas Product/Coupon necesarias para componerlo |
| Cabecera propia real sin detalles | Conservar composición y reglas actuales de dependencias; no introducir un atajo adicional que transforme sus fallos en vacío |
| Excepción de almacenamiento | Mantener 500 genérico; no equiparar excepción a consulta sin filas |
| Fallos de Product/Coupon en recorridos que los consultan | Mantener 500 de GetCart; cupón 404 sigue significando sin descuento. Productos no resueltos conservan IsSuccess falso y resultado parcial |
| 404 de ruta/configuración o respuesta 401/403 recibida por Web | Mantener rechazo; no existe fallback MVC para esos estados |
| Fallo de transporte Web → API | Mantener error y la conversión actual a 502 en el cargador |

No consultar ProductAPI para una cabecera que se confirmó ausente no es convertir un fallo descendente en éxito: no se ha solicitado ni fallado una dependencia necesaria para ese estado. En cambio, la cabecera real vacía sigue su comportamiento actual y sus pruebas de fallo.

Checkout GET, Checkout POST y EmailCart MVC deberán rechazar explícitamente la representación sin cabecera persistida con **404**, antes de mostrar checkout, crear pedido o solicitar correo. Sus guardas actuales de cabecera nula se mantienen para fallos; se añade el caso de identificador 0. No basta con cambiar GetCart: hoy las tres acciones aceptarían la cabecera virtual. No se propone cambiar aquí el tratamiento de una cabecera real con ID positivo y sin detalles; prohibir todas las operaciones sobre ese estado sería otra decisión funcional.

## 6. Archivos que sería necesario modificar

Plan de modificación para B, **sin aplicar**:

| Área | Archivos y modificación prevista |
| --- | --- |
| Producción, 2 archivos | `Mango.Services.ShoppingCartAPI/Controllers/CartAPIController.cs`: únicamente rama propia sin cabecera de GetCart. `Mango.Web/Controllers/CartController.cs`: guardas de Checkout GET/POST y EmailCart para no operar con cabecera virtual; CartIndex y el cargador pueden conservar su flujo normal de éxito |
| Pruebas, 3 archivos previstos | `Mango.Services.ShoppingCartAPI.Tests/GetCartAuthorizationTests.cs`: separar propio vacío de rechazo ajeno. `Mango.Services.ShoppingCartAPI.Tests/WebCompatibilityTests.cs`: consumidores y errores conservados. Nuevo `Mango.Services.ShoppingCartAPI.Tests/CartEmptyContractTests.cs`: contrato HTTP y navegación con fakes y almacenamiento InMemory para lecturas, sin SQL |
| OpenSpec, 4 archivos del change | `proposal.md`, `design.md`, `tasks.md` y `specs/shopping-cart-api/spec.md` bajo `openspec/changes/harden-shoppingcart-authorization/`: reflejar decisión, contrato, consumidores, verificaciones y alcance actualizado |
| Auditoría, tras verificación futura | `cart-empty-regression-diagnosis.md`, `hardening-final-review.md` e `index-hardening.md` bajo `docs/audits/shoppingcart-authorization/`: registrar implementación y resultados reales |

No se prevén cambios en ResponseDto, CartDto, CartHeaderDto, BaseService, CartService, ICartService, autenticación, vistas, RemoveCart, otros servicios, paquetes o esquema de datos. La protección adicional queda en el consumidor MVC, sin modificar OrderAPI ni la API de correo. No actualizar especificaciones principales ni artefactos archivados durante esta fase; la sincronización posterior tiene su propio flujo y autorización.

Reutilizar las pruebas existentes de autenticación y dependencias sin editarlas si sus verificaciones se conservan. El archivo nuevo propuesto evita ampliar pruebas que abren SQL; su nombre no constituye una creación autorizada en esta intervención.

## 7. Pruebas de regresión imprescindibles

Todas son **propuestas, no ejecutadas**:

1. Propio inexistente: JWT y ruta coherentes, consulta sin cabecera, HTTP API 200 y forma exacta del DTO vacío; cero escrituras y cero llamadas descendentes/publicaciones. Verificar deserialización real a los DTO Web existentes.
2. CartIndex: con sesión válida y respuesta anterior, HTTP final 200 y mensaje vacío en la vista; con carrito real y productos, misma presentación y totales que antes.
3. Último borrado seguido de redirección: fake de RemoveCart exitoso, MVC 302 a CartIndex, seguimiento con la misma sesión válida y nuevo GET que devuelve vacío; comprobar 200 final. El fake prueba el recorrido HTTP/MVC, no la eliminación SQL. Reutilizar la evidencia estática de limpieza y no atribuir a esta prueba garantías relacionales.
4. Cabecera real sin detalles: éxito si dependencias son válidas y error ante su fallo, conservando los casos existentes de ProductFailureIntegrationTests. No mezclar este estado con el DTO virtual.
5. Ruta incorrecta/configuración: HTTP 404 con cuerpo vacío, ProblemDetails o cuerpo genérico permanece 404 hasta el navegador; nunca se muestra el mensaje de carrito vacío. El cuerpo no decide por comparación de texto.
6. Identidad y autorización: faltan token/subject, token inválido o expirado, identificador malformado, cookie y bearer discordantes; conservar rechazos. Para cualquier identificador ajeno válido, probar carrito existente e inexistente con igual 404 y cuerpo, también ADMIN, sin consultas de existencia ajena ni efectos.
7. Errores: preservar 400, 401 y 403 recibidos por Web; ProductAPI/CouponAPI no disponibles o inválidos no pasan a vacío; GetCart 500 y transporte Web/API 502 conservan error. Mantener la excepción documentada de CouponAPI 404 sin descuento.
8. Operaciones sensibles: GET y POST Checkout y EmailCart rechazan DTO virtual con 404, sin llamadas a OrderService ni envío de correo; conservan su recorrido con carrito real. ApplyCoupon/RemoveCart ajenos o inexistentes mantienen sus propios rechazos.
9. GET repetido del vacío: no crea cabeceras ni detalles y mantiene un DTO consistente. No afirmar que este caso secuencial verifica concurrencia.

La ejecución futura deberá estar filtrada a pruebas sin SQL y sin 4.2. RealJwtWebIntegrationTests tiene una variante SQL y MvcHttpIntegrationTests usa SQL directamente: no ejecutar clases completas sin revisar esos efectos. Puede reutilizarse su configuración de JWT y MVC en el fixture nuevo con dependencias controladas. EF InMemory no demuestra transacciones, ExecuteDelete o limpieza relacional; no intentar sustituir esa evidencia con mocks.

Después de una implementación autorizada: compilar los proyectos afectados, ejecutar únicamente las pruebas acordadas, validar `harden-shoppingcart-authorization` con OpenSpec estricto y comprobar `git diff --check`. Nada de ello se presenta como aprobado en este análisis.

## 8. Impacto sobre seguridad y compatibilidad

B conserva el control efectivo en la API: autenticación primero, igualdad de identidad para este GET y consulta de datos limitada al subject. No concede acceso por el UserId de un DTO, no introduce bypass ADMIN y no cambia las restricciones de las escrituras o del mensaje de correo. La protección frente a BOLA exige autorización por objeto en las operaciones que usan identificadores; un cambio de estado HTTP no sustituye esos controles. Referencia: [OWASP API1:2023, Broken Object Level Authorization](https://owasp.org/API-Security/editions/2023/en/0xa1-broken-object-level-authorization/).

Sí cambia una garantía formal del delta actual: deja de ser idéntica la respuesta de un propio inexistente y una identidad ajena. Eso debe reconocerse y aprobarse expresamente. No se introduce un oráculo sobre otros usuarios si toda identidad distinta del JWT recibe el mismo 404 antes de comprobar su existencia. La diferencia de 200/404 solo aporta información propia al usuario autenticado.

Riesgos concretos: un cliente externo puede depender del 404 anterior; una guarda olvidada puede permitir checkout vacío o una solicitud innecesaria de correo; consumidores pueden interpretar ID 0 como fila real; un cambio demasiado amplio en la rama de identidad puede revelar existencia ajena; omitir ProductAPI para cabeceras reales vacías ocultaría errores ya cubiertos por pruebas. La propuesta evita esas ampliaciones y exige verificaciones específicas. No se garantiza compatibilidad con consumidores no inventariados fuera del repositorio.

La preferencia previa por un discriminador respondía a un alcance que prohibía cambiar el comportamiento de la API. Con la decisión de contrato abierta entre A y B, añadir ese protocolo ya no es la solución de menor complejidad para los consumidores conocidos de Mango.

## 9. Decisiones pendientes de autorización y estado

1. Aprobar B y el cambio público de GetCart propio inexistente de 404 a 200, manteniendo la ocultación de existencia ajena.
2. Aprobar la representación vacía propuesta y su uso exclusivamente de lectura, junto con el 404 preventivo de Checkout/EmailCart para cabecera virtual. La aprobación no incluye cambiar operaciones sobre una cabecera real vacía.
3. Autorizar primero la actualización formal de los cuatro artefactos del change: separar escenarios propio vacío/ajeno, documentar la forma del resultado y la excepción al requisito general de not-found, conservar ApplyCoupon sin cabecera como 404, precisar dependencias y añadir tareas de consumidores/regresión. **OpenSpec debe actualizarse antes de implementar B**; no basta con registrar esta recomendación en auditoría.
4. Autorizar después implementación y validación acotadas, incluyendo los dos archivos productivos y pruebas específicas, sin SQL, migraciones, paquetes, otros servicios o concurrencia. No sincronizar ni archivar sin autorización independiente.

Esta intervención solo genera `docs/audits/shoppingcart-authorization/cart-empty-contract-decision.md`. La rama y HEAD se conservan. Al comenzar había siete archivos rastreados modificados y seis documentos sin seguimiento, sin staging; se preservan. Se usaron únicamente lecturas, comparaciones y consultas de estado OpenSpec. El estado de artefactos de planificación completos mostrado por el CLI no significa que la implementación ni sus verificaciones estén cerradas.

El análisis se detiene aquí, a la espera de autorización expresa; no se adopta B automáticamente.
