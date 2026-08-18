## Why

The `product-api` baseline documented several error-handling, validation, and status-code gaps that make `Mango.Services.ProductAPI` inconsistent with `Mango.Services.CouponAPI` and unsafe for clients to rely on: not-found lookups throw and are swallowed into an always-200 response, `PUT` blindly calls `Update()` without checking existence, `ProductDto` carries no validation so invalid payloads are silently persisted, and raw exception messages leak to callers. This change fixes those specific issues while deliberately keeping the public, unauthenticated read endpoints as-is.

## What Changes

- **BREAKING**: Change all `ProductAPIController` action return types from `ResponseDto` to `ActionResult<ResponseDto>`, enabling real HTTP status codes (404, 400, 500) instead of always HTTP 200. Existing clients that only inspect the JSON body are unaffected; clients relying on the current "always 200" behavior will see this change.
- Replace `.FirstAsync()` with `.FirstOrDefaultAsync()` in `Get(int id)` and `Delete(int id)`; when no match is found, return HTTP 404 with `IsSuccess = false` instead of relying on an unhandled `InvalidOperationException`.
- Change `PUT /api/product` to look up the existing `Product` by `ProductId` via `FirstOrDefaultAsync` first: return HTTP 404 if it doesn't exist, otherwise update its fields and save.
- Add server-side validation to `ProductDto`: `Name` required (non-empty), `Price` in range `1-1000` (mirroring the `Product` entity's existing, currently-unenforced annotations). Invalid `POST`/`PUT` payloads return HTTP 400 with validation details instead of being persisted.
- Replace raw `ex.Message` exposure in every generic `catch` block with a fixed, generic message, returned via `StatusCode(500, _response)`.
- Document explicitly in the spec that `GET /api/product` and `GET /api/product/{id}` remain intentionally unauthenticated (public product catalog) - this is a deliberate design decision, not an oversight, and no `[Authorize]` is being added to either action or to the controller class.

Out of scope for this change (explicitly deferred, no urgency): pagination on `GET /api/product`, uniqueness constraint on `Name`, and the redundant `builder.Services.AddAuthentication()` call in `Program.cs`.

## Capabilities

### Modified Capabilities
- `product-api`: `Retrieve product by id`, `Update product (admin only)`, `Delete product (admin only)`, `Create product (admin only)`, `Unhandled errors surface as HTTP 200 with exception detail` (becomes a proper 500 with a generic message), and `JWT-based authentication and authorization` (adds an explicit "intentionally public reads" clause) all change behavior. A new requirement documenting DTO validation is added.

## Impact

- Affected code: `Mango.Services.ProductAPI/Controllers/ProductAPIController.cs`, `Mango.Services.ProductAPI/Models/Dto/ProductDto.cs`.
- Affected API: response envelope stays `ResponseDto`, but HTTP status codes for `GET /api/product/{id}`, `PUT /api/product`, `DELETE /api/product/{id}`, `POST /api/product` change from always-200 to 200/400/404/500 as appropriate. Downstream consumers (`Mango.Web`, `Mango.Services.ShoppingCartAPI`) that only read the JSON body are unaffected; any consumer branching on HTTP status code must be reviewed.
- No schema, dependency, or authentication-model changes. No changes to `GET /api/product` or `GET /api/product/{id}` authorization (still public).
