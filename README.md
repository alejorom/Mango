Prompt usados para documentar especificaciones SDD

# Mango.Services.CouponAPI
Actuá como analista técnico documentando este microservicio (.NET 8, EF Core) 
para un proceso de Spec-Driven Development. Escaneá el proyecto 
Mango.Services.CouponAPI y generame un resumen en markdown con estas secciones:

1. **Entidades de dominio** — modelos/entities y su DbContext (campos, relaciones, 
   validaciones a nivel de modelo)
2. **Endpoints expuestos** — cada endpoint del/los Controller(s): método HTTP, ruta, 
   qué recibe, qué devuelve, y su propósito de negocio
3. **Reglas de negocio** — validaciones o lógica que encuentres en Services/Controllers 
   (ej: reglas de descuento, códigos únicos, expiración de cupones, etc.)
4. **Autenticación/Autorización** — si el API valida algo de .NET Identity o tokens, 
   y qué endpoints están protegidos
5. **Dependencias externas** — paquetes NuGet clave, configuración de DB, si ya tiene 
   algo de mensajería (MessageBus) conectado o referenciado
6. **Puntos ambiguos o incompletos** — cualquier cosa que te parezca que falta, está 
   a medio hacer, o no está clara desde el código

No inventes nada que no esté en el código. Si algo no existe todavía, decilo explícitamente.

## Uno
/opsx-propose

Este NO es un cambio nuevo. Necesito documentar el ESTADO ACTUAL del 
microservicio Mango.Services.CouponAPI como baseline inicial del spec, 
ya que es código existente y todavía no tiene documentación formal.

Escaneá el proyecto y generá la propuesta cubriendo:
1. Entidades de dominio (modelos + DbContext)
2. Endpoints expuestos (método, ruta, propósito)
3. Reglas de negocio encontradas en Services/Controllers
4. Autenticación/Autorización (.NET Identity)
5. Dependencias externas (NuGet, DB, MessageBus si ya está referenciado)
6. Puntos ambiguos o incompletos

No inventes nada que no esté en el código.

## Dos
/opsx-propose

Corregir el manejo de errores en Mango.Services.CouponAPI (MODIFIED Requirements 
sobre el spec existente coupon-api):

1. Los endpoints GET/DELETE por id y por code no deben devolver HTTP 200 cuando 
   el recurso no existe — deben usar 404 Not Found.
2. Reemplazar el uso de .First() por .FirstOrDefault(), con manejo explícito 
   de "no encontrado" en vez de depender de una excepción.
3. Agregar validación server-side en CouponDto (CouponCode requerido, 
   DiscountAmount > 0, MinAmount >= 0).
4. No exponer el mensaje crudo de la excepción (ex.Message) al cliente en 
   la respuesta.

Esto modifica requirements ya existentes en openspec/specs/coupon-api/spec.md, 
no agrega un servicio nuevo.


# Mango.Services.CouponAPI
## Uno 
/opsx-propose

Este NO es un cambio nuevo. Necesito documentar el ESTADO ACTUAL del 
microservicio Mango.Services.AuthAPI como baseline inicial del spec, 
ya que es código existente y todavía no tiene documentación formal.

Escaneá el proyecto y generá la propuesta cubriendo:
1. Entidades de dominio (modelos, ApplicationUser, DbContext — incluyendo si 
   usa ASP.NET Core Identity)
2. Endpoints expuestos (registro, login, asignación de roles, etc.)
3. Reglas de negocio (validaciones de password, unicidad de email/username, 
   generación y expiración de JWT)
4. Autenticación/Autorización (cómo se emite el JWT: claims, secret, issuer, 
   audience — este es el servicio que genera los tokens que Coupon y otros 
   consumen)
5. Dependencias externas (paquetes NuGet, DB, y OJO: si ya tiene referencia 
   a MessageBus/Service Bus, dado que vimos un error de configuración ahí 
   al hacer login)
6. Puntos ambiguos o incompletos

No inventes nada que no esté en el código.

## Dos
/opsx-propose

Corregir varios problemas encontrados en Mango.Services.AuthAPI durante el 
baseline (MODIFIED Requirements sobre openspec/specs/auth-api/spec.md):

CRÍTICO - Seguridad:
1. El endpoint POST /api/auth/AssignRole no tiene ningún control de autorización 
   — cualquier caller sin token puede asignarse o asignar a otros el rol ADMIN. 
   Debe requerir un JWT válido con rol ADMIN, igual que los endpoints de 
   escritura en CouponAPI.

Bugs que pueden crashear (HTTP 500 no controlado):
2. En AuthService.Login, CheckPasswordAsync(user, ...) se llama antes de 
   verificar si user es null. Reordenar: verificar null primero, y si el 
   usuario no existe, devolver directamente el resultado de "credenciales 
   incorrectas" sin llamar a CheckPasswordAsync.
3. En AuthAPIController.AssignRole, model.Role.ToUpper() se llama sin validar 
   que Role no sea null/vacío. Agregar validación antes de llamar al service.

Deuda técnica:
4. El catch vacío en AuthService.Register no registra el error en ningún lado. 
   Agregar logging del error real (no exponerlo al cliente, solo loguearlo 
   server-side).

Fuera de alcance para este change: el error no controlado del MessageBus al 
publicar en registro (requiere decisión de arquitectura más amplia sobre 
manejo de fallos en mensajería, se tratará en un change aparte).


# Mango.Services.ProductAPI
## Uno 
/opsx-propose

Este NO es un cambio nuevo. Necesito documentar el ESTADO ACTUAL del 
microservicio Mango.Services.ProductAPI como baseline inicial del spec, 
ya que es código existente y todavía no tiene documentación formal.

Escaneá el proyecto y generá la propuesta cubriendo:
1. Entidades de dominio (modelos + DbContext)
2. Endpoints expuestos (método, ruta, propósito)
3. Reglas de negocio encontradas en Services/Controllers
4. Autenticación/Autorización (patrón de JWT: ¿usa el mismo 
   WebApplicationBuilderExtensions.AddAppAuthetication() que CouponAPI, 
   leyendo ApiSettings:Secret directo, o el patrón anidado de AuthAPI?)
5. Dependencias externas (NuGet, DB, y si tiene referencia a MessageBus/
   Service Bus — ProductAPI suele ser consumido por ShoppingCartAPI para 
   validar productos, así que prestá atención a eso)
6. Puntos ambiguos o incompletos (aplicá el mismo criterio que ya usamos: 
   .First() vs FirstOrDefault, códigos HTTP en errores, validación de DTOs, 
   manejo de excepciones)

No inventes nada que no esté en el código.

## Dos
/opsx-propose

Corregir varios problemas encontrados en Mango.Services.ProductAPI durante 
el baseline (MODIFIED Requirements sobre openspec/specs/product-api/spec.md):

1. GET /api/product y GET /api/product/{id} permanecen SIN autenticación 
   — esto es intencional (catálogo público de una tienda), NO se debe agregar 
   [Authorize] a ninguno de los dos. Documentar explícitamente esta decisión 
   en el spec para que quede claro que es a propósito, no un descuido.

2. PUT /api/product llama a Update() sin verificar primero que el producto 
   exista. Cambiar a: buscar el producto por ProductId con FirstOrDefaultAsync, 
   si no existe devolver 404, si existe actualizar sus campos y guardar.

3. Reemplazar .FirstAsync() por .FirstOrDefaultAsync() en Get(id) y Delete(id), 
   con manejo explícito de "no encontrado" devolviendo 404 en vez de depender 
   de la excepción.

4. Cambiar el tipo de retorno de todas las acciones del controller de 
   ResponseDto a ActionResult<ResponseDto>, para poder devolver códigos HTTP 
   reales (404, 400, 500) en vez de siempre 200.

5. Agregar validación server-side en ProductDto: Name requerido (no vacío), 
   Price dentro del rango 1-1000 (mismo rango que ya existe en la entidad 
   Product pero nunca se aplicaba). Payloads inválidos en POST/PUT devuelven 
   400 con el detalle de validación.

6. Dejar de exponer ex.Message crudo al cliente en los catch genéricos; usar 
   un mensaje fijo y devolver StatusCode(500, ...).

Fuera de alcance para este change: falta de paginación en GET /api/product, 
falta de unicidad en Name, y el AddAuthentication() duplicado en Program.cs 
(candidatos a limpieza menor, sin urgencia).

# Mango.Services.ShoppingCartAPI
## Uno
/opsx-propose

Este NO es un cambio nuevo. Necesito documentar el ESTADO ACTUAL del 
microservicio Mango.Services.ShoppingCartAPI como baseline inicial del spec, 
ya que es código existente y todavía no tiene documentación formal.

Escaneá el proyecto y generá la propuesta cubriendo:
1. Entidades de dominio (CartHeader, CartDetails, o como se llamen, y su DbContext)
2. Endpoints expuestos (método, ruta, propósito - agregar/actualizar/eliminar 
   items del carrito, aplicar cupón, checkout, etc.)
3. Reglas de negocio (cómo se calcula el total, cómo se aplican descuentos, 
   qué pasa si un producto ya no existe)
4. Dependencias con OTROS microservicios: confirmá cómo consume ProductAPI 
   (ya sabemos que es vía HTTP, con un HttpClient llamado "Product" y sin 
   EnsureSuccessStatusCode - confirmalo), y verificá si también consume 
   CouponAPI (¿para validar/aplicar cupones al carrito?) - de ser así, 
   documentá el mecanismo exacto (HTTP directo, igual que Product)
5. Autenticación/Autorización (¿mismo patrón de CouponAPI/ProductAPI con 
   ApiSettings:Secret plano, o el anidado de AuthAPI?)
6. Mensajería: ¿tiene referencia a Mango.MessageBus? ¿Para qué (checkout, 
   confirmación de orden)?
7. Puntos ambiguos o incompletos (mismo criterio de siempre: manejo de 
   excepciones, validación de DTOs, códigos HTTP, qué pasa si ProductAPI 
   o CouponAPI no responden o devuelven error)

No inventes nada que no esté en el código.

# Dos
/opsx-propose

Corregir varios problemas encontrados en Mango.Services.ShoppingCartAPI durante 
el baseline (MODIFIED Requirements sobre openspec/specs/shopping-cart-api/spec.md):

1. **CRÍTICO**: CouponService.GetCoupon devuelve new CouponDto() (nunca null) 
   en cualquier fallo, haciendo que el chequeo "coupon != null" en GetCart sea 
   siempre verdadero. Cambiar GetCoupon para devolver null explícitamente cuando 
   CouponAPI falla o el response no es exitoso, y ajustar GetCart para manejar 
   ese null correctamente (no aplicar descuento si el cupón no se pudo resolver).

2. **CRÍTICO**: Si ProductAPI está caído, ProductService.GetProducts() devuelve 
   lista vacía, causando NullReferenceException en GetCart cuando item.Product 
   es null. Agregar un chequeo explícito: si un producto del carrito no se 
   encuentra en la respuesta de ProductAPI, omitirlo del cálculo (o marcarlo 
   como no disponible) en vez de crashear, y reflejar esta situación en la 
   respuesta (por ejemplo, IsSuccess = false con un mensaje claro, o un campo 
   que indique productos no resueltos).

3. Reemplazar .First() por .FirstOrDefault() en RemoveCart (búsqueda de 
   CartDetails por cartDetailsId), con manejo explícito de "no encontrado" 
   devolviendo 404 en vez de depender de la excepción.

4. Cambiar el tipo de retorno de todas las acciones del controller (GetCart, 
   ApplyCoupon, CartUpsert, RemoveCart, EmailCartRequest) de ResponseDto/object 
   a ActionResult<ResponseDto>, para poder devolver códigos HTTP reales 
   (404, 500) en vez de siempre 200.

5. Reemplazar ex.ToString() por un mensaje genérico fijo en ApplyCoupon y 
   EmailCartRequest (mismo criterio que ex.Message en los demás métodos), 
   devolviendo StatusCode(500, ...) en los catch genéricos de los 5 métodos.

6. Eliminar la llamada redundante builder.Services.AddAuthentication() en 
   Program.cs (después de builder.AddAppAuthetication(), que ya configura 
   todo el JWT Bearer) - mismo hallazgo ya corregido en product-api.

Fuera de alcance para este change: agregar [Authorize] al controller y validar 
que el userId coincida con el token (change aparte: 
harden-shoppingcart-authorization). CartUpsert procesando solo el primer item 
de CartDetails y los campos Name/Phone/Email nunca poblados server-side quedan 
también fuera de alcance por ahora, salvo que el análisis del agente sugiera 
que son triviales de corregir junto con lo demás.

# Mango.Services.EmailAPI
## Uno
/opsx-propose

Este NO es un cambio nuevo. Necesito documentar el ESTADO ACTUAL del 
microservicio Mango.Services.EmailAPI como baseline inicial del spec, 
ya que es código existente y todavía no tiene documentación formal.

Escaneá el proyecto y generá la propuesta cubriendo:
1. Entidades de dominio (si persiste algo en DB, o si es puramente un 
   consumidor de mensajería sin estado propio)
2. Endpoints expuestos, si tiene alguno (es posible que este servicio no 
   exponga API REST y solo consuma mensajes del MessageBus)
3. Consumo de MessageBus: ¿qué colas escucha? (ya sabemos de AuthAPI que 
   existe "registeruser", y de ShoppingCartAPI que existe "emailshoppingcart" 
   - confirmá si EmailAPI es quien consume esas colas)
4. Qué hace con cada mensaje (¿envía emails reales? ¿los loguea? ¿los persiste 
   en alguna tabla de "log de emails enviados"?)
5. Dependencias externas (¿usa algún proveedor real de email como SendGrid, 
   SMTP, o solo simula el envío?)
6. Puntos ambiguos o incompletos (mismo criterio de siempre)

No inventes nada que no esté en el código.
