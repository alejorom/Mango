# Design

## Context

La rama `fix/product-price-range-validation` parte de `main` local en `b560b8b3d1e47698580cac9f9bd980c333d25b61`. El árbol estaba limpio al salir de `feature/establish-api-test-foundations`, cuyo checkpoint ProductAPI es `d35db642e949f7a12c2feb1684b23c5006e7d058`. No se incorporan ese checkpoint, su commit AuthAPI anterior ni el hardening `f434fed0013ab8454a074017b78a20b3015abe38`.

ProductAPI apunta a net11.0; declara ASP.NET Core/EF Core 11.0.0 y AutoMapper 16.1.1. `ProductDto.Price` y `Product.Price` son double y tienen `[Range(1, 1000)]`. Los literales enteros seleccionan RangeAttribute(int, int), que convierte a Int32 antes de comparar. La conversión redondea: 0.99 pasa como 1 y 1000.01 como 1000. El atributo no cambia el precio original del objeto.

La evidencia anterior, conservada exclusivamente en el checkpoint, fue 12 casos Unit: 10 aprobados, 2 fallidos y 0 omitidos. Las dos assertions de rechazo permanecen intactas allí. No hubo verificación HTTP de estos valores. Esto acredita el fallo del DTO, no una escritura inválida ya observada por HTTP.

La spec canónica exige 1..1000 inclusivo y 400 antes de persistencia para POST/PUT inválidos. AddControllers, ApiController y los controles ModelState.IsValid del controlador usan la misma validación, sin comparación adicional del precio. GET lista y por id son públicos; POST/PUT/DELETE requieren ADMIN. No se altera esa matriz.

Program.cs aplica migraciones al arrancar. La solución de esta base contiene nueve proyectos y ningún proyecto de pruebas versionado; no existe en ella el cambio establish-api-test-foundations. Ver propuesta para el alcance y delta de product-api para las dos precisiones normativas; el escenario histórico del esquema se conserva, pero no exige ejecutar migraciones en esta corrección.

## Goals / Non-Goals

**Goals:** cumplir el intervalo existente sobre el valor double original, mantener coherencia DTO/entidad y demostrar rechazo por MVC antes de guardar. Hacer la corrección y su regresión reproducibles desde esta rama sin depender de AuthAPI o ramas ajenas.

**Non-Goals:** migrar Price a decimal, definir precisión monetaria, redondear o corregir entradas, ampliar el intervalo, agregar reglas de negocio, limpiar datos existentes, crear CHECK constraints, ejecutar SQL o infraestructura, refactorizar controladores/arranque, actualizar paquetes existentes o completar foundations/hardening.

## Decisions

### D1. Cambiar únicamente las dos anotaciones productivas

Tras autorización de implementación, sustituir `[Range(1, 1000)]` por `[Range(1d, 1000d)]` en ProductDto y Product. Así se selecciona la sobrecarga double y se evita la conversión entera. Conservar double, Required, mensajes predeterminados, campos, mapping, rutas y firmas. No agregar comentarios ni documentación XML al código.

Un validador propio o IValidatableObject supondría más código para una regla ya expresable por DataAnnotations. Migrar a decimal afectaría contratos y datos; cambiar las pruebas o permitir esos precios en la spec ocultaría el defecto. Ninguna alternativa se adopta.

La validación del atributo de entidad se probará explícitamente con Validator; no se presenta como una restricción SQL ni se asume que EF Core valide por sí solo las anotaciones al guardar.

### D2. Regresión mínima independiente sobre main

En la implementación futura, crear desde cero `Mango.Services.ProductAPI.Tests/Mango.Services.ProductAPI.Tests.csproj`, net11.0, con referencia exclusiva a ProductAPI y entrada única bajo Tests en Mango.sln para Debug/Release. Estructura limitada a Unit, Integration, TestSupport y README. No agregar una biblioteca compartida ni referencias a AuthAPI, MessageBus o pruebas ajenas.

Usar xUnit 2.9.3, runner 3.1.4, Microsoft.NET.Test.Sdk 17.14.1, EF Core InMemory 11.0.0 y FrameworkReference Microsoft.AspNetCore.App; versiones tomadas como convención de lectura del trabajo anterior, sin trasladar archivos ni commits. No actualizar paquetes productivos. Restaurar solo este proyecto y documentar cualquier fallo real de restore/build, sin editar configuración personal.

Esta rama no importa el proyecto parcial del checkpoint. El nombre coincide deliberadamente con la convención de un proyecto individual por API; crear una segunda suite permanente con otro nombre duplicaría esa responsabilidad. Si posteriormente se integran ambas iniciativas, sus archivos y entradas de solución requerirán reconciliación explícitamente autorizada; esta intervención no la realiza ni marca tareas de foundations como completadas.

### D3. Matriz de límites trazable

Aplicar la matriz a ProductDto y Product con Validator.TryValidateObject, comprobando el miembro Price y que su valor no cambia. Todos los demás campos deben ser válidos para aislar la causa. Los valores se suministran como double, sin parseo dependiente de cultura ni tolerancias que permitan atravesar el límite.

| ID | Precio | Resultado de validación | POST y PUT con ADMIN |
|---|---|---|---|
| B1 | -1, 0, 0.99, 1000.01, 1001 | Inválido | 400, error de Price y cero escrituras |
| B2 | Math.BitDecrement(1d), Math.BitIncrement(1000d) | Inválido | 400, error de Price y cero escrituras |
| B3 | 1d, 1000d | Válido | 200 y precio recibido conservado |
| B4 | 1.01d, 999.99d | Válido | 200 y fracción conservada |
| B5 | Nombre null, vacío o espacio con precio válido | Inválido por Name | 400 y cero escrituras |

B2 detecta regresiones aun más próximas al límite sin cambiar el contrato. Serializar como número JSON con System.Text.Json y comprobar que su lectura conserva el double usado en el caso; no enviar strings numéricos o literales especiales fuera del contrato JSON. Mantener exactamente las expectativas de rechazo de 0.99 y 1000.01, aunque los archivos de prueba se escriban independientemente aquí.

### D4. Verificación MVC sin startup productivo ni infraestructura

Crear un host de composición de pruebas sobre `127.0.0.1:0`, con controladores de ProductAPI reales, AddControllers y su serializador MVC predeterminado, mapping productivo, extensión real AddAppAuthetication, autorización y MapControllers. Limpiar fuentes de configuración antes de registrar dependencias y usar únicamente ApiSettings:Secret/Issuer/Audience sintéticos. Emitir JWT ADMIN desde un helper local; no ejecutar AuthAPI ni pedir tokens reales. No sustituir la validación por fakes ni cambiar opciones de ModelState para hacer pasar casos.

Registrar solo AppDbContext de ProductAPI sobre una base InMemory única por fixture. Sembrar los productos necesarios directamente desde un contexto de inspección con la misma raíz/nombre, sin EnsureCreated/Migrate ni seed de migraciones. Para PUT usar un id existente; para POST un nuevo id, evitando que 404 o colisiones oculten el problema de rango.

Antes de cada rechazo capturar los productos persistidos; después comprobar igualdad completa y cero intentos de SaveChanges/SaveChangesAsync mediante un interceptor local. La respuesta 400 del pipeline puede ser ValidationProblemDetails; comprobar status y error asociado a Price/Name sin exigir ResponseDto para errores automáticos de ApiController. Para casos válidos verificar 200, respuesta y precio persistido sin redondeo. El valor esperado es el mismo double serializado, no una representación monetaria decimal nueva.

Verificar ambos GET sin Authorization como regresión de acceso público y un rechazo 401 de POST/PUT sin token. Las escrituras válidas se ejercitan con JWT ADMIN real de prueba. No ampliar la suite a otros dominios o a un hardening general de autorización. Cualquier defecto ajeno que impida esta verificación se documenta como bloqueo sin alterar producción fuera de D1.

Liberar hosts/clientes/scopes con tiempos acotados (30 segundos para arranque, parada y peticiones); cliente sin proxy/redirecciones. No ejecutar Program.cs, Swagger, redirección HTTPS, SQL Server, migraciones, Azure Service Bus o Stripe. La evidencia demuestra composición MVC y lógica EF aisladas, no startup completo, esquema SQL, collation, transacciones o concurrencia. No solicitar tenants, cuentas, credenciales o secretos.

### D5. Verificación y aceptación de implementación

1. **AC1:** antes del cambio productivo registrar el fallo de al menos los casos 0.99/1000.01 sobre DTO y entidad; no suprimirlos ni cambiar assertions, cultura o validadores.
2. **AC2:** después de D1, todos los casos B1–B5 de validación pasan con el resultado esperado en ambos tipos, conservando los valores originales.
3. **AC3:** POST/PUT ejecutan B1–B5 en MVC según D4: rechazos 400 con estado íntegro/cero saves y aceptaciones 200 con precios originales. GET lista y detalle siguen públicos.
4. **AC4:** ejecutar Unit, Integration y proyecto completo sin filtro, con casos descubiertos, cero fallos y cero skips; repetir una vez la suite completa para detectar contaminación de estado. No aceptar cero casos o un 500 como éxito.
5. **AC5:** diff productivo limitado a las dos anotaciones; nuevo proyecto/ref de solución/documentación limitados a regresión de este defecto. Sin archivos de foundations/hardening, cambios canónicos o generados, nuevas migraciones, actualizaciones de paquetes existentes ni accesos externos.
6. **AC6:** README y tasks registran comandos, SDK, conteos TRX, fallo previo y resultado posterior; OpenSpec estricto y git diff --check satisfactorios antes de solicitar revisión. No atribuir estas pruebas al checkpoint de otra rama.

Comandos previstos para implementación; no se ejecutan en esta fase:

```powershell
dotnet --version
dotnet restore Mango.Services.ProductAPI.Tests/Mango.Services.ProductAPI.Tests.csproj
dotnet test Mango.Services.ProductAPI.Tests/Mango.Services.ProductAPI.Tests.csproj --no-restore --configuration Release --filter Category=Unit --logger 'trx;LogFileName=unit.trx' --results-directory Mango.Services.ProductAPI.Tests/TestResults
dotnet test Mango.Services.ProductAPI.Tests/Mango.Services.ProductAPI.Tests.csproj --no-restore --configuration Release --filter Category=Integration --logger 'trx;LogFileName=integration.trx' --results-directory Mango.Services.ProductAPI.Tests/TestResults
dotnet test Mango.Services.ProductAPI.Tests/Mango.Services.ProductAPI.Tests.csproj --no-restore --configuration Release --logger 'trx;LogFileName=all.trx' --results-directory Mango.Services.ProductAPI.Tests/TestResults
```

No ejecutar la solución completa ni suites de ramas ajenas. Guardar TRX localmente, sin versionar resultados, binarios o secretos.

## Risks / Trade-offs

- [Se rechazarán entradas antes aceptadas indebidamente] → Es la corrección del contrato existente; conservar extremos/fracciones válidos y no normalizar silenciosamente los inválidos.
- [Los datos históricos podrían contener precios fuera de rango] → No se investigan ni modifican aquí; el arreglo controla validación de nuevas solicitudes, sin migración ni saneamiento implícitos.
- [El proyecto de regresión coincide en ruta con el checkpoint] → Desarrollo independiente desde main y futura reconciliación manual autorizada antes de integrar iniciativas; no copiar archivos ni commits ahora.
- [InMemory no equivale a SQL Server] → Limitar evidencia a validación, pipeline y estado funcional aislado; no prometer integridad relacional ni resultados de migraciones.
- [El diagnóstico previo no ejecutó HTTP] → Los casos HTTP son verificaciones futuras obligatorias; no afirmar que ya se observaron escrituras inválidas.
- [Restore, dependencias o comportamiento ajeno bloquean pruebas] → Registrar el fallo y detener el incremento afectado; no ampliar el arreglo ni actualizar paquetes para ocultarlo.

## Migration Plan

No hay cambios de esquema ni despliegue en esta fase. Tras autorización posterior, crear la regresión mínima independiente, registrar el fallo previo, cambiar ambas anotaciones y completar la verificación por categorías y sin filtro. Documentar resultados antes de proponer integración. Una reversión futura se limitaría al diff de esta corrección mediante el flujo Git autorizado; nunca altera otras ramas o datos por sí sola.

No hay decisiones técnicas bloqueantes pendientes en esta propuesta. La implementación y cualquier integración/reconciliación entre ramas requieren instrucciones posteriores independientes. El checkpoint de foundations sigue bloqueado allí hasta una verificación posterior autorizada sobre la corrección integrada; esta planificación no lo cierra.
