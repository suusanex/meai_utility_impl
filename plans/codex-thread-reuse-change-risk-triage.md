# Change Risk Triage

## Recommended profile

contract-kernel

## Reasoning

変更対象は CodexAppServer provider 内に限定されるが、`CodexRpcSession`・`CodexAppServerChatClient`・DI 設定・新規 JSON 永続化ストアの間で、実行時にまたがる契約（threadId 解決、オプション解決、永続化、wiring）が新規に導入される。  
特に durable state（JSON read-modify-write）と production wiring（DI 登録・コンストラクタ依存）の不整合は、stub テストだけ通って本番接続で失敗する典型リスクである。  
一方で対象境界は 1〜4 件の狭い runtime contract に収まるため、`full-coverage` は過剰であり、guardrail chain を維持した `contract-kernel` が最小十分。

## High-risk boundaries

| Boundary | Producer | Consumer | Mechanism | Risk type |
| --- | --- | --- | --- | --- |
| Thread resolution and persistence boundary | `CodexRpcSession.ResolveThreadIdAsync`（予定） | `ICodexThreadStore` / `FileCodexThreadStore` | メソッド呼び出し + JSON file read/write (`threads.json`) | Durable state mismatch, stale mapping, atomic update failure |
| Runtime option mapping boundary | `CodexAppServerChatClient.BuildRuntimeOptions` | `CodexRpcSession` (`CreateThreadStartParams` / `CreateTurnStartParams`) | extension parameters + provider options + runtime options record 変換 | Option mapping mismatch, invalid validation path |
| DI wiring boundary | `CodexAppServerServiceExtensions.AddCodexAppServer` | `CodexAppServerChatClient` → `CodexRpcSession`（`ICodexThreadStore` 依存） | DI registration / constructor binding | Production wiring gap, substitute-only success |
| Library-facing thread registry boundary | `CodexThreadRegistry`（予定） | ライブラリ使用側（`ICodexThreadRegistry` 呼び出し元） | 公開 API `ListAsync` / `TryGetByThreadKeyAsync` + DTO 変換 | Public API/DTO mismatch, key semantics mismatch |

## Selected runtime contracts to cover

| Contract ID | Boundary | What is at risk | Why selected | Triage status | Next action |
| --- | --- | --- | --- | --- | --- |
| RC-001 | Thread resolution and persistence boundary | `ReuseOrCreateByKey` で threadKey→threadId の復元・保存・`LastUsedAt` 更新が壊れると、意図した thread 継続ができない | durable state と multi-call 継続要件の中核で、破綻時の影響が最も大きいため | Deferred | `runtime-contract-kernel` で JSON schema、更新順序、atomic write、失敗時エラー契約を最小契約として固定する |
| RC-002 | Runtime option mapping boundary | `codex.threadReusePolicy` / `threadId` / `threadKey` / `threadName` / `threadStorePath` が誤変換されると、意図しない policy 分岐や検証漏れが起きる | 外部入力から runtime 挙動へ直接影響し、InvalidRequestException 契約が重要なため | Deferred | `runtime-contract-kernel` で入力型・正規化・必須条件（policy 別）を契約化する |
| RC-003 | DI wiring boundary | `ICodexThreadStore` 未登録・誤スコープ・誤注入で、実行時に thread reuse が動かない | テスト代替では通っても production wiring で欠落しやすい境界のため | Deferred | `runtime-contract-kernel` で production 実装と test substitute の対応点を明示し、`verification-kernel` で wiring 検証対象に固定する |
| RC-004 | Library-facing thread registry boundary | ライブラリ使用側 I/F が保存済み thread の列挙・参照を返せない/誤った key 意味で返すと、再利用対象の選択ができない | Plan の FR-10 / AC-11 / AC-12 に直接対応し、公開 I/F の契約不整合は実装開始前に固定すべきため | Deferred | `runtime-contract-kernel` で `ICodexThreadRegistry` の `ListAsync` / `TryGetByThreadKeyAsync` と DTO フィールド契約（threadKey 主キー）を固定する |

## Candidate runtime contracts not selected

| Contract ID | Boundary | Why not selected | Candidate status | Suggested next action |
| --- | --- | --- | --- | --- |
| RC-C01 | Codex app-server error notification (`error`, `turn/completed`) handling | 既存 `CodexRpcSession` で既に実装済みの領域で、今回の主要差分は thread 解決・永続化・設定解決に集中しているため | OutOfScopeForThisPass | 実装後の回帰失敗が出た場合に追加 triage |
| RC-C02 | 複数プロセス間ロック（named mutex / lock file） | 要件上「安全実装が難しければ TODO 明記可」であり、今回の最小実装要件は atomic write + 同一プロセス排他のため | OutOfScopeForThisPass | 将来 slice として multi-process coordination を独立契約で扱う |
| RC-C03 | README 利用ガイド記述の網羅性 | runtime contract 不整合より文書品質の論点で、guardrail chain 対象としての優先度が低いため | OutOfScopeForThisPass | 実装後に docs レビューで確認 |

## Risk trigger scan

| Risk trigger | Present / Absent / Unclear | Notes |
| --- | --- | --- |
| Cross-process or cross-service sequence | Absent | provider 内の in-proc 呼び出し中心。外部プロセス連携は既存 codex RPC 通信で今回差分の中心ではない |
| Queue / event / webhook / background worker | Absent | 新規に queue/event 系は導入しない |
| External API or SDK | Present | Codex JSON-RPC と file I/O（`Environment.SpecialFolder.LocalApplicationData` + JSON）を利用 |
| Authentication or authorization | Absent | 認証方式の追加変更なし |
| Durable state / retry / replay / idempotency | Present | threadKey→threadId 永続化、`LastUsedAt` 更新、atomic write が新規追加 |
| Startup wiring / DI / configuration | Present | `ICodexThreadStore` / `ICodexThreadRegistry` の登録と `CodexAppServerChatClient` 依存注入が新規追加 |
| Production implementation split from test substitute | Present | File store（本番）と in-memory/stub store（テスト）の乖離リスクがある |
| Multiple runtime participants coordinating state | Unclear | 複数プロセス同時実行時の同一 JSON 更新は今回 TODO 許容で完全保証しない |
| Observable behavior spanning more than one component | Present | ChatClient（入力解決）→ RpcSession（分岐）→ ThreadStore（永続化）→ turn/start まで跨る |

## Suggested next agent

**Immediate next agent**: `runtime-contract-kernel.agent.md`

**Required inputs**:
- `plans/codex-thread-reuse-plan.md`
- `plans/codex-thread-reuse-change-risk-triage.md`
- 対象コード:  
  `src/MeAiUtility.MultiProvider.CodexAppServer/CodexAppServerChatClient.cs`  
  `src/MeAiUtility.MultiProvider.CodexAppServer/CodexRpcSession.cs`  
  `src/MeAiUtility.MultiProvider.CodexAppServer/Configuration/CodexAppServerServiceExtensions.cs`  
  `src/MeAiUtility.MultiProvider.CodexAppServer/Options/CodexRuntimeOptions.cs`  
  `src/MeAiUtility.MultiProvider.CodexAppServer/Options/CodexAppServerProviderOptions.cs`  
  （新規予定）`src/MeAiUtility.MultiProvider.CodexAppServer/Threading/*`

**Minimum required downstream flow（selected contracts: RC-001〜RC-004）**:
1. runtime-contract-kernel で RC ごとの runtime contract identification を固定  
2. runtime-contract-kernel で participant / boundary mapping を固定  
3. test-design-kernel で test point mapping を作成  
4. test-design-kernel で stub/fake/in-memory 使用点と production 対応を識別  
5. 実装 agent で production implementation binding を実装  
6. verification-kernel で production wiring / entrypoint verification を実施  
7. 未完了・不一致は explicit unresolved status で残す

## Out of scope for this triage

- repository 全体の再探索（CodexAppServer provider 以外）  
- 実装コード、テストコード、既存 Plan の更新  
- full runtime evidence / full integration test design の作成  
- `StdioCodexTransport` / `SystemCodexProcessRunner` 詳細実装の再分析（今回の主変更境界外）

## Handoff Packet

- Profile used: triage-only
- Source artifacts:
  - ユーザー要求（Codex thread reuse 追加仕様）
  - `plans/codex-thread-reuse-plan.md`
- Selected contracts / IDs: RC-001, RC-002, RC-003, RC-004
- Files inspected:
  - `plans/codex-thread-reuse-plan.md`
  - `src/MeAiUtility.MultiProvider.CodexAppServer/CodexRpcSession.cs`
  - `src/MeAiUtility.MultiProvider.CodexAppServer/CodexAppServerChatClient.cs`
  - `src/MeAiUtility.MultiProvider.CodexAppServer/Configuration/CodexAppServerServiceExtensions.cs`
  - `src/MeAiUtility.MultiProvider.CodexAppServer/Options/CodexRuntimeOptions.cs`
  - `src/MeAiUtility.MultiProvider.CodexAppServer/Options/CodexAppServerProviderOptions.cs`
  - `tests/MeAiUtility.MultiProvider.CodexAppServer.Tests/Fakes/ScriptedCodexTransport.cs`
- Files intentionally not inspected:
  - `src/MeAiUtility.MultiProvider.CodexAppServer/Stdio/*`（今回追加契約の中心外）
  - `tests/MeAiUtility.MultiProvider.CodexAppServer.Tests/Stdio/*`（同上）
  - 他 provider 全般（scope 外）
- Decisions made:
  - 推奨 profile は `contract-kernel`（高リスク境界はあるが 4 契約で bounded）
  - selected contracts は durable state、option mapping、DI wiring、library-facing registry の 4 件に限定
  - 複数プロセス排他は本 triage では `Unclear` とし、候補契約として defer
- Do not redo unless new evidence appears:
  - 主要リスクは `thread/start` 置換そのものより、threadId 解決の永続化契約・入力解決契約・DI binding 契約に集中する
  - guardrail chain は RC-001〜RC-004 に集中適用すれば最小十分
- Remaining work:
  - RC-001〜RC-004 の runtime contract 明文化（未着手, Deferred）
  - 複数プロセス排他方針の最終判断（未着手, Deferred）
- Recommended next step:
  - `runtime-contract-kernel.agent.md` を実行し、RC-001〜RC-004 を対象に最小 runtime contract artifact を作成する
- Required downstream guardrails:
  - RC-001: runtime contract identification → participant/boundary mapping → test point mapping → stub/fake/in-memory usage identification → production implementation binding → production wiring/entrypoint verification → unresolved status 明示
  - RC-002: runtime contract identification → participant/boundary mapping → test point mapping → stub/fake/in-memory usage identification → production implementation binding → production wiring/entrypoint verification → unresolved status 明示
  - RC-003: runtime contract identification → participant/boundary mapping → test point mapping → stub/fake/in-memory usage identification → production implementation binding → production wiring/entrypoint verification → unresolved status 明示
  - RC-004: runtime contract identification → participant/boundary mapping → test point mapping → stub/fake/in-memory usage identification → production implementation binding → production wiring/entrypoint verification → unresolved status 明示
