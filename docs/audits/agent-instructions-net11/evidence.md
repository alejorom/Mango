# Evidencias: instrucciones multiagente y .NET 11

## Objetivo y alcance

Auditar la coherencia de las instrucciones de agentes con la solución y proponer una actualización documental. Se inspeccionaron `AGENTS.md`, `.github/copilot-instructions.md`, `openspec/config.yaml`, las instrucciones y recursos locales de agentes, `README.md`, `Mango.sln`, proyectos `.csproj`, configuración de arranque, controladores, mensajería y especificaciones vigentes. La auditoría no ejecutó compilaciones ni pruebas.

## Estado tecnológico verificado

- Los nueve proyectos de `Mango.sln` declaran `net11.0`. El SDK instalado incluye `11.0.100`; no se identificó `global.json` en el repositorio.
- Los servicios declaran paquetes ASP.NET Core JWT y OpenAPI `11.0.0`, EF Core y proveedor SQL Server `11.0.0`; Auth declara Identity EF Core `11.0.0`. Son versiones declaradas, no una validación de ejecución.
- La solución contiene una aplicación MVC, siete servicios y una biblioteca de mensajería. Usa controladores ASP.NET Core, SQL Server, Identity/JWT, HTTP entre servicios y Azure Service Bus. Declara AutoMapper `16.1.1`, Swashbuckle `11.0.1`, Azure.Messaging.ServiceBus `7.20.1` y Newtonsoft.Json `13.0.4`; Stripe.net es `52.4.2` en Coupon y `52.4.0` en Order.
- `openspec.cmd --version` devuelve `1.15.0`. Hay especificaciones principales para Auth, Coupon, Product, ShoppingCart y Email; no hay baseline de Order o Reward. No se encontraron proyectos de pruebas en la solución.

## Hallazgos

Prioridad indica urgencia de corrección documental, no una evaluación completa de seguridad.

| ID | Archivo afectado | Problema identificado | Evidencia verificable | Prioridad | Modificación recomendada |
|---|---|---|---|---|---|
| H01 | `AGENTS.md`; `.github/copilot-instructions.md` | Versiones de plataforma obsoletas. | `AGENTS.md:1-2,17`; `.github/copilot-instructions.md:4,20,23`; `Mango.Services.AuthAPI/Mango.Services.AuthAPI.csproj:4,11-16`. | Alta | Actualizar a .NET 11 y EF Core 11, distinguiendo versiones declaradas de validación ejecutada. |
| H02 | `.github/copilot-instructions.md` | Inventario de servicios y mensajería incompleto. | Tabla de servicios y colas en `.github/copilot-instructions.md:8-15,76-80`; `Mango.sln:28-31`; `Mango.Services.OrderAPI/Controllers/OrderAPIController.cs:187`; `Mango.Services.RewardAPI/Services/AzureServiceBusConsumer.cs:30`. | Media | Añadir Order, Reward y el flujo de recompensas con sus límites observados. |
| H03 | `.github/copilot-instructions.md` | Se describe un formato de respuesta uniforme que no existe en todas las APIs. | `.github/copilot-instructions.md:61-62`; `Mango.Services.OrderAPI/Controllers/OrderAPIController.cs:72`; `Mango.Services.AuthAPI/Controllers/AuthAPIController.cs:27`. | Alta | Describir convenciones por servicio y evitar imponer el patrón existente como universal. |
| H04 | `.github/copilot-instructions.md` | La guía general de autorización no refleja las diferencias entre servicios. | `.github/copilot-instructions.md:63`; `Mango.Services.ProductAPI/Controllers/ProductAPIController.cs:26,67-68`; `Mango.Services.ShoppingCartAPI/Controllers/CartAPIController.cs:26-27`; `openspec/specs/shopping-cart-api/spec.md:192`. | Alta | Documentar políticas observadas por endpoint y señalar la ausencia de autorización en ShoppingCart como riesgo pendiente. |
| H05 | `AGENTS.md`; `.github/copilot-instructions.md` | La consistencia de Newtonsoft puede interpretarse como configuración global MVC. | `AGENTS.md:17`; `.github/copilot-instructions.md:67`; `Mango.Services.CouponAPI/Program.cs:19`; `Mango.MessageBus/Mango.MessageBus.csproj:11`. | Media | Precisar uso explícito en clientes y mensajería y conservarlo sin afirmar que MVC lo configura globalmente. |
| H06 | `.github/copilot-instructions.md` | Se atribuye a `AGENTS.md` una referencia a MassTransit/RabbitMQ que no existe actualmente. | `.github/copilot-instructions.md:27`; `AGENTS.md:1-2`. | Baja | Eliminar la referencia histórica incorrecta y conservar la descripción de Azure Service Bus. |
| H07 | `AGENTS.md` | Reglas de salida duplicadas y restricciones absolutas dificultan informes o diagnósticos solicitados. | `AGENTS.md`, secciones Rules, Critical Constraints y Output. | Media | Consolidar reglas y ajustar la salida al tipo de tarea y a las instrucciones del usuario. |
| H08 | `README.md` | La documentación principal consiste en prompts históricos y menciona .NET 8. | `README.md:1-4` y secciones de prompts por servicio. | Media | Crear una guía actual de solución y separar o identificar el material histórico. |
| H09 | `openspec/specs/auth-api/spec.md` | El título y la descripción de dependencias contradicen la autorización actual de Auth. | `openspec/specs/auth-api/spec.md:94,113`; `Mango.Services.AuthAPI/Controllers/AuthAPIController.cs:55-57`; `Mango.Services.AuthAPI/Program.cs:64-65`. | Alta | Revisar y corregir la especificación vigente mediante el flujo OpenSpec apropiado, preservando el historial archivado. |
| H10 | `openspec/config.yaml` | Falta contexto de proyecto para la generación de artefactos; es una oportunidad de mejora, no un error de esquema. | `openspec/config.yaml:1-3` y ejemplos comentados de `context` y `rules`; versión instalada `1.15.0`. | Baja | Considerar contexto conciso y reglas de artefactos; validar compatibilidad antes de adoptarlos. |
| H11 | `.github/copilot-instructions.md` | La guía de migraciones deja sin resolver qué servicios las aplican al arrancar. | `.github/copilot-instructions.md:68`; `Mango.Services.CouponAPI/Program.cs:68`; `Mango.Services.AuthAPI/Program.cs:68,72`. | Media | Registrar la diferencia por servicio y pedir comprobación antes de ejecutar aplicaciones. |

## Propuesta de actualización

- **Común:** usar `AGENTS.md` como fuente de reglas compartidas: límites de dominio, controladores clásicos, contratos existentes, uso actual de Newtonsoft, seguridad por recurso, diagnóstico y validación proporcional. Diferenciar estado observado de requisitos para cambios nuevos.
- **Copilot:** reducir `.github/copilot-instructions.md` a particularidades de la herramienta y referencias al contexto común, verificando que la herramienta las incorpore efectivamente.
- **Codex:** conservar `AGENTS.md` como entrada principal; mantener las skills y workflows OpenSpec existentes sin modificación.
- **Claude:** evaluar un `CLAUDE.md` breve como entrada al contexto común; existen `.claude/commands/opsx/` y `.claude/skills/`, pero no se encontró `CLAUDE.md`.
- **Antigravity:** evaluar una entrada específica solo tras confirmar su mecanismo de carga; existen workflows en `.agents/workflows/`. Un enlace textual por sí solo no demuestra carga automática.
- **OpenSpec:** considerar `context` y `rules` breves en `openspec/config.yaml`, sin duplicar instrucciones extensas ni alterar el esquema `spec-driven` o la configuración existente. Verificar con OpenSpec 1.15.0.
- **Documentación:** actualizar README y la especificación vigente de Auth identificada en H09. Los artefactos archivados son historia y no deben presentarse como documentación operativa actual.

## Impacto y riesgos

La propuesta prevista es documental: no cambia APIs, persistencia, paquetes ni infraestructura. Las instrucciones pueden, sin embargo, influir en futuros cambios arquitectónicos o de seguridad; por ello deben reflejar límites existentes y no convertir recomendaciones en comportamiento implementado. Cambiar reglas de carga o duplicarlas entre agentes puede causar omisiones, contradicciones o instrucciones desactualizadas. La ausencia de pruebas automatizadas limita la verificación de comportamiento; no se comprobó la migración mediante build, ejecución o integración.

## Plan de implementación

1. Aprobar la fuente común, el alcance y la política de carga por herramienta.
2. Revisar `AGENTS.md` y `.github/copilot-instructions.md`; preservar decisiones vigentes y la lección de archivado Git.
3. Actualizar `README.md` y, si se aprueba, `openspec/config.yaml`; evaluar archivos de entrada para Claude y Antigravity tras verificar su mecanismo de carga.
4. Corregir la especificación de Auth con el procedimiento OpenSpec acordado; no modificar skills, prompts generados ni archivos archivados sin necesidad demostrada.
5. Contrastar cada afirmación con código y proyectos; verificar carga por herramienta, validar la configuración con OpenSpec 1.15.0 y revisar el diff. Compilar y probar la solución solo dentro del alcance y autorización correspondientes.

## Conclusión

Se recomienda consolidar las reglas compartidas y actualizar las descripciones obsoletas sin rediseñar los servicios. Quedan pendientes de aprobación la estructura multiagente, los archivos de entrada adicionales, el destino de los prompts históricos, el alcance de la corrección de la spec de Auth y una eventual fijación del SDK mediante `global.json`. Ninguna recomendación queda implementada por este informe.

## Intervención documental

Se actualizaron `AGENTS.md`, `.github/copilot-instructions.md` y `README.md`. H01-H08 y H11 se atendieron en esos documentos; H09 sigue pendiente porque afecta una especificación OpenSpec, y H10 sigue pendiente porque `openspec/config.yaml` quedó fuera del alcance. La intervención conserva los prompts históricos identificados en README y no modifica código, proyectos, configuración ni artefactos OpenSpec.

Verificaciones: contraste documental con los `.csproj`, `Program.cs` y controladores citados en los hallazgos; búsqueda de versiones antiguas en los documentos actualizados y revisión del alcance mediante Git. No se ejecutaron compilaciones, pruebas ni migraciones.
