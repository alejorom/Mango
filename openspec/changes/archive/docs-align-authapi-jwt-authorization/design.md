# Design

## Context

The archived `fix-authapi-security-issues` changed the authorization requirement body but retained the old name. The dependency requirement still describes JWT Bearer as not validating requests. Current AuthAPI registers JWT Bearer and protects AssignRole. See proposal.md and finding H09 in `docs/audits/agent-instructions-net11/evidence.md`.

## Goals / Non-Goals

**Goals:** Correct both stale statements while preserving existing scenarios and the behavior recorded by the archived security change.

**Non-Goals:** Change authentication, authorization, HTTP contracts, code, packages, archived artifacts or unrelated requirements.

## Decisions

- Use `RENAMED Requirements` for the title only: synchronization preserves the original requirement body and scenarios. Adding a new requirement would duplicate the existing contract.
- Use `MODIFIED Requirements` for the entire external dependencies block with its existing scenario; only the JWT Bearer description changes. A partial replacement could discard the scenario.
- Keep the existing 401, 403 and anonymous registration/login scenarios intact. Runtime verification is outside this documentation-only change; static comparison does not establish runtime behavior.

## Risks / Trade-offs

- [Risk] Rename or synchronization could drop scenarios or create a duplicate requirement. → Compare the synchronized result with the original, verify exact names and scenario counts, and validate with OpenSpec 1.15.0.
- [Risk] A documentation correction could accidentally imply changed behavior. → Review the final diff against the existing requirement and archived security decision.

## Migration Plan

After approval, apply the documentation tasks and synchronize through the OpenSpec workflow. Review the resulting spec diff; if incorrect, revert the documentation change through normal version control without altering runtime code.
