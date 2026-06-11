# Slice Plan: SL-006 opt-in integration と production-binding smoke

## Goal

placeholder integration test を廃止し、Copilot SDK / Codex App Server の opt-in smoke test と skip reason を追加し、production runtime path に対する binding gap を検出する。

## Non-goals

- always-on real runtime CI。
- runtime API 実装。
- README 全面改訂。

## Parent requirements covered

FR-005.

## Parent acceptance conditions covered

AC-006, AC-015 の integration / production binding 部分。

## Affected components / modules

- `tests/MultiCodingAgentFacade.IntegrationTests/OptInIntegrationPlaceholderTests.cs`
- integration test project
- production DI / client construction path
- README integration instructions consumed from SL-003

## Expected implementation scope

- placeholder `Assert.True(true)` test を削除する。
- environment/config gated skip を実装する。
- Copilot SDK opt-in smoke test を追加する。
- Codex App Server opt-in smoke test を追加する。
- skip reason を観測可能にする。
- secret-safe output にする。
- opt-in enabled 時は production client construction path を使う。

## Internal high-risk boundary candidates

- fake-only success。
- real runtime missing environment。
- secret leakage。
- test framework skip semantics。

## Cross-slice dependencies

SL-004 Copilot API と SL-005 Codex API を消費する。SL-002 CI はこの project を credentials なしで通す必要がある。SL-003 は opt-in 手順を説明する。

## Related Cross-slice Contract IDs

XC-PR22-003, XC-PR22-004, XC-PR22-005.

## Cross-slice contract excerpt

- XC ID: XC-PR22-005
- This slice role: Consumer/Producer
- Mechanism: opt-in xUnit tests -> production DI/client path -> optional real runtime
- Required fields / state / identifiers: opt-in environment variable names, skip reason, production client type, config section, secret masking, smoke prompt / timeout
- Owned by this slice: integration test gating and production-binding smoke
- Consumed by this slice: public runtime APIs from SL-004 / SL-005
- Deferred / unresolved fields: real runtime execution evidence is ManualOnly unless environment is available
- Authoritative source: `plans/pr22-review-remediation-slice-decomposition.md` の `Cross-slice contracts`

## Implementation-realization risks

Present. Current integration test is placeholder `Assert.True(true)`.

## Recommended process profile

`standard-slice`

## Immediate next agent

`slice-prep`

## Required inputs for next agent

- `plans/pr22-review-remediation-plan.md`
- `plans/pr22-review-remediation-change-risk-triage.md`
- `plans/pr22-review-remediation-slice-decomposition.md`
- SL-004 and SL-005 outputs
- current integration tests
- test framework skip pattern
- README integration instructions

## Stop condition

integration project has no placeholder pass, skips cleanly without runtime credentials, and opt-in smoke tests use production client paths when enabled. Real runtime execution evidence may remain ManualOnly.
