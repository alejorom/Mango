# Pruebas de ProductAPI — incremento bloqueado

Alcance autorizado: tareas 3.1–3.5 de `establish-api-test-foundations`. Se creó este proyecto `net11.0`, con referencia exclusiva a ProductAPI y entrada única bajo Tests en `Mango.sln`, para Debug/Release. Usa las mismas versiones de Test SDK, xUnit, runner y EF InMemory que AuthAPI.Tests, sin referenciar ese proyecto ni cambiar paquetes existentes.

## Ejecución realizada

Desde la raíz del repositorio, SDK 11.0.100:

```powershell
dotnet restore Mango.Services.ProductAPI.Tests/Mango.Services.ProductAPI.Tests.csproj --verbosity minimal
dotnet test Mango.Services.ProductAPI.Tests/Mango.Services.ProductAPI.Tests.csproj --no-restore --configuration Release --filter Category=Unit --logger 'trx;LogFileName=unit.trx' --results-directory Mango.Services.ProductAPI.Tests/TestResults
```

La restauración finalizó con código 0 después de repetir con permiso del entorno el intento bloqueado por el sandbox (NU1301). La compilación Release fue correcta. El runner descubrió 12 casos Unit: 10 aprobados, 2 fallidos y 0 omitidos; `dotnet test` finalizó con código 1. El TRX local `TestResults/unit.trx` y los directorios bin/obj están ignorados por Git.

## Bloqueo de validación

La [spec product-api](../openspec/specs/product-api/spec.md), requisito Product payload validation rules y escenario Price out of range, exige rechazar precios inferiores a 1 o superiores a 1000. El DTO productivo declara `[Range(1, 1000)]` con límites enteros. Las pruebas usan Validator.TryValidateObject sobre el DTO real, sin sustituir sus atributos ni modificar cultura, opciones MVC o validadores:

| Caso Unit | Esperado por la spec | Observado |
|---|---|---|
| `ProductPriceOutsideRangeIsRejected(0.99)` | Validación falsa | Validación verdadera |
| `ProductPriceOutsideRangeIsRejected(1000.01)` | Validación falsa | Validación verdadera |

Se mantienen las assertions de rechazo: no se eliminan casos ni se cambia su expectativa para obtener una suite verde. La tarea 3.2 no se considera completada. Resolver la discrepancia entre la regla existente y la spec requiere una decisión/autorización fuera del alcance actual, que prohíbe modificar producción. No se ha comprobado el resultado HTTP de estos payloads; el hallazgo se limita a la validación del DTO ejercitada directamente.

## Trazabilidad implementada

| Requisito / decisión | Pruebas y resultado |
|---|---|
| Name requerido | `ProductNameIsRequired`: null, vacío y espacio rechazados; 3/3 aprobados |
| Price 1..1000 inclusive | `ValidProductAcceptsInclusivePriceLimits`: 1, 1000 y 42.5 aceptados; 3/3 aprobados |
| Price fuera de rango | `ProductPriceOutsideRangeIsRejected`: -1 y 0 rechazados; 0.99 y 1000.01 aceptados indebidamente; 2/4 aprobados |
| Mapping real y preservación de campos (D2 / tarea 3.2) | Configuración AutoMapper válida, DTO → entidad → DTO y actualización de entidad existente preservan identidad, nombre, precio, descripción, categoría e imagen; 2/2 aprobados |
| Proyecto individual y descubrimiento (D1 / tarea 3.1) | Una referencia a ProductAPI y una entrada de solución Debug/Release; 12 casos descubiertos |

## Pendientes y límites

Las tareas 3.2–3.5 permanecen pendientes. No se implementaron ni ejecutaron integraciones HTTP; no existen todavía casos Category=Integration ni evidencia de ejecución del proyecto sin filtro. Este README documenta el resultado parcial, no acredita la finalización de 3.5.

`TestSupport/ProductTestData.cs` contiene fixtures de producto y un helper JWT local para el host previsto por D3/D4; no invoca AuthAPI ni necesita su generador, login o suite. El helper aún no tiene verificación HTTP. La implementación posterior deberá conservar ambos GET del catálogo públicos, sin Authorization, y verificar ADMIN/401/403 en las escrituras con el validador productivo real; no se ha demostrado todavía esa matriz.

No se modificó código productivo, AuthAPI.Tests ni ShoppingCart/hardening. No se ejecutaron Program.cs, SQL Server, migraciones, Azure Service Bus, Stripe o pruebas de concurrencia. No se solicitaron ni configuraron cuentas, tenants, credenciales o secretos. Las claves y datos incluidos son sintéticos locales; no se hicieron llamadas reales a servicios externos.

Avisos heredados observados: NU1903 para AutoMapper 16.1.1 y advertencias de nullability en ProductAPI. El runner informa el uso de un directorio temporal privado por ausencia de VSTEST_ARTIFACTS_DIRECTORY con este Test SDK. No se actualizaron paquetes ni se alteró configuración personal para resolver esos avisos.
