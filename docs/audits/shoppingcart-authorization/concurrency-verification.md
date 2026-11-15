# Verificación parcial de concurrencia

## Alcance

Fecha: 2026-11-15. Se realizaron cambios en ApplyCoupon, CartUpsert y RemoveCart para condicionar escrituras por propietario y usar transacciones `Serializable` en las operaciones compuestas. La tarea 4.2 permanece pendiente: los resultados siguientes no verifican todas las garantías exigidas.

## Entorno y seguridad

Las pruebas SQL usan conexiones independientes a `Mango_ShoppingCart_Concurrency_Test` en la instancia local predeterminada. Cada contexto abre una conexión que comprueba nombre de base, equipo e instancia antes de los datos sintéticos; no usa la configuración habitual de ShoppingCartAPI. Los datos de prueba se eliminan mediante sus identificadores sintéticos. Al finalizar se verificaron cero cabeceras y detalles en el laboratorio y ambas bases Mango estaban `ONLINE`.

## Resultados

- **ApplyCoupon, cambio de propietario:** prueba automatizada del controlador real con dos contextos SQL Server. Un interceptor detuvo el UPDATE después de la lectura; otra conexión cambió el propietario y la operación original devolvió 404 sin modificar el cupón. La prueba pasó tras corregir el punto de interceptación; el primer intento terminó por timeout.
- **CartUpsert, creación concurrente:** prueba del controlador real con dos contextos; pasó en una ejecución. No se fuerza una intercalación concreta y se permiten errores controlados, por lo que no demuestra unicidad global ni ausencia de deadlocks.
- **RemoveCart frente a inserción:** prueba concurrente de los controladores reales; pasó en una ejecución. Verifica que, si ambas operaciones informan éxito, el detalle insertado permanece. No fuerza el punto crítico ni acredita todas las intercalaciones.
- **Batería existente:** 8 de 25 pruebas pasaron y 17 fallaron al ejecutarse antes de incorporar las dos pruebas adicionales de concurrencia. Las pruebas existentes usan EF Core InMemory, que no admite las transacciones relacionales ni `ExecuteUpdate`/`ExecuteDelete` introducidos; es necesario adaptar la batería a un proveedor relacional o separar apropiadamente las pruebas. No se han omitido ni marcado como exitosas.
- Compilación del proyecto de pruebas y dependencias satisfactoria; validación estricta de OpenSpec y `git diff --check` satisfactorios. Se reportaron advertencias NU1903 de AutoMapper.

## Pendiente

Repetir y sincronizar de forma determinista las carreras de inserción de producto, creación de carrito y eliminación de último detalle con inserción; verificar rollback, filas afectadas, respuestas y ausencia de pérdida de datos bajo las intercalaciones relevantes. Registrar tiempos de espera y deadlocks del código real: las pruebas actuales solo tienen límites de timeout, no recogen eventos de bloqueo y no permiten afirmar que no haya deadlocks. Resolver la incompatibilidad de la batería InMemory y volver a ejecutar todas las pruebas. La ausencia de restricciones únicas y la integridad de incrementos siguen fuera del alcance de esta fase; `Serializable` no acredita unicidad frente a escritores que no sigan el mismo protocolo.

## Diagnóstico y pruebas posteriores

Se repitieron las 24 pruebas anteriores excluyendo las pruebas SQL nuevas: 17 fallaron y 7 pasaron. Los fallos se clasificaron en 13 casos de CartUpsert y 3 de RemoveCart, que alcanzan transacciones no admitidas por EF InMemory, y 1 de ApplyCoupon, que alcanza una actualización relacional no admitida por ese proveedor. Las respuestas observadas eran 500 o resultados ausentes; esos fallos por sí solos no demostraban una regresión SQL Server.

Se migraron las pruebas de esas tres clases a conexiones independientes de SQL Server con IDs sintéticos únicos y limpieza acotada. Se conservaron los escenarios y comprobaciones de persistencia, rechazos y respuestas; el conteo de escrituras se adaptó a comandos relacionales. Autenticación y GetCart conservaron sus pruebas previas. La batería completa pasó con 28 pruebas antes de agregar las últimas pruebas sincronizadas.

En SQL Server, un caso del controlador real comprobó creación, incremento, cupón y eliminación del último detalle. Una prueba sincronizada retuvo el upsert tras leer la cabecera y observó que una transferencia de propiedad concurrente esperaba al menos 300 ms hasta liberar la operación; ambos terminaron correctamente. Una falla sintética del comando de inserción devolvió 500 y no dejó cabecera ni detalle; el interceptor pudo impedir el lote antes de ejecutar cualquier inserción, por lo que no demuestra rollback tras una escritura parcial. La prueba anterior de cambio de propiedad antes del UPDATE de cupón sigue pasando en la batería.

Una nueva prueba sincronizada detuvo RemoveCart después de eliminar el detalle, mientras otra sesión intentaba insertar. En la primera ejecución SQL Server informó deadlock 1205 y seleccionó la inserción competidora como víctima; no se capturó el grafo de deadlock, por lo que su cadena exacta de bloqueos no se ha determinado. Una repetición produjo timeout de SQL Server de 10 segundos durante la liberación/rollback de la transacción de prueba. La prueba falló y no permitió demostrar de forma completa el estado de ambas operaciones ni descartar una regresión en el manejo de conflictos. Se detuvo el trabajo sin cambiar código productivo adicional. No se realizaron reintentos automáticos.

Tras las dos pruebas fallidas se identificaron y eliminaron exclusivamente las dos cabeceras sintéticas restantes; la comprobación final devolvió cero cabeceras y detalles. Ambas bases estaban ONLINE. Quedan pendientes el diagnóstico del timeout y del deadlock, pruebas deterministas de inserción simultánea del mismo producto y verificación completa de rollback y respuestas. La tarea 4.2 no se marca completa. No se afirma que todos los escenarios de concurrencia estén cubiertos ni que InMemory compruebe garantías SQL.
