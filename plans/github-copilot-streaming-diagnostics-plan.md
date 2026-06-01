# Plan Kernel

## Goal

GitHub Copilot SDK provider について、現在の疑似ストリーミング実装を source of truth ベースで見直し、SDK が安全に提供できる event-driven な逐次出力が確認できる場合はそれを用いた実ストリーミングへ移行する。
安全な実ストリーミング経路を production 品質で確立できない場合は、Streaming capability 表示を実装実態に合わせて是正しつつ、要求受理から完了・失敗までの段階診断ログを Microsoft.Extensions.Logging から観測できるようにする。

## Non-goals

- full runtime evidence、PlantUML、full integration test design の作成
- GitHub.Copilot.SDK 0.2.1-preview.1 の未確認 public API を前提にした確定設計
- OpenAI、Azure OpenAI、Codex App Server 各 provider の機能追加
- GitHub Copilot provider に対する prompt 仕様変更、モデル選択仕様変更、認証方式追加
- ログ出力に secret や prompt 全文を恒久的に含める診断方針への変更
- streaming 実装可否に関係なく public API を無条件で破壊的変更すること

## Functional requirements

### FR-1: Streaming capability の実態整合

GitHub Copilot provider は、observable な streaming 実装実態に合わせて SupportsStreaming と IsSupported(Streaming) を整合させる。

- 安全な SDK event-driven streaming path が確認できる場合: 実ストリーミングを返す
- 安全な path が確認できない場合: Streaming を未対応として advertise し、疑似ストリーミングを廃止または使用不能化する

### FR-2: Wrapper 契約の根本原因是正

現在の根本原因仮説である「ICopilotSdkWrapper が final text 契約に閉じているため chat client に真の streaming path が存在しない」を解消するため、wrapper 契約を streaming/event 観測可能な形へ整理する。

- 非破壊的変更案: 既存 final-text API を維持したまま、streaming/event 用の追加契約を別メソッドまたは別型で増設する
- 破壊的変更案: wrapper 契約を request lifecycle/event-first 契約へ再定義し、非 streaming もその上で終端集約する

### FR-3: 実ストリーミングの逐次返却

実ストリーミングを採用する場合、GitHubCopilotChatClient.GetStreamingResponseAsync は GetResponseAsync 完了後の空白分割ではなく、SDK 由来の delta/event を逐次 ChatResponseUpdate として返す。

- first SDK event
- first delta
- progress/heartbeat
- final

の各段階を区別できる構造にする。

### FR-4: 非ストリーミング互換性の維持

GetResponseAsync は引き続き最終応答を返し、実ストリーミング採用時も非 streaming 呼び出しから見た最終テキスト契約と例外契約を維持する。

### FR-5: 段階診断ログの追加

Microsoft.Extensions.Logging から、少なくとも以下の段階を provider 利用者が切り分け可能な粒度で観測できるようにする。

- request accepted
- model list
- client create / client reuse
- session create
- message send
- first SDK event
- first delta
- progress / heartbeat
- final
- timeout
- cancellation
- disconnected
- exception wrap

### FR-6: 相関 ID と telemetry 起点の統一

GitHub Copilot provider は既存の ChatTelemetry を利用可能な source of truth とし、TraceId / RequestId または同等の correlation 情報を request lifecycle 全体で一貫して扱う。

### FR-7: 安全なログ出力

診断ログは secret masking を前提とし、ProviderOverride は非機密属性のみを構造化ログへ出し、API key / bearer token / GitHub token / 添付機密パス全文などの漏えいを避ける。

### FR-8: Timeout と cancellation の一貫適用

TimeoutSeconds は wrapper / session / streaming loop / final wait の各段階で一貫して適用し、cancellation、timeout、SDK 切断、その他例外がどの段階で発生したかを識別可能にする。

### FR-9: DI と production binding の維持

GitHubCopilotServiceExtensions における AddGitHubCopilot / AddGitHubCopilotSdkWrapper の解決関係は維持し、DefaultCopilotSdkWrapper と GitHubCopilotSdkWrapper の束縛変更が必要な場合も production と test substitute の切り替え可能性を保つ。

### FR-10: テストとドキュメントの更新

unit test、opt-in E2E、README もしくは provider 利用ドキュメントを更新し、streaming 可否、段階ログ、timeout/cancellation の期待動作、非 streaming 互換を利用者が判断できる状態にする。

## Acceptance conditions

| ID | 対応 FR | 成功基準（observable な behavior） |
| --- | --- | --- |
| AC-1 | FR-1 | 安全な SDK streaming path が採用された場合、GitHubCopilotChatClient の streaming capability 表示は true のままで、GetStreamingResponseAsync は GetResponseAsync の完了待ちを前提にせず逐次 update を返す |
| AC-2 | FR-1 | 安全な SDK streaming path が採用されない場合、GitHubCopilotChatClient は Streaming 未対応として advertise し、利用者が疑似ストリーミングを実ストリーミングと誤認しない |
| AC-3 | FR-2, FR-3 | wrapper 契約から event-driven path を利用できる設計になり、chat client 側で response.Text の空白分割による update 生成が不要になる |
| AC-4 | FR-4 | GetResponseAsync は streaming 採否にかかわらず assistant の最終テキストを返し、既存の invalid model、timeout、send failure などの例外意味論を維持する |
| AC-5 | FR-5 | request accepted から final / timeout / cancellation / disconnected / exception wrap まで、少なくとも 1 つの request 相関情報付きログ列として観測できる |
| AC-6 | FR-5, FR-6 | model list、client create-reuse、session create、message send、first SDK event、first delta の各段階が区別可能なログまたは event name で記録される |
| AC-7 | FR-6 | GitHub Copilot provider の成功・失敗ログで TraceId と RequestId、または同等の correlation 情報を request 単位で追跡できる |
| AC-8 | FR-7 | ProviderOverride の type/base URL/Azure API version など非機密情報のみがログで観測され、token / key / bearer / prompt 機密値はマスクまたは未出力になる |
| AC-9 | FR-8 | TimeoutSeconds が request ごとに一貫適用され、timeout、cancellation、disconnected が同一 failure として潰れず段階別に識別できる |
| AC-10 | FR-9 | AddGitHubCopilot、AddGitHubCopilotProvider、AddGitHubCopilotSdkWrapper の既存 DI テスト観点を維持しつつ、新しい wrapper 契約でも production 実装と test substitute の差し替えが可能である |
| AC-11 | FR-10 | unit test で streaming capability 分岐、段階ログ、timeout/cancellation、secret masking、non-streaming compatibility を検証できる |
| AC-12 | FR-10 | opt-in E2E で少なくとも GitHub Copilot 実呼び出し時の非 streaming 成功経路と、streaming 採用時は first event から final までの観測点を確認できる |
| AC-13 | FR-10 | README または provider ドキュメントから、GitHub Copilot provider の streaming サポート実態、ログ観測点、注意事項が読み取れる |

## Affected components / modules

### 変更が必要な候補

- src/MeAiUtility.MultiProvider.GitHubCopilot/GitHubCopilotChatClient.cs
- src/MeAiUtility.MultiProvider.GitHubCopilot/Abstractions/ICopilotSdkWrapper.cs
- src/MeAiUtility.MultiProvider.GitHubCopilot/GitHubCopilotSdkWrapper.cs
- src/MeAiUtility.MultiProvider.GitHubCopilot/Configuration/GitHubCopilotServiceExtensions.cs
- src/MeAiUtility.MultiProvider.GitHubCopilot/Options/GitHubCopilotProviderOptions.cs
- src/MeAiUtility.MultiProvider/Telemetry/ChatTelemetry.cs
- tests/MeAiUtility.MultiProvider.GitHubCopilot.Tests/GitHubCopilotChatClientTests.cs
- tests/MeAiUtility.MultiProvider.GitHubCopilot.Tests/GitHubCopilotSdkWrapperTests.cs
- tests/MeAiUtility.MultiProvider.GitHubCopilot.Tests/ConfigurationTests/GitHubCopilotServiceExtensionsTests.cs
- tests/MeAiUtility.MultiProvider.IntegrationTests/E2ETests/GitHubCopilotOptInE2ETests.cs
- README.md

### 読み取りのみで十分と判断した範囲

- 他 provider 実装全般
- Codex App Server 関連 plans / runtime artifacts
- GitHub Copilot embedding 実装

## Expected implementation scope

### 推奨スコープ: 非破壊的変更回避案

1. ICopilotSdkWrapper に既存 final-text 契約を残したまま、streaming/event 観測用の追加契約を導入する
2. GitHubCopilotSdkWrapper が SDK session/event API 候補を使用して lifecycle event と delta を観測・正規化する
3. GitHubCopilotChatClient は streaming request では追加契約を利用し、non-streaming request では従来の最終テキスト契約を維持する
4. 実ストリーミングが成立しない環境では capability を false に倒せるよう feature advertisement を実装実態に合わせる
5. ChatTelemetry ベースの traceId/requestId を provider 全体に通し、stage-based structured logging を追加する
6. 既存 unit test と opt-in E2E を増補し、README を更新する

### 代替スコープ: 必要時の破壊的変更案

1. ICopilotSdkWrapper を event-first 契約へ再定義し、final text 取得もその上位集約として統一する
2. DefaultCopilotSdkWrapper、mock/stub、DI テスト、E2E 補助 wrapper を新契約へ更新する
3. 既存テスト・利用コードへの影響を明示し、breaking change として artifact / docs に記録する

### この pass で implementation agent に期待すること

- まず非破壊的変更回避案の成立可否を評価し、成立するならそれを優先する
- SDK public surface の確証不足が残る場合のみ、change-risk-triage と implementation-handoff-review で破壊的変更案の要否を判断する

## Known high-risk boundaries

| Risk trigger | Present / Absent / Unclear | 補足 |
| --- | --- | --- |
| Cross-process or cross-service sequence | Absent | provider 内 request lifecycle に閉じる |
| Queue / event / webhook / background worker | Absent | 常設 worker はないが SDK session event は存在候補 |
| External API or SDK | Present | GitHub.Copilot.SDK 0.2.1-preview.1 の public event/session surface は一部確認済みだが direct streaming method 名は未確定 |
| Authentication or authorization | Absent | 認証追加は scope 外。ただしログで secret masking は必要 |
| Durable state / retry / replay / idempotency | Unclear | 永続 state は主目的ではないが timeout / disconnected 後の finalization 取り扱いに再送・重複リスクがある |
| Startup wiring / DI / configuration | Present | ICopilotSdkWrapper 契約変更は AddGitHubCopilot / AddGitHubCopilotSdkWrapper / DefaultCopilotSdkWrapper に影響する |
| Production implementation split from test substitute | Present | mock wrapper、recording wrapper、default wrapper、実 SDK wrapper の契約整合が必要 |
| Multiple runtime participants coordinating state | Present | ChatClient、CopilotClientHost、SDK wrapper、SDK session/event callback、logger/telemetry が同一 request を協調処理する |
| Observable behavior spanning more than one component | Present | capability 表示、wrapper 契約、SDK event、logging、timeout/cancellation が複数コンポーネントを跨ぐ |

詳細な contract analysis と final selection は change-risk-triage に委ねる。

## Out of scope for this pass

- GitHub.Copilot.SDK 内部実装の reverse engineering を前提にした仕様化
- full runtime evidence、sequence diagram、full integration test point ledger
- provider 横断の telemetry 再設計
- GitHub Copilot 以外の provider の logging/streaming 実装修正
- prompt capture ポリシー変更
- E2E 自動常時実行化

## Handoff to change-risk-triage

以下の high-risk boundary candidates を change-risk-triage で分類し、implementation-handoff-review まで流せる contract selection を行ってください。

1. SDK streaming contract boundary
   GitHub.Copilot.SDK で確認済みの CreateSessionAsync、On(Action<SessionLifecycleEvent>)、On(string, Action<SessionLifecycleEvent>)、SessionEvent、SessionEvent.FromJson(string)、Rpc.SessionLogResult.EventId を、実ストリーミング source of truth としてどこまで採用できるか。

2. Wrapper API evolution boundary
   ICopilotSdkWrapper を非破壊的に拡張できるか、それとも event-first 契約への破壊的再定義が必要か。

3. Lifecycle logging and correlation boundary
   request accepted、model list、client create-reuse、session create、message send、first SDK event、first delta、progress、final、timeout、cancellation、disconnected、exception wrap を、ChatTelemetry と既存 logger にどう束ねるか。

4. Timeout / cancellation / disconnection boundary
   session.SendAndWaitAsync 中心の現行 path と、event-driven path の timeout 適用・キャンセル伝播・切断検知をどこで統一するか。

5. Production vs test substitute boundary
   DefaultCopilotSdkWrapper、GitHubCopilotSdkWrapper、mock wrapper、RecordingForwardingCopilotSdkWrapper を新契約でどう揃えるか。

6. Capability advertisement boundary
   実ストリーミング不成立時に SupportsStreaming / IsSupported(Streaming) を false に倒す条件と、non-streaming 互換維持の整理。

## Handoff Packet

- Profile used: plan-kernel
- Plan artifact: plans/github-copilot-streaming-diagnostics-plan.md
- Source artifacts:
  - ユーザー要求本文
  - ユーザー提示の確認済み事実 1-7
  - local NuGet cache の GitHub.Copilot.SDK 0.2.1-preview.1 XML / DLL 調査結果（ユーザー提示事実として採用）
- Selected contracts / IDs: none selected by this agent; final selection belongs to change-risk-triage
- Files inspected:
  - src/MeAiUtility.MultiProvider.GitHubCopilot/GitHubCopilotChatClient.cs
  - src/MeAiUtility.MultiProvider.GitHubCopilot/Abstractions/ICopilotSdkWrapper.cs
  - src/MeAiUtility.MultiProvider.GitHubCopilot/GitHubCopilotSdkWrapper.cs
  - src/MeAiUtility.MultiProvider.GitHubCopilot/Configuration/GitHubCopilotServiceExtensions.cs
  - src/MeAiUtility.MultiProvider.GitHubCopilot/Options/GitHubCopilotProviderOptions.cs
  - src/MeAiUtility.MultiProvider/Telemetry/ChatTelemetry.cs
  - tests/MeAiUtility.MultiProvider.GitHubCopilot.Tests/GitHubCopilotChatClientTests.cs
  - tests/MeAiUtility.MultiProvider.GitHubCopilot.Tests/GitHubCopilotSdkWrapperTests.cs
  - tests/MeAiUtility.MultiProvider.GitHubCopilot.Tests/ConfigurationTests/GitHubCopilotServiceExtensionsTests.cs
  - tests/MeAiUtility.MultiProvider.IntegrationTests/E2ETests/GitHubCopilotOptInE2ETests.cs
  - plans/codex-thread-reuse-plan.md
  - README.md
- Files intentionally not inspected:
  - src/MeAiUtility.MultiProvider.OpenAI/**
  - src/MeAiUtility.MultiProvider.AzureOpenAI/**
  - src/MeAiUtility.MultiProvider.CodexAppServer/**
  - tests/MeAiUtility.MultiProvider.OpenAI.Tests/**
  - tests/MeAiUtility.MultiProvider.AzureOpenAI.Tests/**
  - full GitHub.Copilot.SDK package source beyond user-confirmed API names
- Decisions made:
  - bounded plan とし、full runtime evidence と full integration design は扱わない
  - 根本原因仮説を「wrapper API が final text 契約に閉じていること」として採用した
  - cheap check として event/session public surface 候補の存在を採用し、実ストリーミング path の候補ありと判断した
  - implementation 方針は非破壊的変更回避案を優先し、必要時のみ破壊的変更案を代替として残した
- Do not redo unless new evidence appears:
  - GitHubCopilotChatClient の現行 GetStreamingResponseAsync は response.Text の空白分割による疑似ストリーミングである
  - ICopilotSdkWrapper は現状 ListModelsAsync と SendAsync(final string) のみを公開している
  - GitHubCopilotSdkWrapper は session.SendAndWaitAsync ベースで最終テキストを返しつつ、SDK trace 翻訳と session.event 由来の解析基盤を既に持つ
  - ChatTelemetry は TraceId / RequestId を持つ既存起点である
  - AddGitHubCopilot / AddGitHubCopilotSdkWrapper の DI 束縛は既に存在する
- Remaining work:
  - SDK の public event/session API を production streaming source of truth として採用する最終可否判断
  - 非破壊的 wrapper 拡張で十分か、破壊的再設計が必要かの triage
  - timeout / cancellation / disconnected の contract 境界選定
  - implementation-handoff-review で使う selected runtime contracts の明確化
- Recommended next step:
  - change-risk-triage を実行し、plans/github-copilot-streaming-diagnostics-plan.md を入力 artifact として high-risk boundary candidates 1-6 を分類する