# Copilot Instructions — Mango

## Resumen del proyecto
Mango es una aplicación de e-commerce basada en **arquitectura de microservicios** en .NET 8, construida como proyecto de aprendizaje. Consta de una app web MVC (`Mango.Web`) que consume una serie de APIs REST independientes, cada una con su propia base de datos SQL Server.

## Servicios (proyectos del .sln)
| Proyecto | Rol | Puerto local (https) |
|---|---|---|
| `Mango.Web` | Frontend ASP.NET Core MVC (Razor Views) | 7197 |
| `Mango.Services.AuthAPI` | Autenticación/autorización (Identity + JWT) | 7002 |
| `Mango.Services.ProductAPI` | Catálogo de productos | 7000 |
| `Mango.Services.CouponAPI` | Cupones de descuento | 7001 |
| `Mango.Services.ShoppingCartAPI` | Carrito de compras | 7003 |
| `Mango.Services.EMailAPI` | Envío de correos (consumidor de mensajes) | 7238 |
| `Mango.MessageBus` | Librería compartida para publicar mensajes | (librería, no API) |

Nota: `Mango.Web` resuelve las URLs de las APIs vía `appsettings.json` → sección `ServiceUrls`. No asumas puertos fijos sin revisar ese archivo si cambian.

## Stack técnico
- **.NET 8** (todos los proyectos usan `net8.0`)
- **ASP.NET Core Web API** (controllers clásicos, no Minimal APIs) en los microservicios
- **ASP.NET Core MVC** (controllers + Razor Views) en `Mango.Web`
- **EF Core 8** + **SQL Server** por servicio (cada API tiene su propio `AppDbContext` — sin base de datos compartida)
- **AutoMapper** para mapeo Entity ↔ DTO (`MappingConfig.cs` por servicio, registrado como singleton)
- **JWT Bearer** para autenticación entre servicios (`Microsoft.AspNetCore.Authentication.JwtBearer`)
- **ASP.NET Core Identity** en `AuthAPI` para gestión de usuarios/roles
- **Azure Service Bus** para mensajería asíncrona (vía `Mango.MessageBus`), usando `Newtonsoft.Json` para serializar mensajes. *(Nota: el `AGENTS.md` del repo menciona MassTransit/RabbitMQ, pero el código real usa `Azure.Messaging.ServiceBus` directamente — prioriza lo que veas en el código sobre esa referencia.)*
- **Swashbuckle/Swagger** para documentación de APIs, con seguridad JWT configurada en el swagger UI
- **Duende.IdentityModel** en `Mango.Web` para el flujo de tokens contra `AuthAPI`

## Arquitectura y estructura de carpetas
Cada microservicio API sigue esta estructura consistente:
```
Mango.Services.<Nombre>API/
├── Controllers/        # Un controller principal, ruta api/<recurso>
├── Data/                # AppDbContext
├── Extensions/          # WebApplicationBuilderExtensions.AddAppAuthetication()
├── Migrations/          # EF Core migrations
├── Models/
│   └── Dto/              # DTOs de entrada/salida
├── MappingConfig.cs     # Perfiles de AutoMapper
└── Program.cs            # Composición de servicios (top-level statements)
```

`Mango.Web` sigue el patrón MVC clásico:
```
Mango.Web/
├── Controllers/
├── Models/
├── Service/
│   ├── IService/          # Interfaces (IBaseService, IProductService, etc.)
│   ├── BaseService.cs      # Cliente HTTP genérico compartido
│   ├── TokenProvider.cs    # Manejo de token JWT del usuario
│   └── <Nombre>Service.cs  # Un servicio por API remota consumida
├── Utility/                # Constantes (SD = Static Details), enums como ApiType
└── Views/
```

## Convenciones de código observadas
- **Namespaces y controllers**: `[ApiController]`, ruta explícita `[Route("api/<recurso>")]`, nombre `<Recurso>APIController`.
- **Respuesta uniforme**: todos los endpoints de las APIs devuelven `ActionResult<ResponseDto>`, con un campo `_response` reutilizado por método (`IsSuccess`, `Message`, `Result`).
- **Manejo de errores**: `try/catch` genérico por acción, mensaje de error fijo ("An unexpected error occurred"), `StatusCode(500, _response)`. No se usa middleware global de excepciones — si agregas endpoints, sigue este mismo patrón salvo que se indique lo contrario.
- **Autorización por rol**: operaciones de escritura (`POST`/`PUT`/`DELETE`) suelen llevar `[Authorize(Roles = "ADMIN")]`; lecturas solo `[Authorize]`.
- **DI de AutoMapper**: se registra como instancia (`AddSingleton(mapper)`) construida manualmente desde `MappingConfig.RegisterMaps()`, no vía `AddAutoMapper(...)`.
- **Autenticación JWT**: configurada mediante un método de extensión propio `builder.AddAppAuthetication()` (ojo: nombre con typo, "Authetication" — respétalo si vas a llamarlo o renómbralo explícitamente si el usuario lo pide).
- **Cliente HTTP hacia otras APIs**: `Mango.Web` nunca llama `HttpClient` directo desde un controller. Todo pasa por `IBaseService.SendAsync(RequestDto, withBearer)`, que usa `IHttpClientFactory` con el named client `"MangoAPI"` y agrega el Bearer token vía `ITokenProvider`.
- **Serialización**: `Newtonsoft.Json` se usa de forma consistente en `Mango.Web` y en la mensajería (`Mango.MessageBus`), no `System.Text.Json`.
- **Migraciones**: cada API tiene una función local `ApplyMigration()` en `Program.cs` para aplicar migraciones pendientes al arrancar (a confirmar si se invoca en todos los servicios — verificar antes de asumir que corre automáticamente).

## Comandos de build/test/run
```bash
# Restaurar y compilar toda la solución
dotnet restore Mango.sln
dotnet build Mango.sln

# Ejecutar un servicio específico (ejemplo CouponAPI)
dotnet run --project Mango.Services.CouponAPI

# Aplicar migraciones EF Core de un servicio
dotnet ef database update --project Mango.Services.CouponAPI
```
No se encontraron proyectos de test (`*.Tests.csproj`) en el repo — a confirmar si existen en otra rama o si aún no se han creado.

## Patrones a seguir
- Mantén el aislamiento de datos por microservicio: nunca hagas que un servicio acceda directamente al `AppDbContext` de otro.
- Para nuevos endpoints en una API, replica el patrón `ResponseDto` + AutoMapper + try/catch visto en `CouponAPIController`.
- Para que `Mango.Web` consuma un nuevo endpoint, agrega el método en el `Service` correspondiente usando `IBaseService.SendAsync`, no `HttpClient` directo.
- Comunicación entre servicios de forma asíncrona (ej. notificaciones, emails) → usar `IMessageBus.PublishMessage` hacia Azure Service Bus, no llamadas HTTP síncronas.

## Patrones a evitar
- No introduzcas Minimal APIs en los microservicios existentes; todos usan controllers clásicos.
- No reemplaces `Newtonsoft.Json` por `System.Text.Json` en código existente sin que se pida explícitamente (rompería consistencia con el resto del proyecto).
- No agregues una base de datos compartida entre servicios ni FKs cruzadas entre `AppDbContext` de distintas APIs.

## Notas adicionales
- El repo incluye un `AGENTS.md` en la raíz con reglas de estilo de respuesta para asistentes de IA (formato de salida, brevedad, selección de modelo). Esas reglas complementan este archivo pero no lo reemplazan: **este archivo describe el proyecto; `AGENTS.md` describe cómo debe comportarse el asistente**.
- Existe una carpeta `openspec/` con specs propias del proyecto (`specs/`, `changes/`) — revisar su contenido antes de proponer cambios de arquitectura, ya que puede documentar decisiones o trabajo en curso no reflejado aún en el código.
