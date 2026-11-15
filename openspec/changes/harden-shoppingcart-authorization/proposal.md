# Proposal

## Why

ShoppingCartAPI accepts unauthenticated requests and trusts client-supplied identities and cart references, exposing carts and email publication to cross-user access. Audit SC-01 through SC-05 in `docs/audits/shoppingcart-authorization/evidence.md` are static findings, not executed exploits.

## What Changes

- **BREAKING** Require valid JWTs and owner authorization on all five cart actions (SC-01, SC-02, SC-03, SC-04).
- Return indistinguishable 404 responses for foreign and nonexistent carts in GetCart and ApplyCoupon; reject malformed identifiers with 400 and missing or invalid authentication with 401. CartUpsert retains creation for the authenticated user. Preserve existing routes and DTO shapes.
- Reconstruct the email payload from trusted data and an approved authorized recipient source; reject without publishing otherwise (SC-05). Recipient source is an explicit pending decision.
- Add automated security and integration coverage, including Web and existing downstream token propagation.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `shopping-cart-api`: authentication, resource ownership and trustworthy cart-email publication.

## Impact

ShoppingCartAPI authorization and email payload behavior change; Mango.Web and consumers may receive new 400, 401 and 404 responses. ProductAPI, CouponAPI, AuthAPI and EmailAPI integrations must be checked without widening service scope absent demonstrated need. Existing message shape is retained where feasible. No shared database or unapproved cross-service dependency is introduced.

## Exclusions

Multi-item upsert redesign, unrelated order security, changes to other services without demonstrated need, and unapproved recipient-source design are excluded. Archived baseline and error-handling changes remain historical.
