# Implementation Handoff Review

## Verdict

READY_WITH_NOTES

## Blocking issues

None

## Non-blocking notes

- **Acceptance conditions coverage is partial**: `plans/codex-app-server-plan.md` の Functional requirements 2（stdio JSON-RPC transport）と 3（per-call 設定の受け渡し）は、RC/TP では具体化されているが、Plan の `Acceptance conditions` では独立した observable behavior として明示されていない。Status: PartiallyDone
- **Plan draft inconsistency remains documented upstream**: `plans/codex-app-server-runtime-contract-kernel.md` に、`CodexAppServerChatClient` の旧コンストラクタ記述と factory-based DI 方針の不整合が note として残っている。RC/TP 側では実装方針は十分追跡可能で blocking ではないが、実装 prompt では factory-based 方針を明示した方が安全。Status: NotImplementedOrMismatch
- **Manual-only / deferred verification remains after implementation**: 実 `codex app-server` 起動経路と `item/permissions/requestApproval` の最終確認は post-implementation / verification-kernel 側の扱いであり、この handoff pass では blocking ではない。Status: Deferred

## Required handoff inputs

- plans/codex-app-server-plan.md（Plan Kernel — source of truth）
- plans/codex-app-server-provider-change-risk-triage.md
- plans/codex-app-server-runtime-contract-kernel.md
- plans/codex-app-server-test-design-kernel.md

## Missing or inconsistent mappings

| Plan item | Runtime Contract ID | Test Point ID | Issue |
| --- | --- | --- | --- |
| Functional requirement 2: stdio JSON-RPC transport | RC-001, RC-002 | TP-001-a〜e, TP-002-a〜f | RC/TP では十分具体化されているが、Plan `Acceptance conditions` では transport 自体の observable acceptance が独立明記されていない |
| Functional requirement 3: per-call settings | RC-001-c, RC-001-d, RC-001-e | TP-001-c, TP-001-d, TP-001-e | RC/TP では model / effort / sandboxPolicy が追跡可能だが、Plan `Acceptance conditions` に per-call 設定観測条件が明示されていない |

## Recommended implementation prompt additions

- Plan を source of truth としつつ、`CodexAppServerChatClient` は **Singleton + `ICodexTransportFactory` 注入** 方針で統一すること
- 実装後は verification-kernel で manual-only 項目（実 `codex app-server` 起動経路、approval variants）を確認する前提で進めること

## Handoff Packet

- Profile used: triage-only (implementation-handoff-review)
- Source artifacts:
  - `plans/codex-app-server-plan.md`
  - `plans/codex-app-server-provider-change-risk-triage.md`
  - `plans/codex-app-server-runtime-contract-kernel.md`
  - `plans/codex-app-server-test-design-kernel.md`
- Selected contracts / IDs:
  - Runtime Contracts: RC-001-a〜e, RC-002-a〜f, RC-003-a〜d
  - Test Points: TP-001-a〜e, TP-002-a〜f, TP-003-a-1〜TP-003-d
- Files inspected:
  - `plans/codex-app-server-plan.md`
  - `plans/codex-app-server-provider-change-risk-triage.md`
  - `plans/codex-app-server-runtime-contract-kernel.md`
  - `plans/codex-app-server-test-design-kernel.md`
- Files intentionally not inspected:
  - production source files under `src/**` — documents-only policy
  - test source files under `tests/**` — documents-only policy
  - existing implementation-handoff review artifact — file not present
- Decisions made:
  - Verdict: `READY_WITH_NOTES`
  - Check 2/3/4/5/6 は blocking なし
  - Plan → selected RC → TP → production binding requirement の接続は成立
  - Notes は主に Plan acceptance coverage の薄さと上流文書の軽微な不整合
- Do not redo unless new evidence appears:
  - RC-001 / RC-002 / RC-003 の selected scope は triage と runtime-contract-kernel で一貫
  - test-design-kernel は selected RC 全件に TP を割り当て済み
  - fake/stub 使用 TP では `Production binding required? = Yes` が維持されている
- Remaining work:
  - Acceptance conditions の明示不足は残るが implementation 開始は可能。Status: PartiallyDone
  - 実 `codex app-server` 起動経路、`item/permissions/requestApproval` variant、streaming cleanup は downstream verification / deferred scope。Status: Deferred
- Recommended next step:
  - implementation agent に上記 4 artifacts を渡して実装開始
  - 実装後に verification-kernel で production binding / wiring / manual-only checks を確認
