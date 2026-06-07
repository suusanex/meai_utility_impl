# Parent Review Gate

## Verdict per slice

| Slice ID | Verdict | Can implement now? | Parallel group | Blocking reason |
| --- | --- | --- | --- | --- |
| SL-001 | READY_FOR_IMPLEMENTATION | Yes | impl-g1 | Authorized first. Must produce deleted path list and authoritative `MultiCodingAgentFacade.sln(x)` graph. |
| SL-002 | READY_FOR_IMPLEMENTATION | Yes | impl-g4 | SL-001 deletion output, SL-003 migration note, and SL-006 integration skip policy are available. `release.yml` is explicitly in scope. |
| SL-003 | READY_FOR_IMPLEMENTATION | Yes | impl-g5 | SL-004/SL-005 public API outputs and SL-006 opt-in env names / skip policy are available for docs/sample consumption. |
| SL-004 | READY_FOR_IMPLEMENTATION | Yes | impl-g2 | Authorized after SL-001. Keep writes runtime-specific; do not edit shared Core unless returning a blocker. |
| SL-005 | READY_FOR_IMPLEMENTATION | Yes | impl-g3 | Authorized after SL-001. Keep writes runtime-specific; do not edit shared Core unless returning a blocker. |
| SL-006 | READY_FOR_IMPLEMENTATION | Yes | impl-g6 | SL-004/SL-005 runtime API outputs are available; use `MCAF_GITHUB_COPILOT_INTEGRATION` and `MCAF_CODEX_APP_SERVER_INTEGRATION` as opt-in environment variables. |

## Cross-slice contract review

| XC ID | Producer | Consumer | Status | Notes |
| --- | --- | --- | --- | --- |
| XC-PR22-001 | SL-001 | SL-002, SL-003 | Deferred | SL-001 prep defines solution graph and deleted path output. Final verification must confirm old solution/project removal and new solution graph usage. |
| XC-PR22-002 | SL-004, SL-005 | SL-003 | Deferred | Docs/sample must consume runtime public API. SL-003 must not invent public fields before SL-004/SL-005 settle them. |
| XC-PR22-003 | SL-004 | SL-003, SL-006 | Producer ready / consumer deferred | Copilot metadata, model capability, provider override removal, permission policy, and AdvancedOptions policy are produced by SL-004 and must be consumed by docs / opt-in smoke. |
| XC-PR22-004 | SL-005 | SL-003, SL-006 | Producer ready / consumer deferred | Codex thread/turn/status/correlation/diagnostics fields are produced by SL-005 and must be consumed by docs / opt-in smoke without fabricated IDs. |
| XC-PR22-005 | SL-004, SL-005, SL-006 | SL-002, final verification | Producer ready / consumer deferred | SL-006 removed placeholder success, records skip reasons, and verifies production DI binding. SL-002 / final verification still consume the result. |
| XC-PR22-006 | SL-001, SL-003 | SL-002, final verification | Deferred | Old-name audit must distinguish stale public source/workflow from migration/history/planning references. |

## Field continuity review

| Field / state / identifier | Required by | Source / producer | Consumer | Status | Notes |
| --- | --- | --- | --- | --- | --- |
| new solution graph and project path list | SL-002, SL-003, final verification | SL-001 | CI, docs, audit | Deferred | Must be produced after deletion; current prep must not treat existing mixed tree as final. |
| deleted old project path list | SL-002, final verification | SL-001 implementation result | audit and final verification | Deferred | Must be explicit, not reconstructed from README prose. |
| Copilot `TraceId` / `RequestId` / runtime name | SL-003, SL-006 | SL-004 | docs/sample, opt-in smoke | Producer ready | SL-004 exposes these on response/update models; consumers must avoid invented values. |
| Copilot elapsed / finish / diagnostics / SDK metadata | SL-003, SL-006 | SL-004 | docs/sample, opt-in smoke | Producer ready | SL-004 exposes nullable/source-backed metadata; consumers must preserve nullable policy. |
| Copilot supported/default reasoning effort values | SL-003, SL-006 | SL-004 | docs/sample, model tests | Producer ready | SL-004 exposes `SupportedReasoningEfforts`, `DefaultReasoningEffort`, and compatibility bool. |
| Copilot permission policy | SL-003, SL-006 | SL-004 | README/sample, opt-in smoke | Done | Default is `ApproveAll`; other choices must be possible; README must explain sub-agent-oriented default. |
| provider override validation outcome | SL-004, SL-006 | SL-004 | tests, docs, opt-in smoke | Done | `ProviderOverride` concept is removed; expose supported runtime-specific typed parameters only. |
| Codex thread id / turn id / status | SL-003, SL-006 | SL-005 | docs/sample, opt-in smoke | Producer ready | SL-005 exposes typed turn result / response/update fields from app-server flow, not fabricated fallback. |
| Codex request id / trace id | SL-003, SL-006 | SL-005 | response/update/docs/tests | Producer ready | SL-005 separates facade `RequestId`, telemetry `TraceId`, and JSON-RPC envelope id. |
| Codex diagnostics / error summary | SL-003, SL-006 | SL-005 | response/update/docs/tests | Done | Expose information surfaced by Codex App Server; this library is not an intentional hiding layer. |
| opt-in env var names and skip reason | SL-002, SL-003, SL-006 | SL-006 | CI, README, integration tests | Producer ready | Use `MCAF_GITHUB_COPILOT_INTEGRATION` and `MCAF_CODEX_APP_SERVER_INTEGRATION`; disabled path is skipped with reason. |
| old-name allowlist path | SL-002, final verification | SL-003 migration note + plans/specs history | audit | Producer ready / consumer deferred | SL-003 result provides migration-context old terms; SL-002 must encode scoped audit and leave final PASS to parent verification. |

## Implementation authorization

- Authorized slices: SL-002
- Serialized slices:
  - Implementation order: SL-001 completed -> SL-004 and SL-005 completed -> SL-006 completed -> SL-003 completed -> SL-002 finalization -> cross-slice verification.
  - SL-002 implementation is now authorized. It must include `.github/workflows/release.yml` because this is the current distribution path.
  - SL-003 final docs/sample implementation is now authorized because SL-004 and SL-005 public API outputs are available.
- Blocked slices:
  - SL-002 waits for SL-001 and later SL-003 / SL-006 audit inputs.
  - SL-003 waits for SL-004 / SL-005 runtime public API outputs.
  - none for current pass.
- Human decision required:
  - None blocking implementation start.
  - Remaining low-impact design choices are delegated to implementation agents with the instruction to choose the caller-convenient option.

## Parent instructions for slice-impl

SL-002 is authorized for slice-impl.

For SL-004 slice-impl:

1. implement Copilot response / streaming / model capability / permission handling per the consumed human decision,
2. remove `ProviderOverride` as a public concept and expose only runtime-specific typed supported parameters,
3. keep writes under `src/MultiCodingAgentFacade.GitHubCopilot` and `tests/MultiCodingAgentFacade.GitHubCopilot.Tests` unless a blocker is returned,
4. produce `plans/pr22-review-remediation-slice-SL-004-implementation-result.md`,
5. keep XC-PR22-002 / XC-PR22-003 / XC-PR22-005 Deferred.

For SL-005 slice-impl:

1. implement Codex response / streaming metadata propagation and `CodexRpcSession` typed turn result,
2. expose Codex App Server surfaced diagnostics rather than hiding them,
3. choose caller-convenient failed-turn / request-id modeling and document it in the result,
4. keep writes under `src/MultiCodingAgentFacade.CodexAppServer` and `tests/MultiCodingAgentFacade.CodexAppServer.Tests` unless a blocker is returned,
5. produce `plans/pr22-review-remediation-slice-SL-005-implementation-result.md`,
6. keep XC-PR22-002 / XC-PR22-004 / XC-PR22-005 Deferred.

For SL-002 slice-impl:

1. update `.github/workflows/ci.yml` to restore/build/test `MultiCodingAgentFacade.sln` and include the integration project through the solution,
2. update `.github/workflows/release.yml` to restore/build `MultiCodingAgentFacade.sln` and package the new `MultiCodingAgentFacade.*` library artifacts for net8.0 / net10.0,
3. add a scoped old-name / old-dependency audit mechanism if needed, excluding generated `bin/obj` and allowing only migration/history/planning contexts with explicit reasons,
4. ensure stale active public guidance / workflow references to `MeAiUtility.sln`, `MeAiUtility.MultiProvider`, `AddMultiProviderChat`, `ProviderOverride`, `IChatClient`, OpenAI/Azure provider switching are not allowed outside scoped contexts,
5. consume SL-003 migration note and SL-006 opt-in skip behavior; do not declare global final old-name PASS beyond slice-local verification,
6. keep writes under `.github/workflows/**`, any audit script/test files needed for SL-002, and `plans/pr22-review-remediation-slice-SL-002-implementation-result.md` unless a blocker is returned,
7. keep XC-PR22-001 / XC-PR22-005 / XC-PR22-006 Deferred until parent cross-slice verification.

For later slice-impl:

1. pass each slice-impl the parent Plan, parent triage, decomposition, assigned slice artifact, assigned prep artifact, this parent review gate, and the slice execution table,
2. keep cross-slice contracts Deferred until `cross-slice-verification-kernel`,
3. forbid production code/test edits by the parent agent unless `PARENT_DIRECT_IMPLEMENTATION_EXCEPTION` is explicitly approved.

## Parent review summary

All executable slices have valid slice-prep artifacts.
SL-001, SL-003, SL-004, SL-005, and SL-006 have completed. Human decisions supplied after PREP_ONLY have been consumed, and SL-002 is authorized as the final delegated implementation slice.
