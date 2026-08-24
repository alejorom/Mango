## Context

`Mango.Services.ShoppingCartAPI` is existing, deployed code. This change produces no implementation changes — it writes a baseline spec documenting current behavior. See proposal.md for motivation.

Notable current-state constraints that shape what the spec documents:

- Two owned tables (`CartHeaders`, `CartDetails`) in a dedicated SQL Server instance. No shared DB with other services.
- ProductAPI and CouponAPI are called over HTTP at request time (synchronous composition). Both use named `HttpClient` instances registered in DI, decorated with `BackendApiAuthenticationHttpClientHandler` to propagate the caller's JWT.
- Azure Service Bus (`emailshoppingcart` queue) is used for one outbound message (email request). No inbound message consumption in this service.
- `CartHeaderDto` contains `Name`, `Phone`, `Email` fields absent from the `CartHeader` entity. These are carried only in the DTO and published in the `EmailCartRequest` payload.

## Goals / Non-Goals

**Goals:**
- Produce an accurate spec of all five endpoints, the two downstream HTTP integrations, the Service Bus integration, and the JWT auth configuration, grounded strictly in the code as it exists today.

**Non-Goals:**
- Proposing fixes to the gaps identified (no `[Authorize]`, inconsistent exception detail, potential NullReferenceException on missing product). Those belong in a follow-up change.
- Documenting internal class structure or AutoMapper profiles.

## Decisions

**Decision: Document the missing `[Authorize]` as a spec fact, not fix it.**
The spec records the current observable behavior (no authorization enforced at action level). Fixing it is a behavior change and requires its own proposal. Documenting it here makes the gap explicit for future changes.

*Alternative considered: silently skip it.* Rejected — would make the spec inaccurate and hide a real security gap.

**Decision: Document `CouponService.GetCoupon` returning `new CouponDto()` (not null) on failure.**
The controller checks `coupon != null` — this check always passes. The effective guard is `CartTotal > coupon.MinAmount` (MinAmount defaults to 0). The spec documents this behavior faithfully.

*Alternative considered: describe the intent (return null on failure).* Rejected — the spec must match the code, not the intent.

**Decision: Note the `ProductService` empty-list behavior on failure without prescribing a fix.**
If ProductAPI is down and `GetProducts` returns an empty list, `item.Product` in `GetCart` will be null, causing a `NullReferenceException` on `item.Product.Price`. The spec documents the empty-list contract of the service method; the runtime failure is captured under risks.

## Risks / Trade-offs

- **No `[Authorize]` on cart endpoints**: Any caller (authenticated or not) can read/modify any user's cart by supplying the correct `userId`. Future change should add `[Authorize]` and verify the token's `sub` claim matches the requested `userId`.
- **ProductAPI failure causes NullReferenceException in GetCart**: If `GetProducts` returns empty, `item.Product` is null; `item.Product.Price` throws. The generic `try/catch` absorbs the exception and returns `IsSuccess = false`, but the error is silent.
- **Exception detail exposed**: `ApplyCoupon` and `EmailCartRequest` use `ex.ToString()` (includes stack trace in `Message`). Acceptable for a learning project; would be a disclosure risk in production.
- **Inconsistent HTTP status codes**: All actions return HTTP 200 even on error. Consumers must check `ResponseDto.IsSuccess` to detect failures — HTTP status alone is unreliable.
- **`CartHeaderDto.Name/Phone/Email` never populated by server**: These fields exist in the DTO and are included in the Service Bus payload, but no endpoint populates them from the cart data. They would need to be supplied by the client when calling `EmailCartRequest`.

## Open Questions

- Should `[Authorize]` be added to `CartAPIController` and should `GetCart` enforce that the authenticated user's `sub` matches the requested `userId`? (Out of scope for this baseline — deferred to a security hardening change.)
