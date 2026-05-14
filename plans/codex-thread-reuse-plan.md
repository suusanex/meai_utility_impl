# Plan Kernel

## Goal

Codex App Server provider に thread の新規作成・続行・永続化機能を追加する。
呼び出し側が `AlwaysNew`（毎回新規）、`ReuseByThreadId`（直接 threadId 指定）、`ReuseOrCreateByKey`（threadKey 経由の JSON 永続化）の 3 つのポリシーから選べるようにし、プロセス再起動後も同じ Codex thread に続行できるようにする。

## Non-goals

- WebSocket transport への thread reuse 拡張
- 他の provider（OpenAI、Azure、Copilot）への影響
- Codex 側のエラーから自動的に新規 thread へフォールバックする曖昧な復旧ロジック（`ReuseOrCreateByKey` で stale thread 検出時は `ProviderException` で明示的に失敗させてよい）
- 複数プロセス間の完全な排他保証（TODO として明記し、ファイル破損を起こしにくい実装に留める）
- threadKey 一覧表示・削除 CLI の提供
- full runtime evidence（PlantUML 等）や full integration test design の作成

## Functional requirements

### FR-1: CodexThreadReusePolicy enum の追加

`public enum CodexThreadReusePolicy` を `Threading/` ディレクトリ配下に追加する。
値は `AlwaysNew`（既定）、`ReuseByThreadId`、`ReuseOrCreateByKey` の 3 つ。

### FR-2: CodexAppServerProviderOptions への設定追加

`CodexAppServerProviderOptions` に以下のプロパティを追加する（全て省略可能）。

- `ThreadReusePolicy` (`CodexThreadReusePolicy`, 既定 `AlwaysNew`)
- `ThreadId` (`string?`)
- `ThreadKey` (`string?`)
- `ThreadName` (`string?`)
- `ThreadStorePath` (`string?`)

### FR-3: CodexRuntimeOptions への値追加

`CodexRuntimeOptions` レコードに以下のプロパティを追加する。

- `ThreadReusePolicy` (`CodexThreadReusePolicy`)
- `ThreadId` (`string?`)
- `ThreadKey` (`string?`)
- `ThreadName` (`string?`)
- `ThreadStorePath` (`string?`)

### FR-4: extension parameter の解析

`CodexAppServerChatClient.BuildRuntimeOptions` に以下の extension parameter 解析を追加する。
既存の `codex.*` 解析パターンに従い、型不一致は `InvalidRequestException` にする。

| キー | 型 | 備考 |
|---|---|---|
| `codex.threadReusePolicy` | `string` | enum 文字列。大文字小文字不問で正規化 |
| `codex.threadId` | `string` | |
| `codex.threadKey` | `string` | |
| `codex.threadName` | `string` | |
| `codex.threadStorePath` | `string` | |

### FR-5: ICodexThreadStore インターフェースと実装

`internal interface ICodexThreadStore` を追加する（メソッド: `TryGetByKeyAsync`, `SaveAsync`）。

`FileCodexThreadStore` を追加し、以下を実装する。

- `ThreadStorePath` が指定されれば、そのパスを使用する
- 未指定なら `Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)` 配下の `MeAiUtility\CodexAppServer\threads.json` に保存する
- JSON フォーマット: `{ "threads": [ { ... } ] }` の配列
- 書き込みは temp file → rename の安全な更新にする
- 同一プロセス内では `SemaphoreSlim` で排他する
- 複数プロセス排他は TODO として明記する（最低限ファイル破損を起こさない実装にする）

`CodexThreadRecord` は以下のフィールドを持つ sealed record とする。

- `ThreadKey` (`string`)
- `ThreadId` (`string`)
- `ThreadName` (`string?`)
- `WorkingDirectory` (`string?`)
- `ModelId` (`string?`)
- `CreatedAt` (`DateTimeOffset`)
- `LastUsedAt` (`DateTimeOffset`)

### FR-6: CodexRpcSession の ResolveThreadIdAsync 追加

`CodexRpcSession.ExecuteTurnAsync` 内で、既存の `thread/start` 固定呼び出しを `ResolveThreadIdAsync` に置き換える。

| ポリシー | 動作 |
|---|---|
| `AlwaysNew` | 現状互換。`thread/start` を呼び出して threadId を得る。threadKey が指定されていても JSON 保存はしない |
| `ReuseByThreadId` | `runtimeOptions.ThreadId` が空なら `InvalidRequestException`。`thread/start` を呼ばず、指定 threadId で `turn/start` を送る |
| `ReuseOrCreateByKey` | `runtimeOptions.ThreadKey` が空なら `InvalidRequestException`。JSON ストアで検索し、保存済みなら `LastUsedAt` を更新してその threadId を使う。なければ `thread/start` して新規 threadId を JSON に保存してから `turn/start` する |

### FR-7: CodexAppServerChatClient への ICodexThreadStore 依存注入

`CodexAppServerChatClient` は `ICodexThreadStore` を constructor injection で受け取り、`CodexRpcSession` に渡す（または session 生成前に呼び出す）。

### FR-8: DI 登録の更新

`CodexAppServerServiceExtensions.AddCodexAppServer` に `ICodexThreadStore` と `ICodexThreadRegistry` の DI 登録を追加する（`FileCodexThreadStore` / `CodexThreadRegistry` を singleton として登録）。

### FR-9: ドキュメント追記

README または provider ドキュメントに、thread reuse 使用時の注意（呼び出し元は「今回の追加指示だけ」を messages に渡すこと、過去履歴の再投入で二重化が起きること）を記述する。

### FR-10: ライブラリ使用側向け thread 一覧取得 I/F 追加

ライブラリ使用側が thread 再利用対象を列挙・参照できる公開 I/F を追加する。

- `ICodexThreadRegistry`（public）を追加し、少なくとも次を提供する
  - `ListAsync`（保存済み thread レコードの一覧取得）
  - `TryGetByThreadKeyAsync`（threadKey 指定の単一取得）
- 返却型は内部 record をそのまま公開せず、公開 DTO（例: `CodexThreadDescriptor`）を返す
  - 含める項目: `ThreadKey`, `ThreadId`, `ThreadName`, `WorkingDirectory`, `ModelId`, `CreatedAt`, `LastUsedAt`
- `threadName` は重複しうるため、参照の主キーは `threadKey` とする
- 読み取り専用 I/F とし、更新・削除 API はこの pass では追加しない

## Acceptance conditions

| ID | 対応 FR | 成功基準（observable な behavior） |
|---|---|---|
| AC-1 | FR-1, FR-6 | `AlwaysNew` のとき、毎回 `thread/start` が送られる |
| AC-2 | FR-6 | `ReuseByThreadId` のとき、`thread/start` が送られず、指定 threadId で `turn/start` が送られる |
| AC-3 | FR-6 | `ReuseByThreadId` で threadId 未指定なら `InvalidRequestException` が返る |
| AC-4 | FR-5, FR-6 | `ReuseOrCreateByKey` で保存済み record がある場合、`thread/start` が送られず、保存済み threadId で `turn/start` が送られる |
| AC-5 | FR-5, FR-6 | `ReuseOrCreateByKey` で保存済み record がない場合、`thread/start` が送られ、返却 threadId が JSON store に保存され、その threadId で `turn/start` が送られる |
| AC-6 | FR-6 | `ReuseOrCreateByKey` で threadKey 未指定なら `InvalidRequestException` が返る |
| AC-7 | FR-4 | extension parameter `codex.threadReusePolicy` / `codex.threadId` / `codex.threadKey` / `codex.threadName` / `codex.threadStorePath` が `CodexRuntimeOptions` に反映される |
| AC-8 | FR-4 | 型不一致の extension parameter（例: `codex.threadReusePolicy` に bool を渡す）で `InvalidRequestException` が返る |
| AC-9 | FR-5 | JSON store は `threadKey` / `threadName` / `threadId` を保存し、別インスタンス（同一プロセス内）から読み戻せる |
| AC-10 | FR-6 | 既存の `thread/start` / `turn/start` payload 生成、delta 集約、approval handling の既存テストが引き続き通過する |
| AC-11 | FR-10 | ライブラリ使用側が `ICodexThreadRegistry.ListAsync` を呼ぶと、保存済み thread レコードの一覧（`threadKey`, `threadId`, `threadName` など）を取得できる |
| AC-12 | FR-10 | `ICodexThreadRegistry.TryGetByThreadKeyAsync` で既存 key は 1 件取得、未登録 key は `null` を返し、例外を握りつぶさない |

## Affected components / modules

### 変更が必要なファイル

| ファイル | 変更内容 |
|---|---|
| `src/MeAiUtility.MultiProvider.CodexAppServer/Options/CodexAppServerProviderOptions.cs` | `ThreadReusePolicy`, `ThreadId`, `ThreadKey`, `ThreadName`, `ThreadStorePath` プロパティを追加 |
| `src/MeAiUtility.MultiProvider.CodexAppServer/Options/CodexRuntimeOptions.cs` | 同プロパティ 5 つを record に追加 |
| `src/MeAiUtility.MultiProvider.CodexAppServer/CodexAppServerChatClient.cs` | extension parameter 解析追加、`ICodexThreadStore` constructor injection、`BuildRuntimeOptions` 修正 |
| `src/MeAiUtility.MultiProvider.CodexAppServer/CodexRpcSession.cs` | `ResolveThreadIdAsync` を追加し、`ExecuteTurnAsync` の `thread/start` 固定呼び出しを置き換え。`ICodexThreadStore` を constructor で受け取る |
| `src/MeAiUtility.MultiProvider.CodexAppServer/Configuration/CodexAppServerServiceExtensions.cs` | `ICodexThreadStore` / `ICodexThreadRegistry` の singleton 登録を追加 |
| `src/MeAiUtility.MultiProvider.CodexAppServer/Threading/CodexThreadRegistry.cs` | `ICodexThreadStore` を利用して公開 DTO を返す読み取り専用 facade を実装 |
| `README.md` | thread reuse の使い方と注意点を追記 |

### 新規作成ファイル

| ファイル | 内容 |
|---|---|
| `src/MeAiUtility.MultiProvider.CodexAppServer/Threading/CodexThreadReusePolicy.cs` | `public enum CodexThreadReusePolicy` |
| `src/MeAiUtility.MultiProvider.CodexAppServer/Threading/CodexThreadRecord.cs` | `internal sealed record CodexThreadRecord` |
| `src/MeAiUtility.MultiProvider.CodexAppServer/Threading/ICodexThreadStore.cs` | `internal interface ICodexThreadStore` |
| `src/MeAiUtility.MultiProvider.CodexAppServer/Threading/FileCodexThreadStore.cs` | `internal sealed class FileCodexThreadStore : ICodexThreadStore` |
| `src/MeAiUtility.MultiProvider.CodexAppServer/Threading/ICodexThreadRegistry.cs` | `public interface ICodexThreadRegistry`（library 使用側の一覧/参照 I/F） |
| `src/MeAiUtility.MultiProvider.CodexAppServer/Threading/CodexThreadDescriptor.cs` | `public sealed record CodexThreadDescriptor`（公開読み取り DTO） |

### 読み取りのみ（変更不要）

- `src/MeAiUtility.MultiProvider.CodexAppServer/Abstractions/ICodexTransport.cs` — transport 層はそのまま
- `src/MeAiUtility.MultiProvider.CodexAppServer/Abstractions/ICodexTransportFactory.cs` — factory はそのまま
- `tests/MeAiUtility.MultiProvider.CodexAppServer.Tests/Fakes/ScriptedCodexTransport.cs` — 既存 fake を再利用

## Expected implementation scope

1. `Threading/` ディレクトリ配下の 6 ファイルを新規作成する（`CodexThreadReusePolicy`, `CodexThreadRecord`, `ICodexThreadStore`, `FileCodexThreadStore`, `ICodexThreadRegistry`, `CodexThreadDescriptor`）
2. `CodexRuntimeOptions` と `CodexAppServerProviderOptions` に 5 プロパティを追加する
3. `CodexAppServerChatClient` の `BuildRuntimeOptions` に extension parameter 解析 5 件を追加し、constructor で `ICodexThreadStore` を受け取るよう修正する
4. `CodexRpcSession` に `ResolveThreadIdAsync` を追加し、`ExecuteTurnAsync` を修正する。`ICodexThreadStore` を constructor で受け取る（または上位から注入される）
5. `ICodexThreadRegistry`（public）と `CodexThreadDescriptor`（public）を追加し、store からの読み取り API を設計・実装する
6. `CodexAppServerServiceExtensions.AddCodexAppServer` に `ICodexThreadStore` と `ICodexThreadRegistry` の DI 登録を追加する
7. テストを追加する（AC-1 〜 AC-12 を網羅。実 codex CLI は使わない）
8. README に注意事項を追記する

## Known high-risk boundaries

| Risk trigger | 判定 | 補足 |
|---|---|---|
| Cross-process or cross-service sequence | Absent | thread reuse は同一プロセス内の JSON store 経由で完結 |
| Queue / event / webhook / background worker | Absent | |
| External API or SDK | Present | `FileCodexThreadStore` の JSON 読み書き（ファイル I/O）。同一プロセス内排他は SemaphoreSlim、複数プロセス排他は TODO |
| Authentication or authorization | Absent | |
| Durable state / retry / replay / idempotency | Present | JSON store への書き込みは temp file → rename で安全性を確保する必要がある。`LastUsedAt` 更新の read-modify-write が競合点になる可能性がある |
| Startup wiring / DI / configuration | Present | `ICodexThreadStore` と `ICodexThreadRegistry` を DI に追加する。`CodexRpcSession` の依存変更と公開 I/F 解決性の両方が wiring リスクになる |
| Production implementation split from test substitute | Present | `ICodexThreadStore` に対するテスト用スタブが必要。`FileCodexThreadStore` のみでなく、テスト用インメモリ実装が必要 |
| Multiple runtime participants coordinating state | Unclear | 複数プロセスが同じ `threads.json` に書く場合。現実装では TODO として排他なし |
| Observable behavior spanning more than one component | Present | `ResolveThreadIdAsync`（`CodexRpcSession`）→ `ICodexThreadStore`（`FileCodexThreadStore`）→ JSON ファイルの境界 |

詳細な contract analysis は `change-risk-triage.agent.md` に委ねる。

## Out of scope for this pass

- `ReuseOrCreateByKey` で stale thread 検出・自動再作成ロジック（ProviderException で失敗させる実装に留める）
- 複数プロセス間の named mutex / lock file 排他（TODO として明記）
- `AlwaysNew` での threadKey / threadId の JSON 保存（`ReuseOrCreateByKey` を使う設計にする）
- CLI ツールによる thread 一覧・削除操作
- runtime evidence（PlantUML sequence diagrams）や full integration test design の作成
- WebSocket transport への thread reuse 拡張

## Handoff to change-risk-triage

以下の high-risk boundary candidates を `change-risk-triage.agent.md` で分類・確認してください。

1. **Durable state / JSON 永続化の read-modify-write**: `FileCodexThreadStore` が `LastUsedAt` 更新のために JSON を読み込み → 更新 → temp file → rename する一連のシーケンス。複数プロセスが同じファイルに書く場合の競合が潜在的リスク。
2. **Startup wiring / DI**: `CodexRpcSession` が新たに `ICodexThreadStore` に依存する。既存テストが `CodexRpcSession` を直接構築している場合、コンストラクタ変更がテスト wiring を壊す可能性がある。
3. **Production implementation split from test substitute**: `ICodexThreadStore` に対するテスト用スタブが必要。既存 `ScriptedCodexTransport` パターンに合わせたスタブ実装の設計が必要。
4. **Library-facing interface consistency**: `ICodexThreadRegistry` が返す公開 DTO と JSON store record の対応関係（threadKey 主キー、threadName 非一意）を明示し、利用側が再利用対象を列挙できることを確認する必要がある。

---

## Handoff Packet

- **Profile used**: plan-kernel
- **Plan artifact**: `plans/codex-thread-reuse-plan.md`
- **Source artifacts**: 要求仕様（ユーザー入力）、既存の `plans/codex-app-server-plan.md`（初期実装 Plan、参照のみ）
- **Selected contracts / IDs**: none selected by this agent; final selection belongs to change-risk-triage
- **Files inspected**:
  - `src/MeAiUtility.MultiProvider.CodexAppServer/CodexRpcSession.cs`
  - `src/MeAiUtility.MultiProvider.CodexAppServer/CodexAppServerChatClient.cs`
  - `src/MeAiUtility.MultiProvider.CodexAppServer/Options/CodexRuntimeOptions.cs`
  - `src/MeAiUtility.MultiProvider.CodexAppServer/Options/CodexAppServerProviderOptions.cs`
  - `src/MeAiUtility.MultiProvider.CodexAppServer/Configuration/CodexAppServerServiceExtensions.cs`
  - `tests/MeAiUtility.MultiProvider.CodexAppServer.Tests/Fakes/ScriptedCodexTransport.cs`
  - `tests/MeAiUtility.MultiProvider.CodexAppServer.Tests/CodexAppServerChatClientTests.cs`（抜粋）
  - `plans/codex-app-server-plan.md`（既存 Plan の slug 確認のみ）
- **Files intentionally not inspected**:
  - `Stdio/StdioCodexTransport.cs`、`Stdio/SystemCodexProcessRunner.cs` — transport 層は変更不要なため未読
  - `tests/MeAiUtility.MultiProvider.CodexAppServer.Tests/Stdio/*` — 今回の scope 外
  - `tests/MeAiUtility.MultiProvider.CodexAppServer.Tests/ConfigurationTests/*` — DI 登録変更の影響確認は実装時に行う
  - codebase 全体の他プロバイダー実装 — 今回の変更は CodexAppServer provider 専用のため不要
- **Decisions made**:
  - 既存の `codex-app-server-plan.md` は初期実装の Plan であり、今回の要求は別 slug（`codex-thread-reuse-plan.md`）として新規作成した
  - `CodexRpcSession` に `ICodexThreadStore` を constructor injection するか、上位から渡すかは、既存テストが `new CodexRpcSession(transport, logger)` で直接構築していることを確認した上で、constructor に追加する方針にした（テスト側はスタブを渡す）
  - `AlwaysNew` では JSON 保存を行わない（`ReuseOrCreateByKey` を使う設計）
  - ライブラリ利用側の列挙・参照ニーズに対応するため、`ICodexThreadRegistry`（public）+ `CodexThreadDescriptor`（public DTO）を追加し、`ICodexThreadStore` を直接公開しない方針にした
  - stale thread 検出時の自動再作成は Non-goal とし、`ProviderException` で失敗させる
  - 複数プロセス排他は TODO として明記する
- **Do not redo unless new evidence appears**:
  - 既存の extension parameter 解析パターン（`GetExtensionString`、`GetExtensionBoolean`、`GetExtensionInt`）は `CodexAppServerChatClient` に実装済みで、`codex.threadReusePolicy` 等も同パターンで追加できる
  - `ScriptedCodexTransport` / `StubCodexTransportFactory` の既存スタブは thread reuse テストにも再利用可能（`OnClientMessageAsync` で `thread/start` の有無を制御できる）
  - `CodexRpcSession` の `_threadId` フィールドは現在 `thread/start` 応答から設定されているが、`ResolveThreadIdAsync` に置き換えることで既存フローを保ちつつ新ポリシーを追加できる
- **Remaining work**: なし（bounded implementation として十分）
- **Recommended next step**: `change-risk-triage.agent.md` を実行し、上記 4 つの high-risk boundary candidates（公開 I/F 追加を含む）に対してリスク分類と process profile の推奨を行ってください。入力として `plans/codex-thread-reuse-plan.md` を渡してください。
