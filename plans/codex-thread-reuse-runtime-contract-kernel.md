# Runtime Contract Kernel

## Scope

本 artifact は `plans/codex-thread-reuse-change-risk-triage.md` と `plans/codex-thread-reuse-plan.md` を入力とし、selected contracts **RC-001 / RC-002 / RC-003 / RC-004** のみを対象にした contract-kernel です。  
対象は CodexAppServer provider の thread reuse 追加に伴う runtime contract identification と participant/boundary mapping に限定し、実装・テスト設計・verification は含みません。

## Runtime Contract Kernel

| Contract ID | Scenario | Producer | Consumer | Message / API / Event | Required fields | Error / timeout behavior | Production implementation address | Verification hook |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| RC-001 | `ReuseOrCreateByKey` で threadId を復元または新規作成し `turn/start` に渡す | `CodexRpcSession.ResolveThreadIdAsync`（予定） | `ICodexThreadStore` / `FileCodexThreadStore`（予定） + Codex RPC `turn/start` consumer | `ICodexThreadStore.TryGetByKeyAsync(threadKey)` / `SaveAsync(record)`、JSON `threads[]`、Codex RPC `thread/start` 応答 `result.thread.id`、`turn/start.params.threadId` | `threadReusePolicy`, `threadKey`, `threadId`, `threadName`, `workingDirectory`, `modelId`, `createdAt`, `lastUsedAt` | 期待値: `ReuseOrCreateByKey` で `threadKey` 未指定は `InvalidRequestException`。保存済み thread が stale/invalid の場合は自動復旧せず明示失敗（`ProviderException`）。詳細な I/O 例外マッピングは out of scope for this pass | `src/MeAiUtility.MultiProvider.CodexAppServer/CodexRpcSession.cs`（既存、`ResolveThreadIdAsync` 追加予定）、`src/MeAiUtility.MultiProvider.CodexAppServer/Threading/ICodexThreadStore.cs`（予定）、`src/MeAiUtility.MultiProvider.CodexAppServer/Threading/FileCodexThreadStore.cs`（予定）、`src/MeAiUtility.MultiProvider.CodexAppServer/Threading/CodexThreadRecord.cs`（予定） | to be assigned by test-design-kernel |
| RC-002 | extension/provider options を runtime options に解決し policy 分岐へ渡す | `CodexAppServerChatClient.BuildRuntimeOptions` | `CodexRuntimeOptions` → `CodexRpcSession`（policy 判定・`thread/start` / `turn/start` 送信） | `ChatOptions.AdditionalProperties["meai.extensions"]` の `codex.threadReusePolicy` / `codex.threadId` / `codex.threadKey` / `codex.threadName` / `codex.threadStorePath`、provider options fallback | `meai.extensions`（`ExtensionParameters` 型）、`codex.threadReusePolicy`, `codex.threadId`, `codex.threadKey`, `codex.threadName`, `codex.threadStorePath`、provider defaults（`AlwaysNew`） | 期待値: extension 型不一致は `InvalidRequestException`。policy 別必須値（`ReuseByThreadId` の `threadId`、`ReuseOrCreateByKey` の `threadKey`）は明示失敗。policy 別の最終検証地点詳細は out of scope for this pass | `src/MeAiUtility.MultiProvider.CodexAppServer/CodexAppServerChatClient.cs`（既存 `BuildRuntimeOptions` / extension 解析）、`src/MeAiUtility.MultiProvider.CodexAppServer/Options/CodexRuntimeOptions.cs`（既存、5項目追加予定）、`src/MeAiUtility.MultiProvider.CodexAppServer/Options/CodexAppServerProviderOptions.cs`（既存、5項目追加予定） | to be assigned by test-design-kernel |
| RC-003 | DI で thread store / thread registry を本番実装へ束縛して ChatClient から利用可能にする | `CodexAppServerServiceExtensions.AddCodexAppServer` | `CodexAppServerChatClient`（constructor injection）→ `CodexRpcSession`（thread store 使用）と ライブラリ使用側（`ICodexThreadRegistry` 解決） | `IServiceCollection.AddSingleton(...)` registration chain（`ICodexThreadStore` → `FileCodexThreadStore`、`ICodexThreadRegistry` → `CodexThreadRegistry`、`CodexAppServerChatClient`） | `ICodexThreadStore` registration、`ICodexThreadRegistry` registration、`CodexAppServerChatClient` constructor parameter、`CodexRpcSession` constructor parameter、`CodexAppServerProviderOptions` singleton | 期待値: 必須 DI 登録が欠けると activation failure。ライフサイクル不一致による並行動作影響の詳細検証は out of scope for this pass | `src/MeAiUtility.MultiProvider.CodexAppServer/Configuration/CodexAppServerServiceExtensions.cs`（既存、store/registry 登録追加予定）、`src/MeAiUtility.MultiProvider.CodexAppServer/CodexAppServerChatClient.cs`（既存 constructor 変更予定）、`src/MeAiUtility.MultiProvider.CodexAppServer/CodexRpcSession.cs`（既存 constructor 変更予定）、`src/MeAiUtility.MultiProvider.CodexAppServer/Threading/CodexThreadRegistry.cs`（予定） | to be assigned by test-design-kernel |
| RC-004 | ライブラリ使用側が保存済み thread 一覧/単体参照を取得する | `CodexThreadRegistry`（予定） | ライブラリ使用側（`ICodexThreadRegistry` 呼び出し元） | 公開 API `ICodexThreadRegistry.ListAsync` / `TryGetByThreadKeyAsync`、DTO `CodexThreadDescriptor` 返却 | `threadKey`, `threadId`, `threadName`, `workingDirectory`, `modelId`, `createdAt`, `lastUsedAt`（`threadKey` を主キーとして扱う） | 期待値: 未登録 key は `null` を返す。I/O や serialization 異常時は明示失敗（例外伝播）し、黙って空結果にフォールバックしない。詳細な例外種別マッピングは out of scope for this pass | `src/MeAiUtility.MultiProvider.CodexAppServer/Threading/ICodexThreadRegistry.cs`（予定）、`src/MeAiUtility.MultiProvider.CodexAppServer/Threading/CodexThreadRegistry.cs`（予定）、`src/MeAiUtility.MultiProvider.CodexAppServer/Threading/CodexThreadDescriptor.cs`（予定）、`src/MeAiUtility.MultiProvider.CodexAppServer/Threading/FileCodexThreadStore.cs`（予定） | to be assigned by test-design-kernel |

## Notes / assumptions

- `FileCodexThreadStore` と `Threading/*` は未実装のため、production address は Plan で明示された追加先を採用した（Status: `PartiallyDone`）。
- RC-001 の複数プロセス排他（named mutex / lock file）は triage 時点で `Unclear`。この kernel では atomic update 要件までを対象とし、完全排他は `Deferred`。
- RC-002 の policy 必須値検証を `BuildRuntimeOptions` で行うか `ResolveThreadIdAsync` で行うかは未確定。契約としては「無効入力は `InvalidRequestException` で明示失敗」を固定し、実装位置は `Deferred`。
- RC-003 は stub/fake 側成功と production DI 欠落の乖離が主要リスク。production wiring の成立判定は verification-kernel で行う（Status: `Deferred`）。
- RC-004 の公開 I/F 契約（DTO フィールドと `threadKey` 主キー意味論）は Plan FR-10 / AC-11 / AC-12 に整合させる（Status: `Done`）。
- 本 slice は 4 contracts で因果関係を表現できるため、現時点で runtime-evidence / full-coverage へのエスカレーションは不要（Status: `Done`）。

## Handoff Packet

- Profile used: contract-kernel
- Source artifacts:
  - `plans/codex-thread-reuse-change-risk-triage.md`
  - `plans/codex-thread-reuse-plan.md`
- Selected contracts / IDs: RC-001, RC-002, RC-003, RC-004
- Files inspected:
  - `plans/codex-thread-reuse-change-risk-triage.md`
  - `plans/codex-thread-reuse-plan.md`
  - `src/MeAiUtility.MultiProvider.CodexAppServer/CodexAppServerChatClient.cs`
  - `src/MeAiUtility.MultiProvider.CodexAppServer/CodexRpcSession.cs`
  - `src/MeAiUtility.MultiProvider.CodexAppServer/Configuration/CodexAppServerServiceExtensions.cs`
- Files intentionally not inspected:
  - `src/MeAiUtility.MultiProvider.CodexAppServer/Stdio/*`（selected contracts の境界外）
  - `tests/MeAiUtility.MultiProvider.CodexAppServer.Tests/*`（test point 設計は次段 `test-design-kernel` の責務）
  - 他 provider 配下の実装（scope 外）
- Decisions made:
  - triage で選定済み RC-001〜RC-004 をそのまま採用し、新規 Contract ID は追加しない
  - production implementation address は「既存実装ファイル + Plan で明示された追加予定ファイル」のみ記載し、推測は行わない
  - error/timeout は contract に必須の明示失敗条件のみ固定し、詳細実装差分は out of scope とした
- Do not redo unless new evidence appears:
  - thread reuse 変更の高リスク境界は durable state（RC-001）、option mapping（RC-002）、DI wiring（RC-003）、library-facing registry（RC-004）の 4 点で最小十分
  - guardrail chain はこの 4 contracts を軸に downstream へ渡せばよい
- Remaining work:
  - RC-001: JSON 書き込みの具体的 atomic 手順と複数プロセス排他方針の最終化（Status: `Deferred`）
  - RC-002: policy 必須項目検証の実装位置確定（BuildRuntimeOptions vs ResolveThreadIdAsync）（Status: `Deferred`）
  - RC-003: production DI wiring の実証（起動経路での解決確認）（Status: `Deferred`）
  - RC-004: 公開 I/F の例外契約（invalid key / I/O 異常）の最終固定（Status: `Deferred`）
- Recommended next step:
  - `test-design-kernel.agent.md` を実行し、RC-001〜RC-004 に対する test point mapping と stub/fake/in-memory 使用識別を作成する。入力は `plans/codex-thread-reuse-runtime-contract-kernel.md` と `plans/codex-thread-reuse-change-risk-triage.md`。
