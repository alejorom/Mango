# Integración de Mango.Web con ShoppingCartAPI

## Pipeline HTTP MVC con cookie

La prueba `MvcHttpIntegrationTests` levanta hosts locales controlados con controladores MVC, autenticación por cookie y vistas reales de Mango.Web, y middleware JWT y controladores reales de ShoppingCartAPI. El servicio de login de AuthAPI se sustituye por un stub que entrega un JWT firmado por su `JwtTokenGenerator`; por tanto, la validación de credenciales y el endpoint de login real de AuthAPI quedan fuera de esta evidencia. Los productos, cupones y bus son simulados. El carrito utiliza la base SQL aislada verificada por `SqlLabTestContext`, con datos sintéticos y limpieza por propietario; no se aplican migraciones.

Se comprobó la redirección anónima a login, el renderizado de login y carrito, la emisión y utilización de cookies de autenticación y token, y la propagación del JWT validado. Por HTTP MVC se ejercitaron GetCart, CartUpsert, ApplyCoupon, EmailCartRequest y RemoveCart; se verificaron los cambios SQL autorizados, una publicación simulada, 400 para identidad malformada, 401 para JWT inválido y 404 para un carrito ajeno sin cambios ni publicación. La prueba pasó; dos ejecuciones iniciales fallaron por dependencias ausentes en el host de prueba, corregidas únicamente en el fixture. La compilación, validación estricta OpenSpec y `git diff --check` pasaron. Con esta evidencia y las pruebas previas, 6.1 queda completada.

## Escrituras SQL con JWT firmado y acciones MVC

El host de prueba usa el middleware JWT y los controladores reales de ShoppingCartAPI; cada conexión SQL valida la instancia local y `Mango_ShoppingCart_Concurrency_Test` antes de operar. Los usuarios y datos son sintéticos, se eliminan por propietario al terminar y no se ejecutan migraciones. Product/Coupon y el bus se simulan. El JWT fue firmado con el generador de AuthAPI y validado por ShoppingCartAPI; no se ejecutó el login de AuthAPI.

Se invocaron directamente las acciones MVC de Web para lectura, incremento de producto, aplicación de cupón, correo y eliminación; las escrituras propias se verificaron en SQL. Con un segundo JWT se comprobó 404 para intentos ajenos de RemoveCart, ApplyCoupon y CartUpsert, y que el detalle y cupón propios permanecieran sin cambios. Se verificó la publicación autorizada mediante el bus simulado. No se comprobaron bloqueos ni concurrencia.

Los controladores MVC se invocaron directamente con un principal construido a partir de los claims del JWT; esta prueba **no ejecuta el middleware de cookie de Mango.Web, el login ni el renderizado HTTP completo de las vistas**. Por tanto, 6.1 sigue pendiente de esos recorridos. La propagación del bearer validado hacia Product y Coupon y sus fallos se verificó con clientes HTTP controlados; 6.2 se considera completada. La ejecución final de la batería filtrada aprobó 24 pruebas. Una ejecución inicial recibió 400 en EmailCart porque el producto simulado carecía de campos requeridos; se corrigió el fixture, no el producto.

## Corrección del cliente CouponAPI

`GetByCode` de CouponAPI devuelve 404 cuando el código no existe. CouponService ahora reserva null para ese estado, conserva el código de otros fallos HTTP en una excepción de mensaje genérico, trata fallos de comunicación y rechaza cuerpos exitosos vacíos o inválidos. El controlador existente devuelve 500 ante un fallo descendente; no lo presenta como 401 del usuario. No se cambiaron reglas de descuento ni contratos.

Con JWT firmado por el generador de AuthAPI, el host controlado verificó descuento para cupón válido, ausencia de descuento para 404, error de GetCart ante 401 descendente y ausencia de nueva publicación de EmailCart ante fallos. Las pruebas del cliente cubrieron 400, 401, 403, 404, 500, cuerpo vacío y fallo de comunicación. ApplyCoupon no consulta CouponAPI: sus dos pruebas existentes de persistencia y rechazos pasaron en SQL Server aislado, con datos sintéticos. Un primer intento desde el sandbox falló al establecer la conexión cifrada; el intento autorizado fuera de él pasó. No se han comprobado todos los recorridos MVC o las escrituras con JWT real, por lo que 6.1 y 6.2 siguen pendientes.

Resultado final: 34 pruebas filtradas de integración/controladores y 2 de ApplyCoupon aprobadas. No se ejecutaron pruebas de concurrencia.

## Integración con JWT firmado

Se ejecutó un host de prueba local con el middleware JWT y controlador reales de ShoppingCartAPI, sin iniciar `Program.cs` ni sus migraciones. Se utilizó EF InMemory, un bus simulado, respuestas HTTP controladas de Product/Coupon y usuarios sintéticos. El JWT fue firmado por `JwtTokenGenerator` de AuthAPI con configuración de prueba; no se ejecutó el endpoint de login de AuthAPI. `TokenProvider`, `BaseService` y `CartService` de Web enviaron ese token al host. El bearer recibido en ambos clientes descendentes coincidió con el emitido y validado.

La prueba ejercitó GetCart propio (éxito), identificador malformado (400), carrito ajeno (404), JWT inválido (401), y las cuatro acciones POST sin token (401). EmailCart propio publicó una vez al bus simulado; ante un 401 controlado de Product o Coupon no se publicó otro mensaje. Las pruebas Web existentes verifican también 400/401/404 con transporte simulado. No se ejecutaron operaciones relacionales exitosas de ApplyCoupon, CartUpsert o RemoveCart ni un flujo MVC completo; EF InMemory no sustituye SQL Server para esos escenarios.

**Defecto reproducido:** con un cupón persistido y respuesta HTTP 401 vacía de CouponAPI, `CouponService.GetCoupon` deserializa null y devuelve null indistinguible de un cupón no encontrado (`Mango.Services.ShoppingCartAPI/Service/CouponService.cs`). `CartAPIController.GetCart` omite el descuento si recibe null y devuelve HTTP 200 con `IsSuccess = true` y descuento cero (`Controllers/CartAPIController.cs`). La prueba `RealJwtWebIntegrationTests.IssuedJwtFlowsFromWebToOwnedCartAndDownstream` confirma el resultado y el bearer real. El mismo fallo en EmailCart no produce publicación. Corrección mínima propuesta, pendiente de autorización: distinguir el error HTTP descendente de un cupón no encontrado en CouponService, conservando su código internamente y dejando que el controlador responda un error controlado; verificar la semántica de 404 de CouponAPI antes de decidir su tratamiento.

Resultado: 18 pruebas filtradas aprobadas; una ejecución intermedia falló por una aserción de prueba sensible a mayúsculas en nombres JSON, corregida en el harness. 6.1 y 6.2 siguen pendientes. No se modificó código productivo, ni se ejecutaron pruebas de concurrencia, migraciones o escrituras SQL.

## Corrección del cliente ProductAPI

El cliente deserializaba el cuerpo antes de comprobar HTTP y accedía a `IsSuccess` sobre null cuando el cuerpo estaba vacío. Ahora rechaza estados no exitosos con `HttpRequestException` de mensaje fijo y `StatusCode` conservado; una respuesta HTTP exitosa con cuerpo vacío se rechaza mediante `InvalidDataException`. El procesamiento de respuestas válidas se conserva. No se añade un mapeo del 401 descendente al 401 público: el controlador existente captura el fallo y devuelve 500. El mensaje fijo del fallo HTTP evita reflejar el cuerpo o la descripción remitidos por ProductAPI.

Se ejecutaron 28 pruebas filtradas de compatibilidad Web, correo y GetCart: todas pasaron. Incluyen HTTP 400/401/404/500 descendente, 401 con cuerpo vacío, 200 vacío, respuesta válida, bearer sintético y rechazos HTTP de Web. La propagación de token se prueba con autenticación simulada, no como JWT real de extremo a extremo. Compilación y validación estricta OpenSpec satisfactorias; 6.1 y 6.2 permanecen pendientes por falta de verificación de recorridos completos y de propagación y fallos bajo autenticación real. No se ejecutaron pruebas de concurrencia.

## Intervención de compatibilidad

- Se reprodujo con pruebas controladas una excepción al solicitar correo después de un GetCart fallido y la interpretación como éxito de HTTP 400 con cuerpo `{}`. Se cambió Mango.Web para conservar el estado HTTP de errores del carrito, no deserializar sus cuerpos como éxito y evitar el acceso a una cabecera inexistente. Remove, ApplyCoupon, RemoveCoupon y EmailCart devuelven el código de error en lugar de buscar vistas inexistentes. No se alteró el contrato JSON de ResponseDto; el estado se conserva internamente.
- Las pruebas controladas comprueban las cinco rutas de CartService, bearer sintético y respuestas 200/400/401/404; para los controladores prueban 400/401/404 en CartIndex, Remove, ApplyCoupon, RemoveCoupon y EmailCart. No se han ejercitado todas las vistas, recorridos completos ni un JWT validado de extremo a extremo.
- Se comprobó con HTTP simulado y un contexto de autenticación simulado que los clientes Product y Coupon usan el handler y reenvían el access token almacenado. Esto no prueba la autenticación real de extremo a extremo.
- **Bloqueo 6.2 identificado anteriormente, corregido en la intervención posterior:** un HTTP 401 con cuerpo vacío de ProductAPI causaba `NullReferenceException` en `ShoppingCartAPI/Service/ProductService.cs`, mientras CouponService devuelve null ante el mismo cuerpo. El controlador de ShoppingCartAPI captura excepciones y responde 500; no se ha probado aquí una solicitud integrada por todo el controlador.
- Resultado de `WebCompatibilityTests`: 12 pruebas aprobadas, incluida la prueba que documenta el comportamiento defectuoso actual del cliente Product. La restauración inicial dentro del sandbox falló por acceso a NuGet; se ejecutó posteriormente con los permisos correspondientes sin actualizar versiones.
- 6.1 y 6.2 siguen pendientes: faltan flujos completos y resolución/aceptación del fallo descendente. No se modificó ShoppingCartAPI ni se retomó 4.2.

## Alcance y estado

Inspección estática de las tareas 6.1 y 6.2 del cambio `harden-shoppingcart-authorization`. Se detuvo al identificar incompatibilidades en el tratamiento de respuestas de error. No se ejecutaron flujos ni integraciones simuladas; las tareas permanecen pendientes. No se realizaron escrituras en bases de datos.

## Flujos y JWT

- `Mango.Web/Service/CartService.cs:15` implementa clientes para ApplyCoupon, EmailCartRequest, GetCart, RemoveCart y CartUpsert. Todos usan `BaseService.SendAsync` con el valor predeterminado `withBearer = true`; `Mango.Web/Service/BaseService.cs:21` y `:30-33` muestran que el token procede de `ITokenProvider` y se agrega a la cabecera Authorization. Esto es evidencia estática, no confirma la recepción por ShoppingCartAPI.
- `Mango.Web/Controllers/CartController.cs:32-41` obtiene el subject y consulta el carrito; `HomeController.cs:59-80` construye la solicitud de alta a partir del subject. Los demás flujos de carrito se encuentran en `CartController.cs:44-53`, `:74-96` y `:139-149`. No se han ejercitado de extremo a extremo.
- `Mango.Services.ShoppingCartAPI/Program.cs:30-33` configura los clientes HTTP Product y Coupon con `BackendApiAuthenticationHttpClientHandler`. El handler obtiene `access_token` del contexto y construye una cabecera Bearer (`Utility/BackendApiAuthenticationHttpClientHandler.cs:15-21`). La propagación efectiva y el comportamiento de fallos de ambos clientes siguen sin probarse. La inspección no identificó una dependencia nueva en estas rutas.

## Incompatibilidades observadas por inspección

1. `BaseService.cs:61-74` transforma 401 y 404 en `ResponseDto` fallidos y trata 400 mediante la deserialización genérica del cuerpo. En `CartController.cs:44-53`, `:74-96` y `:139-149`, el fallo de Remove, ApplyCoupon, RemoveCoupon o EmailCart devuelve `View()` sin indicar otra vista. Bajo la búsqueda convencional de MVC eso requiere vistas con esos nombres, ausentes en `Mango.Web/Views/Cart/`; el fallo puede terminar en error de búsqueda de vista en vez de una respuesta de usuario controlada. No se ha reproducido en ejecución.
2. Si GetCart devuelve 401, 404 u otro resultado fallido, `CartController.cs:32-41` devuelve un `CartDto` sin inicializar `CartHeader` (`Models/CartDto.cs:3-6`). `EmailCart` accede después a `cart.CartHeader.Email` (`CartController.cs:139-143`), lo que provoca una referencia nula en ese camino. Un carrito inexistente también puede aparecer como carrito vacío en CartIndex; la vista comprueba expresamente una cabecera nula (`Views/Cart/CartIndex.cshtml:1-2`). No se ha reproducido en ejecución.

## Pendiente

- Decidir y autorizar la corrección de los caminos de error de Mango.Web antes de continuar 6.1. Verificar mediante flujos controlados las respuestas 400, 401 y 404 y los cinco recorridos.
- Completar 6.2 con clientes HTTP controlados que prueben el reenvío efectivo de JWT y los fallos de ProductAPI y CouponAPI. No se ha modificado código productivo ni se ha retomado la tarea 4.2.
