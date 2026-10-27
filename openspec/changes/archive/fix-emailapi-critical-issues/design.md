## Context

See proposal.md. Fix: LogAndEmail swallows DB errors without logging. EmailCartAndLog crashes on null Product.

## Goals / Non-Goals

**Goals:**
- Prevent silent data loss when DB persistence fails
- Prevent poison messages from null Product references
- Maintain consistency with Service Bus broker's retry/dead-letter policy

**Non-Goals:**
- Custom retry logic (use broker's built-in policy)
- Change Service Bus topology
- Add new external dependencies

## Decisions

### Inject ILogger<EmailService> and log exceptions in LogAndEmail
EmailService receives ILogger<EmailService> via DI. LogAndEmail logs full exception before throwing, visible in Application Insights or console logs.

**Rationale**: Audit trail; errors now detectable in production monitoring.
**Alternative**: Silent re-throw (loses visibility); custom retry loop (duplicates broker).

### Throw from LogAndEmail instead of return false
LogAndEmail throws after logging, callers (EmailCartAndLog, LogOrderPlaced, RegisterUserEmailAndLog) don't need to check return value. Exception propagates to handler → message stays unacked → Service Bus retries or dead-letters.

**Rationale**: Explicit failure path consistent with spec (Path 1). Simple, no check needed.
**Alternative**: Return false, check in caller (requires 3 changes, error-prone).

### Null-check per item in EmailCartAndLog loop
Loop through CartDetails, skip items with null Product instead of crashing. Append "[Some products could not be included]" note in message.

**Rationale**: Defensive; logs most info; allows message to complete and be audited instead of blocking queue.
**Alternative**: Throw on null Product (poison message); filter at entry (hides the issue).

## Risks / Trade-offs

**No risks identified.** Changes are purely defensive, no breaking changes or architectural impact.
