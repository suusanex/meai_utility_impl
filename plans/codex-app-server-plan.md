# Plan Kernel

## Goal

MeAiUtility.MultiProvider に Codex App Server を新しい IChatClient プロバイダ（"CodexAppServer"）として追加する。Codex App Server をローカル subprocess（stdio JSON-RPC）経由で run-to-completion 型の IChatClient としてラップし、既存のアプリから GitHub Copilot provider と同様に呼び出せることを目的とする。

## Non-goals

- Codex 用のリッチな UI（承認ダイアログ等）やイベントビューアの実装
- WebSocket transport の MVP 対応
- ライブラリ側での認証情報管理（`codex login` は外部前提）
- 既存プロバイダーの挙動変更
- full runtime evidence（PlantUML 等）や full integration test design の作成

## Functional requirements

1. IChatClient としての基本機能
   - GetResponseAsync: ユーザー入力を Codex に渡し、turn 完了まで待ち最終テキストを ChatResponse として返す
   - GetStreamingResponseAsync: 可能なら item/agentMessage/delta 相当を ChatResponseUpdate として逐次配信。実現困難なら NotSupportedException を検討
2. Codex App Server と stdio JSON-RPC で通信する transport 層（MVP は stdio のみ）
3. per-call 設定（ModelId など）を ChatOptions / ConversationExecutionOptions / AdditionalProperties 経由で渡せること
4. 承認（approval）ポリシーの実装方針
   - 既定: approvalPolicy = "never"（UI を出さない）
   - AutoApprove 設定で自動承認 or FailOnApprovalRequest を切替可能にする
5. テスト可能性
   - FakeCodexTransport / FakeCodexTransportFactory により、codex CLI 非依存で単体テスト可能にする
6. 例外・タイムアウト・キャンセルの明確な伝播

## Acceptance conditions

- 正常動作: GetResponseAsync 呼び出しに対し、FakeTransport の script に従った turn/completed で ChatResponse が返ること（テストで検証可能）
- ストリーミング: GetStreamingResponseAsync を呼ぶと delta が都度 yield される（MVP でサポートする場合）
- タイムアウト: 設定した TimeoutSeconds を超えると呼び出しは OperationCanceledException かタイムアウト例外で終了する
- Cancellation: 呼び出し元の CancellationToken を受け付け、consumer が中断しても subprocess が確実に停止する
- 承認要求: waitingOnApproval が来た場合、AutoApprove=false で ProviderException を返す。AutoApprove=true の挙動は schema 確認後に決定される
- DI: AddCodexAppServer + AddMultiProviderChat の組合せで IChatClient が CodexAppServerChatClient として解決される

## Affected components / modules (concrete files)

- 追加（新プロジェクト候補）: src/MeAiUtility.MultiProvider.CodexAppServer (推奨: MeAiUtility.MultiProvider.CodexAppServer)
  - CodexAppServerProviderOptions
  - ICodexTransport / StdioCodexTransport / FakeCodexTransport
  - ICodexTransportFactory / DefaultCodexTransportFactory / FakeCodexTransportFactory
  - ICodexProcessRunner / SystemCodexProcessRunner
  - CodexRpcSession (JSON-RPC session handling, ReadLoop / pending request routing)
  - CodexAppServerChatClient : IChatClient 実装
  - Unit tests: tests/MeAiUtility.MultiProvider.CodexAppServer.Tests
- 既存コアの変更点（追加のみ）:
  - src/MeAiUtility.MultiProvider/Options/MultiProviderOptions.cs (CodexAppServer section + Validate)
  - src/MeAiUtility.MultiProvider/Options/ExtensionParameters.cs (AllowedPrefixes に "codex" を追加)
  - src/MeAiUtility.MultiProvider/Configuration/ProviderFactory.cs または ProviderRegistry に "CodexAppServer" の登録ロジック
  - extension メソッド: src/MeAiUtility.MultiProvider/Configuration/CodexAppServerServiceExtensions.cs (AddCodexAppServer)

## Expected implementation scope

- p1: プロジェクト骨格（csproj、namespaces、tests プロジェクト）を追加
- p2: Options と抽象型の定義（ProviderOptions, CodexSessionConfig, ICodexTransport など）
- p3: JSON-RPC プロトコルの実装（CodexRpcSession、ReadLoop、メッセージ model）
- p4: IChatClient 実装（CodexAppServerChatClient） — Singleton でステートレス、per-call 状態は CodexRpcSession
- p5: DI 登録と core オプションの拡張（AddCodexAppServer、MultiProviderOptions.Validate、ExtensionParameters）
- p6: Unit tests（FakeCodexTransport による protocol script tests）
- p7: README / ドキュメント更新

実装 agent は各タスクを順に実行し、各完了時に unit tests を実行して破壊的変更がないことを確認すること。

## Known high-risk boundary candidates

- Cross-process JSON-RPC contract (client→server): thread/start / turn/start の JSON フィールド名・構造（model, approvalPolicy, sandbox/networkAccess の配置, effort） — Present
- Server→client notifications: item/agentMessage/delta の shape（itemId, delta text）、turn/completed/turn/failed の payload — Present
- Startup wiring / DI / configuration: MultiProviderOptions.Validate、ProviderFactory/ProviderRegistry の provider mapping、AddCodexAppServer 登録 — Present
- Production implementation split from test substitute: FakeCodexTransport による単体テスト vs StdioCodexTransport の実プロセス path — Present
- Queue / background ReadLoop coordination: ReadLoop Task、Channel bridge、pending request routing、cleanup on EOF/kill — Present

詳細な contract の確定と test point の選択は change-risk-triage / runtime-contract-kernel に委ねる。

## Out of scope for this pass

- WebSocket transport の実装（設計に拡張余地は残すが MVP では未実装）
- Approval UI や対話的承認フローの実装
- Codex App Server の全イベント公開
- 完全な integration テスト（manual/explicit tests をオプションで用意するが CI では実行しない）
- ランタイム実行環境固有の詳細（Windows stdio peculiarities 等）

## Handoff to change-risk-triage

この Plan は high-risk boundary の候補を挙げた上で、詳細 contract の選択と最終 triage を change-risk-triage に委ねる。特に以下を確認することを要請する:

- thread/start / turn/start の正確な JSON schema を `codex app-server generate-json-schema` で取得して RC を確定すること（RC-001）
- item/agentMessage/delta と turn/completed/failed の payload schema を確定すること（RC-002）
- DI wiring の具体的チェックポイント（MultiProviderOptions.Validate、ProviderRegistry mapping、AddCodexAppServer）を triage で確定すること（RC-003）

## Handoff Packet

- Profile used: plan-kernel
- Plan artifact: plans/codex-app-server-plan.md
- Source artifacts:
  - `C:\Users\suusa\.copilot\session-state\046969ce-097b-4cd2-9b63-aff3890f18ff\plan.md` (working draft)
  - OpenAI Codex App Server docs (referenced by user)
  - Existing provider implementations in src/MeAiUtility.MultiProvider.GitHubCopilot
- Selected contracts / IDs: none selected by this agent; final selection belongs to change-risk-triage
- Files inspected (representative):
  - src/MeAiUtility.MultiProvider/Configuration/ProviderFactory.cs
  - src/MeAiUtility.MultiProvider/Options/MultiProviderOptions.cs
  - src/MeAiUtility.MultiProvider/Options/ExtensionParameters.cs
  - src/MeAiUtility.MultiProvider.GitHubCopilot/GitHubCopilotChatClient.cs
- Files intentionally not inspected:
  - src/MeAiUtility.MultiProvider.OpenAI/** (out of scope)
  - Full tests and integration suites (detailed design deferred)
- Decisions made:
  - MVP transport: stdio JSON-RPC; WebSocket deferred
  - approvalPolicy default: "never"; AutoApprove configurable but behavior to be finalized after schema check
  - IChatClient registered as Singleton; per-call state kept in CodexRpcSession
- Do not redo unless new evidence appears:
  - Basic DI singleton constraint (IChatClient registered as Singleton) — derived from provider registration pattern
- Remaining work (NeedsHumanDecision):
  - Confirm exact JSON schema for thread/start, turn/start, and notifications (itemId field name, networkAccess placement, effort string mapping)
  - Decide automatic approval method name or fail-closed behavior prior to implementing AutoApprove=true
  - Resolve Plan draft inconsistency: CodexAppServerChatClient のコンストラクタが transport を直接受け取る表記と Factory パターンのどちらに統一するか
- Recommended next step: run `change-risk-triage.agent.md` followed by `runtime-contract-kernel.agent.md` to lock RC-001/RC-002/RC-003

---

Status: Done
