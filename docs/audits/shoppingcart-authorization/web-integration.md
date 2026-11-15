# Integración de Mango.Web con ShoppingCartAPI

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
