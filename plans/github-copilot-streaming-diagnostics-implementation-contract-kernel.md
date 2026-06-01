# Implementation Contract Kernel

## スコープ

本 artifact は GitHub Copilot SDK provider の streaming / diagnostics 強化に対する RC-002 の implementation-realization risk を bounded に固定するための document-only pass である。対象は次の 3 点に限定する。

- `ICopilotSdkWrapper` を non-breaking に拡張する minimum sufficient な contract shape
- `GitHubCopilotChatClient` / `GitHubCopilotSdkWrapper` / `GitHubCopilotServiceExtensions` / test substitute の production/test binding
- implementation-handoff-review が `READY_WITH_NOTES` 以上になるための exit criteria

本 pass では production code と test code は変更しない。RC-001 と RC-003 の詳細 sequence 設計や logger event ID 設計は扱わず、RC-002 を unblock するために必要な implementation path だけを固定する。

## Plan が要求する実装要件

| Requirement | Expected by Plan | Evidence found | Status |
| --- | --- | --- | --- |
| RC-002 preferred shape | `ICopilotSdkWrapper` は既存 `SendAsync` を維持したまま streaming 専用 surface を追加し、`GitHubCopilotChatClient` が疑似ストリーミングではなく wrapper 経由の event-driven path を使えること | 現状 interface は `ListModelsAsync` / `SendAsync` のみで、`GitHubCopilotChatClient.GetStreamingResponseAsync` は `GetResponseAsync` 後の空白分割に依存している | Confirmed |
| final text aggregation ownership | raw SDK event の解釈と final text 集約は wrapper 側で閉じ、chat client は正規化済み streaming update を `ChatResponseUpdate` に変換するだけにすること | `GitHubCopilotSdkWrapper` は `session.event` trace translation と `SendAndWaitAsync` による final text 取得をすでに持ち、SDK event の source of truth に最も近い | Confirmed |
| capability advertisement binding | `SupportsStreaming` / `IsSupported(Streaming)` は hard-coded true ではなく、wrapper 実装実態へ接続されること | 現状 `GitHubCopilotChatClient` は wrapper 実装に関係なく `SupportsStreaming => true` / `IsSupported(Streaming) => true` を返す | MissingButRequired |
| fail-fast / substitute compatibility | `DefaultCopilotSdkWrapper`、Moq mock、`StubCopilotSdkWrapper`、`RecordingForwardingCopilotSdkWrapper` が preferred shape で fail-fast または forward を明示できること | 現状 substitute は全て旧契約のみを前提にしており、追加 streaming surface は未定義 | MissingButRequired |
| fallback shape | additive extension で wrapper responsibility を閉じられない場合だけ event-first の breaking shape を fallback として残すこと | Plan と triage が breaking 案を fallback 扱いに限定している | Confirmed |
| handoff readiness | implementation-handoff-review へ渡すため、production/test substitute binding map と exit criteria を明文化すること | runtime-contract-kernel と test-design-kernel では TP-005 が未確定のままで、realization shape の明文化が未完了 | MissingButRequired |

## Dependency と API surface の確認結果

| Dependency / API / symbol | Expected source | Found location | Status | Notes |
| --- | --- | --- | --- | --- |
| `ICopilotSdkWrapper.ListModelsAsync` / `SendAsync` | 現行 wrapper 契約 | `src/MeAiUtility.MultiProvider.GitHubCopilot/Abstractions/ICopilotSdkWrapper.cs` | Confirmed | final string 契約のみ。streaming 用 public surface はまだない |
| `GitHubCopilotSdkWrapper.SendAsync` | production wrapper の final-text path | `src/MeAiUtility.MultiProvider.GitHubCopilot/GitHubCopilotSdkWrapper.cs` | Confirmed | `CreateSessionAsync` と `SendAndWaitAsync` を使い、final text を返す |
| `session.event` trace translation | SDK event の既存観測基盤 | `src/MeAiUtility.MultiProvider.GitHubCopilot/GitHubCopilotSdkWrapper.cs` | Confirmed | `TryTranslateSdkTraceMessage` / `TryExtractSessionEventText` があり、event-driven path を wrapper 側で正規化する根拠になる |
| `GitHubCopilotChatClient.GetStreamingResponseAsync` | 現行 streaming entrypoint | `src/MeAiUtility.MultiProvider.GitHubCopilot/GitHubCopilotChatClient.cs` | Confirmed | `GetResponseAsync` 後の空白分割であり、real streaming の production address ではない |
| `GitHubCopilotChatClient.SupportsStreaming` / `IsSupported(Streaming)` | capability advertisement | `src/MeAiUtility.MultiProvider.GitHubCopilot/GitHubCopilotChatClient.cs` | Confirmed | 実装実態と切り離された hard-coded true |
| `DefaultCopilotSdkWrapper` | provider-only registration 時の fail-fast substitute | `src/MeAiUtility.MultiProvider.GitHubCopilot/Configuration/GitHubCopilotServiceExtensions.cs` | Confirmed | `ListModelsAsync` と `SendAsync` だけ fail-fast。新 surface の fail-fast は未定義 |
| `StubCopilotSdkWrapper` | DI test substitute | `tests/MeAiUtility.MultiProvider.GitHubCopilot.Tests/ConfigurationTests/GitHubCopilotServiceExtensionsTests.cs` | Confirmed | 旧契約のみ実装。preferred shape では compile/runtime compatibility 方針が必要 |
| `RecordingForwardingCopilotSdkWrapper` | opt-in E2E substitute | `tests/MeAiUtility.MultiProvider.IntegrationTests/E2ETests/GitHubCopilotOptInE2ETests.cs` | Confirmed | 旧契約のみ forward。streaming E2E を追加するなら新 surface forward が必要 |
| wrapper-owned streaming update DTO | additive extension を成立させるための最小追加型 | repository 内には未存在 | MissingButRequired | `ChatResponseUpdate` を直接 wrapper 契約に使うより provider 内部 DTO を 1 つだけ追加する方が責務が閉じる |
| wrapper-level streaming capability flag | capability advertisement を wrapper 実態へ接続するための最小追加 surface | repository 内には未存在 | MissingButRequired | `GitHubCopilotChatClient` が concrete type 判定に依存しないよう property または同等の surface が必要 |
| breaking fallback contract | additive で閉じない場合の代替 path | repository 内には未存在 | AllowedReuse | fallback としてのみ許容し、preferred では採用しない |

## 選択した実装アプローチ

### Implementation decisions

1. Preferred contract shape は additive extension とする。`ICopilotSdkWrapper` の既存 `ListModelsAsync` / `SendAsync` はそのまま残し、streaming request 専用に 1 つの capability surface と 1 つの streaming method を追加する。
2. capability surface は wrapper 実装実態を返す値とし、`GitHubCopilotChatClient.SupportsStreaming` / `IsSupported(Streaming)` はこの値へ単純委譲する。chat client 内で hard-coded true を維持しない。
3. streaming method は raw SDK event ではなく provider 内部の正規化 update を返す。update は minimum sufficient として `Kind`、`TextDelta`、`FinalText` を持つ。`FinalText` は terminal update でのみ設定する。
4. final text aggregation の責務は wrapper 側に置く。理由は SDK event 名と payload 形状の差異吸収、trace translation、timeout/cancellation/disconnected との整合が wrapper 側に集約されているためである。chat client は正規化 update を `ChatResponseUpdate` に写像するだけに留める。
5. non-streaming path は既存 `SendAsync` をそのまま使う。streaming path だけ新 surface を通す。これにより `GetResponseAsync` の既存例外契約と final text 契約を維持する。
6. `DefaultCopilotSdkWrapper` は preferred shape でも fail-fast を維持する。capability は false、streaming method 呼び出し時は既存と同系統の `WrapperNotConfiguredMessage` を投げる。
7. mock/stub/recording wrapper は preferred shape では次の 3 群に分ける。Moq mock は必要な test でのみ新 surface を setup する。`StubCopilotSdkWrapper` は capability false のまま non-streaming 専用 substitute として残す。`RecordingForwardingCopilotSdkWrapper` は streaming E2E を追加する段階でのみ新 surface forward を実装する。
8. fallback contract shape は additive extension で次の条件のどれかを満たせない場合にのみ採用する。a) wrapper が final text aggregation を自前で閉じられない。b) capability advertisement を wrapper 実態へ安定接続できない。c) production/test substitute の差し替えを同一 interface family で維持できない。

### Preferred contract shape

preferred は次の形を minimum sufficient として固定する。

- `ICopilotSdkWrapper` に wrapper-level streaming capability surface を追加する
- `ICopilotSdkWrapper` に streaming request 専用 method を 1 つ追加する
- provider 内部 DTO を 1 つ追加し、normalized update を表現する
- normalized update は `Delta`、`Progress`、`Completed` の 3 種だけを public contract に持つ
- `Completed` update にだけ `FinalText` を載せる
- error、timeout、cancellation、disconnected は update ではなく既存と同じ exception / cancellation path で表現する

この shape により public API の追加数を最小化しつつ、chat client は streaming path で only-once terminal completion と delta 逐次返却を扱える。

### Fallback contract shape

fallback は breaking change として event-first wrapper へ再定義する案であり、preferred が成立しない場合にだけ使う。

- final-text `SendAsync` と streaming method を統合し、常に lifecycle updates と terminal completion を返す 1 本の execution surface に置き換える
- non-streaming `GetResponseAsync` は terminal completion の `FinalText` を読むだけの consumer にする
- `DefaultCopilotSdkWrapper`、`StubCopilotSdkWrapper`、`RecordingForwardingCopilotSdkWrapper`、Moq setup を全て新契約へ寄せる

fallback は wrapper 実装数と test blast radius を増やすため、preferred が不成立と確認されるまで採用しない。

## 必要なコード変更

### Production binding

| Binding point | Current state | Required change | Status |
| --- | --- | --- | --- |
| `ICopilotSdkWrapper` | final-text only | capability surface と streaming method を additive に追加する | MissingButRequired |
| `GitHubCopilotSdkWrapper` | `SendAsync` だけが production path | SDK event を normalized update に変換し、terminal completion で `FinalText` を返す streaming path を追加する | MissingButRequired |
| `GitHubCopilotChatClient.GetStreamingResponseAsync` | `GetResponseAsync` 後の空白分割 | wrapper streaming method を直接 consume し、`Delta` のみ `ChatResponseUpdate` へ変換する | MissingButRequired |
| `GitHubCopilotChatClient.SupportsStreaming` / `IsSupported(Streaming)` | hard-coded true | wrapper capability surface に委譲する | MissingButRequired |
| `GitHubCopilotChatClient.GetResponseAsync` | `SendAsync` を呼ぶ | 変更しない。non-streaming compatibility の anchor として維持する | Confirmed |
| `DefaultCopilotSdkWrapper` | old contract だけ fail-fast | new capability surface=false と new streaming method fail-fast を追加する | MissingButRequired |
| `GitHubCopilotServiceExtensions` | provider-only では default wrapper、wrapper registration 後は real wrapper | binding 形は維持し、新 surface を含む同一 interface 解決が成立することだけ確認する | Confirmed |

### Production/test substitute binding map

| Substitute / binding | Intended role | Required behavior under preferred shape | Status |
| --- | --- | --- | --- |
| `GitHubCopilotSdkWrapper` | production 実装 | capability=true、streaming method 実装、`SendAsync` 維持 | MissingButRequired |
| `DefaultCopilotSdkWrapper` | provider-only fail-fast substitute | capability=false、streaming method は既存と同系統メッセージで fail-fast | MissingButRequired |
| Moq `ICopilotSdkWrapper` | unit test substitute | non-streaming test は既存 setup のまま、streaming test だけ capability と streaming method を追加 setup | AllowedReuse |
| `StubCopilotSdkWrapper` | DI registration test substitute | capability=false の non-streaming substitute として維持。streaming test には使わない | AllowedReuse |
| `RecordingForwardingCopilotSdkWrapper` | opt-in E2E substitute | 現段階では non-streaming forward のまま維持可。streaming E2E を追加する時点で capability forward と streaming method forward を実装する | AllowedReuse |
| concrete type 判定による chat client 分岐 | implementation shortcut | 採用しない。`GitHubCopilotChatClient` が `GitHubCopilotSdkWrapper` 型判定で capability を決める実装は禁止 | RejectedSubstitute |

### Exit criteria for handoff

implementation-handoff-review を `READY_WITH_NOTES` 以上にするための最低条件を次で固定する。

1. preferred contract shape が artifact 上で 1 通りに固定され、fallback は「不成立時のみ」の条件付き記述に留まっていること。
2. final text aggregation responsibility が wrapper 側と明記され、chat client が raw SDK event を解釈しないこと。
3. `SupportsStreaming` / `IsSupported(Streaming)` の接続先が wrapper capability surface と明記され、hard-coded true が残らないこと。
4. `DefaultCopilotSdkWrapper`、Moq mock、`StubCopilotSdkWrapper`、`RecordingForwardingCopilotSdkWrapper` の expected behavior が明文化されていること。
5. non-streaming path は `SendAsync` 維持、streaming path だけ new surface 使用と明記されていること。
6. unresolved は SDK event taxonomy の詳細や E2E 追加条件のような non-blocking 項目だけに限定され、wrapper realization shape 自体は unresolved に戻らないこと。

## 禁止される代替実装

| Similar existing path | Why it is not sufficient | Allowed reuse, if any |
| --- | --- | --- |
| `GetResponseAsync` 完了後の `response.Text.Split(' ')` | SDK event を使わない疑似ストリーミングであり、FR-1 / FR-3 を満たさない | なし |
| `GitHubCopilotChatClient` が raw SDK event JSON を直接解釈する path | SDK payload 正規化、timeout/cancellation/disconnected、final aggregation の責務が chat client へ漏れる | wrapper が返す normalized update のみ再利用可 |
| concrete type 判定で `GitHubCopilotSdkWrapper` のときだけ streaming=true とする path | DI substitute を壊し、test-only success を招く | wrapper capability surface への委譲のみ許容 |
| additive shape を使わず、最初から全 wrapper を breaking 再設計する path | plan の preferred 方針に反し、blast radius が過大 | preferred 不成立時のみ fallback として許容 |
| `StubCopilotSdkWrapper` や `RecordingForwardingCopilotSdkWrapper` を streaming substitute と見なす path | 現状は old contract only であり、false positive な streaming coverage を作る | non-streaming substitute としてのみ再利用可 |

## 検証フック

- `tests/MeAiUtility.MultiProvider.GitHubCopilot.Tests/GitHubCopilotChatClientTests.cs` では non-streaming `SendAsync` 互換維持と、streaming path が空白分割を使わないことを分離して確認する。
- `tests/MeAiUtility.MultiProvider.GitHubCopilot.Tests/GitHubCopilotSdkWrapperTests.cs` では normalized update への変換、terminal `FinalText`、timeout/cancellation の exception path、diagnostics logging を確認する。
- `tests/MeAiUtility.MultiProvider.GitHubCopilot.Tests/ConfigurationTests/GitHubCopilotServiceExtensionsTests.cs` では provider-only fail-fast、two-step registration、custom substitute override が new surface を含んでも成立することを確認する。
- `tests/MeAiUtility.MultiProvider.IntegrationTests/E2ETests/GitHubCopilotOptInE2ETests.cs` は既定の non-streaming 検証を維持し、streaming E2E を追加する場合だけ `RecordingForwardingCopilotSdkWrapper` の forward 拡張を要求する。
- implementation-handoff-review では TP-005 に対し「preferred shape のまま production substitute と test substitute を両立できるか」を主要 gate とする。

## 未解決の実装実現性項目

- `Progress` update をどの SDK event 名に対応付けるかは `GitHub.Copilot.SDK` の観測結果待ちであり、Status は `ApiSurfaceUnknown`。ただし preferred shape 自体は阻害しない。
- `disconnected` を terminal exception として扱うか、途中 update 後の failure として扱うかは `runtime-contract-kernel` / `verification-kernel` で詰める必要があり、Status は `NeedsHumanDecision`。ただし wrapper-owned aggregation 方針は維持する。
- `ICopilotSdkWrapper` の additive extension を default interface member で持つか、通常メンバー追加として全明示実装を更新するかは public API 運用判断が必要であり、Status は `NeedsHumanDecision`。preferred は source churn を最小にする default implementation 方向だが、方針未承認でも capability surface と streaming method の 2 面追加自体は固定済みである。
- `RecordingForwardingCopilotSdkWrapper` に streaming forward を入れるタイミングは opt-in E2E 拡張時でよく、この pass では `OutOfScopeForThisPass` とする。
- fallback へ切り替える判定は「wrapper が terminal `FinalText` を責務として閉じられない」または「substitute binding map を additive で満たせない」場合に限定し、現時点で fallback へ移る確定証拠はない。Status は `Confirmed`。

## Handoff Packet

- Profile used: contract-kernel
- Source artifacts:
  - plans/github-copilot-streaming-diagnostics-plan.md
  - plans/github-copilot-streaming-diagnostics-change-risk-triage.md
  - plans/github-copilot-streaming-diagnostics-runtime-contract-kernel.md
  - plans/github-copilot-streaming-diagnostics-test-design-kernel.md
- Selected contracts / IDs:
  - RC-002
  - RC-001 のうち wrapper event source に依存する部分
  - RC-003 のうち capability / diagnostics binding に依存する部分
- Files inspected:
  - src/MeAiUtility.MultiProvider.GitHubCopilot/Abstractions/ICopilotSdkWrapper.cs
  - src/MeAiUtility.MultiProvider.GitHubCopilot/GitHubCopilotSdkWrapper.cs
  - src/MeAiUtility.MultiProvider.GitHubCopilot/GitHubCopilotChatClient.cs
  - src/MeAiUtility.MultiProvider.GitHubCopilot/Configuration/GitHubCopilotServiceExtensions.cs
  - tests/MeAiUtility.MultiProvider.GitHubCopilot.Tests/GitHubCopilotChatClientTests.cs
  - tests/MeAiUtility.MultiProvider.GitHubCopilot.Tests/GitHubCopilotSdkWrapperTests.cs
  - tests/MeAiUtility.MultiProvider.GitHubCopilot.Tests/ConfigurationTests/GitHubCopilotServiceExtensionsTests.cs
  - tests/MeAiUtility.MultiProvider.IntegrationTests/E2ETests/GitHubCopilotOptInE2ETests.cs
- Files intentionally not inspected:
  - src/MeAiUtility.MultiProvider/Telemetry/ChatTelemetry.cs の詳細 logger event taxonomy
  - tests/MeAiUtility.MultiProvider.GitHubCopilot.Tests/ProviderOverrideTests.cs
  - tests/MeAiUtility.MultiProvider.IntegrationTests/ContractTests/**
  - GitHub.Copilot.SDK package 内の public surface 以上の reverse engineering 対象
- Decisions made:
  - preferred は additive wrapper extension とした
  - final text aggregation は wrapper responsibility とした
  - capability advertisement は wrapper capability surface へ接続すると固定した
  - non-streaming path は `SendAsync` 維持、streaming path だけ新 surface 使用と固定した
  - `DefaultCopilotSdkWrapper` は fail-fast、`StubCopilotSdkWrapper` は non-streaming substitute、`RecordingForwardingCopilotSdkWrapper` は将来の streaming E2E 拡張時に forward 追加と整理した
  - breaking fallback は additive 不成立時のみに限定した
- Do not redo unless new evidence appears:
  - 現行 `ICopilotSdkWrapper` は final-text only である
  - 現行 `GitHubCopilotChatClient.GetStreamingResponseAsync` は疑似ストリーミングである
  - `GitHubCopilotSdkWrapper` には `session.event` trace translation の基盤がある
  - `GitHubCopilotServiceExtensions` には fail-fast default wrapper と real wrapper override の経路がある
- Remaining work:
  - preferred shape の具体的 naming を最小化して implementation-handoff-review へ渡す
  - `Progress` / `disconnected` の runtime semantics を RC-001 / RC-003 側で詰める
  - default interface implementation 採否を public API 運用で判断する
  - streaming E2E を実施する場合の `RecordingForwardingCopilotSdkWrapper` forward 拡張可否を確認する
- Recommended next step:
  - implementation-handoff-review で本 artifact を preferred production binding として採用し、TP-005 を `READY_WITH_NOTES` 判定可能か確認する。もし additive shape の public API 採否または wrapper-owned terminal `FinalText` が承認されない場合のみ fallback contract shape の review に進む。