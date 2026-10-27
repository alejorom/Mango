## Why

Two critical defects in EmailAPI produce silent data loss and poison message blocking:
1. LogAndEmail swallows DB exceptions without logging, message still marked complete
2. EmailCartAndLog crashes on null Product, message reintries forever

## What Changes

Add ILogger to EmailService. Verify LogAndEmail return values in callers, throw if false. Add null-checks per CartDto item before accessing Product fields.

## Capabilities

### New Capabilities

(None)

### Modified Capabilities

- `email-api`: LogAndEmail exception handling + null-safety on Cart items

## Impact

EmailService, AzureServiceBusConsumer exception paths. No API contract changes, no schema changes, only defensive code.
