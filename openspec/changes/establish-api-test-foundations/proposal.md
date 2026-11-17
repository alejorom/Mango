# Proposal

## Why

Seis servicios de Mango no tienen un proyecto de pruebas individual en ninguna de las dos ramas inspeccionadas. Establecer una base ejecutable y aislada por servicio permitirá verificar sus contratos actuales sin SQL Server, Azure Service Bus ni Stripe reales, y sin depender del hardening abierto de ShoppingCart.

## What Changes

- Proponer seis proyectos hermanos en la raíz: `Mango.Services.AuthAPI.Tests`, `Mango.Services.ProductAPI.Tests`, `Mango.Services.CouponAPI.Tests`, `Mango.Services.OrderAPI.Tests`, `Mango.Services.EMailAPI.Tests` y `Mango.Services.RewardAPI.Tests`, todos para `net11.0`.
- Incorporarlos posteriormente a `Mango.sln`, con pruebas unitarias y de integración de componentes/HTTP aisladas, dobles locales y ejecución explícita mediante `dotnet test`.
- Ejecutar la implementación en orden AuthAPI → ProductAPI → CouponAPI → OrderAPI → EMailAPI → RewardAPI. AuthAPI es prioritario por la criticidad de autenticación, JWT, Identity y roles; las demás suites deben ejecutarse independientemente de AuthAPI mediante autenticación de prueba local donde corresponda. Respetar los endpoints públicos existentes, incluidas las consultas del catálogo sin JWT.
- Adoptar xUnit y sus assertions, siguiendo las convenciones observadas en el proyecto ShoppingCart de la rama de hardening, sin copiar sus archivos ni referenciar ese proyecto. No introducir una biblioteca compartida de testing ni un framework de mocking por defecto.
- Definir cobertura inicial por API, autorización y validación según cada endpoint, categorías de ejecución y evidencia trazable. No prometer cobertura completa ni garantías relacionales usando EF InMemory.
- Esta intervención crea exclusivamente planificación. La creación de proyectos y pruebas requiere autorización posterior.

## Capabilities

### New Capabilities

Ninguna capacidad de negocio nueva. Esta iniciativa incorpora herramientas de verificación de comportamiento existente.

### Modified Capabilities

Ninguna. Se usa `skip_specs: true`, conforme al esquema instalado para cambios de tooling sin cambios de comportamiento. Los criterios medibles y su trazabilidad están en `design.md` y `tasks.md`; no se inventan deltas ni se editan las especificaciones canónicas. Las cinco capacidades actuales son `auth-api`, `coupon-api`, `product-api`, `email-api` y `shopping-cart-api`; Order y Reward aún no tienen baseline canónico.

## Impact

- Base: `main` local en `b560b8b3d1e47698580cac9f9bd980c333d25b61`, coincidente con la referencia local `origin/main`; no se consultó el remoto. La rama nueva es `feature/establish-api-test-foundations`.
- Inventario: siete servicios API, incluidos EMail y Reward, que son consumidores sin endpoints propios; `Mango.Web` y `Mango.MessageBus` no son APIs de dominio adicionales.
- El único proyecto de pruebas existente está en `feature/harden-shoppingcart-authorization`, HEAD `f434fed0013ab8454a074017b78a20b3015abe38`: `Mango.Services.ShoppingCartAPI.Tests`. No está versionado ni incluido en la solución de `main`. ShoppingCart queda reservado a ese trabajo; no se crea un duplicado desde esta base.
- El alcance futuro permite agregar solo los seis proyectos, sus referencias en la solución y documentación de ejecución localizada en ellos. No modifica código productivo, paquetes de proyectos existentes, pruebas existentes, infraestructura, UX ni contratos HTTP o de mensajería.
- No existe infraestructura disponible de Azure Service Bus ni Stripe para pruebas. No se solicitarán ni configurarán tenants, cuentas, credenciales o secretos, ni se harán llamadas reales. Order, EMail y Reward se probarán con dobles locales y componentes aislados sin modificar producción; los escenarios que requieran infraestructura externa quedarán documentados como limitaciones.
- No se integra, sincroniza, archiva ni cierra el hardening; sus tareas 4.2, 8.2 y 8.3 permanecen pendientes. Tampoco se ejecutan sus pruebas SQL ni de concurrencia.
