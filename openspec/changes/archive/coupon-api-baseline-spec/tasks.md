## 1. Validate baseline accuracy

- [X] 1.1 Cross-check `specs/coupon-api/spec.md` requirements against current `Mango.Services.CouponAPI` source (`Controllers/CouponAPIController.cs`, `Models/Coupon.cs`, `Data/AppDbContext.cs`, `Extensions/WebApplicationBuilderExtensions.cs`, `Program.cs`)
- [X] 1.2 Confirm no NuGet package or `ProjectReference` for a message bus (RabbitMQ/MassTransit) exists in `Mango.Services.CouponAPI.csproj`
- [X] 1.3 Confirm no ASP.NET Core Identity (`UserManager`/`IdentityDbContext`) usage exists in this service

## 2. Review ambiguous/incomplete points

- [X] 2.1 Share the "ambiguous or incomplete" findings (unhandled not-found lookups via `.First()`, HTTP 200 returned on errors, exception messages leaked in `Message`, no DTO-level validation, no `CouponCode` uniqueness enforcement, no pagination on list endpoint) with the team for triage
- [X] 2.2 Decide whether any findings should become separate follow-up change proposals (out of scope for this baseline)

## 3. Publish baseline spec

- [X] 3.1 Run `openspec validate --change coupon-api-baseline-spec --strict` and fix any structural issues
- [X] 3.2 Sync `specs/coupon-api/spec.md` into `openspec/specs/coupon-api/spec.md` (via `/opsx-archive` or `/opsx-sync-specs`)
- [X] 3.3 Archive the change once the main spec reflects the documented baseline
