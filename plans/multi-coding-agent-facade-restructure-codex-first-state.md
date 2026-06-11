# Codex-first State: multi-coding-agent-facade-restructure

## 現在の段階

- Skill: `token-aware-full-coverage-3layer`
- Parent profile: full-coverage 3-layer orchestration
- Current phase: Layer 2 SL-004 ready for slice-prep / implementation planning
- Implementation status: SL-001 ImplementedPartialVerified; SL-002 ImplementedVerified; SL-003 ImplementedVerified
- Next gate: SL-004 slice-prep -> parent review update

## Source artifacts

- `plans/multi-coding-agent-facade-restructure-plan.md`
- `plans/multi-coding-agent-facade-restructure-change-risk-triage.md`
- `plans/multi-coding-agent-facade-restructure-slice-decomposition.md`
- `plans/multi-coding-agent-facade-restructure-slice-SL-001.md`
- `plans/multi-coding-agent-facade-restructure-slice-SL-002.md`
- `plans/multi-coding-agent-facade-restructure-slice-SL-003.md`
- `plans/multi-coding-agent-facade-restructure-slice-SL-004.md`
- `plans/multi-coding-agent-facade-restructure-slice-SL-005.md`
- `plans/multi-coding-agent-facade-restructure-slice-SL-001-prep.md`
- `plans/multi-coding-agent-facade-restructure-parent-review-gate.md`
- `plans/multi-coding-agent-facade-restructure-slice-SL-001-implementation-handoff-review.md`
- `plans/multi-coding-agent-facade-restructure-slice-SL-001-implementation-result.md`
- `plans/multi-coding-agent-facade-restructure-slice-SL-001-verification-kernel.md`
- `plans/multi-coding-agent-facade-restructure-slice-SL-002-prep.md`
- `plans/multi-coding-agent-facade-restructure-slice-SL-002-implementation-handoff-review.md`
- `plans/multi-coding-agent-facade-restructure-slice-SL-002-implementation-result.md`
- `plans/multi-coding-agent-facade-restructure-slice-SL-002-verification-kernel.md`
- `plans/multi-coding-agent-facade-restructure-slice-SL-003-prep.md`
- `plans/multi-coding-agent-facade-restructure-slice-SL-003-implementation-handoff-review.md`
- `plans/multi-coding-agent-facade-restructure-slice-SL-003-implementation-result.md`
- `plans/multi-coding-agent-facade-restructure-slice-SL-003-verification-kernel.md`
- `AGENTS.md`

## Slice 実行表

| Slice ID | Goal | Recommended profile | Blocking dependency | Shared ownership risk | Related XC IDs | Prep agent | Implementation allowed now? | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| SL-001 | 新 solution/project/Core foundation と旧 provider switching 削除の土台を作る | standard-slice | none | High: solution/project graph, Core semantics, deletion boundary | XC-001, XC-002, XC-003, XC-004 | slice-prep | Completed | 新 project graph と Core foundation は実装・検証済み。 |
| SL-002 | Codex typed client/request/response/streaming を existing JSON-RPC 実装へ接続する | standard-slice | SL-001 | High: Codex production JSON-RPC path and config binding | XC-001, XC-002, XC-005 | slice-prep | Completed | Codex typed runtime は実装・検証済み。 |
| SL-003 | Copilot typed client/request/response/streaming/model list を existing SDK wrapper へ接続する | standard-slice | SL-001 | High: Copilot SDK wrapper/session config and public API | XC-001, XC-003, XC-005 | slice-prep | Completed | Copilot typed runtime は実装・検証済み。 |
| SL-004 | 新 API tests、旧依存混入チェック、opt-in integration 整理を行う | standard-slice | SL-001, SL-002, SL-003 | High: test substitute to production binding | XC-004, XC-005 | slice-prep | ReadyForPrep | Codex/Copilot public API が実装済みになったため次の対象。 |
| SL-005 | README/migration/samples/CI/release を新目的に更新する | standard-slice | SL-001, SL-002, SL-003 | Medium: docs/workflow stale reference risk | XC-004 | slice-prep | ReadyForPrep | runtime public API は確定。SL-004 と順序調整が必要。 |
| SL-006 | cross-slice verification と residual decision を行う | contract-kernel | SL-001..SL-005 | High: parent AC / XC completeness | XC-001..XC-005 | cross-slice-verification-kernel | No | 実装 slice ではなく final gate。 |

## Cross-slice contract ledger

| XC ID | Producer | Consumer | Status | Notes |
| --- | --- | --- | --- | --- |
| XC-001 | SL-001 | SL-002, SL-003, SL-004, SL-005 | ProducerReady | Core exception names、`RuntimeName`、trace/request id、logging helpers を保持する。 |
| XC-002 | SL-002 | SL-004, SL-006 | ProducerReady | Codex typed request fields から JSON-RPC params / thread store までの連続性。 |
| XC-003 | SL-003 | SL-004, SL-006 | ProducerReady | Copilot typed request fields から SDK wrapper/session config までの連続性。 |
| XC-004 | SL-001, SL-002, SL-003 | SL-005, SL-006 | ProducerPartial | solution/project/runtime public API names は提供済み。docs/workflows/release zip は SL-005。 |
| XC-005 | SL-002, SL-003 | SL-004, SL-006 | ProducerReady | Codex/Copilot とも fake substitute と production DI binding evidence を実装済み。 |

## Parent orchestration decision

- SL-001 は Core/project/public naming の producer として実装・検証済み。
- SL-002 は Codex public API / JSON-RPC mapping の producer として実装・検証済み。
- SL-003 は Copilot public API / SDK wrapper mapping の producer として実装・検証済み。
- SL-004 は Codex/Copilot production API 確定後の test migration / repository-wide gates として次に扱える。
- SL-005 は runtime public API names が確定したため prep 可能。ただし SL-004 の denylist 方針と整合させる。
- `plan-slice-decomposition` から直接実装しない。

## Unresolved items

- SL-004 slice-prep result 未作成。
- SL-005 slice-prep result 未作成。
- SL-006 は dependency waiting。
- SL-001 residual: old source directories remain as migration source.
- SL-001 residual: `GitHub.Copilot.SDK` transitively brings `Microsoft.Extensions.AI.Abstractions` and `Nerdbank.MessagePack` warnings.
- SL-002 residual: real Codex CLI / app-server E2E は bounded scope 外。
- SL-003 residual: real authenticated GitHub Copilot E2E は bounded scope 外。
