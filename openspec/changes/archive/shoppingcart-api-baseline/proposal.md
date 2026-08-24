## Why

`Mango.Services.ShoppingCartAPI` is deployed and functional but has no formal specification. This change documents the existing implementation as the initial baseline spec so future changes have a foundation to delta against.

## What Changes

- No code changes. Documentation only.
- Establishes `shopping-cart-api` as a new capability under `openspec/specs/`.
- Covers all five endpoints, the two HTTP-based downstream integrations (ProductAPI, CouponAPI), the Azure Service Bus integration, and the JWT auth configuration.

## Capabilities

### New Capabilities

- `shopping-cart-api`: Full baseline spec for the ShoppingCartAPI microservice — domain model, endpoints, business rules, downstream dependencies (ProductAPI, CouponAPI), messaging (Azure Service Bus), and authentication.

### Modified Capabilities

*(none — no existing spec is changing)*

## Impact

- `openspec/specs/shopping-cart-api/spec.md` (new file)
- `Mango.Services.ShoppingCartAPI` codebase — read-only reference; no code modified
