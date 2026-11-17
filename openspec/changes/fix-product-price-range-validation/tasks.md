# Tasks

Implementación autorizada de las tareas 1.1–4.3. Aplicar las decisiones D1–D5 y AC1–AC6 de design.md; no importar commits/archivos de foundations o hardening, ejecutar infraestructura ni modificar especificaciones canónicas. El fallo previo esperado es evidencia de reproducción; las tareas solo se completan después de verificar su resultado especificado, sin relajar las assertions.

## 1. Preparar la regresión independiente

- [x] 1.1 Confirmar autorización posterior, rama/base y estado del árbol; verificar con git status/log que esta rama sigue independiente de d35db64/aecb1e2 y f434fed, y que no se requiere integración de otras iniciativas para continuar.
- [x] 1.2 Crear desde cero el proyecto mínimo Mango.Services.ProductAPI.Tests con net11.0, versiones D2 y referencia exclusiva a ProductAPI, e incluirlo una vez en Tests de Mango.sln; verificar csproj, Debug/Release, dotnet sln list y restore sin cambios a paquetes existentes o configuración personal.

## 2. Reproducir y corregir las dos anotaciones

- [x] 2.1 Añadir regresiones Unit de ProductDto y Product para 0.99 y 1000.01 con los demás campos válidos; ejecutar antes de corregir producción y registrar el fallo esperado de las assertions de rechazo, sin copiar ni modificar las pruebas del checkpoint.
- [x] 2.2 Cambiar únicamente los dos atributos a Range con límites double explícitos según D1 y completar la matriz Unit B1–B5 sobre DTO/entidad; verificar todos los resultados y la preservación del valor original mediante dotnet test --filter Category=Unit, sin tolerancias, redondeo, cambios de cultura o validadores sustitutos.
- [x] 2.3 Documentar en el README local el origen del defecto, comandos, fallo previo y resultado posterior de Unit, matriz B1–B5 y referencia a los dos requisitos del delta; verificar que cada caso tiene prueba y evidencia, sin atribuir resultados al checkpoint de otra rama.

## 3. Comprobar el comportamiento HTTP de límites

- [x] 3.1 Crear el host MVC aislado D4 con controladores, mapping y validador JWT productivos, helper ADMIN local, base InMemory por fixture e interceptor de guardado; verificar arranque/parada acotados, ambos GET públicos sin Authorization y POST/PUT sin token rechazados con 401, sin Program.cs, migraciones o servicios externos.
- [x] 3.2 Implementar B1, B2 y B5 para POST/PUT mediante HTTP, usando id existente para PUT; verificar 400 con error de Price/Name, snapshots íntegros y cero intentos de SaveChanges/SaveChangesAsync, incluyendo 0.99/1000.01 y vecinos representables de los límites.
- [x] 3.3 Implementar B3 y B4 para POST/PUT con JWT ADMIN local; verificar 200, persistencia y precio devuelto idéntico al double enviado, incluidos 1, 1000, 1.01 y 999.99, sin afectar productos ajenos al caso.
- [x] 3.4 Completar la trazabilidad HTTP, categorías/comandos y límites del host/InMemory en README; ejecutar Category=Integration y verificar casos descubiertos, cero fallos/skips y correspondencia con todos los escenarios de precio del delta, sin prometer cobertura SQL o de startup completo.

## 4. Verificación conjunta y entrega del arreglo

- [x] 4.1 Ejecutar Unit, Integration y el proyecto completo sin filtro en Release, repetir una vez la suite completa y revisar los TRX; verificar conteos no nulos, cero fallos/skips, fixtures liberadas y ausencia de dependencia de AuthAPI o infraestructura.
- [x] 4.2 Ejecutar openspec validate fix-product-price-range-validation --strict y git diff --check; revisar que el diff productivo contiene solo las dos anotaciones y que los demás cambios se limitan al proyecto de regresión, su referencia de solución, README y seguimiento de este cambio, sin canónicos, archivos generados ni modificaciones ajenas.
- [x] 4.3 Registrar SDK, comandos/conteos finales y verificación de AC1–AC6 en README/tasks; comprobar que main, checkpoint d35db64 y hardening f434fed se conservan y que ninguna tarea de foundations/hardening se modifica o reclama completada. Entregar para revisión sin commits, push, integración, sync o archive no autorizados.

## Evidencia de ejecución

SDK 11.0.100; restore del proyecto independiente satisfactorio tras autorizar acceso a NuGet bloqueado por el sandbox. AC1: TRX previo `price-range-before.trx`, 4 fallos esperados (DTO/entidad, 0.99/1000.01); assertions intactas. AC2–AC4: Release, Unit 28/28 (`price-range-unit-final.trx`), Integration 32/32 (`price-range-integration.trx`), proyecto completo 60/60 (`price-range-all.trx`) y repetición 60/60 (`price-range-repeat.trx`), cero fallos/omisiones; comandos y trazabilidad detallados en el README del proyecto. TRX generados e ignorados por Git.

AC5–AC6: dos únicas líneas productivas cambiadas, a `[Range(1d, 1000d)]`; nuevo proyecto/referencia/documentación propios del arreglo. OpenSpec estricto, `git diff --check` y revisión de whitespace de archivos nuevos satisfactorios. Sin cambios canónicos, generados, secretos detectados ni infraestructura externa. main/HEAD conservan b560b8b, foundations d35db64 y hardening f434fed; sin incorporar sus commits o modificar sus tareas. Staging vacío y sin commits, push, integración, sync o archive. Límites: InMemory/host compuesto no acreditan SQL ni startup completo; datos históricos y reconciliación futura del proyecto homónimo quedan fuera del incremento. Avisos heredados registrados en README.
