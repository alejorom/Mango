# Proposal

## Why

ShoppingCartAPI accepts unauthenticated requests and trusts client-supplied identities and cart references, exposing carts and email publication to cross-user access. Audit SC-01 through SC-05 in `docs/audits/shoppingcart-authorization/evidence.md` are static findings, not executed exploits.

The hardening also changed the empty-cart journey: GetCart returns 404 when the authenticated user's header is absent, and Mango.Web propagates it instead of displaying the empty view. The approved alternative B in `docs/audits/shoppingcart-authorization/cart-empty-contract-decision.md` treats this absence as a successful logical empty state. Only this planning update is authorized now; implementation and regression verification remain pending separate authorization.

## What Changes

- **BREAKING** Require valid JWTs and owner authorization on all five cart actions (SC-01, SC-02, SC-03, SC-04).
- Return indistinguishable 404 responses for GetCart identities different from the authenticated subject, whether their carts exist or not, without consulting foreign existence; ADMIN has no bypass. ApplyCoupon continues returning 404 for foreign or nonexistent required carts. Reject malformed identifiers with 400 and missing or invalid authentication or unusable identity with 401. CartUpsert retains creation for the authenticated user. Preserve existing routes and DTO shapes.
- **BREAKING contract revision, alternative B:** GetCart for the authenticated subject with no persisted header returns HTTP 200, `IsSuccess = true` and a non-null CartDto: non-null CartHeader with `CartHeaderId = 0`, JWT UserId, zero total and discount, and an empty non-null CartDetails collection. This is a read-only projection, not a database insert. Preserve composition and error handling for real headers, including headers without details; do not convert storage or dependency failures into empty carts.
- Mango.Web displays this successful empty result through its existing CartIndex view. Checkout GET, Checkout POST and EmailCart reject the virtual header with HTTP 404 before creating an order or requesting email. Preserve 404 for incorrect routes/configuration, existing authentication rejections and real dependency errors; no generic error fallback, message matching or error discriminator is introduced. RemoveCart retains last-header deletion and the following own GetCart returns the logical empty state.
- Reconstruct the email payload from trusted data and use the validated JWT `email` claim as the approved recipient; reject without publishing when it is missing or unusable (SC-05).
- Add automated security and integration coverage, including Web and existing downstream token propagation.
- Require owner-conditioned writes, protected detail insertion, atomic operations with rollback, safe last-header cleanup and safe conflict responses. Verify the critical concurrency scenarios with a bounded SQL Server regression; do not claim universal absence of deadlocks.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `shopping-cart-api`: authentication, resource ownership, trustworthy cart-email publication and the logical empty-cart read contract with safe Web consumers.

## Impact

ShoppingCartAPI authorization and email payload behavior change; Mango.Web and consumers may receive new 400, 401 and 404 responses. ProductAPI, CouponAPI, AuthAPI and EmailAPI integrations must be checked without widening service scope absent demonstrated need. Existing message shape is retained where feasible. No shared database or unapproved cross-service dependency is introduced.

Alternative B changes own-header absence from API 404 to 200 without adding DTO fields. Mango.Web is the only production GetCart client found in this repository; Checkout and EmailCart need explicit virtual-header guards. External clients depending on 404 require compatibility review. The planned production edits are confined to GetCart and the affected Web consumer guards, not BaseService, the shared loader, other services or RemoveCart. This document specifies intended behavior, not implementation already delivered.

## Exclusions

Multi-item upsert redesign, unrelated order security, changes to other services without demonstrated need, and unapproved recipient-source design are excluded. Archived baseline and error-handling changes remain historical.

Global uniqueness per user and product, global atomic increments, coordination across all writers, index and locking optimization, advanced retries and idempotency are deferred to a later concurrency-integrity change. Deferral does not relax owner authorization, protection of foreign resources, atomicity, protected insertion or safe cleanup. Duplicate carts or details, lost increments, contention and ambiguous commit outcomes remain open risks; their acceptance is proposed and requires an explicit decision. The existing main specification is not changed or represented as globally satisfied by this deferral.

The proposed successor remains `harden-shoppingcart-concurrency-integrity`; it has not been created by this update. Tasks 4.2 and 8.2 remain pending. Approving alternative B and its documentation does not authorize implementation, concurrency investigation, synchronization or archiving.
