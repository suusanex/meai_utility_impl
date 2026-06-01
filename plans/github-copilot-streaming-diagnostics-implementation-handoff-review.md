# Implementation Handoff Review

## Verdict

READY_WITH_NOTES

## Blocking issues

None

## Non-blocking notes

- TP-005 の `NeedsHumanDecision` は [plans/github-copilot-streaming-diagnostics-test-design-kernel.md](plans/github-copilot-streaming-diagnostics-test-design-kernel.md) に残っているが、[plans/github-copilot-streaming-diagnostics-implementation-contract-review-kernel.md](plans/github-copilot-streaming-diagnostics-implementation-contract-review-kernel.md) が additive wrapper extension、wrapper-owned final text aggregation、wrapper capability delegation、production/substitute binding map を固定しており、stale note として扱える。Status: Done。
- additive wrapper extension の preferred shape は handoff 開始に十分だが、capability surface と streaming method の具体名、default interface member を使うかどうかは API evolution policy の実装時決定として残る。shape 自体の再判断には戻さない。Status: PartiallyDone。
- RC-001/RC-003 の progress と disconnected の細かな sequence semantics、heartbeat の最終判定条件、opt-in E2E での streaming 観測点追加は downstream verification で詰めればよく、実装着手前の blocker ではない。Status: Deferred。
- README または provider ドキュメント更新は Plan/TP 上で要求済みだが、この handoff では production binding と substitute binding の確認を優先し、文言の具体化は implementation で回収する。Status: Deferred。

## Required handoff inputs

- plans/github-copilot-streaming-diagnostics-plan.md（Plan Kernel — source of truth）
- plans/github-copilot-streaming-diagnostics-change-risk-triage.md
- plans/github-copilot-streaming-diagnostics-runtime-contract-kernel.md
- plans/github-copilot-streaming-diagnostics-test-design-kernel.md
- plans/github-copilot-streaming-diagnostics-implementation-contract-kernel.md
- plans/github-copilot-streaming-diagnostics-implementation-contract-review-kernel.md

## Missing or inconsistent mappings

| Plan item | Runtime Contract ID | Test Point ID | Issue |
| --- | --- | --- | --- |
| FR-2 / AC-3 | RC-002 | TP-005 | [plans/github-copilot-streaming-diagnostics-test-design-kernel.md](plans/github-copilot-streaming-diagnostics-test-design-kernel.md) では additive wrapper extension を `NeedsHumanDecision` として残しているが、[plans/github-copilot-streaming-diagnostics-implementation-contract-review-kernel.md](plans/github-copilot-streaming-diagnostics-implementation-contract-review-kernel.md) の判断を優先すると解消済みとして扱える。artifact 間の stale mapping であり blocker ではない |
| FR-1 / AC-1 / AC-2 | RC-002 | TP-001 / TP-002 | streaming capability の委譲先は implementation contract 系 artifact で wrapper capability surface に固定済みだが、test-design 単体ではまだ surface 名称が表現されていない。shape は十分に固定済みで、命名だけが未記載 |
| FR-8 / AC-9 | RC-001 / RC-003 | TP-003 / TP-009 | timeout/cancellation/disconnected の区別は接続済みだが、progress と disconnected の詳細 sequence semantics は downstream verification へ持ち越しである |
| FR-10 / AC-12 / AC-13 | RC-003 | TP-010 | opt-in E2E と documentation 更新の要求は残っているが、いずれも production binding map の不備ではなく implementation 後の verification/documentation 作業として扱える |

## Recommended implementation prompt additions

- additive wrapper extension を approved preferred shape として固定し、新しい反証が出ない限り event-first breaking fallback を再オープンしないこと。
- `SupportsStreaming` と `IsSupported(Streaming)` は wrapper capability surface にのみ委譲し、hard-coded true と concrete type 判定を禁止すること。
- streaming path では wrapper が正規化した update を使い、raw SDK event 解釈と final text aggregation は wrapper responsibility に固定すること。
- non-streaming path は既存 `SendAsync` を維持し、streaming path だけ新しい surface を使うことで互換性を保つこと。
- `DefaultCopilotSdkWrapper` は capability=false と streaming fail-fast、`StubCopilotSdkWrapper` は non-streaming substitute、`RecordingForwardingCopilotSdkWrapper` は opt-in E2E 拡張時まで non-streaming forward のまま、という binding map を前提にすること。
- secret masking、ProviderOverride 非機密ログ、timeout/cancellation/disconnected の識別、opt-in E2E は shape 再議論ではなく implementation と verification の対象として処理すること。

## Handoff Packet

- Profile used: triage-only (implementation-handoff-review)
- Source artifacts: plans/github-copilot-streaming-diagnostics-plan.md, plans/github-copilot-streaming-diagnostics-change-risk-triage.md, plans/github-copilot-streaming-diagnostics-runtime-contract-kernel.md, plans/github-copilot-streaming-diagnostics-test-design-kernel.md, plans/github-copilot-streaming-diagnostics-implementation-contract-kernel.md, plans/github-copilot-streaming-diagnostics-implementation-contract-review-kernel.md
- Selected contracts / IDs: RC-001, RC-002, RC-003。主な接続対象 TP は TP-001, TP-002, TP-004, TP-005, TP-007, TP-008, TP-010。TP-005 は implementation-contract-review の判断を優先して resolved 扱いとした
- Files inspected: plans/github-copilot-streaming-diagnostics-plan.md, plans/github-copilot-streaming-diagnostics-change-risk-triage.md, plans/github-copilot-streaming-diagnostics-runtime-contract-kernel.md, plans/github-copilot-streaming-diagnostics-test-design-kernel.md, plans/github-copilot-streaming-diagnostics-implementation-contract-kernel.md, plans/github-copilot-streaming-diagnostics-implementation-contract-review-kernel.md
- Files intentionally not inspected: src/** と tests/** は documents-only policy により未読。production/test source file の妥当性確認はこの pass の対象外。既存の implementation-handoff-review artifact は未存在のため新規作成した
- Decisions made: Verdict は READY_WITH_NOTES。Plan の FR/AC は selected contracts と test points へ概ね接続されていると判断した。preferred shape は additive wrapper extension で十分に固定され、wrapper capability delegation も実装着手に必要な粒度へ到達していると判断した。non-streaming 互換、secret masking、ProviderOverride 非機密ログ、timeout/cancellation/disconnected、opt-in E2E、production/substitute binding map は handoff 粒度で可視化されていると判断した
- Do not redo unless new evidence appears: Plan は non-breaking preferred / breaking fallback を source of truth としている。selected contracts は RC-001〜RC-003 で十分に bounded である。wrapper-owned final text aggregation、wrapper capability delegation、Default/Stub/RecordingForwarding/Moq の binding map は implementation-contract 系 artifact で固定済みである。TP-005 の stale note は implementation-contract-review の判断で解消済みと扱ってよい
- Remaining work: additive surface の naming と API evolution policy の細部決定、progress/disconnected の runtime semantics 詰め、stage-based logging の具象 event taxonomy、implementation 後の verification、opt-in E2E 実施条件、README/provider documentation 更新。Status: PartiallyDone
- Recommended next step: implementation agent は Plan を source of truth にしつつ implementation-contract-review を binding clarification として参照し、additive wrapper extension、wrapper capability delegation、stage logging、secret masking、timeout/cancellation/disconnected の識別を実装する。その後 verification-kernel で production binding と opt-in E2E 条件を確認する