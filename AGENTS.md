# Instrucciones comunes para agentes

## Contexto y arquitectura

Mango apunta a .NET 11 y usa ASP.NET Core, EF Core 11 y SQL Server. La solución contiene Mango.Web MVC, Auth, Coupon, Product, ShoppingCart, Email, Order, Reward y Mango.MessageBus. Comprueba versiones y configuración en cada proyecto.

Mantén los límites de dominio: cada servicio administra sus datos; no introduzcas bases compartidas ni acceso directo al DbContext de otro servicio. Conserva controladores clásicos en las APIs. Respeta los flujos HTTP y Azure Service Bus existentes. No rediseñes la arquitectura fuera del alcance solicitado.

## Seguridad y calidad

Diagnostica antes de editar. Revisa autenticación, autorización y propiedad de recursos por endpoint; no supongas una política uniforme. Mantén los contratos, valida entradas, devuelve códigos HTTP apropiados y no expongas detalles internos de excepciones. Aplica manejo de fallos, reintentos o trazabilidad cuando corresponda y dentro del alcance autorizado.

Newtonsoft.Json se usa explícitamente en clientes y mensajería; no migres sus usos existentes sin solicitud. Los controladores usan AddControllers sin configurar explícitamente Newtonsoft como serializador MVC. Usa las convenciones del servicio afectado y características modernas de C# cuando sean apropiadas. No uniformes firmas o comportamiento sin un cambio aprobado. No agregues comentarios ni documentación XML salvo solicitud.

## SDD, trazabilidad y verificación

Revisa las especificaciones y cambios OpenSpec relevantes antes de modificar comportamiento. Distingue comportamiento implementado de mejoras propuestas y conserva trazabilidad entre requisitos, implementación y verificación. No alteres artefactos archivados ni recursos generados sin necesidad justificada. Verifica los cambios con pruebas apropiadas y comunica límites y fallos.

Al comitear un change OpenSpec archivado, usa `git add -A` o Stage All Changes para incluir tanto la desaparición de la ubicación anterior como la aparición de la nueva y permitir que Git detecte el rename. Revisa previamente todos los cambios incluidos y respeta el alcance autorizado.

## Política de ramas Git

Esta política aplica a todos los agentes de IA del repositorio, incluidos Codex, GitHub Copilot, Claude Code y Antigravity.

- `main` es la rama estable de integración; está prohibido desarrollar directamente en ella.
- Antes de modificar archivos, comprueba la rama activa y el estado de Git. Si la rama activa es `main`, detente y solicita autorización para crear una rama de trabajo.
- Inicia cada nuevo cambio OpenSpec en una rama independiente creada desde `main` actualizado, con el nombre `feature/<openspec-change-name>`, `fix/<openspec-change-name>` o `docs/<openspec-change-name>`, según corresponda.
- Cada rama contiene exclusivamente cambios relacionados con su objetivo. No reutilices ramas de cambios ya completados.
- Antes de integrar a `main`, completa los requisitos obligatorios, ejecuta las pruebas correspondientes, valida OpenSpec, resuelve los defectos bloqueantes, documenta y aprueba cualquier trabajo diferido, y sincroniza y archiva el cambio OpenSpec según el flujo aprobado.
- Integra a `main` mediante un Pull Request revisado y aprobado. Después de integrar, actualiza `main` local y crea una rama nueva para el siguiente cambio.
- No ejecutes commits, push, merge, reset, rebase ni eliminación de ramas sin autorización explícita.

## Comunicación

Responde de forma directa y concisa, adaptando el formato al entregable solicitado. Para cambios de código muestra solo los bloques relevantes cuando sea útil. No agregues sugerencias fuera del alcance.
