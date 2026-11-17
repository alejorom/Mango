# Tasks

Seguimiento de implementación: únicamente las tareas 1.1, 1.2 y 2.1–2.5 (AuthAPI) están autorizadas en este incremento. Aplicar D1-D7 de `design.md`; no ejecutar SQL Server, migraciones, concurrencia ni suites del hardening. Cada incremento incluye su documentación y verificación antes del siguiente.

Orden acordado: AuthAPI → ProductAPI → CouponAPI → OrderAPI → EMailAPI → RewardAPI. AuthAPI es prioritario por la criticidad de autenticación, JWT, Identity y roles; las demás suites se ejecutan independientemente mediante JWT sintéticos locales cuando corresponda, sin depender de AuthAPI. Respetar los endpoints públicos existentes, especialmente ambas consultas GET del catálogo sin JWT.

No existe infraestructura disponible de Azure Service Bus ni Stripe para pruebas. No solicitar/configurar tenants, cuentas, credenciales o secretos ni realizar llamadas reales. Usar dobles y componentes aislados de D4 sin modificar producción; documentar como limitaciones los escenarios que requieran infraestructura externa.

## 1. Preparación de la implementación autorizada

- [x] 1.1 Confirmar autorización posterior, rama y base vigentes, árbol limpio salvo esta planificación y ausencia de proyectos nuevos integrados entretanto; verificar con status, log y listado de solución que no se necesita incorporar el hardening para continuar.
- [x] 1.2 Confirmar SDK net11.0 y disponibilidad de las versiones D2 sin actualizar paquetes existentes; verificar con dotnet --version y restauración del primer proyecto nuevo. Si falla, registrar el bloqueo sin modificar configuración personal.

## 2. AuthAPI — prioridad de autenticación, Identity, JWT y roles (A, AC1-AC6)

- [x] 2.1 Crear Mango.Services.AuthAPI.Tests y registrarlo una vez en solución; verificar TFM/paquetes/referencia propios y Debug/Release con csproj y dotnet sln list.
- [x] 2.2 Añadir Unit del generador JWT y ramas del controlador con fakes locales; verificar firma, issuer/audience, claims/roles, ventana de expiración de siete días, errores y publicación solo después de registro exitoso.
- [x] 2.3 Registrar Identity real con almacenamiento aislado y spy IMessageBus; verificar por integración registro válido/duplicado/password inválido y login válido/usuario ausente/contraseña incorrecta, observando filas y publicaciones.
- [x] 2.4 Añadir host HTTP con extensión JWT real; verificar registro/login públicos, AssignRole con ADMIN, role vacío/usuario ausente 400, 401 sin token o JWT inválido/expirado y 403 sin ADMIN, con ausencia de cambios/publicaciones en rechazos.
- [x] 2.5 Documentar matriz A, configuración JWT anidada, comandos y límites en README local; ejecutar ambas categorías y proyecto sin filtro con casos descubiertos, cero fallos/skips y sin arranque productivo ni Azure.

Evidencia del incremento AuthAPI (2026-11-17): SDK 11.0.100, restore y compilación Release correctos; Unit 15/15, Integration 30/30 y proyecto completo 45/45, con cero fallos/omisiones. Comandos, trazabilidad, avisos heredados y límites en `Mango.Services.AuthAPI.Tests/README.md`; TRX locales ignorados por Git. No se ejecutaron migraciones, infraestructura ni pruebas del hardening. Las tareas restantes no están autorizadas en este incremento.

## 3. ProductAPI — catálogo público y pruebas independientes (P, AC1-AC6)

- [ ] 3.1 Crear Mango.Services.ProductAPI.Tests con TFM/paquetes/referencia D1-D2, carpetas usadas y entrada única Debug/Release en solución; verificar csproj, dotnet sln list y descubrimiento de los casos añadidos en 3.2.
- [ ] 3.2 Añadir Unit para validación de ProductDto y mapping real; verificar Name requerido y límites 1/1000, valores exteriores y preservación de campos mediante assertions.
- [ ] 3.3 Añadir host HTTP local con validador JWT real, tokens sintéticos propios e InMemory; verificar ambas lecturas públicas sin Authorization, CRUD ADMIN con estado persistido, 200, 404 GET/PUT/DELETE y 500 genérico mediante fallo controlado, sin arrancar AuthAPI ni llamar Program.cs.
- [ ] 3.4 Cubrir cada escritura con 401 sin token, firma/issuer/audience inválidos y token expirado, 403 sin ADMIN y POST/PUT inválidos 400; verificar ausencia de cambios persistidos y respuestas del pipeline, no solo llamadas directas.
- [ ] 3.5 Documentar comandos, categorías Unit/Integration, matriz P → pruebas y límites del host/proveedor en el README local; ejecutar dotnet test del proyecto con filtros Unit, Integration y sin filtro, exigiendo casos descubiertos, cero fallos/skips y ninguna dependencia de AuthAPI o de infraestructura externa.

## 4. CouponAPI — validación y Stripe aislado (C, AC1-AC6)

- [ ] 4.1 Crear Mango.Services.CouponAPI.Tests y su referencia única en solución; verificar TFM/paquetes/referencia a Coupon y Debug/Release.
- [ ] 4.2 Añadir Unit de DTO/mapping y fake Stripe.IHttpClient con fixture serial que restaure StripeConfiguration.StripeClient en finally; verificar petición/respuesta sintética del SDK y rechazo de solicitudes inesperadas sin red.
- [ ] 4.3 Añadir integración de lecturas lista/id/code/casing, GET/DELETE inexistentes, CRUD ADMIN con Stripe fake y fallo controlado genérico; verificar códigos 200/404/500 y estado persistido según cada caso, sin atribuir collation SQL.
- [ ] 4.4 Añadir host HTTP con validador JWT real y tokens sintéticos propios, sin AuthAPI; verificar 401 en todos los endpoints para token ausente/firma/issuer/audience inválidos/expirado, 403 de escrituras sin ADMIN y 400 en POST/PUT inválidos; comprobar límites 0.01/0 y ausencia de efectos DB/Stripe al rechazar.
- [ ] 4.5 Documentar matriz C, discrepancia descuento > 0 frente al mínimo anotado, serialización Stripe y comandos en README local; ejecutar Unit, Integration y suite sin filtro con casos descubiertos, cero fallos/skips y cero llamadas reales.

## 5. OrderAPI — contratos actuales y pagos simulados (O, AC1-AC6)

- [ ] 5.1 Crear Mango.Services.OrderAPI.Tests y registrarlo una vez en solución; verificar TFM/paquetes/referencia propia y Debug/Release, manteniendo Stripe.net 52.4.0 productivo intacto.
- [ ] 5.2 Añadir Unit de mappings y fixtures de HTTP/Stripe/bus; verificar transporte Stripe controlado, restauración global y rechazo de peticiones inesperadas antes de ejecutar acciones de pago.
- [ ] 5.3 Añadir integración de CreateOrder, GetOrders y GetOrder; verificar redondeo, estado pending, detalles guardados, listado ADMIN y filtro userId existente, documentando que no se demuestra propiedad vinculada a claims.
- [ ] 5.4 Probar ProductService y BackendApiAuthenticationHttpClientHandler con IHttpClientFactory/HttpMessageHandler locales; verificar bearer y JSON Newtonsoft, respuesta exitosa, sobre IsSuccess=false, transporte fallido y JSON inválido según comportamiento vigente.
- [ ] 5.5 Añadir host HTTP con validador JWT real y tokens sintéticos propios, sin AuthAPI; verificar 401 en las seis acciones para ausencia/firma/issuer/audience inválidos/expiración, solicitudes autenticadas válidas y binding inválido 400 con ausencia de efectos en rechazos, sin agregar requisitos de rol o validación inexistentes.
- [ ] 5.6 Probar CreateStripeSession, ValidateStripeSession y cancelación con SDK/HTTP simulados; verificar datos enviados, cambios de estado/identificadores y publicación de recompensas solo en el camino aprobado, sin Stripe/Azure reales.
- [ ] 5.7 Documentar matriz O con símbolos del código, falta de baseline, brechas de propiedad/errores y límites de evidencia en README local, incluidos pagos/reembolsos reales y entrega Azure no disponibles; ejecutar categorías y proyecto sin filtro con casos descubiertos y cero fallos/skips.

## 6. EMailAPI — procesamiento y registro (E, AC1-AC6)

- [ ] 6.1 Crear Mango.Services.EMailAPI.Tests y entrada única en solución; verificar nombres con casing EMail, net11.0, paquetes y referencia a su API, sin inventar controladores HTTP.
- [ ] 6.2 Añadir Unit de contratos Newtonsoft para CartDto, email de registro y RewardsMessage con payloads sintéticos; verificar campos deserializados, sin atribuir ejecución a callbacks privados del broker.
- [ ] 6.3 Añadir integración directa de EmailService con opciones InMemory compartidas por fixture; verificar notificaciones cart/registro/orden, productos nulos omitidos y aviso parcial, destinatario, contenido, cantidad y timestamp de EmailLogger.
- [ ] 6.4 Inyectar fallo de SaveChanges mediante interceptor de pruebas en las opciones; verificar logging y propagación de excepción, sin afirmar cobertura del ack/reintento de Service Bus.
- [ ] 6.5 Documentar matriz E, literal de aviso divergente, ausencia de endpoints/SMTP y límites de transporte en README local; ejecutar categorías y suite sin filtro con casos descubiertos, cero fallos/skips y sin construir consumidores reales.

## 7. RewardAPI — persistencia y límites actuales (R, AC1-AC6)

- [ ] 7.1 Crear Mango.Services.RewardAPI.Tests y referencia única en solución; verificar net11.0, paquetes, referencia exclusiva a Reward y Debug/Release, sin agregar endpoints.
- [ ] 7.2 Añadir Unit del contrato Newtonsoft de RewardsMessage; verificar OrderId, UserId y RewardsActivity con datos sintéticos sin invocar el consumidor.
- [ ] 7.3 Añadir integración de UpdateRewards con almacenamiento aislado; verificar campos, fecha acotada y persistencia de eventos separados sin compartir estado entre casos, sin declarar idempotencia.
- [ ] 7.4 Inyectar falla de persistencia con interceptor; verificar que no se crea fila y caracterizar la excepción actualmente absorbida, documentando que no garantiza ack seguro ni reintento.
- [ ] 7.5 Documentar matriz R con símbolos de código por ausencia de baseline y límites de evidencia en README local; ejecutar categorías y proyecto sin filtro con casos descubiertos, cero fallos/skips y sin Azure/SQL.

## 8. Verificación conjunta de las seis bases

- [ ] 8.1 Revisar csproj, referencias y solución contra AC1; verificar exactamente seis proyectos nuevos, sin referencias a tests ajenos y sin cambios a proyectos/paquetes existentes ni a ShoppingCart.
- [ ] 8.2 Ejecutar dos veces la lista positiva D6 en orden Auth/Product/Coupon/Order/EMail/Reward en Release; verificar conteos TRX no nulos, cero fallos/skips, categorías explícitas, limpieza de fixtures y cero infraestructura, migraciones o concurrencia. Verificar también ejecución individual de cada suite ajena a Auth sin AuthAPI activo ni referencias a su proyecto/suite; no ejecutar la suite del hardening.
- [ ] 8.3 Revisar trazabilidad A/P/C/O/E/R y AC2-AC6 en los README locales; verificar que cada familia tiene prueba y evidencia, y que límites/discrepancias, incluidos escenarios externos de D4 sin infraestructura disponible, no se presentan como requisitos plenamente cubiertos. Registrar y someter a aceptación cualquier trabajo diferido antes de reclamar cierre.
- [ ] 8.4 Validar estrictamente establish-api-test-foundations y ejecutar git diff --check; revisar alcance final y hashes de main/hardening, conservando sus tareas 4.2, 8.2 y 8.3 pendientes. Entregar evidencia de implementación sin commits, push, integración, sync o archive no autorizados.
