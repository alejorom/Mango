## Context

EmailAPI is a message-driven microservice with no REST API. Current implementation:
- Receives events via Azure Service Bus (3 queue/subscription subscriptions)
- Logs events to SQL Server via EF Core
- Uses AzureServiceBusConsumer as background processor
- EmailService builds notification content and persists to EmailLogger table

See proposal.md for motivation.

## Goals / Non-Goals

**Goals:**
- Document current behavior as baseline
- Identify design constraints and risks
- Enable future improvements with clear contracts

**Non-Goals:**
- Change current implementation
- Add email sending capability
- Add REST API endpoints
- Refactor exception handling

## Decisions

### Azure Service Bus as message transport
EmailAPI consumes from Service Bus using `Azure.Messaging.ServiceBus` SDK with:
- One ServiceBusProcessor per queue/subscription
- Deserialization via Newtonsoft.Json (consistent with Mango.MessageBus)
- Manual CompleteMessageAsync on success

**Rationale**: Decouples EventPublisher from consumer; enables async scaling.
**Alternatives considered**: Direct HTTP calls (tightly coupled), RabbitMQ (different transport).

### Email content construction in-memory, persisted to DB
EmailService builds HTML messages (StringBuilder) and logs via EmailLogger entity.
No external SMTP or email service integration - purely log-based.

**Rationale**: Audit trail for notifications; no external dependency risk; log data available for future email integration.
**Alternatives considered**: Real email sending to SMTP (adds complexity, vendor lock-in); queued email jobs (adds complexity without current need).

### EF Core with DbContextOptions passed to EmailService
EmailService receives DbContextOptions singleton; creates new AppDbContext per message.
Migrations run at startup via `ApplyMigration()`.

**Rationale**: Simplicity; avoids DbContext scope issues in background consumer.
**Alternatives considered**: Scoped DbContext (adds DI complexity); async DbContext pooling (premature optimization).

### Three separate Service Bus processors (no topic/subscription routing)
Cart, RegisterUser, and OrderPlaced are three distinct processors in a single consumer class.
Each has its own ProcessMessageAsync handler.

**Rationale**: Clear separation of message types; independent error handling per queue.
**Alternatives considered**: Topic-based subscription with one processor (adds routing complexity).

## Risks / Trade-offs

**[Risk] TWO distinct exception paths mask data loss**
→ Path 1: OnXxxReceived handlers (cart/user/order) rethrow exceptions → message stays unacknowledged → Service Bus retries/dead-letters per config. Explicit, tied to broker.
→ Path 2: EmailService.LogAndEmail silently catches DB errors, returns false, callers ignore return value → message still CompleteMessageAsync'd → data loss. Silent, no audit.
→ ErrorHandler (Console.WriteLine) only catches ProcessErrorAsync from SDK, not handler exceptions.
→ Mitigation: Make LogAndEmail throw; callers must propagate or log explicitly. Add structured logging throughout.

**[Risk] Config keys for queues/topics are manually maintained**
→ Typos in config keys cause processor to ignore messages silently.
→ Mitigation: Add validation at startup; enum-based queue name configuration.

**[Risk] Hard-coded placeholder email addresses**
→ RegisterUserEmailAndLog uses `<<uncorreo>>@<<dominio>>.com` (literal placeholder).
→ LogOrderPlaced uses `dotnetmastery@gmail.com` (hard-coded).
→ Mitigation: Move to config; add validation that prevents invalid emails.

**[Risk] No metrics or tracing**
→ No observability into message processing rate, latency, or failures.
→ Mitigation: Add Application Insights or similar; instrument via ILogger.

**[Trade-off] No external email sending**
→ Current design logs but never delivers email. Useful for audit, but users see no notification.
→ Choice: Accept for now; add SMTP/SendGrid integration as future capability.

## Open Questions

- Should EmailService validate CartDto/RewardsMessage/email format before persisting?
- Should failed message processing trigger a retry policy or dead-letter queue?
- Is the `<<uncorreto>>@<<dominio>>.com` placeholder intentional, or a TODO?

