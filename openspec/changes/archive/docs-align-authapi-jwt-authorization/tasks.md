# Tasks

## 1. Review documentary correction

- [x] 1.1 Compare the rename and complete modified dependency block against the main AuthAPI spec and archived `fix-authapi-security-issues`; verify the existing requirement text and scenarios remain unchanged by the rename.
- [x] 1.2 Confirm the delta preserves 401, 403, ADMIN authorization and anonymous registration/login contracts by comparing the existing Role assignment and authorization scenarios with the source spec.
- [x] 1.3 Run `openspec validate docs-align-authapi-jwt-authorization --strict` and resolve any delta validation errors without changing the behavior.

## 2. Synchronization follow-up

- [x] 2.1 Once synchronization is separately authorized, sync the delta using the OpenSpec workflow and verify the main spec contains the renamed requirement once, its original scenarios and the corrected dependency block.
- [x] 2.2 Review the resulting spec diff and run OpenSpec validation; confirm no C# code, HTTP contracts or archived changes were modified.
