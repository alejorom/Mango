## Why

`Mango.Services.CouponAPI` currently returns HTTP 200 for every response, even when a lookup fails or an unhandled exception occurs. Not-found lookups rely on `.First()` throwing an exception that is caught generically, and the raw `ex.Message` is returned to the caller. There is also no server-side validation on `CouponDto`, so invalid payloads (empty code, non-positive discount, negative minimum amount) are accepted. These were flagged as ambiguous/incomplete points in the `coupon-api` baseline spec and should be corrected now.

## What Changes

- **BREAKING**: `GET /api/coupon/{id}`, `GET /api/coupon/GetByCode/{code}`, and `DELETE /api/coupon/{id}` return HTTP 404 Not Found (instead of HTTP 200 with `IsSuccess = false`) when the coupon does not exist.
- Replace `.First()` with `.FirstOrDefault()` in all lookups; explicitly check for `null` and return a not-found result instead of relying on a thrown `InvalidOperationException`.
- Add server-side validation on `CouponDto`: `CouponCode` required (non-empty), `DiscountAmount` greater than 0, `MinAmount` greater than or equal to 0. Invalid payloads on `POST`/`PUT` return HTTP 400 Bad Request with validation error details, not a generic `ResponseDto` with `IsSuccess = true` shape.
- **BREAKING**: Unexpected server errors return HTTP 500 with a generic message; the raw `ex.Message` is no longer included in the response body.

## Capabilities

### New Capabilities
- None.

### Modified Capabilities
- `coupon-api`: not-found and error responses now use proper HTTP status codes instead of always returning 200; lookups no longer depend on `.First()` throwing; `CouponDto` gains server-side validation; exception messages are no longer exposed to callers.

## Impact

- Affected code: `Mango.Services.CouponAPI/Controllers/CouponAPIController.cs`, `Mango.Services.CouponAPI/Models/Dto/CouponDto.cs`.
- Affected consumers: any client of `Mango.Services.CouponAPI` that currently branches on `ResponseDto.IsSuccess` with an HTTP 200 status for not-found cases must be updated to handle HTTP 404/400/500 status codes.
- No new external dependencies; no database schema changes.
