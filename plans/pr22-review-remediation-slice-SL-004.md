# Slice Plan: SL-004 Copilot runtime contract remediation

## Goal

Copilot response / streaming update / model capability / provider override validation / permission handling / AdvancedOptions policy を review 要件に合わせる。

## Non-goals

- Codex JSON-RPC result model。
- README 全面改訂。
- CI old-name audit。
- real Copilot authenticated E2E。

## Parent requirements covered

FR-007, FR-009, FR-010, FR-011, FR-012.

## Parent acceptance conditions covered

AC-008, AC-011, AC-012, AC-013, AC-014.

## Affected components / modules

- `src/MultiCodingAgentFacade.GitHubCopilot/GitHubCopilotAgentResponse.cs`
- `src/MultiCodingAgentFacade.GitHubCopilot/GitHubCopilotStreamingUpdate.cs`
- `src/MultiCodingAgentFacade.GitHubCopilot/GitHubCopilotAgentRequest.cs`
- `src/MultiCodingAgentFacade.GitHubCopilot/Abstractions/ICopilotSdkWrapper.cs`
- `src/MultiCodingAgentFacade.GitHubCopilot/GitHubCopilotSdkWrapper.cs`
- `src/MultiCodingAgentFacade.GitHubCopilot/Options/ProviderOverrideOptions.cs`
- `src/MultiCodingAgentFacade.GitHubCopilot/Options/GitHubCopilotOptions.cs`
- Copilot tests

## Expected implementation scope

- response metadata fields を追加する。
- streaming update に request / trace correlation を持たせる。
- SDK metadata extension point と finish status / diagnostics summary を設計する。
- `CopilotModelInfo` に supported/default reasoning effort values を持たせる。
- provider override fail-fast validation を追加する。
- permission handling を option / default / logging / docs と接続できる shape にする。
- AdvancedOptions は typed-first policy に沿って整理する。

## Internal high-risk boundary candidates

- external SDK model list / session config / event stream。
- SDK permission callback。
- provider override secret-safe diagnostics。

## Cross-slice dependencies

SL-003 docs/sample と SL-006 opt-in smoke がこの API / policy を消費する。

## Related Cross-slice Contract IDs

XC-PR22-002, XC-PR22-003, XC-PR22-005.

## Cross-slice contract excerpt

- XC ID: XC-PR22-003
- This slice role: Producer
- Mechanism: typed API -> `CopilotSessionConfig` -> SDK session / event stream
- Required fields / state / identifiers: `TraceId`, `RequestId`, runtime name, elapsed time, finish status, diagnostics summary, SDK metadata, model id, supported/default reasoning efforts, provider override fields, permission approval policy
- Owned by this slice: Copilot runtime result and SDK mapping
- Consumed by this slice: Core exception / diagnostics semantics from existing foundation
- Deferred / unresolved fields: real SDK behavior remains opt-in/manual for SL-006
- Authoritative source: `plans/pr22-review-remediation-slice-decomposition.md` の `Cross-slice contracts`

## Implementation-realization risks

Present. Current response model is thin, `CopilotModelInfo` is bool-only, provider override validation is delayed, and permission handling is fixed `ApproveAll`.

## Recommended process profile

`standard-slice`

## Immediate next agent

`slice-prep`

## Required inputs for next agent

- `plans/pr22-review-remediation-plan.md`
- `plans/pr22-review-remediation-change-risk-triage.md`
- `plans/pr22-review-remediation-slice-decomposition.md`
- current Copilot source and tests
- PR #22 review comment

## Stop condition

Copilot public API can carry required metadata and fail-fast / permission policy is represented in production code and slice-local tests. Real SDK execution can remain ManualOnly / opt-in.
