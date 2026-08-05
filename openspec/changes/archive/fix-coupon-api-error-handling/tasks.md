## 1. Validation

- [x] 1.1 Add data annotations to `CouponDto`: `[Required]` on `CouponCode`, validation ensuring `DiscountAmount > 0`, and `MinAmount >= 0`
- [x] 1.2 In `Post` and `Put` actions, check `ModelState.IsValid` and return `BadRequest(ModelState)` before mapping/persisting when invalid

## 2. Not-found handling

- [x] 2.1 Replace `.First(...)` with `.FirstOrDefault(...)` in `Get(int id)`, `GetByCode(string code)`, and `Delete(int id)`
- [x] 2.2 Return `NotFound(_response)` with `IsSuccess = false` and a generic "coupon not found" message when the lookup result is `null`, instead of relying on a thrown exception

## 3. Error response shape

- [x] 3.1 Change controller action return types from `ResponseDto` to `ActionResult<ResponseDto>`
- [x] 3.2 Replace `_response.Message = ex.Message` in all catch blocks with a fixed generic message, and return `StatusCode(500, _response)`

## 4. Verification

- [ ] 4.1 Add/update tests covering: not-found by id, not-found by code, not-found on delete (expect 404); invalid create/update payloads (expect 400); unexpected error path (expect 500 with generic message) (SKIPPED: sin proyecto de tests aún, se prueba manual vía 4.2 por ahora)
- [x] 4.2 Manually verify via `Mango.Services.CouponAPI.http` that success paths still return the expected `CouponDto` payloads
- [x] 4.3 Check `Mango.Web`'s coupon service/controllers for any logic assuming HTTP 200 on not-found, and update if found (Confirmado: BaseService.cs ya maneja 404/500 explícitamente sin excepción. Gap menor: 400 cae en default y no expone detalle de ModelState - candidato a change futuro, fuera de alcance aquí)

## 5. Publish

- [x] 5.1 Run `openspec validate --strict` for this change and fix any structural issues
- [x] 5.2 Sync `specs/coupon-api/spec.md` into `openspec/specs/coupon-api/spec.md`
- [x] 5.3 Archive the change once implementation and spec sync are complete
