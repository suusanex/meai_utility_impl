# Test Design Kernel

## Scope

この artifact は **Codex App Server Provider** 追加変更の selected runtime contracts RC-001-a〜RC-001-e、RC-002-a〜RC-002-f、RC-003-a〜RC-003-d に対する test point mapping を定義する。

**入力ソース**:
- 主要入力: `plans/codex-app-server-runtime-contract-kernel.md`（Contract ID、Scenario、Error/timeout behavior、Production implementation address を取得）
- 補助入力: `plans/codex-app-server-provider-change-risk-triage.md`（RC 選定理由と境界リスク）
- 補助入力: `plans/codex-app-server-plan.md`（実装方針・コンポーネント・テスト可能性設計）
- 既存テスト規約参照: `tests/MeAiUtility.MultiProvider.GitHubCopilot.Tests/` および `tests/MeAiUtility.MultiProvider.Tests/`

**対象 Contract IDs**: RC-001-a, RC-001-b, RC-001-c, RC-001-d, RC-001-e, RC-002-a, RC-002-b, RC-002-c, RC-002-d, RC-002-e, RC-002-f, RC-003-a, RC-003-b, RC-003-c, RC-003-d

---

## Test Design Kernel

| Test Point ID | Runtime Contract ID | What to verify | Stub / fake allowed? | Production binding required? | Expected observation | Status |
| --- | --- | --- | --- | --- | --- | --- |
| TP-001-a | RC-001-a | `CodexRpcSession` が initialize 時に FakeCodexTransport に送信する JSON-RPC メッセージが `method: "initialize"` を持ち、`params.clientInfo.name` および `params.clientInfo.version` が文字列として設定されていること | Yes (`FakeCodexTransport`) | Yes | FakeTransport.SentMessages[0] を JSON デシリアライズすると `method == "initialize"` かつ `params.clientInfo.name` が空でない文字列であること | Done |
| TP-001-b | RC-001-b | initialize 応答受信後、`CodexRpcSession` が `method: "initialized"` の JSON-RPC notification を FakeCodexTransport に送信すること（params なし） | Yes (`FakeCodexTransport`) | Yes | FakeTransport.SentMessages に `method == "initialized"` の notification が存在し、`params` が null または空オブジェクトであること | Done |
| TP-001-c | RC-001-c | `CodexRpcSession` が `method: "thread/start"` を送信し、レスポンスの `result.thread.id` から threadId を取得して内部に保持すること。またサーバーエラー応答（`{id, error: {code, message}}`）に対して `ProviderException` をスローすること | Yes (`FakeCodexTransport`) | Yes | (正常) FakeTransport.SentMessages に `method == "thread/start"` の request が存在し、レスポンス scriptで返した threadId が後続 turn/start の `params.threadId` に使われること。(エラー) サーバーエラーを script で返すと GetResponseAsync が `ProviderException` をスローすること | Done |
| TP-001-d | RC-001-d | `CodexRpcSession` が `method: "turn/start"` を送信する際、`params.threadId` に thread/start で取得した threadId が設定され、`params.input` が `[{type: "text", text: <ユーザー入力>}]` 形式であること。また `ReasoningEffortLevel.High` が `"high"`、`Low` が `"low"` にマップされること | Yes (`FakeCodexTransport`) | Yes | (shape) FakeTransport.SentMessages から turn/start JSON を抽出し、`params.threadId` が thread/start レスポンスと一致し、`params.input[0].type == "text"` であること。(effort) ChatOptions で ReasoningEffortLevel.High を渡した場合、`params.effort == "high"` であること | Done |
| TP-001-e | RC-001-e | `turn/start` の送信 JSON に `params.sandboxPolicy` オブジェクトが含まれ（例: `{type: "workspaceWrite", networkAccess: false}`）、`thread/start` の JSON には `networkAccess` フィールドが存在しないこと | Yes (`FakeCodexTransport`) | Yes | (thread/start JSON) `params` に `networkAccess` キーが存在しないこと。(turn/start JSON) `params.sandboxPolicy` が存在し、`type` および `networkAccess` フィールドを持つこと。`params` の top-level に `networkAccess` キーが存在しないこと | Done |
| TP-002-a | RC-002-a | FakeCodexTransport から `method: "item/agentMessage/delta"` notification（`delta`, `itemId`, `threadId`, `turnId` 全フィールド設定）を複数送ると、GetResponseAsync が返す `ChatResponse.Text` にそれらの delta が itemId 単位で結合されていること | Yes (`FakeCodexTransport`) | Yes | FakeTransport script で delta "Hello" + " World" を同 itemId で送り、最後に turn/completed を送ると、`ChatResponse.Text == "Hello World"` であること | Done |
| TP-002-b-1 | RC-002-b | `turn/completed` の `params.turn.status == "completed"` 受信時に、GetResponseAsync が delta 蓄積テキストを含む `ChatResponse` を正常返却すること | Yes (`FakeCodexTransport`) | Yes | FakeTransport script で delta + turn/completed(status:"completed") を送ると、GetResponseAsync が例外なしで ChatResponse を返し、Text が delta 蓄積値と一致すること | Done |
| TP-002-b-2 | RC-002-b | `turn/completed` の `params.turn.status == "failed"` かつ `params.turn.error.message` が設定されている場合に、GetResponseAsync が `ProviderException` をスローし、そのメッセージに `turn.error.message` の内容が含まれること | Yes (`FakeCodexTransport`) | Yes | FakeTransport script で `turn/completed` (status:"failed", error.message:"model overload") を送ると、GetResponseAsync が `ProviderException` をスローし、`ex.Message` に "model overload" が含まれること | Done |
| TP-002-b-3 | RC-002-b | `turn/completed` の `params.turn.status == "interrupted"` 受信時に、GetResponseAsync が `OperationCanceledException` をスローすること | Yes (`FakeCodexTransport`) | Yes | FakeTransport script で `turn/completed` (status:"interrupted") を送ると、GetResponseAsync が `OperationCanceledException` をスローすること | Done |
| TP-002-c-1 | RC-002-c | `method: "error"` notification で `willRetry: true` を受信した場合、GetResponseAsync は例外をスローせず、その後の `turn/completed` (status:"completed") で正常に ChatResponse を返すこと | Yes (`FakeCodexTransport`) | Yes | FakeTransport script で error(willRetry:true) → turn/completed(status:"completed") を送ると、GetResponseAsync が例外なしで ChatResponse を返すこと | Done |
| TP-002-c-2 | RC-002-c | `method: "error"` notification で `willRetry: false` を受信した場合、GetResponseAsync が `ProviderException` をスローすること | Yes (`FakeCodexTransport`) | Yes | FakeTransport script で error(willRetry:false, error.message:"fatal") を送ると、GetResponseAsync が `ProviderException` をスローすること | Done |
| TP-002-d | RC-002-d | FakeCodexTransport が stdout EOF（ReadLinesAsync の終端）を返すと、pending 状態の GetResponseAsync が `InvalidOperationException`（またはそれをラップした `ProviderException`）をスローし、永遠に待機状態にならないこと | Yes (`FakeCodexTransport`、EOF を返す script) | Yes | FakeTransport を EOF-only script で設定して GetResponseAsync を呼ぶと、合理的な時間内（テストタイムアウト内）に例外がスローされること | Done |
| TP-002-e-1 | RC-002-e | `AutoApprove == false` の設定で `item/commandExecution/requestApproval` ServerRequest が来た場合に、FakeTransport に `{id, result: {decision: "cancel"}}` の応答が送信され、GetResponseAsync が `ProviderException` をスローすること | Yes (`FakeCodexTransport`) | Yes | FakeTransport script で ServerRequest(method:"item/commandExecution/requestApproval") を送ると、FakeTransport の SentMessages に `decision == "cancel"` の応答が含まれ、GetResponseAsync が `ProviderException` をスローすること | Done |
| TP-002-e-2 | RC-002-e | `AutoApprove == true` の設定で `item/commandExecution/requestApproval` ServerRequest が来た場合に、FakeTransport に `{id, result: {decision: "acceptForSession"}}` の応答が送信され、その後 turn/completed で正常に ChatResponse が返ること | Yes (`FakeCodexTransport`) | Yes | FakeTransport script で ServerRequest → turn/completed(status:"completed") を送ると、FakeTransport の SentMessages に `decision == "acceptForSession"` の応答が含まれ、GetResponseAsync が ChatResponse を返すこと | Done |
| TP-002-f-1 | RC-002-f | `thread/status/changed` notification で `params.status.activeFlags` に `"waitingOnUserInput"` が含まれる場合に、GetResponseAsync が `ProviderException("User input required")` をスローすること | Yes (`FakeCodexTransport`) | Yes | FakeTransport script で thread/status/changed(activeFlags:["waitingOnUserInput"]) を送ると、GetResponseAsync が `ProviderException` をスローし、メッセージに "user input" 相当の文字列が含まれること | Done |
| TP-002-f-2 | RC-002-f | `thread/status/changed` notification で `params.status.activeFlags` に `"waitingOnApproval"` が含まれる場合に、GetResponseAsync は例外をスローせず、その後に届く ServerRequest（RC-002-e）を処理できる状態を維持すること | Yes (`FakeCodexTransport`) | Yes | FakeTransport script で thread/status/changed(activeFlags:["waitingOnApproval"]) → ServerRequest(requestApproval, AutoApprove:true) → turn/completed(status:"completed") を送ると、GetResponseAsync が例外なしで ChatResponse を返すこと | Done |
| TP-003-a-1 | RC-003-a | `MultiProviderOptions { Provider = "CodexAppServer", CodexAppServer = new object() }` に対して `Validate()` が例外をスローしないこと | No（直接 `MultiProviderOptions.Validate()` を呼ぶ） | No | `Assert.That(() => options.Validate(), Throws.Nothing)` が通ること | Done |
| TP-003-a-2 | RC-003-a | `MultiProviderOptions { Provider = "CodexAppServer", CodexAppServer = null }` に対して `Validate()` が `InvalidOperationException` をスローすること | No | No | `Assert.That(() => options.Validate(), Throws.InstanceOf<InvalidOperationException>())` が通ること | Done |
| TP-003-b | RC-003-b | `ProviderFactory.Create()` が `Provider = "CodexAppServer"` 設定で `CodexAppServerChatClient` 型を解決すること（ProviderRegistry に明示的 mapping があること） | Yes（テスト用 DI に `CodexAppServerChatClient` の fake 実装またはスタブを登録） | Yes（`ProviderFactory.DiscoverProviderType` に `"CodexAppServer" => "CodexAppServerChatClient"` の explicit switch entry が存在すること） | `factory.Create()` が `CodexAppServerChatClient` 型を返すこと（`Is.TypeOf<CodexAppServerChatClient>()`） | Done |
| TP-003-c-1 | RC-003-c | `services.AddCodexAppServer(configuration)` 呼び出し後、`serviceProvider.GetRequiredService<CodexAppServerChatClient>()` が null でない `CodexAppServerChatClient` インスタンスを Singleton として返すこと | No（実際の DI コンテナに対して検証） | No（production DI 登録そのものを検証する） | `provider.GetRequiredService<CodexAppServerChatClient>()` が同じインスタンスを 2 回呼んでも同一参照を返すこと（Singleton 検証） | Done |
| TP-003-c-2 | RC-003-c | `services.AddCodexAppServer(configuration)` 呼び出し後、`ICodexTransportFactory` が Singleton として解決されること | No | No | `provider.GetRequiredService<ICodexTransportFactory>()` が非 null かつ Singleton（同一参照）であること | Done |
| TP-003-d | RC-003-d | `ExtensionParameters.Set("codex.workingDirectory", "/path")` が `ArgumentException` をスローしないこと。`"codex"` prefix が `AllowedPrefixes` に含まれているため valid な操作であること | No（直接 `ExtensionParameters` を呼ぶ） | No | `Assert.That(() => ext.Set("codex.workingDirectory", "/path"), Throws.Nothing)` が通り、`ext.Get<string>("codex.workingDirectory")` が `"/path"` を返すこと | Done |

In this agent, `Done` means the test design row is complete for this pass.
It does not mean the test has been implemented, executed, or verified.

---

## Required production binding checks

| Test Point ID | Runtime Contract ID | Substitute used / expected | Production implementation to check | Production wiring / entrypoint to check | Notes |
| --- | --- | --- | --- | --- | --- |
| TP-001-a | RC-001-a | `FakeCodexTransport`（`ICodexTransport` stub） | `StdioCodexTransport.SendLineAsync` — initialize request の JSON シリアライズと stdout 書き込み | `DefaultCodexTransportFactory.Create()` → `StdioCodexTransport` 生成 → `StartAsync` でプロセス起動 | `System.Text.Json` によるシリアライズが `ICodexTransport.SendLineAsync` 経由で正しく到達することを確認。FakeTransport では serialize 前後の整合性のみ確認可能。 |
| TP-001-b | RC-001-b | `FakeCodexTransport` | `StdioCodexTransport.SendLineAsync` — initialized notification の送信 | 同上 | `ClientNotification` が `"initialized"` のみを定義する点はスキーマ確認済み。production では notification が正しく stdio に書き込まれることを確認。 |
| TP-001-c | RC-001-c | `FakeCodexTransport` | `StdioCodexTransport.SendLineAsync` — thread/start request の送信。`CodexRpcSession._threadId` の保存ロジック。 | 同上 | `result.thread.id` の JSON パス確認が production バインディング検証の核心。FakeTransport script で想定 JSON を返すだけでは `StdioCodexTransport` の読み込みロジックは未検証。 |
| TP-001-d | RC-001-d | `FakeCodexTransport` | `StdioCodexTransport.SendLineAsync` — turn/start request の送信。`ReasoningEffortLevel` → string マッピングロジック。 | 同上 | `params.input` の shape（array of `{type: "text", text: ...}`）が production シリアライズで正しく出力されることを確認。effort の enum → string 変換が `System.Text.Json` の `[JsonConverter]` または custom logic で実装されていることを確認。 |
| TP-001-e | RC-001-e | `FakeCodexTransport` | `StdioCodexTransport.SendLineAsync` — turn/start の sandboxPolicy オブジェクトシリアライズ | 同上 | `SandboxPolicy` の discriminated union（`workspaceWrite` / `readOnly` / `dangerFullAccess`）の JSON シリアライズが production でも正しい shape になることが核心。FakeTransport では受け取った JSON の文字列比較でのみ確認可能。 |
| TP-002-a | RC-002-a | `FakeCodexTransport`（stdout script injection） | `CodexRpcSession.ReadLoop` — `item/agentMessage/delta` notification の受信・パース・`Dictionary<string, StringBuilder>` 蓄積ロジック | `StdioCodexTransport.ReadLinesAsync` → ReadLoop への JSONL 行供給 | FakeTransport は ReadLinesAsync の代替として script lines を返す。production では OS の stdio pipe バッファリング、行区切り、エンコードが加わる。 |
| TP-002-b-1 | RC-002-b | `FakeCodexTransport` | `CodexRpcSession.ReadLoop` — turn/completed 受信時の TCS completion ロジック | `StdioCodexTransport.ReadLinesAsync` | production では `turn/completed` が複数の delta 後に来る非同期シーケンスである点に注意。 |
| TP-002-b-2 | RC-002-b | `FakeCodexTransport` | `CodexRpcSession.ReadLoop` — `status == "failed"` 時の `ProviderException` 生成と `turn.error.message` の取り込み | 同上 | `TurnError` の `codexErrorInfo` / `additionalDetails` フィールドは今 pass では検証範囲外（OutOfScopeForThisPass）。 |
| TP-002-b-3 | RC-002-b | `FakeCodexTransport` | `CodexRpcSession.ReadLoop` — `status == "interrupted"` 時の `OperationCanceledException` スロー | 同上 | interrupted の判定が `turn.status == "interrupted"` であることはスキーマ確認済み。production で `OperationCanceledException` のキャンセルトークンが適切に連携されているかを確認。 |
| TP-002-c-1 | RC-002-c | `FakeCodexTransport` | `CodexRpcSession.ReadLoop` — `error` notification の `willRetry: true` 時の無視ロジック | `StdioCodexTransport.ReadLinesAsync` | `willRetry: true` の場合はサーバーが retry するため、クライアントは通知を無視して turn/completed を待ち続けること。production で ReadLoop がこのパスを正しく実装していることを確認。 |
| TP-002-c-2 | RC-002-c | `FakeCodexTransport` | `CodexRpcSession.ReadLoop` — `willRetry: false` 時の `ProviderException` 生成 | 同上 | unknown |
| TP-002-d | RC-002-d | `FakeCodexTransport`（EOF script） | `CodexRpcSession.ReadLoop` — EOF 検知時の全 pending TCS fault ロジック。`StdioCodexTransport.ReadLinesAsync` の EOF 検知実装 | `StdioCodexTransport.ReadLinesAsync` が stream 終端で `IAsyncEnumerable` を正常終了すること | production では stdout pipe が予期せず閉じる（プロセスクラッシュ等）場合のみ発生する経路。FakeTransport での EOF シミュレーション方法は実装者が決定する（`IAsyncEnumerable` の早期終了）。 |
| TP-002-e-1 | RC-002-e | `FakeCodexTransport` | `CodexRpcSession.ReadLoop` — ServerRequest の id と method を読み取り、cancel 応答を送信するロジック | `StdioCodexTransport.SendLineAsync`（応答の書き込み）、`StdioCodexTransport.ReadLinesAsync`（ServerRequest の受信） | `item/fileChange/requestApproval` と `item/permissions/requestApproval` の 3 メソッド全てに対して同様のロジックが適用されることを確認。TP-002-e-1 は `commandExecution` のみを対象とするが、production では 3 メソッド分のハンドラが必要。 |
| TP-002-e-2 | RC-002-e | `FakeCodexTransport` | 同上（acceptForSession 応答パス） | 同上 | `decision: "acceptForSession"` はスキーマ確認済み（`"accept"` ではない）。production シリアライズで正しい文字列になることを確認。 |
| TP-002-f-1 | RC-002-f | `FakeCodexTransport` | `CodexRpcSession.ReadLoop` — `thread/status/changed` の `activeFlags` 解析と `waitingOnUserInput` 時の例外ロジック | `StdioCodexTransport.ReadLinesAsync` | `ThreadStatus.type` の discriminated union（`active` / `idle` / `notLoaded` / `systemError`）のパースが production で正しいことを確認。 |
| TP-002-f-2 | RC-002-f | `FakeCodexTransport` | `CodexRpcSession.ReadLoop` — `waitingOnApproval` 時の無視（待機継続）ロジック | 同上 | `waitingOnApproval` と後続 ServerRequest の組み合わせ（TP-002-e-2 との連携）が production で正しくシーケンスされることを確認。 |
| TP-003-b | RC-003-b | テスト用 DI にスタブまたは実装クラスを登録 | `ProviderFactory.DiscoverProviderType` の switch 式に `"CodexAppServer" => "CodexAppServerChatClient"` の明示的 entry が追加されていること（`src/MeAiUtility.MultiProvider/Configuration/ProviderFactory.cs` L40-47） | `ProviderRegistry.Register("CodexAppServer", typeof(CodexAppServerChatClient))` または `DiscoverProviderType` の自動解決 | RC-002-b 注記: `DiscoverProviderType` の fallback（L50: `t.Name.Contains(provider, ...)`）は `"CodexAppServer"` を `"CodexAppServerChatClient"` に Contains マッチさせる可能性があるが、明示的 mapping を追加することが強く推奨される（Runtime Contract Kernel 注記 2）。production binding 検証では明示的 mapping の存在を確認すること。 |

---

## Manual-only checks

以下の項目は `FakeCodexTransport` を使った unit test では観測できず、実際の `codex app-server` を起動した手動または integration test 環境での確認が必要。

1. **StdioCodexTransport + SystemCodexProcessRunner の実プロセス起動**（RC-004 相当、今 pass では OutOfScopeForThisPass）
   - `codex app-server` CLI が正常に起動し stdin/stdout が接続されること
   - プロセスの kill-on-cancel（`CancellationToken` キャンセル時に subprocess が確実に終了すること）
   - Windows stdio peculiarities（改行コード、CR+LF、stdin flush タイミング）

2. **JSON-RPC フィールド名の実際の動作確認**（B-01, B-02 境界）
   - `thread/start` に送った JSON フィールド名（`sandbox`, `model`, `approvalPolicy`）が `codex app-server` に正しく認識されること
   - `turn/start` の `sandboxPolicy` オブジェクト shape が `codex app-server` に正しく認識されること
   - FakeTransport では contract mismatch を検出できない（Change Risk Triage の中核リスク B-01、B-02）

3. **`item/permissions/requestApproval` の response schema 確認**
   - Runtime Contract Kernel `Remaining work` にて `PermissionsRequestApprovalResponse.json` が未確認と記録されている
   - `decision: "acceptForSession"` が `item/permissions/requestApproval` にも適用されるか手動または schema 確認が必要

4. **GetStreamingResponseAsync の Channel bridge cleanup**（RC-005 相当、Deferred）
   - consumer が途中で IAsyncEnumerable を中断した場合に、ReadLoop Task が確実に cleanup され subprocess が kill されること

---

## Notes / assumptions

### 仮定・注意事項

1. **FakeCodexTransport の設計**: この test design は `FakeCodexTransport` が `ICodexTransport` を実装し、script として JSON 行の送受信を制御できることを前提とする。具体的には:
   - **受信 script（stdout 模倣）**: `ReadLinesAsync()` が事前設定した JSONL 行のシーケンスを返す
   - **送信キャプチャ（stdin 模倣）**: `SendLineAsync()` に渡された JSON 文字列を `SentMessages` リストで収集する
   - **EOF 模倣**: `ReadLinesAsync()` が空の `IAsyncEnumerable` を返す、または特定行数後に終了する
   
   この設計は `ICopilotSdkWrapper` の Mock パターン（既存 `GitHubCopilotChatClientTests.cs` の Moq 使用）を参考に、`Moq` または手書き fake いずれでも実装可能と判断した。

2. **TP-003-b の test 実装**: `ProviderFactoryTests.cs` の既存パターン（`ProviderRegistry.Register("OpenAI", typeof(FakeClient))` → `factory.Create()` → `Is.TypeOf<FakeClient>()`）に倣い、`"CodexAppServer"` → `typeof(CodexAppServerChatClient)` の mapping を同様のスタイルで追加する。`CodexAppServerChatClient` が DI に登録されていないと `ProviderFactory.Create()` が `InvalidOperationException` を出すため、テストでは DI に `CodexAppServerChatClient`（またはスタブ）を `AddSingleton` する必要がある。

3. **TP-003-c-1 / TP-003-c-2 の test 実装**: `GitHubCopilotServiceExtensionsTests.cs` の `AddGitHubCopilot_RegistersSdkWrapperAndChatClient` パターンに倣う。`AddCodexAppServer()` に渡す `IConfiguration` は最小限の appsettings mock（`Mock<IConfiguration>` で OK）で構成する。

4. **RC-002-f-2（waitingOnApproval → continue）の test point**: `waitingOnApproval` 受信後に別の notification（ServerRequest）が来るまで待機する設計は、ReadLoop の非同期処理設計に依存する。FakeTransport の script が順序通りに行を返せば観測可能だが、実装が Channel ベースかどうかによって test コードの構造が変わる可能性がある。実装者が Channel 設計を確定後に test 実装詳細を決定すること。

5. **エスカレーション判断**: selected contracts（RC-001〜RC-003）の test points は FakeCodexTransport による unit test スコープ内で定義できる。end-to-end のシナリオ（RC-004: StdioCodexTransport の実プロセス経路）は OutOfScopeForThisPass であり、`integration-test-design.agent.md` へのエスカレーションは今 pass では不要。ただし Manual-only checks に記録した実プロセス確認が RC-001/RC-002 の contract mismatch リスク（B-01、B-02）の最終的な検証である点に注意する。

6. **`ProviderFactory.DiscoverProviderType` の fallback 動作（RC-003-b）**: Runtime Contract Kernel 注記 2 によれば、現在の fallback（`t.Name.Contains("CodexAppServer", ...)`）が `CodexAppServerChatClient` に Contains マッチする可能性がある。TP-003-b は明示的 mapping を追加した後の動作を検証対象とする。明示的 mapping が追加されていない場合は fallback 経由でも解決できるが、verification-kernel は switch 式に明示的 entry が存在することを確認すること（NotImplementedOrMismatch リスクを排除するため）。

7. **`item/permissions/requestApproval` の response schema**: Runtime Contract Kernel `Remaining work` にある通り、`PermissionsRequestApprovalResponse.json` は未確認。TP-002-e-1 / TP-002-e-2 は `commandExecution` を対象とするが、3 メソッド全ての production binding 検証（verification-kernel）では `permissions` variant の decision フィールドが `acceptForSession` / `cancel` であることも確認すること。

---

## Handoff Packet

- Profile used: contract-kernel
- Source artifacts:
  - `plans/codex-app-server-runtime-contract-kernel.md` (主要入力 — Contract IDs、Scenario、Error/timeout behavior、Production implementation address)
  - `plans/codex-app-server-provider-change-risk-triage.md` (RC 選定理由と境界リスク)
  - `plans/codex-app-server-plan.md` (実装方針・テスト可能性設計)
  - `tests/MeAiUtility.MultiProvider.GitHubCopilot.Tests/GitHubCopilotChatClientTests.cs` (テスト規約参照)
  - `tests/MeAiUtility.MultiProvider.GitHubCopilot.Tests/ConfigurationTests/GitHubCopilotServiceExtensionsTests.cs` (DI 登録テストパターン)
  - `tests/MeAiUtility.MultiProvider.Tests/Configuration/ProviderFactoryTests.cs` (ProviderFactory テストパターン)
  - `tests/MeAiUtility.MultiProvider.Tests/Options/MultiProviderOptionsTests.cs` (Validate() テストパターン)
  - `tests/MeAiUtility.MultiProvider.Tests/Options/ExtensionParametersTests.cs` (ExtensionParameters テストパターン)
  - `src/MeAiUtility.MultiProvider/Configuration/ProviderFactory.cs` (DiscoverProviderType の switch 構造確認)
  - `src/MeAiUtility.MultiProvider/Options/MultiProviderOptions.cs` (Validate() の switch 構造確認)
  - `src/MeAiUtility.MultiProvider/Options/ExtensionParameters.cs` (AllowedPrefixes の現在値確認)
- Selected contracts / IDs: RC-001-a, RC-001-b, RC-001-c, RC-001-d, RC-001-e, RC-002-a, RC-002-b, RC-002-c, RC-002-d, RC-002-e, RC-002-f, RC-003-a, RC-003-b, RC-003-c, RC-003-d
- Files inspected:
  - 上記 Source artifacts に記載の全ファイル
- Files intentionally not inspected:
  - `src/MeAiUtility.MultiProvider.CodexAppServer/`（未実装のため存在しない）
  - `D:\Temp\codex-app-server-json\` の JSON schema ファイル群（Runtime Contract Kernel がスキーマ情報を既に取り込み済みのため不要）
  - `tests/MeAiUtility.MultiProvider.IntegrationTests/`（integration test の規約はあるが今 pass の selected contracts は unit test スコープ内であるため参照不要）
  - `src/MeAiUtility.MultiProvider.GitHubCopilot/` の実装詳細（DI 登録パターンは test 側から参照可能なため不要）
- Decisions made:
  - RC-002-b を 3 つの test point（TP-002-b-1/2/3）に分割（`turn.status` が "completed"/"failed"/"interrupted" の 3 variant が独立した observable behavior を持つため）
  - RC-002-e を 2 つの test point（TP-002-e-1/2）に分割（AutoApprove=false と AutoApprove=true で observable behavior が異なるため）
  - RC-002-f を 2 つの test point（TP-002-f-1/2）に分割（waitingOnUserInput が即時例外、waitingOnApproval が待機継続で挙動が正反対のため）
  - RC-002-c を 2 つの test point（TP-002-c-1/2）に分割（willRetry フラグで挙動が正反対のため）
  - TP-003-c（AddCodexAppServer 登録）を 2 つに分割（CodexAppServerChatClient と ICodexTransportFactory が独立した登録要件のため）
  - エスカレーション（`integration-test-design.agent.md`）は不要と判断。selected contracts は FakeCodexTransport による unit test スコープで設計完結。実プロセス確認は Manual-only として記録。
- Do not redo unless new evidence appears:
  - RC-001-e: `turn/start.sandboxPolicy` に `networkAccess` が配置され、`thread/start.params` には存在しない — スキーマ確認済み（Runtime Contract Kernel）
  - RC-002-b: `turn/failed` という別 notification は存在しない — `turn/completed.turn.status == "failed"` で判定 — スキーマ確認済み
  - RC-002-e: AutoApprove=true 時の decision 値は `"acceptForSession"` — スキーマ確認済み
  - RC-003-b: `ProviderFactory.DiscoverProviderType` の現在の switch に `"CodexAppServer"` entry は存在しない（L40-47 確認済み）。fallback Contains マッチは動作する可能性があるが、明示的 entry 追加を推奨。
  - RC-003-a: `MultiProviderOptions.Validate()` の現在の switch に `"CodexAppServer"` ケースは存在しない（`_ => false` に落ちる）— コード確認済み
  - RC-003-d: `ExtensionParameters.AllowedPrefixes` に `"codex"` は存在しない（現在値: `["openai", "azure", "copilot"]`）— コード確認済み
- Remaining work:
  - `FakeCodexTransport` / `FakeCodexTransportFactory` の具体的な API 設計（`ReadLinesAsync` の script 注入方法、`SentMessages` のキャプチャ方法）は実装者が決定する（今 pass は設計 only）
  - `item/permissions/requestApproval` の response schema（`PermissionsRequestApprovalResponse.json`）未確認 — TP-002-e-1/2 の 3 メソッド全てへの適用を verification-kernel が確認すること
  - `GetStreamingResponseAsync` の Channel bridge cleanup（RC-005）は Deferred（RC-002 解決後の concern）
  - RC-004（StdioCodexTransport / SystemCodexProcessRunner の production binding）は OutOfScopeForThisPass — RC-001/RC-002 解決後に integration test または manual check として扱う
- Recommended next step: `verification-kernel.agent.md` を実行し、本 artifact の test points と production binding 要件に対して production implementation の存在・wiring を検証する。入力として本 artifact（`plans/codex-app-server-test-design-kernel.md`）と Runtime Contract Kernel（`plans/codex-app-server-runtime-contract-kernel.md`）を渡す。
