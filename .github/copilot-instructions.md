# Instrucciones para GitHub Copilot

Consulta `AGENTS.md` para las reglas comunes y `openspec/specs/` para los contratos aplicables. Contrasta ambos con el código antes de generar cambios; las instrucciones del usuario prevalecen.

## Mango.Web

Usa los servicios de `Mango.Web/Service` e `IBaseService.SendAsync` para integrar endpoints desde la aplicación MVC. Los DTO de la web son propios. Conserva las convenciones existentes de Razor, tag helpers, validación y estilos al modificar vistas. Consulta `ServiceUrls` y la configuración local en vez de asumir puertos.

## OpenSpec

Los prompts y skills de `.github/prompts/` y `.github/skills/` corresponden a la integración OpenSpec. Sigue el flujo solicitado y no modifiques estos recursos generados sin necesidad demostrada. Antes de proponer un cambio, inspecciona las especificaciones y cambios activos relevantes.
