# Tasks

## 1. Authentication and identity

- [x] 1.1 Add authenticated access to all five actions; verify missing, invalid and expired JWTs return 401 with no data changes or bus publication.
- [x] 1.2 Resolve the authenticated subject with verified ASP.NET Core claim mapping; test real issued-token mapping and reject missing or unusable subject with 401.

## 2. Read authorization

- [x] 2.1 Enforce owner-scoped GetCart; test own, malformed, foreign and nonexistent identifiers with 400 or indistinguishable 404 as applicable.

## 3. Write authorization

- [x] 3.1 Enforce ownership and UserId mismatch rules for ApplyCoupon; test owner, malformed, foreign and nonexistent carts with 400 or indistinguishable 404 and no unauthorized mutation.
- [x] 3.2 Enforce owner-scoped CartUpsert and trusted new-cart ownership; test owner creation/update, malformed UserId and well-formed mismatched UserId with identical 404 whether the other cart exists or not.
- [x] 3.3 Validate supplied header/detail identifiers and nested objects before mapping or persistence; test foreign, inconsistent, fabricated and default IDs without allowing cross-cart mutation.

## 4. Detail ownership

- [x] 4.1 Restrict RemoveCart by persisted parent ownership and preserve last-item cleanup; test own, foreign and nonexistent details, indistinguishable 404 responses and no unauthorized deletion.
- [ ] 4.2 PENDING: owner-conditioned writes and selective transactions have been implemented, but exhaustive verification is postponed for the demo after SQL Server deadlock 1205 and a timeout during transaction release/rollback. The 17 migrated tests and the deterministic tests identified in the evidence passed; the latest synchronized test failed and there is no satisfactory final full-suite execution. Diagnose the conflict and verify owner constraints, rollback and safe cleanup in isolated SQL Server before completion; see `docs/audits/shoppingcart-authorization/concurrency-verification.md`. Global uniqueness, duplicate cleanup, atomic increments, concurrent insertion coordination, advanced retries and comprehensive concurrency testing remain risks for the future `harden-shoppingcart-concurrency-integrity` change; do not claim those guarantees here.

## 5. Cart email security

- [x] 5.1 Reconstruct the owned cart message from trusted data and the approved JWT email claim; test payload integrity and EmailAPI message shape compatibility.
- [x] 5.2 Verify owner success and absence of bus publication for foreign, malformed, manipulated, unavailable-recipient and unresolved-downstream requests.

## 6. Mango.Web compatibility

- [ ] 6.1 Exercise authenticated GetCart, coupon, upsert, remove and email flows through Mango.Web; verify bearer propagation and 400/401/404 handling.
- [ ] 6.2 Verify existing ProductAPI/CouponAPI token forwarding and failure behavior with controlled integrations; confirm no unapproved service dependency is introduced.

## 7. Automated security coverage

- [ ] 7.1 Add and run automated authorization tests covering all five endpoints, owner/foreign/ADMIN, missing/invalid/expired JWT, missing subject, identifier discrepancies and unchanged state on rejection.
- [ ] 7.2 Test bus publication and downstream calls with controlled fakes; verify zero messages for every rejected email request.

## 8. Security and documentation verification

- [ ] 8.1 Run the relevant test suite and build; review response equivalence, sensitive data exposure and all affected endpoints against the delta.
- [ ] 8.2 Validate the change with `openspec validate harden-shoppingcart-authorization --strict` and compare implemented behavior to all security scenarios.
- [ ] 8.3 After separate authorization for synchronization, sync the delta and verify unrelated requirements and scenarios remain intact; confirm no archived changes were modified.
