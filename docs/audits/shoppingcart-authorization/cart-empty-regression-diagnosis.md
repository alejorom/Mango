# Diagnóstico de regresión del carrito vacío

Fecha: 2026-11-15. Proyecto: Mango. Rama: `feature/harden-shoppingcart-authorization`.

## Actualización verificada: alternativa B y D11

La alternativa B fue aprobada, formalizada en los cuatro artefactos del change y posteriormente autorizada para implementación. **La corrección ya está aplicada:** GetCart devuelve HTTP 200, IsSuccess verdadero y un CartDto no nulo cuando la consulta del propietario autenticado confirma ausencia de cabecera. CartHeader es no nulo, con ID 0, UserId del JWT validado y total/descuento cero; CartDetails es una colección vacía no nula. Esa rama no escribe ni crea cabeceras, llama dependencias o publica mensajes.

CartIndex, el cargador compartido, BaseService y los DTO permanecen sin cambios. La vista vacía se obtiene por el resultado exitoso normal; no se interpreta ningún 404 como carrito vacío. Checkout GET, Checkout POST y EmailCart devuelven 404 para la cabecera virtual antes de crear pedidos, iniciar pago o solicitar correo. Los carritos persistidos, incluidos los que no tienen detalles, conservan su comportamiento. RemoveCart y la eliminación de la última cabecera no se modificaron.

La regresión final controlada registra **77 ejecutadas, 77 aprobadas, 0 fallidas y 0 omitidas dentro del filtro**, sin SQL Server, migraciones ni concurrencia. Incluye 29 casos nuevos HTTP/MVC y nueve de GetCart; la consulta API vacía y su deserialización Web, la vista HTTP 200 y los tres rechazos virtuales se verificaron en hosts de prueba con JWT real, cookies y almacenamiento InMemory. El último borrado se simula en transporte; se sigue su 302 con sesión válida y se realiza el GetCart real posterior. Esto verifica navegación y contrato, no limpieza relacional ni transacciones SQL.

El primer pase conjunto tuvo 76 aprobadas y un fallo de AuthenticationTests por DPAPI/EventLog del sandbox, que interrumpía la respuesta al activar un controlador sin dependencias registradas. La prueba sin modificaciones pasó fuera del sandbox; después se repitieron las seis clases juntas en ese entorno y pasaron las 77. No se retiraron aserciones ni se cambiaron permisos, paquetes o código de autenticación. Los resultados de distintas ejecuciones no se suman.

La compilación final de los proyectos referenciados por el proyecto de pruebas fue satisfactoria, cero errores y seis advertencias NU1903 preexistentes de AutoMapper. Una primera compilación detectó una colisión del alias Web en el fixture nuevo, corregida antes de la regresión; el fixture final no añade advertencias de nulabilidad. OpenSpec estricto y git diff --check pasaron. Los detalles, filtro, matriz de escenarios y exclusiones están en [hardening-final-review.md](hardening-final-review.md).

Las pruebas SQL existentes y la reproducción en aplicaciones desplegadas permanecen fuera de esta intervención. 4.2, la conformidad integral de 8.2 y la sincronización 8.3 siguen pendientes. La corrección no acredita concurrencia ni acepta sus riesgos residuales.

**Registro histórico:** el resto de este documento conserva el diagnóstico previo a D11. Su referencia al hardening «actual» describe el estado anterior a esta corrección. La propuesta inicial de un fallback únicamente MVC quedó superada por la decisión B; no se implementó ese fallback ni un discriminador de errores.

## Alcance y conclusión

Diagnóstico estático: comparación de Git, lectura de implementación, especificaciones y pruebas existentes. No se ejecutaron aplicaciones, pruebas, migraciones ni concurrencia; no se accedió a bases de datos. Solo se crea este documento; no se modifican código, pruebas ni OpenSpec.

**Sí existe un cambio funcional demostrable por código:** para un usuario autenticado cuyo carrito no tiene cabecera, main terminaba mostrando la vista vacía con HTTP 200; el hardening termina devolviendo HTTP 404 desde Mango.Web. El mismo cambio afecta la navegación posterior a eliminar correctamente el último detalle, porque esa operación elimina también la cabecera en ambas versiones.

Esto confirma la diferencia y su cadena causal en el código, pero no constituye reproducción del incidente en ejecución. Sin una traza HTTP no se puede afirmar que el 404 concreto observado haya recorrido esa cadena.

## Base de comparación y cambios pendientes

- Base anterior al hardening: `b560b8b3d1e47698580cac9f9bd980c333d25b61` (`docs(openspec): align AuthAPI JWT authorization specification`). Coinciden `main`, `origin/main` y `git merge-base main HEAD` en las referencias locales inspeccionadas. No se hizo fetch ni se verificó el remoto en línea.
- HEAD: `f1551d5` (`fix(shopping-cart): harden dependency error handling`). Se comparó la base contra el árbol de trabajo, incluyendo cambios sin commit, y se revisó por separado el diff de HEAD al árbol de trabajo.
- `d762bfb` introduce la autorización y el 404 de `GetCart` cuando falta la cabecera.
- `689abd0` cambia `CartController`, `BaseService` y `ResponseDto` para propagar errores HTTP al navegador; introduce la regresión visible en `CartIndex`.
- Los cambios pendientes de `CartAPIController` afectan selección de cabecera en `ApplyCoupon`, registro seguro de fallos y comprobación de la limpieza en `RemoveCart`. No alteran `GetCart` ni Mango.Web. La nueva comprobación de limpieza admite que se haya eliminado una cabecera o que permanezca una cabecera propia con detalles. Si falla, devuelve 500; no introduce un nuevo 404 de carrito vacío.
- Ambos proyectos afectados apuntan a `net11.0`. ShoppingCartAPI usa EF Core/SQL Server 11.0.0 y `AddControllers()`; Web usa `AddControllersWithViews()`. Los clientes conservan Newtonsoft.Json 13.0.4.

## Comportamiento de main frente al hardening

En main, `GetCart` busca la cabecera con `First(...)`. Si no existe, la excepción se captura y la API devuelve **HTTP 500**, `IsSuccess = false`; no devuelve un carrito vacío exitoso. `BaseService.SendAsync` convierte ese estado en una respuesta fallida. `LoadCartDtoBasedOnLoggedInUser` devuelve entonces `new CartDto()` y `CartIndex` siempre llama a `View(...)`. La vista admite `CartHeader == null` y muestra `Please add items to cart.`. El estado final de MVC es **HTTP 200**: el comportamiento funcional anterior dependía de ese fallback, aunque la API fallaba.

Con hardening, después de validar identificador e identidad, `GetCart` utiliza `FirstOrDefaultAsync` con el propietario autenticado. Si no hay cabecera, devuelve **HTTP 404**. `BaseService` conserva internamente ese código; el cargador MVC fija `Response.StatusCode = 404` y devuelve null. `CartIndex` devuelve `StatusCode(Response.StatusCode)` y no renderiza la vista vacía.

La tabla supone identidad válida y coincidente, almacenamiento accesible y dependencias válidas salvo donde se indique. Los estados son deducciones estáticas, no mediciones de esta intervención.

| Escenario | main: ShoppingCartAPI → navegador | Hardening actual: ShoppingCartAPI → navegador |
| --- | --- | --- |
| Sin cabecera para el usuario actual | 500 fallido → 200 con vista vacía | 404 fallido → 404 sin vista |
| Cabecera propia existente sin detalles | 200 exitoso → 200 con vista vacía | 200 exitoso → 200 con vista vacía |
| Cabecera propia con detalles y productos resueltos | 200 exitoso → 200 con productos | 200 exitoso → 200 con productos |
| Eliminar el último detalle propio correctamente | RemoveCart 200; MVC 302; GetCart 500 → CartIndex 200 vacío | RemoveCart 200; MVC 302; GetCart 404 → CartIndex 404 |
| Eliminar un detalle propio y conservar otros | RemoveCart 200; MVC 302; GetCart 200 → CartIndex 200 con productos | Mismos estados y presentación |
| GetCart responde HTTP 200 con `IsSuccess = false`, por productos no resueltos | 200 fallido → 200 con vista vacía | 200 fallido → 502; el cliente no conserva un estado de éxito en `HttpStatusCode` |
| GetCart responde HTTP 500 por una excepción | 500 → 200 con vista vacía | 500 → 500 |

**Cabecera sin detalles no equivale a cabecera inexistente.** Con cabecera presente, ambas versiones consultan ProductAPI incluso si no hay detalles; si hay código de cupón, también pueden consultar CouponAPI. No hay una condición que devuelva 404 solo por una colección vacía. Los errores de dependencias pueden impedir el 200 de esta fila.

## Diferencia exacta y puntos responsables

1. [CartAPIController.GetCart](../../../Mango.Services.ShoppingCartAPI/Controllers/CartAPIController.cs), líneas 32–52: `d762bfb` sustituye la búsqueda que lanzaba una excepción por una consulta del propietario autenticado y un retorno explícito `NotFound` si falta la cabecera. El 404 también se devuelve si el identificador bien formado difiere del subject, sin consultar existencia ajena.
2. [BaseService.SendAsync](../../../Mango.Web/Service/BaseService.cs), líneas 61–73: `689abd0` añade el tratamiento específico de respuestas no exitosas de `/api/cart/` y conserva `(int)apiResponse.StatusCode` en `ResponseDto.HttpStatusCode`, propiedad interna agregada en ese commit. No convierte ese error en éxito ni interpreta su cuerpo.
3. [CartController.LoadCartDtoBasedOnLoggedInUser](../../../Mango.Web/Controllers/CartController.cs), líneas 33–43: `689abd0` sustituye `return new CartDto()` por la asignación del estado HTTP y `return null` cuando falla la carga.
4. [CartController.CartIndex](../../../Mango.Web/Controllers/CartController.cs), líneas 21–25: el mismo commit sustituye `return View(await LoadCartDtoBasedOnLoggedInUser())` por la bifurcación que devuelve el estado HTTP si el modelo es null. **Es el punto que convierte el nuevo error de consulta en un 404 final del navegador.**

En `d762bfb`, antes de `689abd0`, la API ya devuelve 404 sin cabecera, pero el fallback MVC anterior todavía permite mostrar la vista vacía con 200. El cambio de la API por sí solo no basta para explicar el nuevo estado final del navegador.

## RemoveCart, rutas y redirecciones

- `CartAPIController.RemoveCart` (líneas 255–304) elimina el detalle propio y elimina condicionalmente su cabecera si ya no tiene detalles. Main también elimina la cabecera al borrar el último detalle. La limpieza no es una nueva decisión funcional del hardening; el mecanismo ahora incorpora restricciones de propietario y transacción.
- `CartController.Remove` (líneas 46–56) conserva `RedirectToAction(nameof(CartIndex))` cuando la API informa éxito. Eso genera un 302 hacia la acción del mismo controlador; no redirige directamente a ShoppingCartAPI. En la vista, el enlace aporta `cartDetailsId` a esta acción.
- [CartService](../../../Mango.Web/Service/CartService.cs), líneas 35–51, conserva `GET /api/cart/GetCart/{userId}` y `POST /api/cart/RemoveCart`. `BaseService` agrega el bearer de `ITokenProvider` por defecto.
- [Mango.Web/Program.cs](../../../Mango.Web/Program.cs), líneas 59–61, conserva la ruta `{controller=Home}/{action=Index}/{id?}`. `/Cart/CartIndex` resuelve a la acción existente. La [vista CartIndex](../../../Mango.Web/Views/Cart/CartIndex.cshtml) sigue presente y sin cambios; líneas 1–2 y 98–103 contemplan un modelo sin cabecera o sin detalles.
- `Program.cs` de ambos proyectos, `CartService`, `CartDto` y la vista `CartIndex` no presentan diferencias respecto de la base en esta comparación. No se encontró un cambio de rutas o de nombre de vista que explique el incidente.

Por tanto, tras un borrado exitoso, el 404 esperado por el código aparece en la consulta **posterior** a la redirección. Si `RemoveCart` devuelve 404 por detalle inexistente o ajeno, `Remove` devuelve 404 directamente y no emite esa redirección: es otro camino que la traza debe distinguir.

## Origen del HTTP 404 y errores de dependencias

**Confirmado por lectura:** hay un origen explícito en ShoppingCartAPI (`GetCart`, cabecera ausente) y una propagación explícita en Mango.Web (`BaseService` → cargador → `CartIndex`). El navegador recibe una respuesta nueva de MVC con ese estado; no recibe directamente la respuesta HTTP de la API. El 404 final puede ser una decisión de la acción encontrada correctamente, no un fallo de enrutamiento.

**Probable para el incidente reportado:** el caso sin cabecera, al acceder a un carrito nunca creado o después de eliminar su último detalle. La coincidencia con el funcionamiento al conservar otros productos respalda esta explicación; no prueba el estado real de los datos.

**No confirmado en ejecución:** que el usuario tenga una cabecera ausente, que cookie y bearer correspondan al mismo subject, que se haya emitido el 302 tras el borrado y que el proceso desplegado corresponda al árbol inspeccionado. Un 404 por identidad distinta, configuración de URL, despliegue o infraestructura requiere evidencia adicional; no se ha demostrado aquí.

En la implementación actual, ProductService lanza ante HTTP no exitoso, incluido 404, o fallos de transporte/contenido. `GetCart` captura la excepción y devuelve **500**, no reexpide el 404 descendente. CouponService devuelve null para su 404 (sin descuento y posible 200); otros errores propagados también terminan en **500** de GetCart. Si falla el transporte entre Web y ShoppingCartAPI, `BaseService` devuelve una respuesta fallida sin `HttpStatusCode`, y el cargador MVC utiliza **502**. Ninguno de esos caminos explica por sí mismo el 404 específico de cabecera ausente.

En main, los clientes descendentes no verifican explícitamente todos los estados HTTP: su resultado depende del cuerpo recibido. ProductService puede devolver una lista vacía ante `IsSuccess = false`, y CouponService puede devolver null ante un cuerpo vacío deserializado como null. También pueden producirse excepciones que GetCart devuelve como 500 y MVC oculta con la vista vacía. No debe restaurarse ese tratamiento general de fallos para corregir este incidente.

## Corrección mínima recomendada, sin implementar

Adaptar **solo la presentación de `CartController.CartIndex`** para reconocer el HTTP 404 de la consulta del carrito del propio usuario autenticado como ausencia de carrito y renderizar la vista existente con un `CartDto` vacío no nulo. Restablecer expresamente **HTTP 200** antes de devolver la vista: el cargador ya dejó `Response.StatusCode` en 404, por lo que cambiar solamente `StatusCode(...)` por `View(...)` no basta.

La excepción debe quedar limitada al resultado de `GetCart` solicitado con el identificador válido del principal autenticado; no aceptar un identificador arbitrario del cliente. Conservar la propagación de 400/401/403/500/502 y demás fallos. Mantener el cargador compartido y los rechazos de Checkout y EmailCart para evitar tratar como válido un carrito ausente en esas operaciones. No cambiar el tratamiento de los 404 de Remove, ApplyCoupon o RemoveCoupon, ni introducir un fallback general en BaseService.

El cliente actual descarta el cuerpo de errores y no puede distinguir semánticamente un 404 de recurso de uno producido por una URL mal configurada. Antes de implementar el fallback, confirmar la traza de la ruta y el origen descritos arriba. La propuesta usa el contrato actual de `GetCart`; no añade un discriminador que permita consultar existencia ajena.

No es necesario conservar artificialmente una cabecera vacía, modificar RemoveCart, crear datos durante GET ni convertir la ausencia de cabecera en HTTP 200 en ShoppingCartAPI. Esa última alternativa alteraría el contrato explícito del hardening y excedería la corrección mínima de compatibilidad MVC.

## Impacto en seguridad y trazabilidad

La propuesta puede corregir la experiencia sin reducir el hardening: conserva JWT obligatorio, consulta por subject, restricciones de propietario en todas las escrituras, validación de identificadores, ausencia de privilegio especial ADMIN y reconstrucción segura del correo. La API sigue devolviendo 404 indistinguibles para recursos ajenos e inexistentes; renderizar una vista vacía del usuario actual no entrega datos ajenos ni autoriza operaciones.

La propuesta y el diseño vigentes del change declaran intencionalmente el nuevo 404 de GetCart sin cabecera y exigen verificar su compatibilidad con Web. El delta `specs/shopping-cart-api/spec.md` recoge `Foreign or nonexistent cart read or coupon update` y exige conservar la limpieza del último detalle. Las tareas 4.1 y 6.1 corresponden a esos flujos. El documento principal de especificación también exige eliminar la cabecera tras el último detalle. Restaurar la vista vacía en CartIndex es compatible con esos requisitos de API; no implica afirmar que la verificación pendiente del hardening esté cerrada. No se modificaron artefactos OpenSpec.

## Evidencia existente y pendiente para confirmar en ejecución

Lectura de pruebas existentes, sin ejecutarlas ni atribuirles un resultado nuevo:

- `GetCartAuthorizationTests.ForeignAndMissingCartsHaveIdenticalResponsesWithoutExternalCallsOrWrites` exige equivalencia de 404 para ajeno y propio inexistente.
- `ProductFailureIntegrationTests.ValidEmptyProductListAllowsEmptyCart` construye una cabecera sin detalles y espera éxito. `ProductFailureDoesNotSucceedForCartWithoutItems` espera 500 ante fallo de ProductAPI con cabecera vacía. No prueban la presentación MVC de un carrito sin cabecera.
- `WebCompatibilityTests.CartActionsPreserveRejectedHttpStatus` (líneas 56–71) exige actualmente 404 también en CartIndex ante un error simulado, sin distinguir ausencia del carrito propio; esa expectativa necesitaría revisión acotada si se implementara la propuesta.
- `MvcHttpIntegrationTests.LoginCookieMvcViewsAndCartAuthorizationUseRealJwt` configura `AllowAutoRedirect = false`; tras eliminar el último detalle comprueba el 302 y la eliminación, pero no sigue la redirección con un token válido. Antes del siguiente CartIndex reemplaza el token por uno inválido y espera 401 (líneas 123–130). No cubre el resultado final de este incidente.

Para una reproducción futura expresamente autorizada faltan:

1. Traza correlacionada del acceso directo sin cabecera: GET de Web, GET exacto a ShoppingCartAPI, estado/cuerpo de API y estado final de MVC, comprobando identidad y versión desplegada sin registrar tokens.
2. Traza del último borrado: RemoveCart 200 exitoso, respuesta MVC 302 y su Location, seguida de GetCart 404 y CartIndex 404 con la misma sesión válida. Distinguirla de un rechazo 404 del propio borrado.
3. Comparación controlada con la base: API 500 y navegador 200 vacío para ausencia de cabecera; confirmar también 200 vacío con cabecera existente sin detalles y 200 al conservar productos, con dependencias válidas.
4. Verificación posterior de la corrección propuesta: ambos recorridos vacíos deben terminar en 200 con el mensaje existente; errores de autenticación, recursos ajenos, borrados rechazados y fallos de dependencias deben conservar sus rechazos. No afirmar equivalencia entre un fallo de dependencia y un carrito vacío.

Estas comprobaciones no se realizaron: las restricciones de este diagnóstico impiden ejecutar aplicaciones, pruebas con servicios/bases de datos o concurrencia.

## Estado de Git

Antes del diagnóstico: rama `feature/harden-shoppingcart-authorization`, HEAD `f1551d5`, siete archivos rastreados modificados y cinco documentos sin seguimiento; sin cambios staged. Se conservaron todos esos archivos. Esta intervención añade únicamente `docs/audits/shoppingcart-authorization/cart-empty-regression-diagnosis.md` sin seguimiento. No se cambió de rama ni se crearon worktrees, commits o pushes.
