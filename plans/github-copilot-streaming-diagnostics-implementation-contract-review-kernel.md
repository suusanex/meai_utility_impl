# Implementation Contract Review Kernel

## 判定結果

READY_FOR_IMPLEMENTATION

## ブロッキング問題

なし。

ready 判定の理由は次のとおり。

- Plan-required implementation path は additive wrapper extension を preferred shape として明示できている。
- final text aggregation を wrapper 側責務に置く判断は、runtime-contract の RC-001/RC-002 と test-design の TP-001/TP-004/TP-005 の目的に整合している。
- SupportsStreaming / IsSupported(Streaming) を wrapper capability surface へ委譲する方針は、hard-coded true を除去する最小経路として十分に具体化されている。
- DefaultCopilotSdkWrapper / Stub / RecordingForwarding / Moq の binding map は implementation-handoff-review が production binding と substitute responsibility を判定できる粒度まで具体化されている。

## 非ブロッキング注記

- test-design artifact 側の TP-005 は NeedsHumanDecision のままだが、implementation-contract-kernel で preferred shape と substitute binding map が固定されたため、現時点では stale note とみなせる。handoff ではこの review artifact を優先 source として扱う必要がある。
- additive extension の具体的 naming は未固定だが、必要 surface は「wrapper capability surface 1 面」と「streaming method 1 面」と「normalized update DTO 1 型」に収束しており、shape の再探索は不要である。
- default interface member を使うか通常メンバー追加にするかは public API 運用判断として残るが、preferred shape 自体の readiness は阻害しない。
- RecordingForwardingCopilotSdkWrapper の streaming forward は opt-in E2E 拡張時の作業として deferred でよく、現段階の production 実装着手を止める論点ではない。
- RC-001 の progress / disconnected の細かな sequence semantics は runtime / verification 側の残課題であり、wrapper ownership や capability binding の判断を覆す材料ではない。

## 確認したスコープ

- plans/github-copilot-streaming-diagnostics-plan.md
- plans/github-copilot-streaming-diagnostics-change-risk-triage.md
- plans/github-copilot-streaming-diagnostics-runtime-contract-kernel.md
- plans/github-copilot-streaming-diagnostics-test-design-kernel.md
- plans/github-copilot-streaming-diagnostics-implementation-contract-kernel.md

## Plan / implementation contract 適合性レビュー

| Checkpoint | Evidence | Status | Notes |
| --- | --- | --- | --- |
| Preferred shape が additive wrapper extension で固定されているか | implementation-contract-kernel が existing SendAsync 維持、streaming 専用 surface 追加、normalized update DTO 追加を minimum sufficient shape として固定している | Pass | plan の preferred shape と一致し、breaking fallback は conditional fallback に留まっている |
| final text aggregation の責務が wrapper 側に固定されているか | implementation-contract-kernel が raw SDK event 解釈と terminal FinalText 集約を wrapper responsibility と明記している | Pass | runtime-contract の RC-001/RC-002 で wrapper が SDK source に最も近い participant とされており整合する |
| runtime-contract / test-design と責務境界が矛盾していないか | runtime-contract は RC-002 で final aggregation ownership 未固定時のみ implementation-contract-kernel 追加としていた。implementation-contract-kernel がその ownership を固定した | Pass | test-design TP-005 の NeedsHumanDecision は後続 artifact で解消された未更新状態であり、blocking drift ではない |
| SupportsStreaming / IsSupported(Streaming) の binding が十分具体的か | implementation-contract-kernel が wrapper capability surface への単純委譲、concrete type 判定禁止、hard-coded true 廃止を明記している | Pass | capability advertisement boundary を additive shape の一部として扱えている |
| production / substitute binding map が handoff 粒度に達しているか | GitHubCopilotSdkWrapper, DefaultCopilotSdkWrapper, Moq, StubCopilotSdkWrapper, RecordingForwardingCopilotSdkWrapper の intended role と expected behavior が列挙されている | Pass | implementation-handoff-review が production binding requirement を直接チェックできる粒度 |
| dependency / API evidence が不足していないか | runtime-contract で SDK public surface 候補(CreateSessionAsync, On(...), SessionEvent, SendAndWaitAsync) が既存 trace translation 基盤と結び付けられている | PassWithNotes | direct event taxonomy の詳細は未確定だが preferred shape の妥当性を阻害しない |
| unjustified substitution が排除されているか | implementation-contract-kernel が concrete type 判定、空白分割疑似ストリーミング、chat client の raw SDK event 解釈を明示的に reject している | Pass | prohibited substitutions の記録は十分 |
| source-of-truth drift がないか | plan の non-breaking 優先、triage の RC-002 条件付き implementation-contract、runtime-contract の unresolved 条件、implementation-contract の preferred shape が同一方向を向いている | Pass | test-design の stale row はあるが Plan / implementation contract の drift ではない |
| required code changes / verification hooks が具体的か | implementation-contract-kernel が production binding と verification hook をファイル責務ごとに整理している | Pass | handoff 前提として十分 |

## Missing or inconsistent mappings

- test-design の TP-005 は additive shape 未確定前提の記述のままで、implementation-contract-kernel の preferred shape 固定をまだ反映していない。
- capability surface と streaming method の naming は未固定であり、handoff では「shape は固定、命名だけ未定」として扱う必要がある。
- default interface member 採否は implementation shape ではなく API evolution policy の論点として残っているため、implementation prompt 側で再度 shape の可否判断に戻さないよう制約を書く必要がある。
- RecordingForwardingCopilotSdkWrapper は non-streaming forward のみを intended role として残す整理であり、streaming E2E を直ちに要求する mapping にはなっていない。

## Recommended implementation prompt additions

- additive wrapper extension を approved preferred shape として固定し、public API policy 上の拒否が新証拠として出ない限り breaking fallback を再検討しないこと。
- final text aggregation は wrapper ownership とし、GitHubCopilotChatClient は normalized update から ChatResponseUpdate への写像だけを担当すること。
- SupportsStreaming / IsSupported(Streaming) は wrapper capability surface にのみ委譲し、concrete type 判定や hard-coded true を使わないこと。
- DefaultCopilotSdkWrapper は capability=false と streaming method fail-fast、StubCopilotSdkWrapper は non-streaming substitute、RecordingForwardingCopilotSdkWrapper は opt-in E2E 拡張時まで non-streaming forward のまま維持、という binding map を前提にすること。
- test-design の TP-005 はこの review artifact で解消済みの unresolved として扱い、shape 決定を再オープンせず verification hook へ進むこと。

## handoff に必要な入力

- plans/github-copilot-streaming-diagnostics-plan.md
- plans/github-copilot-streaming-diagnostics-change-risk-triage.md
- plans/github-copilot-streaming-diagnostics-runtime-contract-kernel.md
- plans/github-copilot-streaming-diagnostics-test-design-kernel.md
- plans/github-copilot-streaming-diagnostics-implementation-contract-kernel.md
- plans/github-copilot-streaming-diagnostics-implementation-contract-review-kernel.md

## Handoff Packet

- Profile used: contract-kernel
- Source artifacts:
  - plans/github-copilot-streaming-diagnostics-plan.md
  - plans/github-copilot-streaming-diagnostics-change-risk-triage.md
  - plans/github-copilot-streaming-diagnostics-runtime-contract-kernel.md
  - plans/github-copilot-streaming-diagnostics-test-design-kernel.md
  - plans/github-copilot-streaming-diagnostics-implementation-contract-kernel.md
- Selected contracts / IDs:
  - RC-001
  - RC-002
  - RC-003
  - TP-001
  - TP-004
  - TP-005
  - TP-007
  - TP-008
- Files inspected:
  - plans/github-copilot-streaming-diagnostics-plan.md
  - plans/github-copilot-streaming-diagnostics-change-risk-triage.md
  - plans/github-copilot-streaming-diagnostics-runtime-contract-kernel.md
  - plans/github-copilot-streaming-diagnostics-test-design-kernel.md
  - plans/github-copilot-streaming-diagnostics-implementation-contract-kernel.md
- Files intentionally not inspected:
  - src/**
  - tests/**
  - GitHub.Copilot.SDK package internals beyond what the input artifacts already established
- Decisions made:
  - verdict は READY_FOR_IMPLEMENTATION とした
  - additive wrapper extension を implementation-handoff-review 前の approved preferred shape とみなした
  - final text aggregation は wrapper responsibility として十分に固定済みと判定した
  - SupportsStreaming / IsSupported(Streaming) の wrapper capability surface 委譲は readiness を満たすと判定した
  - DefaultCopilotSdkWrapper / Stub / RecordingForwarding / Moq の binding map は handoff 粒度として十分と判定した
  - test-design TP-005 の stale unresolved は non-blocking note として扱うと決めた
- Do not redo unless new evidence appears:
  - plan は non-breaking preferred / breaking fallback の順序を要求している
  - runtime-contract は RC-002 の unresolved 時のみ implementation-contract-kernel を要求している
  - implementation-contract-kernel は preferred shape、wrapper-owned FinalText、capability delegation、binding map を明示した
  - 空白分割疑似ストリーミング、chat client raw event 解釈、concrete type 判定 capability は rejected substitute として封じられている
- Remaining work:
  - implementation-handoff-review で preferred shape を production binding requirement と verification hook に接続する
  - naming と default interface member 採否を public API policy の範囲で確定する
  - progress / disconnected の詳細 semantics を runtime / verification 側で詰める
  - streaming E2E を追加する場合のみ RecordingForwardingCopilotSdkWrapper の forward 拡張要否を判断する
- Recommended next step:
  - implementation-handoff-review で本 review artifact を binding map の primary clarification として採用し、shape 再議論ではなく production wiring / verification readiness の確認へ進む