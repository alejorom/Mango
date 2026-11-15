# Laboratorio SQL Server de ShoppingCartAPI

## Destino y alcance

- Instancia local predeterminada `MSSQLSERVER`, equipo `22LAP5CD1195RMS`.
- Base aislada: `Mango_ShoppingCart_Concurrency_Test`. Se conserva para pruebas futuras; no contiene datos de negocio al terminar la preparación.
- Base protegida: `Mango_ShoppingCart`. No se utilizó como destino de escrituras durante este procedimiento.

## Preparación realizada

1. Se confirmó que la instancia era local y predeterminada, que `Mango_ShoppingCart` estaba `ONLINE`, que la base de laboratorio no existía y que la cuenta tenía permiso de creación.
2. Se revisó el script generado sin conexión a partir de la migración `20260529203002_addShoppingCartTables` con EF Core 11. Su SHA-256 era `1645411C427E5EAD4DFB71BF1029822C481B0D73B4207B79D4BED74F44914039`. El archivo temporal se encontraba bajo `%TEMP%\mango-ef-offline-diagnostic-20261115\bin\Debug\net11.0\migration.sql`; puede desaparecer al limpiarse los temporales.
3. Se creó únicamente la base de laboratorio mediante `sqlcmd` y autenticación integrada. El comando de creación comprobó la instancia, el estado de la base protegida y la ausencia del destino antes de crearla.
4. En una conexión `sqlcmd` dirigida explícitamente a la base de laboratorio se ejecutó primero una comprobación de `DB_NAME()` e instancia y, en la misma sesión, el script cuyo hash se había verificado. No se inició ShoppingCartAPI ni se empleó su configuración habitual.

Para repetir la preparación en otro laboratorio autorizado, generar y revisar nuevamente el script offline con la versión EF apropiada; comprobar destino, permisos y ausencia de instrucciones o referencias ajenas; crear solo la base aprobada; y ejecutar la comprobación de contexto y el script revisado en una misma sesión dirigida explícitamente a esa base. No ejecutar el script de creación de tablas sobre esta base ya preparada ni sobre bases existentes.

## Verificaciones

- Ambas bases estaban `ONLINE` después de la operación. Las únicas escrituras SQL ejecutadas por el procedimiento se dirigieron a la creación de la base de laboratorio y a su migración; el estado `ONLINE` no prueba por sí solo ausencia de escrituras de otros procesos en la base protegida.
- Existen `dbo.CartHeaders`, `dbo.CartDetails` y `dbo.__EFMigrationsHistory`, con sus claves primarias.
- La FK `FK_CartDetails_CartHeaders_CartHeaderId` aplica `CASCADE`; existe el índice no único `IX_CartDetails_CartHeaderId`.
- El historial registra `20260529203002_addShoppingCartTables` con versión de producto `11.0.0`. Las dos tablas de negocio contienen cero filas.
- No se copiaron datos existentes ni se ejecutaron pruebas de concurrencia.

## Limitaciones y limpieza futura

La preparación no verifica bloqueos, condiciones de carrera, carga ni comportamiento de los controladores. Las pruebas posteriores deben confirmar nuevamente instancia y base, usar datos sintéticos y conexiones independientes. No se autoriza eliminar la base en este procedimiento. Su eliminación futura requiere autorización explícita, identificación exacta del destino y confirmación previa de que no contiene datos que deban conservarse.
