# Change Risk Triage

## Recommended profile

contract-kernel

## Reasoning

変更対象は GitHub Copilot provider 内に限定されるが、runtime risk は単一クラス内では閉じない。GitHub.Copilot.SDK 0.2.1-preview.1 の event-driven 候補 surface を production source of truth として採用できるかが未確定であり、その不確実性が `GitHubCopilotSdkWrapper` の契約進化、`GitHubCopilotChatClient` の streaming capability 表示、timeout/cancellation/disconnection の failure semantics、段階ログと correlation の一貫性に連鎖する。特に現状は `ICopilotSdkWrapper` が final string 契約のみを公開し、`GitHubCopilotChatClient.GetStreamingResponseAsync` は `GetResponseAsync` 完了後の空白分割で疑似ストリーミングを返しているため、SDK source、wrapper 契約、chat client observable behavior の 3 点を同時に固定しないと、stub テストだけ通って production では真の streaming にならない、または Streaming capability が誤広告のまま残るリスクが高い。

一方で、この変更は queue や durable store を増やすものではなく、high-risk runtime slice は 3 件の runtime contract に収束する。したがって `full-coverage` は過剰であり、selected contracts に対して guardrail chain を維持した `contract-kernel` が minimum sufficient profile である。なお、streaming 実装可否と wrapper 進化の non-breaking / breaking 判断が `runtime-contract-kernel` だけで安全に固定できない場合に限り、`implementation-contract-kernel` を追加するのが最小追加プロセスであり、初手から `standard-slice` や `full-coverage` に広げる必要はない。

## High-risk boundaries

| Boundary | Producer | Consumer | Mechanism | Risk type |
| --- | --- | --- | --- | --- |
| SDK streaming contract boundary | `GitHub.Copilot.SDK` session lifecycle / `SessionEvent` / `session.event` notification | `GitHubCopilotSdkWrapper` | `CreateSessionAsync`, `On(Action<SessionLifecycleEvent>)`, `On(string, Action<SessionLifecycleEvent>)`, `SendAndWaitAsync`, SDK trace translation | SDK public surface ambiguity, delta/final mismatch, disconnected semantics mismatch |
| Wrapper API evolution boundary | `GitHubCopilotSdkWrapper` | `GitHubCopilotChatClient`, `DefaultCopilotSdkWrapper`, mock/stub wrappers, DI registrations | `ICopilotSdkWrapper` method surface, return types, additional lifecycle/event contract | Breaking/non-breaking contract mismatch, substitute-only success |
| Lifecycle logging and correlation boundary | `GitHubCopilotChatClient` and `GitHubCopilotSdkWrapper` | `ILogger`, `ChatTelemetry`, provider consumers | TraceId / RequestId propagation, stage-based structured logging, SDK trace translation | Correlation gap, stage loss, secret masking regression |
| Timeout / cancellation / disconnection boundary | Caller `CancellationToken` and `TimeoutSeconds` | wrapper session path, streaming loop, final wait path | token propagation, timeout window, session completion / event termination | Collapsed failure semantics, partial output finalization mismatch |
| Production vs test substitute boundary | `GitHubCopilotServiceExtensions` and test registrations | `GitHubCopilotSdkWrapper`, `DefaultCopilotSdkWrapper`, mock/stub wrappers | DI binding, singleton resolution, substitute interface coverage | Production binding gap, tests green on stale interface only |
| Capability advertisement boundary | `GitHubCopilotChatClient.SupportsStreaming` / `IsSupported(Streaming)` | `ProviderRegistry`, contract tests, library callers | feature advertisement and streaming entrypoint behavior | False-positive streaming claim, non-regression break |
| ProviderOverride masking / non-regression boundary | `ConversationExecutionOptions.ProviderOverride` and provider options | `BuildSdkSessionConfig`, logs, diagnostics | option mapping and structured log emission | Secret leakage, override regression, masked/unmasked inconsistency |
| Other provider isolation boundary | GitHub Copilot specific runtime/options behavior | non-Copilot providers and shared contract tests | shared abstractions (`ConversationExecutionOptions`, `IProviderCapabilities`, provider-specific tests) | Cross-provider regression, capability matrix drift |

## Selected runtime contracts to cover

| Contract ID | Boundary | What is at risk | Why selected | Triage status | Next action |
| --- | --- | --- | --- | --- | --- |
| RC-001 | SDK streaming contract boundary | SDK event source から wrapper が観測できる signal が `delta`, `progress`, `final`, `disconnected` のどれに相当するかが曖昧なままだと、実ストリーミング採用判断と final string 互換の両方が壊れる | すべての下流判断の source of truth であり、ここが未固定のままでは wrapper も logging も capability も確定できないため | Deferred | `runtime-contract-kernel` で event names / stage semantics / completion semantics / unresolved gaps を最小契約として固定する |
| RC-002 | Wrapper API evolution boundary | `ICopilotSdkWrapper` が final-text 契約のままか、追加 streaming/event 契約を持つか、または event-first へ再定義するかが曖昧なままだと、`GitHubCopilotChatClient`、`DefaultCopilotSdkWrapper`、mock/stub、DI のどれかが stale になり substitute-only success を起こす | 実装の blast radius を最も左右し、capability advertisement と production/test substitute 境界を同時に拘束するため | Deferred | `runtime-contract-kernel` で non-breaking 追加契約案を第一候補として固定し、breaking 再定義しか成立しない場合だけ `implementation-contract-kernel` を追加して interface realization を明文化する |
| RC-003 | Lifecycle logging and correlation boundary | request accepted から first SDK event / first delta / final / timeout / cancellation / disconnected / exception wrap までを `ChatTelemetry.TraceId` / `RequestId` と結び付けられないと、diagnostics 強化の目的が達成できず、`ProviderOverride` の masking も stage ごとにぶれる | user-visible diagnostics の中心であり、timeout/cancellation/disconnection を別 failure として扱うための最小契約でもあるため | Deferred | `runtime-contract-kernel` で stage taxonomy、correlation propagation、masking boundary、timeout/cancellation/disconnected の distinguish 条件を固定する |

## Candidate runtime contracts not selected

| Contract ID | Boundary | Why not selected | Candidate status | Suggested next action |
| --- | --- | --- | --- | --- |
| RC-C01 | Capability advertisement boundary | `SupportsStreaming` / `IsSupported(Streaming)` の是正は RC-002 の wrapper contract と RC-001 の source semantics が固まれば従属的に決まるため、独立 contract としては選ばない | OutOfScopeForThisPass | `test-design-kernel` で RC-001/RC-002 の派生 test point として扱う |
| RC-C02 | ProviderOverride masking / non-regression boundary | `ProviderOverride` 自体の option passthrough は既存 test があり、今回の本質リスクは logging/correlation stage との統合なので RC-003 に従属すると判断した | OutOfScopeForThisPass | `test-design-kernel` で RC-003 の派生 point として secret masking / non-secret field logging を確認する |
| RC-C03 | Other provider isolation boundary | 共有 abstraction は触る可能性があるが、observable behavior の主変更は GitHub Copilot provider 内に閉じており、既存 provider-specific tests が isolation をある程度担保しているため | OutOfScopeForThisPass | 実装後に `verification-kernel` で capability matrix と provider-specific regression を確認する |

## Risk trigger scan

| Risk trigger | Present / Absent / Unclear | Notes |
| --- | --- | --- |
| Cross-process or cross-service sequence | Present | `GitHubCopilotSdkWrapper` は `GitHub.Copilot.SDK` client / session を介し、CLI / RPC 的な外部 runtime participant を含む可能性が高い |
| Queue / event / webhook / background worker | Absent | queue / webhook / background worker はない。event は SDK session lifecycle に閉じる |
| External API or SDK | Present | `GitHub.Copilot.SDK` 0.2.1-preview.1 の public surface 候補を利用する |
| Authentication or authorization | Absent | 認証方式追加は scope 外。secret masking は logging 境界の一部として扱う |
| Durable state / retry / replay / idempotency | Unclear | durable store は追加しないが、disconnect 後 finalization や duplicate/final event の扱いは未確定 |
| Startup wiring / DI / configuration | Present | `AddGitHubCopilot`, `AddGitHubCopilotSdkWrapper`, `DefaultCopilotSdkWrapper`, custom substitute registration に影響する |
| Production implementation split from test substitute | Present | mock/stub/default wrapper と実 SDK wrapper の interface 整合が必要 |
| Multiple runtime participants coordinating state | Present | chat client、wrapper、SDK client/session、logger/telemetry が同一 request lifecycle を協調処理する |
| Observable behavior spanning more than one component | Present | streaming capability、response path、stage logging、timeout/cancellation が複数 component を跨る |

## Suggested next agent

Immediate next agent: `runtime-contract-kernel.agent.md`

Required inputs:
- `plans/github-copilot-streaming-diagnostics-plan.md`
- `plans/github-copilot-streaming-diagnostics-change-risk-triage.md`
- `src/MeAiUtility.MultiProvider.GitHubCopilot/GitHubCopilotChatClient.cs`
- `src/MeAiUtility.MultiProvider.GitHubCopilot/Abstractions/ICopilotSdkWrapper.cs`
- `src/MeAiUtility.MultiProvider.GitHubCopilot/GitHubCopilotSdkWrapper.cs`
- `src/MeAiUtility.MultiProvider.GitHubCopilot/Configuration/GitHubCopilotServiceExtensions.cs`
- `src/MeAiUtility.MultiProvider/Telemetry/ChatTelemetry.cs`
- `tests/MeAiUtility.MultiProvider.GitHubCopilot.Tests/GitHubCopilotChatClientTests.cs`
- `tests/MeAiUtility.MultiProvider.GitHubCopilot.Tests/GitHubCopilotSdkWrapperTests.cs`
- `tests/MeAiUtility.MultiProvider.GitHubCopilot.Tests/ConfigurationTests/GitHubCopilotServiceExtensionsTests.cs`
- `tests/MeAiUtility.MultiProvider.GitHubCopilot.Tests/ProviderOverrideTests.cs`
- `tests/MeAiUtility.MultiProvider.IntegrationTests/ContractTests/CapabilityMatrixTests.cs`
- `tests/MeAiUtility.MultiProvider.IntegrationTests/ContractTests/ProviderSpecificTests.cs`

Minimum required downstream flow:
1. `runtime-contract-kernel` で RC-001〜RC-003 の runtime contract identification を固定する。
2. `runtime-contract-kernel` で participant / boundary mapping を固定する。
3. `runtime-contract-kernel` で test point mapping の起点と stub / fake / mock / in-memory 使用点を識別する。
4. RC-002 について、non-breaking 追加契約で安全に binding できる証拠が不足し、breaking / non-breaking 判断または wrapper realization shape が unresolved の場合のみ `implementation-contract-kernel` を追加する。
5. `test-design-kernel` で RC-001〜RC-003 と RC-C01〜RC-C03 の派生 test points を束ねる。
6. `implementation-handoff-review` で selected contracts と production binding requirement を接続し、未解決は explicit unresolved status で残す。

Selected high-risk contract ごとの required downstream guardrails:
1. RC-001: runtime contract identification → runtime participant and boundary mapping → test point mapping → stub / fake / mock / in-memory usage identification → production implementation binding → production wiring / entrypoint verification → explicit unresolved status。
2. RC-002: runtime contract identification → runtime participant and boundary mapping → test point mapping → stub / fake / mock / in-memory usage identification → production implementation binding → production wiring / entrypoint verification → explicit unresolved status。
3. RC-003: runtime contract identification → runtime participant and boundary mapping → test point mapping → stub / fake / mock / in-memory usage identification → production implementation binding → production wiring / entrypoint verification → explicit unresolved status。

## Out of scope for this triage

- 実装コード、テストコード、既存 plan artifact の更新
- GitHub.Copilot.SDK の内部実装 reverse engineering
- GitHub Copilot 以外の provider 実装詳細の再分析
- full runtime evidence、PlantUML、full integration test design の作成
- opt-in E2E の実行可否判断や本番資格情報前提の確認

## Handoff Packet

- Profile used: triage-only
- Source artifacts: ユーザー要求本文、`plans/github-copilot-streaming-diagnostics-plan.md`、ユーザー提示の GitHub.Copilot.SDK 0.2.1-preview.1 public surface 候補調査結果
- Selected contracts / IDs: RC-001, RC-002, RC-003
- Files inspected: `plans/github-copilot-streaming-diagnostics-plan.md`, `src/MeAiUtility.MultiProvider.GitHubCopilot/Abstractions/ICopilotSdkWrapper.cs`, `src/MeAiUtility.MultiProvider.GitHubCopilot/GitHubCopilotChatClient.cs`, `src/MeAiUtility.MultiProvider.GitHubCopilot/GitHubCopilotSdkWrapper.cs`, `src/MeAiUtility.MultiProvider.GitHubCopilot/Configuration/GitHubCopilotServiceExtensions.cs`, `src/MeAiUtility.MultiProvider/Telemetry/ChatTelemetry.cs`, `tests/MeAiUtility.MultiProvider.GitHubCopilot.Tests/GitHubCopilotChatClientTests.cs`, `tests/MeAiUtility.MultiProvider.GitHubCopilot.Tests/GitHubCopilotSdkWrapperTests.cs`, `tests/MeAiUtility.MultiProvider.GitHubCopilot.Tests/ConfigurationTests/GitHubCopilotServiceExtensionsTests.cs`, `tests/MeAiUtility.MultiProvider.GitHubCopilot.Tests/ProviderOverrideTests.cs`, `tests/MeAiUtility.MultiProvider.IntegrationTests/ContractTests/CapabilityMatrixTests.cs`, `tests/MeAiUtility.MultiProvider.IntegrationTests/ContractTests/ProviderSpecificTests.cs`
- Files intentionally not inspected: `src/MeAiUtility.MultiProvider.OpenAI/**`、`src/MeAiUtility.MultiProvider.AzureOpenAI/**`、`src/MeAiUtility.MultiProvider.CodexAppServer/**`（scope 外）、`tests/MeAiUtility.MultiProvider.IntegrationTests/E2ETests/GitHubCopilotOptInE2ETests.cs`（opt-in E2E 詳細は triage の最小判断に不要）、GitHub.Copilot.SDK package 内部コード全体（public surface 候補以上の reverse engineering は scope 外）
- Decisions made: 推奨 profile は `contract-kernel`、selected contracts は 3 件に限定、capability advertisement / ProviderOverride masking / other provider isolation は派生 contract として非選択、`implementation-contract-kernel` は RC-002 の unresolved 時のみ条件付き追加
- Do not redo unless new evidence appears: 現行 `ICopilotSdkWrapper` は final string 契約のみ、現行 `GitHubCopilotChatClient.GetStreamingResponseAsync` は疑似ストリーミング、`GitHubCopilotSdkWrapper` には SDK trace 翻訳と `session.event` 解析基盤が既にある、`ChatTelemetry` は `TraceId` / `RequestId` を持つ既存起点である、DI では default wrapper と SDK wrapper の差し替え経路が既に存在する
- Remaining work: RC-001 の stage semantics 固定、RC-002 の wrapper shape 固定、RC-003 の stage logging / correlation / masking 契約固定、RC-002 が breaking 判断を要する場合の `implementation-contract-kernel` 実施可否判定
- Recommended next step: `runtime-contract-kernel.agent.md` を実行し、RC-001〜RC-003 を対象に narrow contract artifact を作成する。その結果 RC-002 が unresolved の場合のみ `implementation-contract-kernel` を追加し、その後 `test-design-kernel` → `implementation-handoff-review` へ進む
- Required downstream guardrails: RC-001〜RC-003 の各 contract について runtime contract identification、participant / boundary mapping、test point mapping、stub / fake / mock / in-memory usage identification、production implementation binding、production wiring / entrypoint verification、未完了項目の explicit unresolved status を保持する