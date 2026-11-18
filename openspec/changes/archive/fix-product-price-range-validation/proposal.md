# Proposal

## Why

ProductAPI declara `[Range(1, 1000)]` sobre `Price` de tipo `double`: la sobrecarga entera redondea antes de comparar y acepta valores como 0.99 y 1000.01. Esto incumple el intervalo inclusivo 1..1000 exigido por la especificación vigente y bloqueó la verificación de precios en la iniciativa de bases de pruebas.

## What Changes

- Corregir posteriormente los atributos de precio en `ProductDto` y `Product` usando límites `double` explícitos, sin conversión a entero ni redondeo del precio recibido.
- Precisar en un delta de `product-api` que la validación compara el valor fraccionario original y rechaza precios fuera del intervalo antes de persistir. El intervalo de negocio y el tipo `double` permanecen vigentes.
- Definir regresiones independientes para DTO y entidad, límites inclusivos, vecinos representables de los límites y POST/PUT mediante MVC aislado, con comprobación de ausencia de escrituras para payloads inválidos.
- Preparar en la implementación futura un proyecto mínimo de regresión exclusivo de ProductAPI, creado desde esta base sin importar archivos o commits de las ramas de foundations o hardening.
- Esta intervención solo crea planificación. No corrige atributos ni implementa o ejecuta nuevas pruebas.

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `product-api`: precisar `Product payload validation rules` para precios fraccionarios sin redondeo y alinear `Product domain model and persistence` con la anotación de rango de tipo double. Conservar los escenarios existentes de ambos requisitos.

## Impact

- Rama `fix/product-price-range-validation`, creada directamente desde `main` local en `b560b8b3d1e47698580cac9f9bd980c333d25b61`, coincidente con la referencia local `origin/main`; no se actualizó el remoto.
- Checkpoint de origen del hallazgo confirmado y conservado en `feature/establish-api-test-foundations`: `d35db642e949f7a12c2feb1684b23c5006e7d058`. La rama de hardening conserva `f434fed0013ab8454a074017b78a20b3015abe38`. Ninguno de esos commits se incorpora a esta rama.
- Alcance productivo futuro: únicamente los atributos de `Mango.Services.ProductAPI/Models/Dto/ProductDto.cs` y `Models/Product.cs`. Verificación mínima en `Mango.Services.ProductAPI.Tests`, su referencia de solución y documentación local; el diseño explica su independencia.
- No modificar controladores, autenticación, endpoints públicos, DbContext, esquema SQL, migraciones, tipos monetarios, paquetes existentes ni otras APIs. No ejecutar SQL Server, Azure Service Bus o Stripe ni solicitar infraestructura o secretos.
- No editar ahora especificaciones canónicas, cambios archivados, el cambio de foundations o el hardening. No realizar commits, push, integración, sincronización o archivo en esta intervención.
