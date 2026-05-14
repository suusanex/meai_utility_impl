# Test Design Kernel

## Scope

本 artifact は `plans/codex-thread-reuse-runtime-contract-kernel.md` を主入力とし、selected contracts **RC-001 / RC-002 / RC-003 / RC-004** の test point mapping のみを対象にする。  
補助入力として `plans/codex-thread-reuse-change-risk-triage.md` と既存テスト流儀（`CodexAppServerChatClientTests` / `ScriptedCodexTransport`）を参照し、selected contracts 外の scenario には拡張しない。

## Test Design Kernel

| Test Point ID | Runtime Contract ID | What to verify | Stub / fake allowed? | Production binding required? | Expected observation | Status |
| --- | --- | --- | --- | --- | --- | --- |
| TP-001 | RC-001 | `ReuseOrCreateByKey` で保存済み `threadKey -> threadId` がある場合、`thread/start` を送らず保存済み threadId で `turn/start` される | Yes | Yes | 送信メッセージ列に `thread/start` が存在せず、`turn/start.params.threadId` が保存済み threadId と一致する | Done |
| TP-002 | RC-001 | `ReuseOrCreateByKey` で保存がない場合、新規 `thread/start` 後に `threadKey/threadName/threadId` が store に保存され、同一呼び出しでその threadId が `turn/start` に使われる | Yes | Yes | `thread/start` が 1 回送信され、応答 `result.thread.id` と `turn/start.params.threadId` が一致し、store 読み戻しで同じ `threadKey` に threadId が記録される | Done |
| TP-003 | RC-001 | `ReuseOrCreateByKey` で `threadKey` 未指定時は明示失敗する | Yes | Yes | `InvalidRequestException` が返り、`thread/start` / `turn/start` が送信されない | Done |
| TP-004 | RC-002 | extension parameters（`codex.threadReusePolicy` など）が runtime options に反映され、`ReuseByThreadId` の場合は `thread/start` を送らず指定 threadId で `turn/start` される | Yes | Yes | `turn/start.params.threadId` が extension 指定値と一致し、`thread/start` が送信されない | Done |
| TP-005 | RC-002 | extension parameter の型不一致で `InvalidRequestException` になる（例: `codex.threadReusePolicy` に bool） | Yes | Yes | `InvalidRequestException` が返る。エラーメッセージに対象キーと期待型が含まれる | Done |
| TP-006 | RC-003 | DI 登録により `ICodexThreadStore` の production 実装が解決可能で、`CodexAppServerChatClient` が activation 可能である | No | No | `AddCodexAppServer` 後の service provider から `ICodexThreadStore` と `CodexAppServerChatClient` が解決できる | Done |
| TP-007 | RC-004 | `ICodexThreadRegistry.ListAsync` が保存済み thread を `CodexThreadDescriptor` として列挙し、必須フィールドを欠落なく返す | Yes | Yes | 戻り値一覧の各要素に `threadKey`, `threadId`, `threadName`, `workingDirectory`, `modelId`, `createdAt`, `lastUsedAt` が含まれ、`threadKey` 単位で識別できる | Done |
| TP-008 | RC-004 | `ICodexThreadRegistry.TryGetByThreadKeyAsync` が既存 key は 1 件返し、未登録 key は `null` を返す | Yes | Yes | 既存 key で descriptor 取得、未登録 key で `null`、いずれも silent fallback せず観測可能な結果になる | Done |

In this agent, `Done` means the test design row is complete for this pass.
It does not mean the test has been implemented, executed, or verified.

## Required production binding checks

| Test Point ID | Runtime Contract ID | Substitute used / expected | Production implementation to check | Production wiring / entrypoint to check | Notes |
| --- | --- | --- | --- | --- | --- |
| TP-001 | RC-001 | `ScriptedCodexTransport` + test-side store substitute（in-memory または temp file） | `src/MeAiUtility.MultiProvider.CodexAppServer/CodexRpcSession.cs`（`ResolveThreadIdAsync`）; `src/MeAiUtility.MultiProvider.CodexAppServer/Threading/ICodexThreadStore.cs`; `src/MeAiUtility.MultiProvider.CodexAppServer/Threading/FileCodexThreadStore.cs` | `src/MeAiUtility.MultiProvider.CodexAppServer/CodexAppServerChatClient.cs` から `CodexRpcSession` へ store が渡る経路 | substitute 成功を production 実装存在の証拠にしない |
| TP-002 | RC-001 | `ScriptedCodexTransport` + test-side store substitute（in-memory または temp file） | `src/MeAiUtility.MultiProvider.CodexAppServer/Threading/FileCodexThreadStore.cs`（保存・再読込）; `src/MeAiUtility.MultiProvider.CodexAppServer/Threading/CodexThreadRecord.cs` | `src/MeAiUtility.MultiProvider.CodexAppServer/Configuration/CodexAppServerServiceExtensions.cs` の store 登録が production で有効か | JSON atomic update は verification-kernel で確認対象 |
| TP-003 | RC-001 | `ScriptedCodexTransport`（送信抑制確認） | `src/MeAiUtility.MultiProvider.CodexAppServer/CodexRpcSession.cs`（policy 別入力検証） | `CodexAppServerChatClient -> CodexRpcSession` 呼び出しで検証例外が伝播する経路 | 検証位置（BuildRuntimeOptions vs ResolveThreadIdAsync）は implementation 後に確定 |
| TP-004 | RC-002 | `ScriptedCodexTransport` + `ExtensionParameters` | `src/MeAiUtility.MultiProvider.CodexAppServer/CodexAppServerChatClient.cs`（extension 解析）; `src/MeAiUtility.MultiProvider.CodexAppServer/Options/CodexRuntimeOptions.cs` | ChatClient が runtime options を `CodexRpcSession` に渡す entrypoint（`GetResponseAsync` / `GetStreamingResponseAsync`） | mapping は payload 観測で間接確認、production 側の実アドレス存在を別途確認 |
| TP-005 | RC-002 | `ExtensionParameters` の不正型入力（テスト入力） | `src/MeAiUtility.MultiProvider.CodexAppServer/CodexAppServerChatClient.cs`（`GetExtensionString` 等） | `GetResponseAsync` 呼び出し時に request validation が機能する経路 | validation が test helper 側だけで完結していないことを確認 |
| TP-007 | RC-004 | store substitute（in-memory または temp file） + registry 実装 | `src/MeAiUtility.MultiProvider.CodexAppServer/Threading/ICodexThreadRegistry.cs`; `src/MeAiUtility.MultiProvider.CodexAppServer/Threading/CodexThreadRegistry.cs`; `src/MeAiUtility.MultiProvider.CodexAppServer/Threading/CodexThreadDescriptor.cs` | `src/MeAiUtility.MultiProvider.CodexAppServer/Configuration/CodexAppServerServiceExtensions.cs` で `ICodexThreadRegistry` が解決される経路 | DTO 変換が test substitute 側のみで成立していないことを確認 |
| TP-008 | RC-004 | store substitute（in-memory または temp file） + registry 実装 | `src/MeAiUtility.MultiProvider.CodexAppServer/Threading/CodexThreadRegistry.cs`（key lookup）; `src/MeAiUtility.MultiProvider.CodexAppServer/Threading/FileCodexThreadStore.cs` | `ICodexThreadRegistry.TryGetByThreadKeyAsync` の公開 entrypoint | 未登録 key で `null` を返す契約が production 実装でも維持されることを確認 |

## Manual-only checks

- RC-001 の複数プロセス同時書き込み時に JSON 破損が起きないこと（named mutex / lock file 未導入の場合の運用妥当性）は、CI unit test だけでは十分に観測できないため `ManualOnly`。

## Notes / assumptions

- 既存テスト流儀では `ScriptedCodexTransport` を使った unit test が主であるため、RC-001/RC-002 の test point は substitute 利用を前提に設計した。
- substitute を使う TP-001〜TP-005 では、必ず production binding check を required とした（Stub-complete but production-missing 防止）。
- RC-003 は DI の production wiring 自体を観測対象にするため、`Stub / fake allowed?` は `No` とした。
- RC-004（公開 I/F）は substitute を使った観測点（TP-007/TP-008）で定義し、production binding required を `Yes` に固定した。
- RC-001 の stale thread 自動復旧は Plan の non-goal であり、この pass では対象外（Status: `Deferred`）。
- selected contracts 4 件で観測点を定義可能なため、`integration-test-design` へのエスカレーションは現時点では不要（Status: `Done`）。

## Handoff Packet

- Profile used: contract-kernel
- Source artifacts:
  - `plans/codex-thread-reuse-runtime-contract-kernel.md`
  - `plans/codex-thread-reuse-change-risk-triage.md`
  - `plans/codex-thread-reuse-plan.md`
- Selected contracts / IDs: RC-001, RC-002, RC-003, RC-004
- Files inspected:
  - `plans/codex-thread-reuse-runtime-contract-kernel.md`
  - `plans/codex-thread-reuse-change-risk-triage.md`
  - `tests/MeAiUtility.MultiProvider.CodexAppServer.Tests/CodexAppServerChatClientTests.cs`
  - `tests/MeAiUtility.MultiProvider.CodexAppServer.Tests/Fakes/ScriptedCodexTransport.cs`
- Files intentionally not inspected:
  - `tests/MeAiUtility.MultiProvider.CodexAppServer.Tests/Stdio/*`（selected contracts の test point 設計に不要）
  - `tests` 配下の他 provider テスト（scope 外）
  - production 実装の未作成 `Threading/*` 詳細コード（未実装のため）
- Decisions made:
  - RC-001 に 3 点（reuse hit / reuse miss-create / missing key error）、RC-002 に 2 点（mapping / type mismatch error）、RC-003 に 1 点（DI wiring）、RC-004 に 2 点（list / try-get）を割り当てた
  - substitute 許容 test point（TP-001〜TP-005, TP-007, TP-008）は全て production binding required を `Yes` に固定した
  - RC-003 は production DI 解決を直接観測する test point として定義した
- Do not redo unless new evidence appears:
  - selected contracts の guardrail chain を満たす最小 test point は TP-001〜TP-008 で十分
  - RC-001/RC-002 は unit test substitute で観測し、production binding は verification-kernel で別途確認する構造が必要
- Remaining work:
  - TP-001〜TP-005, TP-007, TP-008 に対応する production binding / wiring の実証（Status: `Deferred`）
  - RC-001 複数プロセス排他の実運用確認（Status: `ManualOnly`）
  - RC-002 の必須検証実装位置確定後のテスト観測点微調整（Status: `PartiallyDone`）
  - RC-004 の公開 DTO 変換規約（null / 空文字扱い）の最終固定（Status: `Deferred`）
- Recommended next step:
  - `verification-kernel.agent.md` を実行し、`plans/codex-thread-reuse-runtime-contract-kernel.md` と本 artifact を入力に、TP-001〜TP-008 の production implementation binding と production wiring / entrypoint verification を確認する
