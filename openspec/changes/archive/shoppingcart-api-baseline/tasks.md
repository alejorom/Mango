## 1. Validate baseline accuracy

- [x] 1.1 Cross-check `specs/shopping-cart-api/spec.md` requirements against current 
  `Mango.Services.ShoppingCartAPI` source (`Controllers/CartAPIController.cs`, 
  `Service/ProductService.cs`, `Service/CouponService.cs`, `Program.cs`, 
  `Extensions/WebApplicationBuilderExtensions.cs`)
- [x] 1.2 Confirm `Mango.MessageBus` project reference and `IMessageBus` usage 
  match the documented `EmailCartRequest` publish-only behavior (no inbound consumption)
- [x] 1.3 Confirm the JWT auth pattern (flat `ApiSettings:Secret` vs nested 
  `ApiSettings:JwtOptions`) actually used in `WebApplicationBuilderExtensions.cs`
- [x] 1.4 Confirm `BackendApiAuthenticationHttpClientHandler` propagates the 
  caller's JWT to both the "Product" and "Coupon" named HttpClients

## 2. Review ambiguous/incomplete points

- [x] 2.1 Share the "ambiguous or incomplete" findings with the team for triage:
  - **CRÍTICO**: `CouponService.GetCoupon` returns `new CouponDto()` (never `null`) 
    on any failure; `CartAPIController.GetCart`'s `coupon != null` check is 
    therefore always true, and with `MinAmount` defaulting to 0, a coupon-service 
    failure silently applies a phantom $0 discount instead of failing visibly
  - **CRÍTICO**: If `ProductAPI` is down, `ProductService.GetProducts()` returns 
    an empty list, causing `item.Product` to be `null` in `GetCart` and 
    `item.Product.Price` to throw `NullReferenceException` — caught generically, 
    surfaces only as a vague `IsSuccess=false` with no indication of the root cause
  - No `[Authorize]` anywhere on `CartAPIController` - any caller can read/modify 
    any user's cart by supplying an arbitrary `userId`, with no check that it 
    matches the token's identity
  - `ApplyCoupon` and `EmailCartRequest` use `ex.ToString()` (leaks full stack 
    trace), worse than the `ex.Message` leak seen in other services
  - All actions return HTTP 200 even on error; no `ActionResult`/status code 
    differentiation anywhere in this controller
  - `Program.cs` has the same redundant `builder.Services.AddAuthentication()` 
    call after `builder.AddAppAuthetication()` already seen in `product-api`
  - `CartUpsert` only ever reads `cartDto.CartDetails.First()` - any additional 
    items in the payload are silently ignored
  - `RemoveCart` uses `.First()` instead of `FirstOrDefault`, throwing on an 
    unknown `cartDetailsId` instead of a controlled not-found response
  - `CartHeaderDto.Name/Phone/Email` are never populated server-side; only 
    present if the client supplies them directly to `EmailCartRequest`
- [x] 2.2 Decide whether any findings should become separate follow-up change 
  proposals (Decisión: SÍ, separados en DOS changes:
  1. fix-shoppingcart-error-handling: bugs confirmados por código (phantom 
     coupon discount, NullReferenceException en cascada por ProductAPI caído, 
     RemoveCart con .First(), ex.ToString() expuesto, ActionResult para status 
     codes reales, AddAuthentication() duplicado)
  2. investigate-shoppingcart-auth-propagation: verificar en runtime si 
     BackendApiAuthenticationHttpClientHandler realmente propaga un JWT válido 
     vía GetTokenAsync, y decidir junto con eso si agregar [Authorize] al 
     controller — ambos temas tocan el mismo modelo de seguridad y conviene 
     resolverlos juntos, después de confirmar el hallazgo de 2.3)
- [ ] 2.3 **SOSPECHA NO CONFIRMADA (requiere verificación en runtime)**: 
  `BackendApiAuthenticationHttpClientHandler` usa 
  `_accessor.HttpContext.GetTokenAsync("access_token")` para propagar el JWT 
  a las llamadas hacia ProductAPI/CouponAPI. `GetTokenAsync` está diseñado para 
  recuperar tokens persistidos vía `SignInAsync` (flujos de cookie/OIDC) — este 
  proyecto usa JWT Bearer puro sin `SignInAsync` en ningún lado visible del 
  código, por lo que es sospechoso que ese token realmente se esté propagando. 
  Si el token resultara null/vacío, las llamadas a Product/CouponAPI viajarían 
  sin autenticación real — lo cual hoy no se manifiesta como error visible 
  porque ambos servicios solo protegen sus endpoints de escritura (POST/PUT/
  DELETE), y ShoppingCartAPI solo les hace GET. NO SE VERIFICÓ EN RUNTIME 
  todavía (requeriría un breakpoint en esa línea durante un GetCart real). 
  Se documenta como sospecha, no como hecho confirmado.  

## 3. Publish baseline spec

- [x] 3.1 Run `openspec validate shoppingcart-api-baseline --strict` and fix 
  any structural issues
- [ ] 3.2 Sync `specs/shopping-cart-api/spec.md` into `openspec/specs/shopping-cart-api/spec.md`
- [ ] 3.3 Archive the change once the main spec reflects the documented baseline