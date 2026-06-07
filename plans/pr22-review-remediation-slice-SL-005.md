# Slice Plan: SL-005 Codex runtime contract remediation

## Goal

Codex turn response / streaming update / `CodexRpcSession.ExecuteTurnAsync` result model を修正し、thread id / turn id / status / request / trace / diagnostics / error summary を response まで保持する。

## Non-goals

- Copilot SDK handling。
- README 全面改訂。
- CI old-name audit。
- real Codex app-server E2E。

## Parent requirements covered

FR-008.

## Parent acceptance conditions covered

AC-009, AC-010.

## Affected components / modules

- `src/MultiCodingAgentFacade.CodexAppServer/CodexAppServerTurnResponse.cs`
- `src/MultiCodingAgentFacade.CodexAppServer/CodexAppServerStreamingUpdate.cs`
- `src/MultiCodingAgentFacade.CodexAppServer/CodexAppServerAgentClient.cs`
- `src/MultiCodingAgentFacade.CodexAppServer/CodexRpcSession.cs`
- `src/MultiCodingAgentFacade.CodexAppServer/Threading/*`
- Codex tests

## Expected implementation scope

- `CodexRpcSession` の戻り値を turn result model にする。
- thread/start result と turn/completed / error / status notification の metadata を保持する。
- streaming update に thread / turn / status / request / trace correlation を持たせる。
- client response mapping で diagnostics / error summary を捨てない。
- fake transport tests で field continuity を確認する。

## Internal high-risk boundary candidates

- stdio JSON-RPC notifications と typed response model の field continuity。
- thread store update timing。
- error / retry / cancellation semantics。

## Cross-slice dependencies

SL-003 docs/sample と SL-006 opt-in smoke がこの response model を消費する。

## Related Cross-slice Contract IDs

XC-PR22-002, XC-PR22-004, XC-PR22-005.

## Cross-slice contract excerpt

- XC ID: XC-PR22-004
- This slice role: Producer
- Mechanism: app-server JSON-RPC -> `CodexRpcSession` turn result -> typed response / streaming update
- Required fields / state / identifiers: thread id, turn id, status, trace id, request id, diagnostics summary, error summary, final text, text delta
- Owned by this slice: Codex result model and JSON-RPC field propagation
- Consumed by this slice: existing thread store and transport abstractions
- Deferred / unresolved fields: app-server protocol drift remains opt-in/manual for SL-006
- Authoritative source: `plans/pr22-review-remediation-slice-decomposition.md` の `Cross-slice contracts`

## Implementation-realization risks

Present. Current `CodexAppServerTurnResponse` has only `Text` and `ExecuteTurnAsync` returns `string`.

## Recommended process profile

`standard-slice`

## Immediate next agent

`slice-prep`

## Required inputs for next agent

- `plans/pr22-review-remediation-plan.md`
- `plans/pr22-review-remediation-change-risk-triage.md`
- `plans/pr22-review-remediation-slice-decomposition.md`
- current Codex source and tests
- PR #22 review comment

## Stop condition

Codex production path returns a typed turn result and thread / turn / status metadata is not discarded before public response / streaming update. Real app-server execution can remain ManualOnly / opt-in.
