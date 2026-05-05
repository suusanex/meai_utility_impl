# Change Risk Triage

## Recommended profile

`contract-kernel`

## Reasoning

この変更は新規プロバイダー追加（単一パッケージ・単一 `IChatClient` 実装）であり、scope は明確に絞られている。しかし以下の理由から `triage-only` 止まりや `fix-slice` には不適切で、`contract-kernel` が最小十分な profile と判断した。

1. **外部プロセスとの cross-process JSON-RPC 通信**: `codex app-server` とのプロトコルフィールド名（`thread/start` の networkAccess 配置、`turn/start` の effort フィールド名、`item/agentMessage/delta` の itemId フィールド名）がいずれも Plan の「未解決事項」として明示されており、`FakeCodexTransport` を使った unit test では検出できない contract mismatch が存在する。
2. **Production 実装と test substitute の乖離**: 全 unit test は `FakeCodexTransport` を経由し、`StdioCodexTransport` + `SystemCodexProcessRunner`（実プロセス起動）の production code path は integration test （optional / manual-only）でしかカバーされない。
3. **DI wiring の 3 箇所同時変更**: `MultiProviderOptions.Validate()`、`ProviderFactory.DiscoverProviderType()`、`ExtensionParameters.AllowedPrefixes` を既存コアパッケージ側でそれぞれ変更する必要があり、いずれか 1 箇所の抜け漏れで起動時エラーになる。
4. **`CodexAppServerChatClient` 設計の内部不整合**: Plan の `CodexAppServerChatClient` コンストラクタ例示（line 226-228）が `ICodexTransport transport` を受け取る旧バージョンのまま残っており、DI 設計方針セクション（Singleton + `ICodexTransportFactory`）と矛盾している。

一方で変更の breadth は狭く（他プロバイダーへの影響なし、独立した新プロジェクト）、`standard-slice` / `full-coverage` は不要と判断した。

---

## High-risk boundaries

| Boundary | Producer | Consumer | Mechanism | Risk type |
| --- | --- | --- | --- | --- |
| B-01 | `CodexRpcSession` (via `StdioCodexTransport`) | `codex app-server` subprocess stdin | stdio JSONL / JSON-RPC 2.0 | Contract mismatch — フィールド名未確認のまま実装すると silent ignore またはサーバーエラー。FakeTransport では検出不能。 |
| B-02 | `codex app-server` subprocess stdout | `CodexRpcSession` ReadLoop | stdio JSONL / JSON-RPC 2.0 notifications | Contract mismatch — `item/agentMessage/delta` の `itemId` フィールド名、`turn/completed` / `turn/failed` の event shape が未確認。 |
| B-03 | `AddMultiProviderChat()` → `ProviderFactory.Create()` → `serviceProvider.GetService(type)` | `CodexAppServerChatClient` (Singleton) | DI コンテナ + 型名解決 | Startup wiring — `MultiProviderOptions.Validate()`、`DiscoverProviderType()`、具象型登録の 3 点が全て揃わないと起動時エラー。 |
| B-04 | `FakeCodexTransport` (test substitute) | `StdioCodexTransport` + `SystemCodexProcessRunner` (production) | `ICodexTransportFactory.Create()` | Production binding gap — unit test は全て fake 経由。実プロセス spawn、stdin pipe 書き込み、stdout EOF 処理、kill-on-cancel が production でのみ動く。 |

---

## Selected runtime contracts to cover

| Contract ID | Boundary | What is at risk | Why selected | Triage status | Next action |
| --- | --- | --- | --- | --- | --- |
| RC-001 | B-01 (client → server) | `thread/start` params の JSON フィールド名と構造：`model`, `approvalPolicy`, `sandbox`, `networkAccess`（直接 vs `sandboxPolicy` 内）, `serviceName`; `turn/start` params の `input` shape と `effort` フィールド名 | Plan が「未解決事項」として明示。フィールド名が間違うと `codex app-server` が設定を無視しサンドボックス違反や不正モデルで動く可能性がある。FakeTransport では検出できない。 | Deferred | `runtime-contract-kernel` で contract を文書化し、`codex app-server generate-json-schema` または `openai/codex` ソースで各フィールドを確認する。 |
| RC-002 | B-02 (server → client) | `item/agentMessage/delta` notification の shape（`itemId`, `delta`, `text` フィールド名）; `turn/completed` / `turn/failed` event 名と payload; ReadLoop の EOF → pending 全 fault 経路 | Plan の「未解決事項 4」に明示。フィールド名が違うと delta 蓄積が空のまま `turn/completed` で空テキストを返す。ReadLoop の EOF fault がないと subprocess 死亡時に永遠待ちになる。 | Deferred | `runtime-contract-kernel` で notification schema を文書化し、ReadLoop の fault path を contract に含める。 |
| RC-003 | B-03 (DI wiring) | `MultiProviderOptions.Validate()` に `"CodexAppServer"` ケース追加; `ProviderFactory.DiscoverProviderType()` に `"CodexAppServer" => "CodexAppServerChatClient"` 追加; `services.AddSingleton<CodexAppServerChatClient>()` の具象型登録 | 3 箇所いずれか 1 つでも抜けると、設定は正しくても起動時 `InvalidOperationException` になる。既存 `ProviderFactoryTests` のパターンで確認可能だが、"CodexAppServer" 用のテストが存在しない。 | Deferred | `runtime-contract-kernel` で wiring contract を定義し、`AddCodexAppServer` + `AddMultiProviderChat` の end-to-end DI 解決テストを test point として記録する。 |

---

## Candidate runtime contracts not selected

| Contract ID | Boundary | Why not selected | Candidate status | Suggested next action |
| --- | --- | --- | --- | --- |
| RC-004 | B-04 (production binding) | `StdioCodexTransport` + `SystemCodexProcessRunner` の実プロセス spawn、stdin 書き込み、stdout/stderr ストリーム、kill-on-cancel の production binding gap | RC-001/RC-002 のフィールド名確認後でないと production integration test の正解判定ができないため今 pass では選ばない。重要なリスクだが RC-001/RC-002 より後続。 | OutOfScopeForThisPass | RC-001/RC-002 解決後に integration test kernel または manual test として扱う。 |
| RC-005 | B-02 → GetStreamingResponseAsync Channel bridge | `Channel<ChatResponseUpdate>` ライフサイクル、consumer 途中終了時の `try/finally` cleanup、ReadLoop ↔ Channel の coordination | RC-002 の notification shape が確定後でないと正しいテスト point が書けない。実装複雑性は高いが，RC-002 の下位 concern。 | OutOfScopeForThisPass | RC-002 解決時に streaming cleanup contract を追加する。 |
| RC-006 | Plan 内部 (`CodexAppServerChatClient` コンストラクタ記述) | Plan の `CodexAppServerChatClient` コンストラクタ例示（`ICodexTransport transport`）と DI 設計方針（`ICodexTransportFactory`）の矛盾 | production code 実装前に解消が必要だが、contract mismatch ではなく Plan 内部の記述誤り。 | OutOfScopeForThisPass | 実装開始前に Plan を修正し、`ICodexTransportFactory` 注入に統一する。 |

---

## Risk trigger scan

| Risk trigger | Present / Absent / Unclear | Notes |
| --- | --- | --- |
| Cross-process or cross-service sequence | Present | `codex app-server` subprocess との stdio JSON-RPC。initialize → thread/start → turn/start → event loop → turn/completed のシーケンス全体が cross-process。 |
| Queue / event / webhook / background worker | Present | `CodexRpcSession` 内の ReadLoop Task (background)、`Channel<ChatResponseUpdate>` (streaming bridge)、notification → TCS routing。 |
| External API or SDK | Present | `codex app-server` CLI（外部ツール）のプロトコル。フィールド名が未確認の箇所が Plan 内に 4 件明示されている。 |
| Authentication or authorization | Absent | 認証は `codex login` 済み状態に完全委任。ライブラリは認証情報を管理しない（Plan に明示）。 |
| Durable state / retry / replay / idempotency | Absent | 各呼び出しは新プロセス、新スレッド。状態永続化なし。リトライ機構なし。 |
| Startup wiring / DI / configuration | Present | `MultiProviderOptions.Validate()`、`ProviderFactory.DiscoverProviderType()`、`ExtensionParameters.AllowedPrefixes`、`AddCodexAppServer()` の 4 箇所。いずれか 1 つでも抜けると起動時失敗。 |
| Production implementation split from test substitute | Present | 全 unit test は `FakeCodexTransport` 経由。`StdioCodexTransport` + `SystemCodexProcessRunner` は integration test（optional/manual-only）でのみ動く。 |
| Multiple runtime participants coordinating state | Present | ReadLoop Task ↔ main coroutine ↔ `ConcurrentDictionary<pending>` ↔ `Channel<notification>` ↔ process stdin/stdout。cleanup/cancel 時の coordination が複雑。 |
| Observable behavior spanning more than one component | Present | `ChatResponse.Text` は `CodexAppServerChatClient` → `CodexRpcSession` → `StdioCodexTransport` → `SystemCodexProcessRunner` → `codex app-server` の 5 層を通じてのみ生成される。 |

---

## Suggested next agent

**Immediate next agent**: `runtime-contract-kernel`

**Required inputs to pass**:
- この triage document
- Plan: `C:\Users\suusa\.copilot\session-state\046969ce-097b-4cd2-9b63-aff3890f18ff\plan.md`
- Selected contract IDs: RC-001, RC-002, RC-003
- 外部仕様参照:
  - `codex app-server generate-json-schema` の出力（または `openai/codex` リポジトリの schema ファイル）
  - `https://developers.openai.com/codex/app-server`（thread/start, turn/start, notification 仕様）

**Minimum required downstream flow for each selected contract**:

RC-001, RC-002, RC-003 それぞれについて、次の chain を維持すること：

1. Runtime contract identification（具体的なフィールド名・型・方向を文書化）
2. Runtime participant and boundary mapping（producer ↔ consumer の具体的なクラス名）
3. Test point mapping（各 contract に対応する unit / integration test の観点一覧）
4. Stub / fake / mock / in-memory usage identification（FakeCodexTransport が何を cover し何を cover しないか）
5. Production implementation binding（`StdioCodexTransport` の該当コードパスを明示）
6. Production wiring / entrypoint verification（DI 登録経路の具体的な trace）
7. Explicit unresolved status（schema 確認前は `NeedsHumanDecision` または `NotImplementedOrMismatch` を明記）

---

## Out of scope for this triage

- `GetStreamingResponseAsync` の `Channel` ライフサイクル詳細（RC-002 解決後の concern）
- `SystemCodexProcessRunner` の実プロセス起動コード（production binding gap は認識したが RC-001/RC-002 後続）
- Windows 上での stdio pipe 挙動差異（外部ツールの環境依存）
- WebSocket transport（Plan が MVP 対象外と明示）
- 既存プロバイダー（OpenAI, AzureOpenAI, GitHubCopilot）への影響（変更なし）
- `README.md` のドキュメント変更（runtime risk なし）
- Plan 内部の記述矛盾（`CodexAppServerChatClient` コンストラクタ例示）の修正（実装開始前に別途処置）

---

## Handoff Packet

- **Profile used**: `contract-kernel`
- **Source artifacts**:
  - Plan: `C:\Users\suusa\.copilot\session-state\046969ce-097b-4cd2-9b63-aff3890f18ff\plan.md`
  - `https://developers.openai.com/codex/app-server`（app-server プロトコル仕様）
- **Selected contracts / IDs**: RC-001, RC-002, RC-003
- **Files inspected**:
  - `src/MeAiUtility.MultiProvider/Configuration/ProviderConfigurationExtensions.cs`
  - `src/MeAiUtility.MultiProvider/Configuration/ProviderFactory.cs`
  - `src/MeAiUtility.MultiProvider/Configuration/ProviderRegistry.cs`
  - `src/MeAiUtility.MultiProvider/Options/MultiProviderOptions.cs`
  - `src/MeAiUtility.MultiProvider/Options/ExtensionParameters.cs`
  - `src/MeAiUtility.MultiProvider/Options/ConversationExecutionOptions.cs`
  - `src/MeAiUtility.MultiProvider.GitHubCopilot/GitHubCopilotChatClient.cs`
  - `src/MeAiUtility.MultiProvider.GitHubCopilot/Options/GitHubCopilotProviderOptions.cs`
  - `src/MeAiUtility.MultiProvider.GitHubCopilot/Abstractions/ICopilotSdkWrapper.cs`
  - `src/MeAiUtility.MultiProvider.GitHubCopilot/GitHubCopilotSdkWrapper.cs` (冒頭のみ)
  - `tests/MeAiUtility.MultiProvider.Tests/Configuration/ProviderFactoryTests.cs`
- **Files intentionally not inspected**:
  - `src/MeAiUtility.MultiProvider.OpenAI/**` — 今回の変更と無関係
  - `src/MeAiUtility.MultiProvider.AzureOpenAI/**` — 今回の変更と無関係
  - `tests/MeAiUtility.MultiProvider.GitHubCopilot.Tests/**` — 既存テストパターンの参照は Triage 段階で十分
  - `tests/MeAiUtility.MultiProvider.IntegrationTests/**` — 今回の変更対象外
  - `specs/**` — Plan で十分にカバーされていると判断
- **Decisions made**:
  - `contract-kernel` を推奨（breadth は狭いが cross-process field name mismatch と production binding gap が存在するため `fix-slice` / `triage-only` より重い profile が必要）
  - RC-004（production binding）は RC-001/RC-002 後続として選択外
  - `CodexAppServerChatClient` コンストラクタ記述の矛盾は実装上のリスクだが runtime contract ではないため RC として選択せず
- **Do not redo unless new evidence appears**:
  - 既存の 4 プロバイダーへの影響なし（`MultiProviderOptions`、`ProviderFactory` の変更は追加のみ）
  - `IChatClient` Singleton 登録が `CodexAppServerChatClient` Singleton 登録を要求する点は既存コードから確認済み
  - `ProviderRegistry.ValidateCapabilities` は `FeatureName.Streaming` のみ要求 → Plan の `SupportsStreaming = true` で満たされる
- **Remaining work**:
  - `codex app-server generate-json-schema` による `thread/start` / `turn/start` / notification フィールド名の確認（RC-001, RC-002）
  - `waitingOnApproval` 受信時の自動承認 method 名確認（RC-002 の一部）
  - Plan 内の `CodexAppServerChatClient` コンストラクタ記述矛盾の修正（RC-003 実装前に必要）
- **Recommended next step**: `runtime-contract-kernel` agent に RC-001, RC-002, RC-003 を渡す
- **Required downstream guardrails**:
  - **RC-001** (client→server field mapping): runtime contract identification（各フィールドの正確な名前と型）; participant mapping（`CodexRpcSession` → `StdioCodexTransport` → stdin pipe → `codex app-server`）; test point（`thread/start` JSON と `turn/start` JSON の sent-lines assertion）; fake usage（`FakeCodexTransport.SentLines` で検証, fake は field 名 validation しない点を明記）; production binding（`StdioCodexTransport.SendLineAsync` での JSON serialize コード）; wiring verification（`StdioCodexTransport` が `DefaultCodexTransportFactory` から生成される経路）; unresolved（schema 確認前の全フィールド名を `NeedsHumanDecision` とする）
  - **RC-002** (server→client notification routing): runtime contract identification（`item/agentMessage/delta` shape, `turn/completed` shape, `turn/failed` shape）; participant mapping（`codex app-server` stdout → `StdioCodexTransport.ReadLinesAsync` → `CodexRpcSession.ReadLoop` → TCS/Channel）; test point（delta 蓄積テスト、turn/completed → ChatResponse、EOF → pending fault）; fake usage（`FakeCodexTransport` の event script 注入が production notification JSON と一致するか検証が必要な点を明記）; production binding（ReadLoop の JSON parse と routing コード）; wiring（`CodexRpcSession` が ReadLoop Task を spawn する entrypoint）; unresolved（`itemId` フィールド名、`turn/failed` payload 構造を `NeedsHumanDecision` とする）
  - **RC-003** (DI wiring): runtime contract identification（`ProviderFactory.Create()` → `serviceProvider.GetService(typeof(CodexAppServerChatClient))` の解決経路）; participant mapping（`MultiProviderOptions` → `ProviderFactory` → `ProviderRegistry` → `serviceProvider`）; test point（`AddCodexAppServer` + `AddMultiProviderChat` で `IChatClient` が `CodexAppServerChatClient` として解決されることの DI テスト）; fake usage（なし — DI テストは実装型で行う）; production binding（`AddCodexAppServer` の `services.AddSingleton<CodexAppServerChatClient>()`）; wiring verification（`MultiProviderOptions.Validate()` の `"CodexAppServer"` ケース、`DiscoverProviderType` の mapping が存在すること）; unresolved（なし — コードで確認可能だが実装前は `NotImplementedOrMismatch`）
