# Runtime Contract Kernel

## Scope

入力: `plans/codex-app-server-plan.md`、`plans/codex-app-server-provider-change-risk-triage.md`、および `codex app-server generate-json-schema` 出力（`D:\Temp\codex-app-server-json\`）。

対象: selected contracts RC-001、RC-002、RC-003。

---

## Runtime Contract Kernel

| Contract ID | Scenario | Producer | Consumer | Message / API / Event | Required fields | Error / timeout behavior | Production implementation address | Verification hook |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| RC-001-a | セッション開始 — initialize | `CodexRpcSession` | `codex app-server` (stdin) | JSON-RPC request: method `"initialize"` | `id` (int), `method: "initialize"`, `params.clientInfo.name` (string), `params.clientInfo.version` (string) | サーバーが initialize に応答しない場合: `TimeoutSeconds` で linked CTS が発火し `OperationCanceledException`。プロセス起動失敗: `StdioCodexTransport.StartAsync` が `InvalidOperationException` をスロー。 | `CodexRpcSession` (未実装)。serialize: `System.Text.Json`. transport write: `StdioCodexTransport.SendLineAsync` (未実装) | to be assigned by test-design-kernel |
| RC-001-b | セッション開始 — initialized notification (client→server) | `CodexRpcSession` | `codex app-server` (stdin) | JSON-RPC notification: method `"initialized"` | `method: "initialized"` (params なし) | 送信失敗: `StdioCodexTransport.SendLineAsync` が `IOException` をスロー → caller に伝播。 | `CodexRpcSession` (未実装)。`ClientNotification` に `initialized` のみ定義。 | to be assigned by test-design-kernel |
| RC-001-c | thread 開始 — thread/start | `CodexRpcSession` | `codex app-server` (stdin) | JSON-RPC request: method `"thread/start"` | `id` (int), `method: "thread/start"`, `params.model` (string\|null), `params.cwd` (string\|null), `params.approvalPolicy` ("never"\|"on-request"\|"on-failure"\|"untrusted"\|null), `params.sandbox` ("read-only"\|"workspace-write"\|"danger-full-access"\|null), `params.serviceName` (string\|null), `params.personality` ("none"\|"friendly"\|"pragmatic"\|null) | サーバーエラー応答: `{id, error: {code, message}}` → `ProviderException` をスロー。`threadId` を取得できない場合は後続の `turn/start` が送信不可。 | `CodexRpcSession` (未実装)。response の `result.thread.id` を `_threadId` に保存。 | to be assigned by test-design-kernel |
| RC-001-d | turn 開始 — turn/start | `CodexRpcSession` | `codex app-server` (stdin) | JSON-RPC request: method `"turn/start"` | `id` (int), `method: "turn/start"`, `params.threadId` (string, required), `params.input` (array of `{type: "text", text: string}`, required), `params.model` (string\|null), `params.effort` ("none"\|"minimal"\|"low"\|"medium"\|"high"\|"xhigh"\|null), `params.approvalPolicy` (AskForApproval\|null), `params.sandboxPolicy` (SandboxPolicy object\|null), `params.cwd` (string\|null), `params.summary` ("auto"\|"concise"\|"detailed"\|"none"\|null), `params.personality` (Personality\|null) | サーバーエラー応答 → `ProviderException` をスロー。 | `CodexRpcSession` (未実装)。`ReasoningEffortLevel.Low/Medium/High` → `"low"/"medium"/"high"` にマップ。 | to be assigned by test-design-kernel |
| RC-001-e | `networkAccess` の送信方法 — sandboxPolicy 構造 | `CodexRpcSession` | `codex app-server` stdin | `turn/start` の `params.sandboxPolicy` フィールド (SandboxPolicy object) | `sandboxPolicy: {type: "workspaceWrite", networkAccess: false}` または `{type: "readOnly", networkAccess: false}` または `{type: "dangerFullAccess"}`. `networkAccess` は `workspaceWrite` / `readOnly` variant の子フィールド (bool). `externalSandbox` variant では `networkAccess: "restricted"\|"enabled"` (string). | 不正な sandboxPolicy: サーバーが JSON-RPC error で応答。 | `CodexRpcSession` (未実装)。**重要**: `ThreadStartParams.sandbox` は SandboxMode 文字列 (`"workspace-write"` 等) のみ。`networkAccess` は `ThreadStartParams` には存在しない。`turn/start` の `sandboxPolicy` オブジェクトで指定する。 | to be assigned by test-design-kernel |
| RC-002-a | turn 中の delta 蓄積 — item/agentMessage/delta | `codex app-server` (stdout) | `CodexRpcSession` ReadLoop | JSON-RPC notification: method `"item/agentMessage/delta"` | `method: "item/agentMessage/delta"`, `params.delta` (string, required), `params.itemId` (string, required), `params.threadId` (string, required), `params.turnId` (string, required) | notification parse 失敗: ReadLoop が `JsonException` を catch → pending TCS を全て fault → caller に `ProviderException` 伝播。 | `CodexRpcSession.ReadLoop` (未実装)。`itemId` 単位で `Dictionary<string, StringBuilder>` に蓄積。`FakeCodexTransport` での script 注入で検証可能。 | to be assigned by test-design-kernel |
| RC-002-b | turn 完了 — turn/completed (status: completed\|interrupted\|failed) | `codex app-server` (stdout) | `CodexRpcSession` ReadLoop | JSON-RPC notification: method `"turn/completed"` | `method: "turn/completed"`, `params.threadId` (string, required), `params.turn.id` (string, required), `params.turn.status` (TurnStatus: `"completed"\|"interrupted"\|"failed"\|"inProgress"`, required), `params.turn.error` (TurnError: `{message, codexErrorInfo?, additionalDetails?}`, populated when status is `"failed"`) | `status == "failed"` → `ProviderException(turn.error.message)` をスロー。`status == "interrupted"` → `OperationCanceledException` をスロー（キャンセルとして扱う）。`status == "completed"` → delta 蓄積テキストを返す。**turn/failed という別 notification は存在しない**。 | `CodexRpcSession` (未実装)。注意: `error` ServerNotification (`willRetry: true`) は retryable error であり turn/completed とは別。 | to be assigned by test-design-kernel |
| RC-002-c | retryable error — error notification | `codex app-server` (stdout) | `CodexRpcSession` ReadLoop | JSON-RPC notification: method `"error"` | `method: "error"`, `params.error.message` (string, required), `params.error.codexErrorInfo` (optional), `params.threadId`, `params.turnId`, `params.willRetry` (bool, required) | `willRetry == true`: 無視して継続（サーバーが retry する）。`willRetry == false`: `ProviderException` をスロー。 | `CodexRpcSession.ReadLoop` (未実装) | to be assigned by test-design-kernel |
| RC-002-d | EOF / プロセス終了 → pending request fault | `codex app-server` プロセス終了 or stdout EOF | `CodexRpcSession` ReadLoop | stdout EOF (no JSON-RPC message) | (なし) | ReadLoop が EOF / `IOException` を検知したら、全 pending `TaskCompletionSource` を `InvalidOperationException("Codex process exited unexpectedly")` で fault させて ReadLoop を終了する。外部タイムアウトで linked CTS が cancel された場合も同様に cleanup。 | `CodexRpcSession.ReadLoop` (未実装)。`StdioCodexTransport.ReadLinesAsync` の EOF 検知に依存。 | to be assigned by test-design-kernel |
| RC-002-e | 承認要求 — ServerRequest (approval) | `codex app-server` (stdout) | `CodexRpcSession` ReadLoop | JSON-RPC request (id あり): method `"item/commandExecution/requestApproval"`, `"item/fileChange/requestApproval"`, `"item/permissions/requestApproval"` | `id` (RequestId), `method` (上記のいずれか), `params` (各 ApprovalParams) | `AutoApprove == true`: 各メソッドに対応した response `{id, result: {decision: "acceptForSession"}}` を返して継続。`AutoApprove == false`: `{id, result: {decision: "cancel"}}` を返し、`ProviderException("Approval requested: {method}")` をスロー。 | `CodexRpcSession.ReadLoop` (未実装)。**approvalPolicy = "never" が既定のため通常は発生しない**。 | to be assigned by test-design-kernel |
| RC-002-f | 承認待ち状態通知 — thread/status/changed | `codex app-server` (stdout) | `CodexRpcSession` ReadLoop | JSON-RPC notification: method `"thread/status/changed"` | `method: "thread/status/changed"`, `params.threadId` (string), `params.status.type` ("active"\|"idle"\|"notLoaded"\|"systemError"), `params.status.activeFlags` (array of "waitingOnApproval"\|"waitingOnUserInput", populated when type == "active") | `activeFlags` に `"waitingOnApproval"` が含まれる場合: 対応する ServerRequest が来るまで待機（RC-002-e が到着する）。`"waitingOnUserInput"` → `ProviderException("User input required")` をスロー。 | `CodexRpcSession.ReadLoop` (未実装) | to be assigned by test-design-kernel |
| RC-003-a | DI wiring — MultiProviderOptions.Validate() | `ProviderConfigurationExtensions.AddMultiProviderChat()` (PostConfigure) | `MultiProviderOptions.Validate()` | `IOptions<MultiProviderOptions>.PostConfigure` callback | `Provider == "CodexAppServer"` かつ `CodexAppServer != null` のとき valid。それ以外は `InvalidOperationException` をスロー。 | `Provider == "CodexAppServer"` で `CodexAppServer` section がない場合: 起動時に `InvalidOperationException("Provider 'CodexAppServer' is invalid or missing provider-specific section.")` をスロー。 | `src/MeAiUtility.MultiProvider/Options/MultiProviderOptions.cs` — `Validate()` の switch 式に `"CodexAppServer" => CodexAppServer is not null` を追加する必要あり（現時点では `_ => false` で invalid 判定）。 | to be assigned by test-design-kernel |
| RC-003-b | DI wiring — ProviderFactory.DiscoverProviderType / ProviderRegistry | `ProviderFactory.Create()` | `serviceProvider.GetService(typeof(CodexAppServerChatClient))` | DI コンテナの型解決 (`serviceProvider.GetService(implementationType)`) | `options.Value.Provider == "CodexAppServer"` のとき、`implementationType == typeof(CodexAppServerChatClient)` に解決される必要がある。現在の `DiscoverProviderType` の `_` 分岐は provider 名と同名の型を全アセンブリから検索するため、`"CodexAppServer"` は `CodexAppServerChatClient` にはマッチしない（名前が違う）。明示的な `"CodexAppServer" => "CodexAppServerChatClient"` mapping が必要。 | DI 解決失敗: `InvalidOperationException("Provider 'CodexAppServer' could not be resolved.")` を起動時にスロー。 | `src/MeAiUtility.MultiProvider/Configuration/ProviderFactory.cs` — `DiscoverProviderType` の switch に `"CodexAppServer" => "CodexAppServerChatClient"` を追加 (L40-47)。 | to be assigned by test-design-kernel |
| RC-003-c | DI wiring — AddCodexAppServer Singleton 登録 | `CodexAppServerServiceExtensions.AddCodexAppServer()` | `serviceProvider.GetService(typeof(CodexAppServerChatClient))` | `services.AddSingleton<CodexAppServerChatClient>()` | `CodexAppServerChatClient` が Singleton として DI に登録されること。`ICodexTransportFactory` が Singleton として登録されること。`CodexAppServerProviderOptions` が Singleton として登録されること。 | 登録漏れ: RC-003-b の `serviceProvider.GetService(typeof(CodexAppServerChatClient))` が `null` を返す → `InvalidOperationException`。 | `src/MeAiUtility.MultiProvider.CodexAppServer/Configuration/CodexAppServerServiceExtensions.cs` (未実装)。パターンは `src/MeAiUtility.MultiProvider.GitHubCopilot/Configuration/GitHubCopilotServiceExtensions.cs` を参照。 | to be assigned by test-design-kernel |
| RC-003-d | ExtensionParameters — "codex" prefix の許可 | `ExtensionParameters.Set("codex.workingDirectory", ...)` | `ExtensionParameters.ValidateKey()` | `ExtensionParameters.AllowedPrefixes` (HashSet) | `AllowedPrefixes` に `"codex"` が含まれること。 | `AllowedPrefixes` に "codex" がない場合: `Set("codex.*", ...)` 呼び出しで `ArgumentException("Unsupported provider prefix 'codex'.")` がスローされる。 | `src/MeAiUtility.MultiProvider/Options/ExtensionParameters.cs` L5 — `AllowedPrefixes` の初期化リストに `"codex"` を追加する必要あり。 | to be assigned by test-design-kernel |

---

## Schema corrections to Plan draft (confirmed from generate-json-schema output)

以下は Plan の「未解決事項」に対して schema 確認により確定した内容。

1. **`sandbox` フィールド名 (ThreadStartParams)**: `ThreadStartParams.sandbox` の型は `SandboxMode` string enum。値は `"read-only"`、`"workspace-write"`、`"danger-full-access"`（ケバブケース）。Plan の `"workspaceWrite"` は誤り。`thread/start` に `networkAccess` フィールドは存在しない。
2. **`networkAccess` の配置**: `networkAccess` は `turn/start` の `sandboxPolicy` オブジェクト内に存在する（例: `{type: "workspaceWrite", networkAccess: false}`）。`thread/start.ThreadStartParams` には存在しない。
3. **`effort` string 値**: `ReasoningEffort` enum: `"none"`, `"minimal"`, `"low"`, `"medium"`, `"high"`, `"xhigh"`。既存 `ReasoningEffortLevel.Low/Medium/High` → `"low"/"medium"/"high"` で問題なし。
4. **`item/agentMessage/delta` フィールド名**: `delta` (string), `itemId` (string), `threadId` (string), `turnId` (string) — 全て確認済み。
5. **`turn/failed` 通知は存在しない**: 失敗は `turn/completed` の `turn.status == "failed"` で表現され、`turn.error` が populated される。
6. **承認 ServerRequest の method 名**: `"item/commandExecution/requestApproval"`、`"item/fileChange/requestApproval"`、`"item/permissions/requestApproval"`。Plan の「承認 method 名未確認」は解消。
7. **承認 response の `decision` 値**: `"accept"` / `"acceptForSession"` / `"decline"` / `"cancel"` (`acceptForSession` が AutoApprove=true 時の推奨値)。
8. **JSON-RPC envelope**: `jsonrpc: "2.0"` フィールドはスキーマに定義されていない（サーバーが省略する）。`id` は string または int64。

---

## Notes / assumptions

### 仮定・注意事項

1. **`thread/start` の `sandbox` フィールドは per-thread 設定のみ**。`networkAccess` を per-call で制御したい場合は `turn/start.sandboxPolicy` オブジェクトを使う。MVP の設計では `thread/start` で sandbox mode を設定し、`turn/start` では `sandboxPolicy` による override を行う。これにより `CodexAppServerProviderOptions.NetworkAccess` は `turn/start.sandboxPolicy` にマップされることに注意。

2. **`ProviderFactory.DiscoverProviderType` の fallback 動作**: 現在の実装 (L49-51) は `t.Name.Contains(provider, ...)` で fallback マッチする。`"CodexAppServer"` は `"CodexAppServerChatClient"` に Contains マッチするため、明示的な mapping (`"CodexAppServer" => "CodexAppServerChatClient"`) を追加しなくても解決できる可能性がある。ただし、**明示的なマッピングを追加することを強く推奨**する（将来の class rename に対してより堅牢、テストで確認が容易）。

3. **`CodexAppServerChatClient` のコンストラクタ設計矛盾**: Plan draft の `CodexAppServerChatClient(ICodexTransport transport)` という記述は誤り。DI 設計方針（Singleton + ICodexTransportFactory）に基づき、コンストラクタは `CodexAppServerChatClient(CodexAppServerProviderOptions options, ICodexTransportFactory factory, ILogger<CodexAppServerChatClient> logger)` とすべきである（実装前に Plan を修正すること）。

4. **`error` notification の `willRetry` 処理**: `willRetry: true` はサーバーが内部で retry するため、クライアントは `error` notification を受け取っても `turn/completed` を待ち続けるべき。ただし retry 回数上限後は `turn/completed (status: failed)` が来る。

5. **`thread/status/changed` (waitingOnApproval) と ServerRequest の関係**: `waitingOnApproval` 状態通知と `item/commandExecution/requestApproval` 等の ServerRequest は概念的に対応するが、ReadLoop はこれらを独立して処理する必要がある（状態通知の有無に依存せず、ServerRequest が来たら応答する）。

6. **`CodexAppServerChatClient.GetStreamingResponseAsync` と `ICodexTransportFactory`**: 各 `GetStreamingResponseAsync` 呼び出しでも factory から新しい transport / process を生成する。streaming の `try/finally` で確実に `process.Kill()` する。RC-005 は今 pass では扱わない。

7. **SandboxMode の文字列値がプランと異なる**:
   - 正しい値: `"read-only"`, `"workspace-write"`, `"danger-full-access"` (ケバブケース)
   - Plan の設定例 (`"workspaceWrite"`) と appsettings の候補は誤り。オプション型で文字列変換が必要か、enum を使う場合は `[JsonPropertyName]` で対処する。
   - `CodexAppServerProviderOptions.SandboxMode` の既定値は `"workspace-write"` にすること。

### エスカレーション不要の判断

- RC-001〜RC-003 は selected contracts の範囲内で contract identification と participant/boundary mapping が完結している。複雑な相互依存はあるが、kernel 表で因果関係を伝えられる。PlantUML sequence diagram へのエスカレーションは不要。

---

## Handoff Packet

- Profile used: contract-kernel
- Source artifacts:
  - `plans/codex-app-server-plan.md`
  - `plans/codex-app-server-provider-change-risk-triage.md`
  - `D:\Temp\codex-app-server-json\v2\ThreadStartParams.json` (schema)
  - `D:\Temp\codex-app-server-json\v2\TurnStartParams.json` (schema)
  - `D:\Temp\codex-app-server-json\v2\AgentMessageDeltaNotification.json` (schema)
  - `D:\Temp\codex-app-server-json\v2\TurnCompletedNotification.json` (schema — includes Turn, TurnStatus, TurnError definitions)
  - `D:\Temp\codex-app-server-json\v2\ThreadStatusChangedNotification.json` (schema)
  - `D:\Temp\codex-app-server-json\v2\ErrorNotification.json` (schema)
  - `D:\Temp\codex-app-server-json\ServerRequest.json` (schema — ServerRequest methods confirmed)
  - `D:\Temp\codex-app-server-json\CommandExecutionRequestApprovalResponse.json` (schema)
  - `D:\Temp\codex-app-server-json\FileChangeRequestApprovalResponse.json` (schema)
  - `D:\Temp\codex-app-server-json\ClientNotification.json` (schema — "initialized" only)
  - `D:\Temp\codex-app-server-json\v1\InitializeParams.json` (schema)
  - `D:\Temp\codex-app-server-json\v1\InitializeResponse.json` (schema)
  - `D:\Temp\codex-app-server-json\JSONRPCMessage.json` (schema)
  - `src/MeAiUtility.MultiProvider/Configuration/ProviderFactory.cs`
  - `src/MeAiUtility.MultiProvider/Configuration/ProviderConfigurationExtensions.cs`
  - `src/MeAiUtility.MultiProvider/Options/MultiProviderOptions.cs`
  - `src/MeAiUtility.MultiProvider/Options/ExtensionParameters.cs`
- Selected contracts / IDs: RC-001 (a-e), RC-002 (a-f), RC-003 (a-d)
- Files inspected: 上記 Source artifacts に記載
- Files intentionally not inspected:
  - `D:\Temp\codex-app-server-json\codex_app_server_protocol.schemas.json` / `v2.schemas.json` (大きなバンドル。個別ファイルで十分)
  - `D:\Temp\codex-app-server-json\v2\ThreadStartResponse.json` 冒頭以外 (threadId の取得方法確認は thread.id フィールドで十分)
  - `D:\Temp\codex-app-server-json\v2\FuzzyFileSearch*.json` 等 (今回の scope 外)
  - 既存 OpenAI / AzureOpenAI / GitHubCopilot プロバイダーの実装詳細 (変更なし)
- Decisions made:
  - `sandbox` string 値はケバブケース確定（"workspace-write", "read-only", "danger-full-access"）
  - `networkAccess` は `turn/start.sandboxPolicy` 内に配置 — `thread/start` には存在しない
  - `turn/failed` notification は存在しない — `turn/completed.turn.status == "failed"` で判定
  - 承認 method 名確定: `"item/commandExecution/requestApproval"`, `"item/fileChange/requestApproval"`, `"item/permissions/requestApproval"`
  - AutoApprove=true 時の response decision: `"acceptForSession"` を使用（session-scoped cache で以降の承認をスキップ）
  - `ProviderFactory.DiscoverProviderType` に明示的な `"CodexAppServer" => "CodexAppServerChatClient"` mapping を追加することを推奨
  - `CodexAppServerProviderOptions.SandboxMode` の既定値は `"workspace-write"` (ケバブケース)
- Do not redo unless new evidence appears:
  - `AgentMessageDeltaNotification` のフィールド名: `delta`, `itemId`, `threadId`, `turnId` — 全て required として確認済み
  - `TurnStatus` enum 値: `"completed"`, `"interrupted"`, `"failed"`, `"inProgress"` — 確認済み
  - `ReasoningEffort` enum 値: `"none"`, `"minimal"`, `"low"`, `"medium"`, `"high"`, `"xhigh"` — 確認済み
  - `AskForApproval` string 値: `"untrusted"`, `"on-failure"`, `"on-request"`, `"never"` — 確認済み (`"on-failure"` は Plan に含まれていなかったが valid な値)
  - ClientNotification は `"initialized"` のみ — client が送る notification はこれのみ
- Remaining work:
  - `turn/start.sandboxPolicy` を使って `networkAccess` を設定する場合と `thread/start.sandbox` のみを使う場合の使い分けを実装 agent が決定する必要がある (MVP では `thread/start.sandbox` で SandboxMode を設定し、`networkAccess` は `turn/start.sandboxPolicy` で個別指定する設計を推奨するが、最終決定は実装 agent に委ねる)
  - `ErrorNotification.willRetry` の retry 最大回数・タイムアウトとの兼ね合いは実装時に確認する (OutOfScopeForThisPass)
  - `item/permissions/requestApproval` の response schema は `PermissionsRequestApprovalResponse.json` を確認すること (NotImplementedOrMismatch — 今 pass では読んでいない)
  - RC-005 (streaming Channel bridge cleanup) は RC-002 解決後の concern として Deferred
  - RC-004 (production binding — StdioCodexTransport / SystemCodexProcessRunner) は RC-001/RC-002 後続として OutOfScopeForThisPass
- Recommended next step: `test-design-kernel.agent.md` を実行し RC-001〜RC-003 の test point を割り当てる。入力として本 artifact と Plan を渡す。
