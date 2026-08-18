## 1. Validation

- [x] 1.1 Add data annotations to `ProductDto`: `[Required]` on `Name`, `[Range(1, 1000)]` on `Price` (mirroring `Product` entity)
- [x] 1.2 In `Post` and `Put` actions, check `ModelState.IsValid` and return `BadRequest(ModelState)` before mapping/persisting when invalid
  (NOTA: requirió adelantar el cambio de tipo de retorno de Post/Put a 
  ActionResult<ResponseDto>, prerrequisito real de BadRequest(ModelState) — 
  Get/GetById/Delete quedan pendientes para el bloque 4 completo)

## 2. Not-found handling

- [x] 2.1 Replace `.FirstAsync(...)` with `.FirstOrDefaultAsync(...)` in `Get(int id)` and `Delete(int id)`
- [x] 2.2 Return `NotFound(_response)` with `IsSuccess = false` and a generic "product not found" message when the lookup result is `null`, instead of relying on a thrown exception
  (NOTA: requirió adelantar el cambio de tipo de retorno de Get(int id) y 
  Delete(int id) a ActionResult<ResponseDto>, mismo prerrequisito que en el 
  bloque 1 — Get() (listado) queda sin tocar para el bloque 4)

## 3. Update existence check

- [x] 3.1 In `Put`, fetch the tracked `Product` via `FirstOrDefaultAsync(u => u.ProductId == productDto.ProductId)` before making any change
- [x] 3.2 Return `NotFound(_response)` when the product doesn't exist, without calling `SaveChangesAsync`
- [x] 3.3 When found, update the tracked entity's fields from `productDto` (instead of mapping a new detached instance + `Update()`) and call `SaveChangesAsync`
  (NOTA: se usó `_mapper.Map(productDto, obj)` para actualizar la entidad 
  trackeada in-place, en vez de asignar campo por campo)

## 4. Error response shape

- [x] 4.1 Change all five controller action return types from `ResponseDto` to `ActionResult<ResponseDto>`
- [x] 4.2 Replace `_response.Message = ex.Message` in all catch blocks with a fixed generic message ("An unexpected error occurred"), and return `StatusCode(500, _response)`

## 5. Verification

- [x] 5.1 Add/update tests covering: not-found by id (expect 404), not-found on delete (expect 404), not-found on update (expect 404), invalid create/update payloads (expect 400), unexpected error path (expect 500 with generic message) - if no test project exists yet, note as skipped and rely on manual verification (5.2)
- [x] 5.2 Manually verify via `Mango.Services.ProductAPI.http` that success paths still return the expected `ProductDto` payloads, and that `GET /api/product`/`GET /api/product/{id}` remain reachable without a token
- [x] 5.3 Check `Mango.Web` and `Mango.Services.ShoppingCartAPI` (`ProductService.GetProducts()`) for any logic assuming HTTP 200 on not-found/validation errors, and update if found
(CONFIRMADO, sin cambios necesarios:
  - Mango.Web: usa IBaseService.SendAsync (BaseService.cs), que ya maneja 404/500 
    explícitamente por status code antes de deserializar — mismo mecanismo ya 
    confirmado en el fix de coupon-api.
  - ShoppingCartAPI.ProductService.GetProducts(): NO llama a EnsureSuccessStatusCode(), 
    lee el body y lo deserializa como ResponseDto sin importar el status code HTTP 
    recibido. Como el shape del body no cambió (solo cambió el status code), sigue 
    funcionando sin modificaciones.
  - HALLAZGO SUELTO, fuera de alcance de este change: ese mismo código de 
    ShoppingCartAPI asume que el body SIEMPRE es JSON parseable como ResponseDto. 
    Si ProductAPI alguna vez devolviera un error no-JSON (ej. una página de error 
    de infraestructura), JsonConvert.DeserializeObject fallaría sin control. 
    Preexistente, no introducido por este fix — candidato a revisar en el 
    baseline de shopping-cart-api.
  - HALLAZGO SUELTO ADICIONAL, fuera de alcance: Mango.Web/ProductService.cs 
    tiene un método GetProductAsync(string productCode) que apunta a 
    /api/product/GetByCode/{productCode}, endpoint que NO existe en 
    ProductAPIController — código muerto/copiado por error, no rompe nada 
    porque no se invoca, pero candidato a limpieza.)
    
## 6. Publish

- [x] 6.1 Run `openspec validate --strict` for this change and fix any structural issues
- [x] 6.2 Sync `specs/product-api/spec.md` into `openspec/specs/product-api/spec.md`
- [x] 6.3 Archive the change once implementation and spec sync are complete
