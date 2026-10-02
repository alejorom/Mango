## Why

EmailAPI existe sin documentación formal. Necesitamos baseline spec del estado actual para:
- Entender qué hace y no hace
- Base para mejoras futuras
- Claridad en consumidores (AuthAPI, ShoppingCartAPI, OrderAPI)

## What Changes

Documentación. Sin cambios de código.

## Capabilities

### New Capabilities

- `email-api`: EmailAPI consume 3 colas (cart, register user, order placed) del Service Bus y registra eventos en DB

### Modified Capabilities

(Ninguna - esto es documentación del estado actual, no cambios de comportamiento)

## Impact

- Codebase: Mango.Services.EMailAPI
- Ningún cambio a APIs, contracts o dependencias externas
- Documentación vive en openspec/specs/

