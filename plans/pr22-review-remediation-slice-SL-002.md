# Slice Plan: SL-002 CI と scoped old-name audit

## Goal

CI を `MultiCodingAgentFacade.sln` 基準へ切り替え、旧 `MeAiUtility` / `MultiProvider` / `IChatClient` / `Microsoft.Extensions.AI` / OpenAI / AzureOpenAI 混入チェックを scoped audit として追加する。

## Non-goals

- runtime API 実装。
- README 本文全面改訂。
- 旧構成削除そのもの。
- real runtime smoke 実行。

## Parent requirements covered

FR-004, FR-013.

## Parent acceptance conditions covered

AC-003, AC-005, AC-015 の audit 部分。

## Affected components / modules

- `.github/workflows/ci.yml`
- audit script or test
- `Directory.Build.props`
- `MultiCodingAgentFacade.sln`
- integration test project inclusion

## Expected implementation scope

- restore / build / test command を `MultiCodingAgentFacade.sln` へ切り替える。
- old-name / old-dependency denylist を追加する。
- migration note、planning artifacts、historical context の allowlist を明示する。
- opt-in integration tests が credentials なしで CI を壊さないことを前提に test target へ含める。

## Internal high-risk boundary candidates

- blanket grep による migration note false positive。
- allowlist 過剰による source / workflow stale reference の false negative。

## Cross-slice dependencies

SL-001 の new solution graph を消費する。SL-003 の migration note path と allowlist を調整する。SL-006 の opt-in integration project を CI 対象に含める。

## Related Cross-slice Contract IDs

XC-PR22-001, XC-PR22-005, XC-PR22-006.

## Cross-slice contract excerpt

- XC ID: XC-PR22-006
- This slice role: Producer/Consumer
- Mechanism: repository scan / CI audit
- Required fields / state / identifiers: denylist terms, scanned paths, allowlisted migration/history/plans paths, workflow command target, test project list
- Owned by this slice: audit rule and CI command
- Consumed by this slice: deleted old paths from SL-001, migration note location from SL-003, opt-in test project from SL-006
- Deferred / unresolved fields: final old-name PASS belongs to cross-slice verification
- Authoritative source: `plans/pr22-review-remediation-slice-decomposition.md` の `Cross-slice contracts`

## Implementation-realization risks

Present. Audit は簡単に見えて、合法的な migration 説明と stale public API を混同しやすい。

## Recommended process profile

`standard-slice`

## Immediate next agent

`slice-prep`

## Required inputs for next agent

- `plans/pr22-review-remediation-plan.md`
- `plans/pr22-review-remediation-change-risk-triage.md`
- `plans/pr22-review-remediation-slice-decomposition.md`
- SL-001 result
- `.github/workflows/ci.yml`
- README / migration note target decision
- integration test project shape

## Stop condition

CI targets the new solution and scoped audit can distinguish stale public old API from legitimate migration / historical references. Final PASS remains deferred to cross-slice verification.
