# Tasks

The approved alternative B is recorded in `docs/audits/shoppingcart-authorization/cart-empty-contract-decision.md` and formalized by D11 and the delta scenarios. After the planning-only update, implementation and controlled non-SQL regression were separately authorized and completed in section 9. Checked entries in sections 1-8 retain their historical baseline scope; section 9 records the revised contract's implementation and verification. Tasks 4.2 and 8.2 remain pending; successful non-SQL regression and documentary validation do not close the outstanding D06 verification or authorize synchronization.

## 1. Authentication and identity

- [x] 1.1 Add authenticated access to all five actions; verify missing, invalid and expired JWTs return 401 with no data changes or bus publication.
- [x] 1.2 Resolve the authenticated subject with verified ASP.NET Core claim mapping; test real issued-token mapping and reject missing or unusable subject with 401.

## 2. Read authorization

- [x] 2.1 Establish the owner-scoped GetCart baseline and tests for own, malformed, foreign and nonexistent identifiers with 400 or indistinguishable 404 as originally applicable. The previous own-missing 404 expectation is superseded by D11; its 200 replacement and revised foreign-existence equivalence remain pending in 9.1 and 9.3.

## 3. Write authorization

- [x] 3.1 Enforce ownership and UserId mismatch rules for ApplyCoupon; test owner, malformed, foreign and nonexistent carts with 400 or indistinguishable 404 and no unauthorized mutation.
- [x] 3.2 Enforce owner-scoped CartUpsert and trusted new-cart ownership; test owner creation/update, malformed UserId and well-formed mismatched UserId with identical 404 whether the other cart exists or not.
- [x] 3.3 Validate supplied header/detail identifiers and nested objects before mapping or persistence; test foreign, inconsistent, fabricated and default IDs without allowing cross-cart mutation.

## 4. Detail ownership

- [x] 4.1 Restrict RemoveCart by persisted parent ownership and preserve last-item cleanup; test own, foreign and nonexistent details, indistinguishable 404 responses and no unauthorized deletion.
- [ ] 4.2 PENDING: preserve owner-conditioned writes and affected-row checks, protection of foreign resources, protected detail insertion, atomicity and rollback, safe last-header cleanup and generic conflict responses. In the isolated SQL Server laboratory, reuse the existing tests for owner/foreign/missing/ADMIN and nested references, ownership changes during writes and insertion, rollback after a write before commit, same-product concurrent outcomes, and last-detail removal versus insertion in both relevant orders. Resolve and document the previously failed synchronized tests and verify safe conflict outcomes, no loss of a confirmed insertion, unchanged foreign state, released barriers and completed tasks before cleanup. A 500, deadlock or timeout is not an automatic pass; stop on persistent tasks, uncertain rollback or integrity loss. Follow the bounded limits and evidence in `docs/audits/shoppingcart-authorization/concurrency-final-diagnosis.md` and `docs/audits/shoppingcart-authorization/concurrency-verification.md`; no exhaustive schedules or universal absence of deadlocks are required. Global uniqueness, duplicate cleanup, global atomic increments, coordination across all writers, index and locking optimization, advanced retries and idempotency remain proposed for the future `harden-shoppingcart-concurrency-integrity` change; document residual risks and obtain explicit acceptance rather than assuming it. Do not claim these deferred guarantees.

## 5. Cart email security

- [x] 5.1 Reconstruct the owned cart message from trusted data and the approved JWT email claim; test payload integrity and EmailAPI message shape compatibility.
- [x] 5.2 Verify owner success and absence of bus publication for foreign, malformed, manipulated, unavailable-recipient and unresolved-downstream requests.

## 6. Mango.Web compatibility

- [x] 6.1 Exercise the baseline authenticated GetCart, coupon, upsert, remove and email flows through Mango.Web; verify bearer propagation and 400/401/404 handling. The successful empty read, followed redirect and virtual-header consumer guards added by D11 remain pending in section 9.
- [x] 6.2 Verify existing ProductAPI/CouponAPI token forwarding and failure behavior with controlled integrations; confirm no unapproved service dependency is introduced.

## 7. Automated security coverage

- [x] 7.1 Add and run automated authorization tests covering all five endpoints, owner/foreign/ADMIN, missing/invalid/expired JWT, missing subject, identifier discrepancies and unchanged state on rejection.
- [x] 7.2 Test bus publication and downstream calls with controlled fakes; verify zero messages for every rejected email request.

## 8. Security and documentation verification

- [x] 8.1 Run the baseline relevant test suite and build; review response equivalence, sensitive data exposure and affected endpoints against the earlier delta. This completed baseline does not establish implementation or regression verification of D11.
- [ ] 8.2 Validate the change with `openspec validate harden-shoppingcart-authorization --strict` and compare implemented behavior to all security scenarios.
- [ ] 8.3 After separate authorization for synchronization, sync the delta and verify unrelated requirements and scenarios remain intact; confirm no archived changes were modified.

## 9. Approved logical empty-cart contract: implemented and verified without SQL

- [x] 9.1 After separate implementation authorization, change only GetCart's successfully established own-header absence to HTTP 200 with `IsSuccess = true`, non-null CartDto and CartHeader, `CartHeaderId = 0`, JWT UserId, zero total/discount and empty non-null CartDetails. Perform no writes, header creation, downstream composition calls or publication for that absence. Preserve JWT checks, syntactic validation and foreign rejection before existence queries; retain storage errors and all real-header composition/error behavior.
- [x] 9.2 Add Mango.Web guards in Checkout GET, Checkout POST and EmailCart that reject the virtual `CartHeaderId = 0` result with HTTP 404 before checkout rendering, order creation or email dispatch. Keep CartIndex on its normal successful empty-view path. Preserve existing null/failure guards, real-header behavior, BaseService, ResponseDto, DTO shapes, the shared loader, routes, views and RemoveCart's last-header cleanup.
- [x] 9.3 Adjust GetCart authorization tests to separate own-header absence from foreign references. Verify the exact successful empty shape, repeated reads without writes or downstream/bus calls, and no foreign-existence queries; compare indistinguishable 404 bodies/status for foreign-existing and foreign-missing identities, including ADMIN. Preserve missing/invalid/expired JWT, unusable subject, malformed IDs, nested reference and cross-owner write rejection coverage.
- [x] 9.4 Add focused non-SQL HTTP/MVC regressions for direct empty CartIndex and successful last-item removal followed through its redirect using the same valid session, ending in HTTP 200 with the existing empty view. Use a controlled successful removal fake and actual empty-read serialization where appropriate; do not claim that this fake verifies relational deletion, rollback or concurrency.
- [x] 9.5 Preserve and exercise own real-cart product/totals behavior and the real-header-without-details cases: valid dependencies allow the existing empty view, while Product/Coupon failures remain failures. Do not add an absence shortcut for a real header. Retain unresolved-product unsuccessful responses with their partial result and CouponAPI 404 without discount.
- [x] 9.6 Verify incorrect-route/configuration 404 with empty, ProblemDetails or generic bodies stays 404; received 400/401/403/500, storage errors and Web/API transport errors retain rejection, including the existing 502 mapping. Cookie/bearer identity mismatch remains rejected. No general error, unsuccessful response, message text or dependency failure is interpreted as a logical empty cart.
- [x] 9.7 Verify virtual-header rejection with HTTP 404 separately in Checkout GET, Checkout POST and EmailCart, with zero order/email calls or bus publication; retain their real-cart flows and ApplyCoupon/RemoveCart foreign/missing rejection behavior.
- [x] 9.8 Compile pertinent projects and run only the affected functional/authorization regressions approved for this correction, with controlled fakes and InMemory reads where suitable. Exclude SQL variants, database access, migrations and concurrency 4.2; record actual pass/fail results and limits. Do not execute a whole mixed SQL/non-SQL integration class without isolating the approved cases.
- [x] 9.9 After verified implementation, update the regression diagnosis, hardening final review and index with the applied contract and actual regression results. Re-run strict OpenSpec validation and compare code against every revised and preserved security scenario for pending 8.2; planning validation is not proof of implementation. Keep 4.2 pending and retain the separately scoped proposed `harden-shoppingcart-concurrency-integrity` successor without creating or executing it here.

### Section 9 verification evidence

Final joint run: 77 executed, 77 passed, 0 failed and 0 skipped within the explicit six-class non-SQL selection. The initial sandbox joint run had one AuthenticationTests host failure from DPAPI/EventLog restrictions; the unchanged test and then the full selection passed outside the sandbox. Earlier runs are not added to the final count. The final test-project build passed with zero errors and six existing NU1903 warnings; strict OpenSpec validation and git diff --check passed.

See `docs/audits/shoppingcart-authorization/hardening-final-review.md` for the exact filter, TRX, per-class counts, failure/retry history and comparison of all 44 delta scenarios. SQL-only and mixed integration classes and every concurrency case were excluded, not counted as skips. The last-removal navigation uses a controlled removal fake followed by real GetCart/serialization over InMemory; it does not verify relational deletion or transactions. Existing write/nested-reference bodies and SQL tests were preserved and reviewed, not rerun. No migrations, dependency updates, delta edits, synchronization, archival, commits or pushes were performed. Tasks 4.2, 8.2 and 8.3 remain pending and the proposed successor retains its separate scope.
