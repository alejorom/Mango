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
- [x] 2.3 VERIFICADO EN RUNTIME (2026-08-24): Se confirmó con un breakpoint en 
  BackendApiAuthenticationHttpClientHandler que GetTokenAsync("access_token") 
  SÍ devuelve un JWT válido y completo durante un GetCart real. La sospecha 
  inicial (que GetTokenAsync solo funciona con flujos SignInAsync) resultó 
  INCORRECTA — ASP.NET Core también lo popula automáticamente al validar un 
  JWT Bearer entrante. CONCLUSIÓN: la propagación de token funciona 
  correctamente, no requiere fix. El siguiente change de autorización 
  (harden-shoppingcart-authorization) se enfoca solo en si agregar [Authorize] 
  al controller, no en el token en sí. 

## 3. Publish baseline spec

- [x] 3.1 Run `openspec validate shoppingcart-api-baseline --strict` and fix 
  any structural issues
- [ ] 3.2 Sync `specs/shopping-cart-api/spec.md` into `openspec/specs/shopping-cart-api/spec.md`
- [ ] 3.3 Archive the change once the main spec reflects the documented baseline