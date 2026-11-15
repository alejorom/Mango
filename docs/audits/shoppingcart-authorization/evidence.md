# Evidencias: autorización de ShoppingCartAPI

## Objetivo y alcance

Evaluar el riesgo de acceso entre usuarios y delimitar una corrección de seguridad para los cinco endpoints de `CartAPIController`: `GetCart`, `ApplyCoupon`, `CartUpsert`, `RemoveCart` y `EmailCartRequest`. Se revisaron la especificación vigente y cambios archivados, autenticación JWT, clientes de Mango.Web, propagación HTTP y el consumidor de email. Las conclusiones describen inspección estática; no se ejecutaron solicitudes ni pruebas de integración.

## Estado actual

ShoppingCartAPI registra JWT Bearer y valida firma, emisor y audiencia; su pipeline incluye autenticación y autorización (`Mango.Services.ShoppingCartAPI/Extensions/WebApplicationBuilderExtensions.cs:19-32`, `Mango.Services.ShoppingCartAPI/Program.cs:61,75-76`). El controlador y sus acciones no exigen autorización (`Mango.Services.ShoppingCartAPI/Controllers/CartAPIController.cs:13-15,26-194`); la spec vigente lo documenta (`openspec/specs/shopping-cart-api/spec.md:192-202`).

AuthAPI emite `sub` con el identificador del usuario (`Mango.Services.AuthAPI/Service/JwtTokenGenerator.cs:38`). ShoppingCart utiliza identificadores de ruta o cuerpo sin compararlos con un principal autenticado. Mango.Web obtiene `sub` para consultar y crear carritos y su cliente HTTP adjunta el token por defecto (`Mango.Web/Controllers/CartController.cs:34-35`, `Mango.Web/Controllers/HomeController.cs:60-80`, `Mango.Web/Service/BaseService.cs:21-33`). La representación efectiva de `sub` como claim en ShoppingCart y el funcionamiento de extremo a extremo requieren pruebas.

## Hallazgos de seguridad

Las severidades son estimaciones basadas en el impacto y el acceso observable en código; la explotación efectiva depende de despliegue, configuración y datos.

| ID | Endpoint o componente | Comportamiento observado y riesgo | Evidencia | Severidad y justificación | Corrección recomendada |
|---|---|---|---|---|---|
| SC-01 | Las cinco acciones | No se exige autenticación en el controlador ni en las acciones; un cliente que alcance la API puede invocarlas sin token. | `CartAPIController.cs:13-15,26-194`; `openspec/specs/shopping-cart-api/spec.md:192-202` | **Crítica**: expone lectura, escritura y publicación a llamadas anónimas. | Exigir JWT válido en todas las acciones y verificar el rechazo sin token válido. |
| SC-02 | `GET GetCart/{userId}` | La consulta usa el identificador de la ruta sin comprobar identidad; permite intentar leer el carrito de otro usuario conocido. | `Mango.Services.ShoppingCartAPI/Controllers/CartAPIController.cs:26-36` | **Alta**: posible divulgación de contenido del carrito entre usuarios. | Comparar con la identidad autenticada antes de consultar y limitar el resultado al propietario. |
| SC-03 | `POST ApplyCoupon` y `POST CartUpsert` | Las búsquedas usan `CartHeader.UserId` del cuerpo; un cliente puede apuntar a un carrito ajeno para cambiar cupón o artículos. Upsert también mapea DTO recibidos. | `Mango.Services.ShoppingCartAPI/Controllers/CartAPIController.cs:81-87,101-137` | **Alta**: posible modificación de datos de otro usuario. | Validar la identidad del cuerpo y obtener o asignar la propiedad desde el principal; no confiar en identificadores o entidades anidadas del cliente. |
| SC-04 | `POST RemoveCart` | Busca un detalle por `cartDetailsId` y lo elimina sin comprobar la propiedad del header asociado. | `Mango.Services.ShoppingCartAPI/Controllers/CartAPIController.cs:152-175` | **Alta**: posible eliminación de artículos de otro usuario. | Resolver el detalle junto con su carrito persistido y verificar propiedad antes de borrar. |
| SC-05 | `POST EmailCartRequest` | Publica el DTO recibido sin comprobar propiedad ni contenido. EmailAPI usa el email y contenido del mensaje recibido para registrar la notificación. | `Mango.Services.ShoppingCartAPI/Controllers/CartAPIController.cs:189-194`; `Mango.Services.EMailAPI/Service/AzureServiceBusConsumer.cs:69-78`; `Mango.Services.EMailAPI/Service/EmailService.cs:23-54,69-80` | **Alta**: posible publicación de datos o destinatarios manipulados; el impacto operativo requiere prueba. | Definir validación o reconstrucción del carrito y del destinatario antes de publicar; comprobar ausencia de publicación en rechazos. |

## Propuesta de solución

Exigir autenticación JWT en las cinco acciones y resolver una identidad estable desde el token validado, verificando en pruebas el mapeo del claim. Rechazar identidades ausentes o no válidas. Comparar cualquier identificador de usuario recibido con esa identidad y comprobar la propiedad de recursos persistidos antes de acceder o mutar; para nuevas cabeceras, establecer la propiedad desde el principal. Los identificadores anidados del cliente no deben habilitar acceso a otro carrito.

Como contrato propuesto, devolver **401** sin token válido y una respuesta acordada de **403 o 404** ante recurso ajeno; mantener el comportamiento existente para operaciones autorizadas y el 404 de detalle inexistente, salvo decisión explícita. Preservar rutas y estructuras existentes cuando sea viable. La autenticación obligatoria y los rechazos de propiedad son cambios de comportamiento y requieren actualización de la spec y revisión de consumidores.

## Impacto

- **ShoppingCartAPI:** autorización de acciones, comprobación de propiedad y cobertura de pruebas. El código y la spec actuales no aplican estas medidas.
- **Mango.Web:** sus clientes envían bearer y ya reconocen 401, 403 y 404 (`Mango.Web/Service/BaseService.cs:63-68`); se deben probar los flujos y el tratamiento de errores, incluidos formularios con identificadores manipulados.
- **Integraciones:** ShoppingCart propaga el token a Product y Coupon (`Mango.Services.ShoppingCartAPI/Program.cs:30-32`; `Mango.Services.ShoppingCartAPI/Utility/BackendApiAuthenticationHttpClientHandler.cs:17-19`). Email consume los mensajes publicados. Debe probarse la compatibilidad del payload y la propagación; no se identificaron otros consumidores directos en el código revisado.
- **Decisiones previas:** el baseline identificó la falta de autorización y propuso comprobar `sub` (`openspec/changes/archive/shoppingcart-api-baseline/design.md:38,46`). El fix de errores dejó explícitamente fuera autorización, múltiples ítems y poblado de datos de contacto (`openspec/changes/archive/fix-shoppingcart-error-handling/proposal.md:14`).

## Criterios de aceptación propuestos

1. Cada acción acepta una solicitud válida del propietario y conserva el resultado funcional y el contrato existente aplicable.
2. Sin token, con token inválido o expirado, cada acción rechaza con 401 sin leer ni modificar recursos ni publicar mensajes. Una identidad ausente o no utilizable se rechaza según el contrato que se apruebe.
3. Un usuario no puede consultar, aplicar cupón, crear o modificar detalles, eliminar ni solicitar email para un carrito ajeno, incluso manipulando identificadores de ruta, cuerpo, detalle o entidades anidadas. El rechazo no cambia datos ni publica mensajes.
4. Un detalle inexistente conserva el tratamiento de recurso no encontrado; un detalle ajeno devuelve la respuesta de autorización acordada. Se prueban también identificadores vacíos e inconsistentes.
5. EmailCart solo publica contenido y destinatario que cumplan la política aprobada. Se verifica que ningún payload rechazado llegue al bus.
6. Las pruebas de integración comprueban `sub` y su mapeo real, autenticación y propiedad en todas las rutas, propagación hacia Product/Coupon y flujos de Mango.Web. No se encontraron proyectos de pruebas en la solución inspeccionada.

## Decisiones pendientes

- Elegir 403 o 404 para recursos ajenos y el tratamiento de identidad ausente.
- Decidir si se rechaza un `UserId` discrepante o se elimina la dependencia de ese campo; definir compatibilidad de los DTO.
- Decidir si `ADMIN` puede acceder a carritos ajenos; no se observó una excepción actual.
- Definir la fuente confiable del destinatario y si el contenido de email se valida o reconstruye desde datos persistidos, considerando los campos de contacto no persistidos.
- Acordar alcance de pruebas e integración y el tratamiento de clientes existentes antes de generar la propuesta OpenSpec.
