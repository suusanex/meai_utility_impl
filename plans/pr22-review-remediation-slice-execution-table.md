# Slice Execution Table

## Execution mode

- Mode: DELEGATED_IMPLEMENTATION
- Source decomposition: `plans/pr22-review-remediation-slice-decomposition.md`
- Parent implementation authorization: SL-002
- Parent direct production edit allowed: No

## Slice execution table

| Slice ID | Goal | Recommended profile | Blocking dependency | Shared ownership risk | Related XC IDs | Delegation required | Prep agent | Implementation allowed now? | Edit owner | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| SL-001 | 旧構成削除と solution graph 固定 | standard-slice | none | High: solution/project graph affects all slices | XC-PR22-001, XC-PR22-006 | Yes | slice-prep | Yes | `plans/pr22-review-remediation-slice-SL-001-prep.md` | Authorized first implementation slice |
| SL-002 | CI と scoped old-name audit | standard-slice | SL-001, SL-003, and SL-006 completed | Medium: workflow and audit interact with docs/tests | XC-PR22-001, XC-PR22-005, XC-PR22-006 | Yes | slice-prep | Yes | `plans/pr22-review-remediation-slice-SL-002-prep.md` | Include `ci.yml` and `release.yml`; audit allowlist consumes SL-003 migration context |
| SL-003 | README / migration note / sample | standard-slice | SL-004, SL-005, and SL-006 completed | Medium: docs consume runtime API and audit allowlist | XC-PR22-002, XC-PR22-003, XC-PR22-004, XC-PR22-006 | Yes | slice-prep | Yes | `plans/pr22-review-remediation-slice-SL-003-prep.md` | Consume runtime fields and `MCAF_*` opt-in names; do not declare audit PASS |
| SL-004 | Copilot runtime contract remediation | standard-slice | SL-001 completed | High: public API, SDK wrapper, options, permission policy | XC-PR22-002, XC-PR22-003, XC-PR22-005 | Yes | slice-prep | Yes | `plans/pr22-review-remediation-slice-SL-004-prep.md` | Authorized after SL-001; avoid shared Core edits unless unavoidable |
| SL-005 | Codex runtime contract remediation | standard-slice | SL-001 completed | High: public API, JSON-RPC result model, thread state | XC-PR22-002, XC-PR22-004, XC-PR22-005 | Yes | slice-prep | Yes | `plans/pr22-review-remediation-slice-SL-005-prep.md` | Authorized after SL-001; avoid shared Core edits unless unavoidable |
| SL-006 | opt-in integration と production-binding smoke | standard-slice | SL-004 and SL-005 completed | Medium: tests consume production APIs and CI behavior | XC-PR22-003, XC-PR22-004, XC-PR22-005 | Yes | slice-prep | Yes | `plans/pr22-review-remediation-slice-SL-006-prep.md` | No placeholder success; real runtime ManualOnly; env vars `MCAF_GITHUB_COPILOT_INTEGRATION`, `MCAF_CODEX_APP_SERVER_INTEGRATION` |

## Parallel preparation decision

All six slices may run slice-prep in parallel because each prep artifact has a disjoint write owner and implementation remains blocked.
Implementation may start only for slices whose `Implementation allowed now?` is `Yes`.

## Implementation serialization notes

- SL-001 implementation must precede all implementation slices that edit workflows, docs, tests, or runtime API consumers.
- SL-004 and SL-005 may be implemented in parallel only if they keep writes to their runtime-specific source/test trees and avoid shared Core diagnostics / exception model edits. If shared Core edits are required, the affected slice must stop and return a parent decision request.
- SL-003 final docs/sample implementation must consume SL-004 and SL-005 public API outputs.
- SL-006 implementation must consume SL-004 and SL-005 response/client shapes.
- Cross-slice contracts remain `Deferred` until cross-slice verification.

## Human decisions consumed

- Copilot permission handling: default is `ApproveAll`, other choices must be possible, and README must explain why default auto approval is chosen for sub-agent-like usage.
- ProviderOverride: remove the override concept from this library surface. Runtime-specific supported parameters should be exposed as typed interfaces; unsupported parameters remain unsupported. README must state that broader/custom use cases should update this library or use the underlying SDK/server directly.
- Codex diagnostics summary: expose information surfaced by Codex App Server; this library should make the target easier to use, not hide it.
- opt-in integration environment prefix: use the new library abbreviation `MCAF`.
- `.github/workflows/release.yml`: include in development scope because it is the current distribution path.
