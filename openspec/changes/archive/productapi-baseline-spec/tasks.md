## 1. Validate baseline accuracy

- [x] 1.1 Cross-check `specs/product-api/spec.md` requirements against current `Mango.Services.ProductAPI` source (`Controllers/ProductAPIController.cs`, `Models/Product.cs`, `Models/Dto/ProductDto.cs`, `Data/AppDbContext.cs`, `Extensions/WebApplicationBuilderExtensions.cs`, `Program.cs`)
- [x] 1.2 Confirm no NuGet package or `ProjectReference` for a message bus (`Mango.MessageBus`/Azure Service Bus) exists in `Mango.Services.ProductAPI.csproj`
- [x] 1.3 Confirm no ASP.NET Core Identity (`UserManager`/`IdentityDbContext`) usage exists in this service
- [x] 1.4 Confirm `Mango.Services.ShoppingCartAPI` reaches this service only via HTTP (`Service/ProductService.cs`, named `HttpClient` "Product") and not through a message bus

## 2. Review ambiguous/incomplete points

- [x] 2.1 Share the "ambiguous or incomplete" findings with the team for triage:
  - `.FirstAsync()` on `Get(id)`/`Delete(id)` throws on not-found instead of using `FirstOrDefaultAsync` + explicit 404
  - Endpoints return bare `ResponseDto` instead of `ActionResult<ResponseDto>`, so every response (success or failure) is HTTP 200
  - Raw exception messages are leaked into `Message` on every catch block
  - `ProductDto` has no validation attributes, so `[Required]`/`[Range(1,1000)]` on the `Product` entity are never enforced on POST/PUT
  - `PUT` calls `Update()` on a freshly-mapped instance without checking the row exists first
  - No class-level `[Authorize]` and no `[Authorize]` at all on the two GET actions - the product catalog is fully readable without authentication
  - No uniqueness constraint on `Name`; no pagination on `GET /api/product`
  - Program.cs llama a builder.Services.AddAuthentication() sin 
  parámetros DESPUÉS de builder.AddAppAuthetication() (que ya configuró JWT Bearer 
  completo) — llamada redundante, no vista en CouponAPI ni AuthAPI, candidata a 
  limpieza aunque no se haya confirmado que cause un problema funcional real  
- [x] 2.2 Decide whether any findings should become separate follow-up change proposals (out of scope for this baseline)

## 3. Publish baseline spec

- [x] 3.1 Run `openspec validate --change productapi-baseline-spec --strict` and fix any structural issues
- [x] 3.2 Sync `specs/product-api/spec.md` into `openspec/specs/product-api/spec.md` (via `/opsx-archive` or `/opsx-sync-specs`)
- [x] 3.3 Archive the change once the main spec reflects the documented baseline
