# Code Review Focus Kernel

## スコープ

- requested change: GitHub Copilot provider の streaming/capability/diagnostics 強化実装差分の human review map 作成
- selected contract IDs: RC-001, RC-002, RC-003
- selected test point IDs: TP-001, TP-002, TP-004, TP-005, TP-007, TP-008, TP-010
- selected gap IDs: none
- diff source: working tree diff
- base/head: unknown (local working tree)
- 対象変更ファイル: 
  - src/MeAiUtility.MultiProvider.GitHubCopilot/Abstractions/ICopilotSdkWrapper.cs
  - src/MeAiUtility.MultiProvider.GitHubCopilot/GitHubCopilotChatClient.cs
  - src/MeAiUtility.MultiProvider.GitHubCopilot/GitHubCopilotSdkWrapper.cs
  - src/MeAiUtility.MultiProvider.GitHubCopilot/Options/GitHubCopilotProviderOptions.cs
  - src/MeAiUtility.MultiProvider.GitHubCopilot/GitHubCopilotCliSdkWrapper.cs
  - src/MeAiUtility.MultiProvider.GitHubCopilot/Configuration/GitHubCopilotServiceExtensions.cs
  - src/MeAiUtility.MultiProvider.GitHubCopilot/CopilotClientHost.cs
  - tests/MeAiUtility.MultiProvider.GitHubCopilot.Tests/GitHubCopilotChatClientTests.cs
  - tests/MeAiUtility.MultiProvider.GitHubCopilot.Tests/GitHubCopilotSdkWrapperTests.cs
  - tests/MeAiUtility.MultiProvider.GitHubCopilot.Tests/ConfigurationTests/GitHubCopilotServiceExtensionsTests.cs
  - tests/MeAiUtility.MultiProvider.IntegrationTests/Samples/InteractiveChatSampleTests.cs
  - README.md

## 判定結果

`FOCUSED_REVIEW_OK`

P0/P1 target は限定できており、human reviewer は streaming error path と capability/binding の境界を先に読むことで効率的にレビュー可能。artifact chain との対応も追跡可能である。一方、manual-only 検証領域と test false confidence が残るため、focused review 後に verification 側で補完が必要。

## Review focus summary

1. P0: streaming path の ErrorPath (exception wrap / timeout / disconnected が期待どおり分類・観測されるか)
2. P0: wrapper capability と streaming 実行経路の整合 (advertisement が実装実態に一致しているか)
3. P1: substitute 実装での SupportsStreaming と SendStreamingAsync 実体の不整合リスク
4. P1: diagnostics ログの security/auth 境界 (token 本文は出さず、presence 情報のみか)
5. P1: README の API surface 記述が新契約に追随しているか

## Changed Surface Map

| Changed file / symbol | Change summary | Related Plan item | Related RC / TP / IC / Gap item | Risk axis | Confidence | Review priority |
| --- | --- | --- | --- | --- | --- | --- |
| src/MeAiUtility.MultiProvider.GitHubCopilot/Abstractions/ICopilotSdkWrapper.cs / SupportsStreaming, SendStreamingAsync, CopilotStreamingUpdate* | wrapper additive extension (capability + streaming update DTO) を追加 | FR-2, FR-1 | RC-002, TP-005, TP-002, IC preferred shape | PublicApi, DependencyBoundary | High | P0 |
| src/MeAiUtility.MultiProvider.GitHubCopilot/GitHubCopilotChatClient.cs / SupportsStreaming, IsSupported, GetStreamingResponseAsync | hard-coded streaming capability を削除し wrapper 委譲へ移行。疑似ストリーミングを廃止し wrapper update 消費へ変更 | FR-1, FR-3, FR-5 | RC-001, RC-002, RC-003, TP-001, TP-002, TP-007 | ErrorPath, ProductionBinding, StateTransition | High | P0 |
| src/MeAiUtility.MultiProvider.GitHubCopilot/GitHubCopilotChatClient.cs / LogProviderOverride | ProviderOverride の非機密属性 + secret presence をログ出力 | FR-7 | RC-003, TP-008 | SecurityAuth | Medium | P1 |
| src/MeAiUtility.MultiProvider.GitHubCopilot/GitHubCopilotSdkWrapper.cs / SupportsStreaming, SendStreamingAsync | streaming 可否を sendStreamingCore 有無に接続。streaming core delegate 経由で update を返却 | FR-2, FR-3 | RC-001, RC-002, TP-001, TP-005 | DependencyBoundary, ProductionBinding | High | P0 |
| src/MeAiUtility.MultiProvider.GitHubCopilot/GitHubCopilotSdkWrapper.cs / WaitWithHeartbeatAsync, LogRequestStart | timeout/cancel/disconnect/heartbeat のログ分岐を強化 | FR-5, FR-8 | RC-003, TP-007, TP-009 | ErrorPath, PerformanceResource | Medium | P1 |
| src/MeAiUtility.MultiProvider.GitHubCopilot/Options/GitHubCopilotProviderOptions.cs | diagnostic content preview の opt-in 設定追加 | FR-7 | RC-003, TP-008 | SecurityAuth, PublicApi | High | P1 |
| src/MeAiUtility.MultiProvider.GitHubCopilot/GitHubCopilotCliSdkWrapper.cs | 互換 wrapper が SupportsStreaming/SendStreamingAsync を委譲 | FR-9 | RC-002, TP-005 | DependencyBoundary, SubstitutionRisk | High | P1 |
| src/MeAiUtility.MultiProvider.GitHubCopilot/Configuration/GitHubCopilotServiceExtensions.cs / DefaultCopilotSdkWrapper | provider-only default wrapper に streaming fail-fast 追加 | FR-9, FR-10 | RC-002, TP-005, TP-006 | ProductionBinding, DependencyBoundary | High | P1 |
| src/MeAiUtility.MultiProvider.GitHubCopilot/CopilotClientHost.cs | model list start/completed ログ追加 | FR-5 | RC-003, TP-007 | Other | High | SKIM |
| tests/MeAiUtility.MultiProvider.GitHubCopilot.Tests/GitHubCopilotChatClientTests.cs | wrapper capability 追従、streaming throws、delta 返却テスト追加 | FR-10 | TP-001, TP-002, TP-004 | TestFalseConfidence | High | P1 |
| tests/MeAiUtility.MultiProvider.GitHubCopilot.Tests/GitHubCopilotSdkWrapperTests.cs | SupportsStreaming 判定と sendStreamingCore 経路のテスト追加 | FR-10 | TP-005, TP-007, TP-008 | TestFalseConfidence, SubstitutionRisk | High | P1 |
| tests/MeAiUtility.MultiProvider.GitHubCopilot.Tests/ConfigurationTests/GitHubCopilotServiceExtensionsTests.cs | default wrapper streaming fail-fast の DI テスト追加 | FR-9, FR-10 | TP-005, TP-006 | ProductionBinding | High | P1 |
| tests/MeAiUtility.MultiProvider.IntegrationTests/Samples/InteractiveChatSampleTests.cs | sample 用 wrapper に SupportsStreaming=true を追加 | FR-1, FR-10 | TP-006 | SubstitutionRisk, TestFalseConfidence | Medium | P1 |
| README.md | diagnostic preview 設定などを追記 | FR-10 | TP-010 | PublicApi | Medium | P1 |

## Critical review targets

| Priority | File / Symbol | Review axis | Why human should read it | Source artifact / evidence | Suggested review depth |
| --- | --- | --- | --- | --- | --- |
| P0 | src/MeAiUtility.MultiProvider.GitHubCopilot/GitHubCopilotChatClient.cs / GetStreamingResponseAsync | ErrorPath | streaming path は finally のみで、non-streaming で行っている timeout/cancellation/runtime-exception の分類・wrap との差異がある。契約上の failure semantics とログ観測点が一致するか確認が必要 | RC-003, TP-007, TP-009, implementation-execution IMPL-003 | Read full method |
| P0 | src/MeAiUtility.MultiProvider.GitHubCopilot/GitHubCopilotSdkWrapper.cs / SupportsStreaming + SendStreamingAsync | ProductionBinding | SupportsStreaming は sendStreamingCore 注入依存。production 実装で capability advertisement が意図どおりか、test-only path になっていないか確認が必要 | RC-001, RC-002, TP-001, TP-002, TP-005, IMPL-004 | Read signature and call sites |
| P0 | src/MeAiUtility.MultiProvider.GitHubCopilot/Abstractions/ICopilotSdkWrapper.cs | PublicApi | interface に default 実装が入り、SupportsStreaming と SendStreamingAsync の契約一貫性が実装側に委ねられる。実装漏れ時に runtime mismatch になり得る | RC-002, TP-005, implementation-contract-kernel | Read signature and call sites |
| P1 | src/MeAiUtility.MultiProvider.GitHubCopilot/Configuration/GitHubCopilotServiceExtensions.cs / DefaultCopilotSdkWrapper | ProductionBinding | provider-only 構成の fail-fast が streaming でも維持されるか、メッセージ・例外型の一貫性を確認すべき | RC-002, TP-005, TP-006 | Read changed branches |
| P1 | tests/MeAiUtility.MultiProvider.IntegrationTests/Samples/InteractiveChatSampleTests.cs / CapturingCopilotSdkWrapper | TestFalseConfidence | SupportsStreaming=true だが SendStreamingAsync 未実装。将来 sample が streaming 呼び出しに拡張されると capability と実体の乖離を引き起こす可能性 | RC-002, TP-006, implementation diff | Compare fake and production paths |
| P1 | src/MeAiUtility.MultiProvider.GitHubCopilot/GitHubCopilotChatClient.cs / LogProviderOverride | SecurityAuth | token 本文は出していないが HasApiKey/HasBearerToken の presence 情報をどこまで許容するかは運用ポリシー依存。監査観点で確認推奨 | FR-7, RC-003, TP-008 | Read changed branches |
| P1 | README.md / ICopilotSdkWrapper セクション | PublicApi | 新契約（SupportsStreaming, SendStreamingAsync, CopilotStreamingUpdate）との記述整合を確認しないと利用者が旧契約前提の実装を作る恐れ | FR-10, TP-010, implementation diff | Read signature and call sites |

## Invariant-affecting diffs

| Invariant | Code location | Expected rule | Suspicious or important change | Review note |
| --- | --- | --- | --- | --- |
| capability は実装実態と一致 | GitHubCopilotChatClient.SupportsStreaming / IsSupported, GitHubCopilotSdkWrapper.SupportsStreaming | advertise した capability は実行可能 path と一致する | capability が wrapper 委譲化され、wrapper は sendStreamingCore 注入有無で判定 | production wrapper で常時 false になる意図か、将来実装前提かを確認 |
| non-streaming 互換維持 | GitHubCopilotChatClient.GetResponseAsync, ICopilotSdkWrapper.SendAsync | 既存 final text/exception semantics を壊さない | streaming 追加後も SendAsync path は温存 | call site 破壊がないかを spot check |
| fail-fast contract 維持 | DefaultCopilotSdkWrapper | provider-only 構成では早期失敗し誤動作しない | streaming fail-fast が追加された | 例外メッセージと運用ドキュメント整合を確認 |

## Error / cancellation / retry paths

| Path | Code location | Expected behavior | Review note |
| --- | --- | --- | --- |
| Streaming cancellation | GitHubCopilotChatClient.GetStreamingResponseAsync | cancellation が stage として識別できること | chat client 側で cancellation catch/log がないため、wrapper/外層で十分か確認 |
| Streaming timeout/disconnected | GitHubCopilotSdkWrapper.SendStreamingAsync | timeout/disconnected が区別可能で観測できること | WaitWithHeartbeatAsync は non-streaming path に実装。streaming path では同等処理が見えないため要確認 |
| Non-streaming timeout/cancellation/disconnected | GitHubCopilotSdkWrapper.WaitWithHeartbeatAsync, GitHubCopilotChatClient.GetResponseAsync | timeout/cancel/disconnect を分類し wrap/再throw | 実装あり。ログ粒度が RC-003 要求を満たすか確認 |

## State transition review points

| State / transition | Code location | Allowed / prohibited behavior | Review note |
| --- | --- | --- | --- |
| first SDK event -> first delta -> progress -> completed | GitHubCopilotChatClient.GetStreamingResponseAsync | allowed: delta 逐次送出, completed で終端。prohibited: 旧来の空白分割疑似 streaming | 構造上は置換済み。completed 後の追加 update 処理は wrapper 側依存 |
| request accepted -> model list -> selected model -> send | GitHubCopilotChatClient.GetResponseAsync / GetStreamingResponseAsync | stage ログが相関IDで追えること | stage は増えたが streaming 失敗終端の記録は要確認 |
| wrapper supports flag -> streaming entrypoint | ICopilotSdkWrapper + GitHubCopilotChatClient | allowed: capability false なら明示的 NotSupported | capability true かつ default SendStreamingAsync 実装未override の場合の実行時差異に注意 |

## Boundary and wiring review points

| Boundary | Code location | Producer | Consumer | Mechanism | Review note |
| --- | --- | --- | --- | --- | --- |
| ChatClient <-> Wrapper streaming | GitHubCopilotChatClient.GetStreamingResponseAsync | GitHubCopilotChatClient | ICopilotSdkWrapper | SendStreamingAsync | 例外/終端条件の責務境界を確認 |
| Wrapper <-> SDK runtime | GitHubCopilotSdkWrapper.SendAsync / SendStreamingAsync | GitHubCopilotSdkWrapper | GitHub.Copilot.SDK | CreateSessionAsync, SendAndWaitAsync, delegate injection | streaming が production SDK path か test delegate path かを明確化 |
| DI provider-only fail-fast | GitHubCopilotServiceExtensions.DefaultCopilotSdkWrapper | DI registration | ChatClient/Host | AddGitHubCopilotProvider | Streaming 呼び出し時も fail-fast になることを確認 |
| compatibility wrapper forwarding | GitHubCopilotCliSdkWrapper | compatibility wrapper | GitHubCopilotSdkWrapper | pass-through | obsolete path が新契約でも齟齬ないか確認 |

## Public API / persistence shape changes

| Surface | Code location | Compatibility concern | Callers / stored data affected | Review note |
| --- | --- | --- | --- | --- |
| ICopilotSdkWrapper に default members 追加 | src/MeAiUtility.MultiProvider.GitHubCopilot/Abstractions/ICopilotSdkWrapper.cs | custom wrapper 実装が SupportsStreaming=true のみ実装し SendStreamingAsync 未実装だと runtime failure | custom wrappers, tests, samples | capability と実体の整合を reviewer が確認 |
| CopilotStreamingUpdate* 型追加 | src/MeAiUtility.MultiProvider.GitHubCopilot/Abstractions/ICopilotSdkWrapper.cs | update semantics (Delta/Progress/Completed) の契約定義が wrapper 実装依存 | wrapper implementations | completed/finalText 要件の明文化が十分か確認 |
| GitHubCopilotProviderOptions 追加 | src/MeAiUtility.MultiProvider.GitHubCopilot/Options/GitHubCopilotProviderOptions.cs | logging behavior 変更 (preview opt-in) | appsettings consumers | README 記述と実際の既定値が一致しているか確認 |

## Tests that may give false confidence

| Test / check | Substitute or limitation | Production path covered? | Human review concern |
| --- | --- | --- | --- |
| GitHubCopilotSdkWrapperTests.SendStreamingAsync_UsesConfiguredStreamCore | sendStreamingCore delegate 注入の unit test | No | 実 SDK session/event streaming ではなく注入 delegate を検証しているため RC-001 の production semantics は未確定 |
| GitHubCopilotChatClientTests.GetStreamingResponseAsync_YieldsWrapperStreamingDeltas | mock wrapper の固定 update | No | chat client 写像は確認できるが wrapper 実実装の failure path は未検証 |
| selected IntegrationTests (ContractTests/ProviderSwitch/InteractiveChatSample) | selected subset 実行のみ | Partially | opt-in E2E 未実行。streaming 実 runtime の event sequence には未到達 |
| InteractiveChatSampleTests.CapturingCopilotSdkWrapper | SupportsStreaming=true だが streaming method 未実装 | No | sample 実装が capability と実体の不整合を許容してしまう可能性 |

## Files safe to skim

| File | Reason | Caveat |
| --- | --- | --- |
| src/MeAiUtility.MultiProvider.GitHubCopilot/CopilotClientHost.cs | model list start/completed の追加ログのみで責務変更は小さい | list failure wrap は既存挙動と比較して確認 |
| src/MeAiUtility.MultiProvider.GitHubCopilot/Options/GitHubCopilotProviderOptions.cs | 設定プロパティ追加のみで制御フロー変更なし | default 値と README 記述整合は確認 |
| src/MeAiUtility.MultiProvider.GitHubCopilot/GitHubCopilotCliSdkWrapper.cs | 単純な forward 追加 | obsolete path でも capability 不整合がないか spot check |

## Files not inspected / uncertainty

| Area or file | Reason not fully inspected | Risk if missed | Recommended action |
| --- | --- | --- | --- |
| tests/MeAiUtility.MultiProvider.IntegrationTests/E2ETests/GitHubCopilotOptInE2ETests.cs | この pass では selected integration 実行結果のみ参照し、opt-in E2E 本体は未実行 | RC-001/RC-003 の実 runtime sequence (progress/disconnected) 見落とし | verification-kernel で ManualOnly として確認 |
| GitHub.Copilot.SDK 実 runtime event taxonomy | SDK 内部実装は scope 外で public surface 前提のみ | update kind と実 event の対応ズレ | manual runtime evidence で追跡 |
| README の ICopilotSdkWrapper セクション全体整合 | 追記箇所中心で確認、全文の新契約整合を深掘りしていない | 利用者が旧契約前提で実装するリスク | human doc review で API section を重点確認 |

## Suggested human review order

1. src/MeAiUtility.MultiProvider.GitHubCopilot/GitHubCopilotChatClient.cs の GetStreamingResponseAsync ErrorPath
2. src/MeAiUtility.MultiProvider.GitHubCopilot/GitHubCopilotSdkWrapper.cs の SupportsStreaming/SendStreamingAsync と non-streaming との差分
3. src/MeAiUtility.MultiProvider.GitHubCopilot/Abstractions/ICopilotSdkWrapper.cs の default contract と custom wrapper 互換
4. src/MeAiUtility.MultiProvider.GitHubCopilot/Configuration/GitHubCopilotServiceExtensions.cs の default fail-fast streaming
5. tests/MeAiUtility.MultiProvider.GitHubCopilot.Tests/GitHubCopilotSdkWrapperTests.cs と tests/MeAiUtility.MultiProvider.GitHubCopilot.Tests/GitHubCopilotChatClientTests.cs の production gap
6. README.md の ICopilotSdkWrapper/GitHubCopilot options セクション

## Handoff Packet

- Profile used: code-review-focus-kernel
- Source artifacts: plans/github-copilot-streaming-diagnostics-plan.md, plans/github-copilot-streaming-diagnostics-change-risk-triage.md, plans/github-copilot-streaming-diagnostics-runtime-contract-kernel.md, plans/github-copilot-streaming-diagnostics-test-design-kernel.md, plans/github-copilot-streaming-diagnostics-implementation-contract-kernel.md, plans/github-copilot-streaming-diagnostics-implementation-contract-review-kernel.md, plans/github-copilot-streaming-diagnostics-implementation-handoff-review.md, plans/github-copilot-streaming-diagnostics-implementation-execution.md
- Diff source: working tree diff
- Selected contracts / IDs: RC-001, RC-002, RC-003
- Selected test point IDs: TP-001, TP-002, TP-004, TP-005, TP-007, TP-008, TP-010
- Selected gap IDs: none
- Files inspected: src/MeAiUtility.MultiProvider.GitHubCopilot/Abstractions/ICopilotSdkWrapper.cs, src/MeAiUtility.MultiProvider.GitHubCopilot/GitHubCopilotChatClient.cs, src/MeAiUtility.MultiProvider.GitHubCopilot/GitHubCopilotSdkWrapper.cs, src/MeAiUtility.MultiProvider.GitHubCopilot/Options/GitHubCopilotProviderOptions.cs, src/MeAiUtility.MultiProvider.GitHubCopilot/GitHubCopilotCliSdkWrapper.cs, src/MeAiUtility.MultiProvider.GitHubCopilot/Configuration/GitHubCopilotServiceExtensions.cs, src/MeAiUtility.MultiProvider.GitHubCopilot/CopilotClientHost.cs, tests/MeAiUtility.MultiProvider.GitHubCopilot.Tests/GitHubCopilotChatClientTests.cs, tests/MeAiUtility.MultiProvider.GitHubCopilot.Tests/GitHubCopilotSdkWrapperTests.cs, tests/MeAiUtility.MultiProvider.GitHubCopilot.Tests/ConfigurationTests/GitHubCopilotServiceExtensionsTests.cs, tests/MeAiUtility.MultiProvider.IntegrationTests/Samples/InteractiveChatSampleTests.cs, README.md
- Files intentionally not inspected: tests/MeAiUtility.MultiProvider.IntegrationTests/E2ETests/GitHubCopilotOptInE2ETests.cs (ManualOnly), GitHub.Copilot.SDK internal implementation (OutOfScopeForThisPass)
- Decisions made: verdict=FOCUSED_REVIEW_OK。P0 は streaming ErrorPath と capability/binding 境界に限定。P1 は substitute mismatch, security logging boundary, documentation drift を設定。broad review は不要。
- Do not redo unless new evidence appears: wrapper additive shape は実装済み。chat client の capability は wrapper 委譲化済み。default wrapper streaming fail-fast は実装済み。selected tests は成功。
- Remaining work: ManualOnly な opt-in E2E、SDK runtime event taxonomy の実観測、README API section の整合確認。Status=PartiallyDone。
- Recommended next step: human code review -> verification-kernel (ManualOnly 領域の確認を含む)
