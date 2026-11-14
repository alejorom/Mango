# Proposal

## Why

Audit finding H09 identifies stale text in the AuthAPI specification that contradicts the JWT authorization introduced by archived change `fix-authapi-security-issues` and the current implementation.

## What Changes

- Rename the existing misleading authorization requirement while preserving its full text and scenarios.
- Correct the external dependencies requirement to describe JWT Bearer request validation as well as token-related use.
- Preserve all HTTP contracts, scenarios, code, and archived changes.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `auth-api`: correct the names and descriptions of existing requirements without changing their specified behavior.

## Impact

Only the AuthAPI specification is affected by the planned synchronization; there are no runtime, API, package, or architectural changes.
