# Integración de Mango.Web con ShoppingCartAPI

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
