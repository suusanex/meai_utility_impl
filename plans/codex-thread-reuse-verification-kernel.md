# Verification Kernel Result

## Scope

本 artifact は `plans/codex-thread-reuse-test-design-kernel.md` を主要入力とし、selected contracts **RC-001 / RC-002 / RC-003 / RC-004** と selected test points **TP-001〜TP-008** を対象にした verification pass です。  
補助入力として `plans/codex-thread-reuse-runtime-contract-kernel.md` と `plans/codex-thread-reuse-change-risk-triage.md` を参照し、production code は selected scope に直接関係する `CodexAppServerChatClient` / `CodexRpcSession` / `ServiceExtensions` / `Threading/*` のみを確認しました。  
selected tests は `dotnet test tests\MeAiUtility.MultiProvider.CodexAppServer.Tests\MeAiUtility.MultiProvider.CodexAppServer.Tests.csproj --no-build --filter "FullyQualifiedName~CodexThreadReuseTests|FullyQualifiedName~CodexThreadStoreAndRegistryTests|FullyQualifiedName~CodexAppServerServiceExtensionsTests"` を実行し、net8.0 / net10.0 の両方で成功しました。

## Runtime contract verification

| Contract ID | Field / behavior | Expected (from Runtime Contract Kernel) | Production evidence | Covered by Test Point ID(s) | Status | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| RC-001 | `threadReusePolicy`, `threadKey`, `threadId`, `threadName`, `workingDirectory`, `modelId`, `createdAt`, `lastUsedAt` | `ReuseOrCreateByKey` で store lookup/save と `turn/start.params.threadId` を接続し、thread metadata を JSON に保持する | `src\MeAiUtility.MultiProvider.CodexAppServer\CodexRpcSession.cs:39-47,598-639`; `src\MeAiUtility.MultiProvider.CodexAppServer\Threading\FileCodexThreadStore.cs:20-107`; `src\MeAiUtility.MultiProvider.CodexAppServer\Threading\CodexThreadRecord.cs:1-10` | TP-001, TP-002, TP-003 | Done | `LastUsedAt` は reuse hit 後に更新、新規作成時は record 作成時に設定されている |
| RC-001 | 明示失敗 (`threadKey` 未指定 / stale-invalid thread は自動復旧しない) | `threadKey` 未指定は `InvalidRequestException`、stale/invalid thread は `ProviderException` で明示失敗 | `src\MeAiUtility.MultiProvider.CodexAppServer\CodexAppServerChatClient.cs:245-250,485-497`; `src\MeAiUtility.MultiProvider.CodexAppServer\CodexRpcSession.cs:613-643`; `src\MeAiUtility.MultiProvider.CodexAppServer\CodexRpcSession.cs:127-149` | TP-003 | PartiallyDone | missing-key は確認済み。stale thread の server-error path は production code 上で確認できるが selected test では未実行 |
| RC-002 | `codex.threadReusePolicy`, `codex.threadId`, `codex.threadKey`, `codex.threadName`, `codex.threadStorePath` の runtime mapping | extension/provider options を `CodexRuntimeOptions` に解決し policy 分岐へ渡す | `src\MeAiUtility.MultiProvider.CodexAppServer\CodexAppServerChatClient.cs:228-271`; `src\MeAiUtility.MultiProvider.CodexAppServer\Options\CodexRuntimeOptions.cs:5-24`; `src\MeAiUtility.MultiProvider.CodexAppServer\Options\CodexAppServerProviderOptions.cs:22-26` | TP-004, TP-005 | Done | provider default → extension override → runtime record の形で接続されている |
| RC-002 | 型不一致 / policy 必須値の fail-fast | extension 型不一致と policy 別必須値不足は `InvalidRequestException` で明示失敗 | `src\MeAiUtility.MultiProvider.CodexAppServer\CodexAppServerChatClient.cs:319-360,464-497` | TP-004, TP-005 | Done | validation は transport 起動前に実施される |
| RC-003 | `ICodexThreadStore` / `ICodexThreadRegistry` registration と `CodexAppServerChatClient` constructor binding | `IServiceCollection` registration chain が production path で有効で、ChatClient が registered store/registry と整合する | `src\MeAiUtility.MultiProvider.CodexAppServer\Configuration\CodexAppServerServiceExtensions.cs:20-25`; `src\MeAiUtility.MultiProvider.CodexAppServer\CodexAppServerChatClient.cs:23-34,37-55` | TP-006 | NotImplementedOrMismatch | `ICodexThreadStore` / `ICodexThreadRegistry` は登録されるが、public `CodexAppServerChatClient` ctor が registered store を受け取らず `new FileCodexThreadStore(...)` を直接生成している |
| RC-004 | `threadKey`, `threadId`, `threadName`, `workingDirectory`, `modelId`, `createdAt`, `lastUsedAt` の DTO 表現 | `ICodexThreadRegistry.ListAsync` / `TryGetByThreadKeyAsync` が `CodexThreadDescriptor` へ全フィールドを写像し、`threadKey` 主キー意味論を保持する | `src\MeAiUtility.MultiProvider.CodexAppServer\Threading\CodexThreadRegistry.cs:5-43`; `src\MeAiUtility.MultiProvider.CodexAppServer\Threading\CodexThreadDescriptor.cs:3-10` | TP-007, TP-008 | Done | DTO は internal record を直接公開せず、必要フィールドのみを public record に変換している |
| RC-004 | 未登録 key は `null`、I/O / serialization 異常は silent fallback しない | `TryGetByThreadKeyAsync` は missing key で `null`、store 側異常は例外伝播 | `src\MeAiUtility.MultiProvider.CodexAppServer\Threading\CodexThreadRegistry.cs:22-43`; `src\MeAiUtility.MultiProvider.CodexAppServer\Threading\FileCodexThreadStore.cs:139-199` | TP-008 | PartiallyDone | missing key は確認済み。I/O / parse 異常は code 上で明示的 `ProviderException` 化されるが、この pass では未実行 |

## Stub-to-Production Binding

| Test Point ID | Stub / fake / in-memory used in test | Production interface | Production concrete implementation | Production wiring / entrypoint | Status | Remaining work |
| --- | --- | --- | --- | --- | --- | --- |
| TP-001 | `ScriptedCodexTransport`, `StubCodexThreadStore` | `ICodexThreadStore` | `FileCodexThreadStore` | `CodexAppServerChatClient` public ctor → internal ctor → `CodexRpcSession` (`src\MeAiUtility.MultiProvider.CodexAppServer\CodexAppServerChatClient.cs:23-34,86-89`) | Bound | None |
| TP-002 | `ScriptedCodexTransport`, `StubCodexThreadStore` | `ICodexThreadStore` | `FileCodexThreadStore` | `CodexRpcSession.ResolveThreadAsync` + `FileCodexThreadStore.SaveAsync`; `AddCodexAppServer` registers `ICodexThreadStore` (`Configuration\CodexAppServerServiceExtensions.cs:20-25`) | Bound | None |
| TP-003 | `ScriptedCodexTransport`, `StubCodexThreadStore` | `ICodexThreadStore` | `FileCodexThreadStore` | `CodexAppServerChatClient.BuildRuntimeOptions` fail-fast → `CodexRpcSession` (`CodexAppServerChatClient.cs:245-250,485-497`) | Bound | None |
| TP-004 | `ScriptedCodexTransport`, `ExtensionParameters` | `ChatOptions.AdditionalProperties["meai.extensions"]` → `CodexRuntimeOptions` contract | `CodexAppServerChatClient.BuildRuntimeOptions` | `GetResponseAsync` / `GetStreamingResponseAsync` → `BuildRuntimeOptions` (`CodexAppServerChatClient.cs:72-90,111-141,206-271`) | Bound | None |
| TP-005 | `ScriptedCodexTransport`, `ExtensionParameters` | `ChatOptions.AdditionalProperties["meai.extensions"]` → `CodexRuntimeOptions` contract | `CodexAppServerChatClient.GetExtensionString/GetExtensionInt/ValidateThreadReuseOptions` | `GetResponseAsync` request validation path (`CodexAppServerChatClient.cs:319-360,485-497`) | Bound | None |
| TP-007 | `StubCodexThreadStore` | `ICodexThreadRegistry` | `CodexThreadRegistry`, `CodexThreadDescriptor` | `AddCodexAppServer` registers `ICodexThreadRegistry`; `CodexThreadRegistry.ListAsync` maps records to descriptors (`Configuration\CodexAppServerServiceExtensions.cs:23-24`; `Threading\CodexThreadRegistry.cs:5-19`) | Bound | None |
| TP-008 | `StubCodexThreadStore` | `ICodexThreadRegistry` | `CodexThreadRegistry`, `CodexThreadDescriptor` | `ICodexThreadRegistry.TryGetByThreadKeyAsync` public entrypoint (`Threading\ICodexThreadRegistry.cs:3-7`; `Threading\CodexThreadRegistry.cs:22-43`) | Bound | None |

## Test observations

| Test Point ID | Runtime Contract ID | Test artifact / Manual-only reason | Substitute used? | Expected observation | Actual observation / status | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| TP-001 | RC-001 | `tests\MeAiUtility.MultiProvider.CodexAppServer.Tests\CodexThreadReuseTests.cs: GetResponseAsync_ReuseOrCreateByKey_UsesStoredRecord` | Yes | 送信メッセージ列に `thread/start` が存在せず、`turn/start.params.threadId` が保存済み threadId と一致する | passes (net8.0 / net10.0) | `StubCodexThreadStore` の `LastUsedAt` 更新も同テストで確認 |
| TP-002 | RC-001 | `tests\MeAiUtility.MultiProvider.CodexAppServer.Tests\CodexThreadReuseTests.cs: GetResponseAsync_ReuseOrCreateByKey_CreatesAndSavesWhenMissing` | Yes | `thread/start` が 1 回送信され、応答 `result.thread.id` と `turn/start.params.threadId` が一致し、store 読み戻しで同じ `threadKey` に threadId が記録される | passes (net8.0 / net10.0) | save/read path と `threadName` 永続化を確認 |
| TP-003 | RC-001 | `tests\MeAiUtility.MultiProvider.CodexAppServer.Tests\CodexThreadReuseTests.cs: GetResponseAsync_ReuseOrCreateByKey_ThrowsWhenThreadKeyMissing` | Yes | `InvalidRequestException` が返り、`thread/start` / `turn/start` が送信されない | passes (net8.0 / net10.0) | fail-fast path を確認 |
| TP-004 | RC-002 | `tests\MeAiUtility.MultiProvider.CodexAppServer.Tests\CodexThreadReuseTests.cs: GetResponseAsync_Extensions_MapThreadReuseOptionsToRuntime` | Yes | `turn/start.params.threadId` が extension 指定値と一致し、`thread/start` が送信されない | passes (net8.0 / net10.0) | 実際には `ReuseOrCreateByKey` path と `threadStorePath` / `threadName` の反映を観測している |
| TP-005 | RC-002 | `tests\MeAiUtility.MultiProvider.CodexAppServer.Tests\CodexThreadReuseTests.cs: GetResponseAsync_Extensions_ThrowWhenTypeMismatch` | Yes | `InvalidRequestException` が返る。エラーメッセージに対象キーと期待型が含まれる | passes (net8.0 / net10.0) | 5 種の型不一致キーを `TestCase` で確認 |
| TP-006 | RC-003 | `tests\MeAiUtility.MultiProvider.CodexAppServer.Tests\ConfigurationTests\CodexAppServerServiceExtensionsTests.cs: AddCodexAppServer_RegistersSingletonServices` | No | `AddCodexAppServer` 後の service provider から `ICodexThreadStore` と `CodexAppServerChatClient` が解決できる | passes (net8.0 / net10.0) | 実テストは `ICodexThreadRegistry` 解決も確認するが、ChatClient が registered store を使うことまでは確認していない |
| TP-007 | RC-004 | `tests\MeAiUtility.MultiProvider.CodexAppServer.Tests\CodexThreadStoreAndRegistryTests.cs: CodexThreadRegistry_ListsAndGetsRecordsByThreadKey` | Yes | 戻り値一覧の各要素に `threadKey`, `threadId`, `threadName`, `workingDirectory`, `modelId`, `createdAt`, `lastUsedAt` が含まれ、`threadKey` 単位で識別できる | passes (net8.0 / net10.0) | 同一テストで list ordering と path passthrough も確認 |
| TP-008 | RC-004 | `tests\MeAiUtility.MultiProvider.CodexAppServer.Tests\CodexThreadStoreAndRegistryTests.cs: CodexThreadRegistry_ListsAndGetsRecordsByThreadKey` | Yes | 既存 key で descriptor 取得、未登録 key で `null`、いずれも silent fallback せず観測可能な結果になる | passes (net8.0 / net10.0) | 同一 test function が existing/missing 両方を検証 |

## Unresolved items

| ID | Type | Why unresolved | Recommended next agent | Target files / addresses |
| --- | --- | --- | --- | --- |
| VK-001 | contract-mismatch | `RC-003` が期待する「registered `ICodexThreadStore` / `ICodexThreadRegistry` を含む DI registration chain」が production `CodexAppServerChatClient` に結線していない。public ctor が `new FileCodexThreadStore(...)` を直接生成し、registered store を使用しない | `coverage-gap-resolution-slice.agent.md` | `src\MeAiUtility.MultiProvider.CodexAppServer\CodexAppServerChatClient.cs`, `src\MeAiUtility.MultiProvider.CodexAppServer\Configuration\CodexAppServerServiceExtensions.cs` |
| VK-002 | manual-only | 複数プロセス同時書き込み時の JSON 破損回避は selected test points では観測されておらず、real-environment での contention 確認が必要 | human review / manual verification | `src\MeAiUtility.MultiProvider.CodexAppServer\Threading\FileCodexThreadStore.cs` |

## Verdict

`BLOCKED_BY_CONTRACT_MISMATCH`

`RC-001`, `RC-002`, `RC-004` の selected scope は production code と selected tests の対応が確認できましたが、`RC-003` に contract mismatch があります。  
`CodexAppServerChatClient` の production ctor が registered `ICodexThreadStore` を使わず `FileCodexThreadStore` を直接 new しており、runtime-contract-kernel の DI binding 契約と一致していないため、この selected scope は blocking 扱いです。

## Handoff Packet

- Profile used: contract-kernel
- Source artifacts:
  - `plans\codex-thread-reuse-test-design-kernel.md`
  - `plans\codex-thread-reuse-runtime-contract-kernel.md`
  - `plans\codex-thread-reuse-change-risk-triage.md`
  - Selected test run output (`dotnet test ... CodexAppServer.Tests ...`)
- Selected contracts / IDs: RC-001, RC-002, RC-003, RC-004
- Selected test point IDs: TP-001, TP-002, TP-003, TP-004, TP-005, TP-006, TP-007, TP-008
- Files inspected:
  - `src\MeAiUtility.MultiProvider.CodexAppServer\CodexAppServerChatClient.cs`
  - `src\MeAiUtility.MultiProvider.CodexAppServer\CodexRpcSession.cs`
  - `src\MeAiUtility.MultiProvider.CodexAppServer\Configuration\CodexAppServerServiceExtensions.cs`
  - `src\MeAiUtility.MultiProvider.CodexAppServer\Options\CodexRuntimeOptions.cs`
  - `src\MeAiUtility.MultiProvider.CodexAppServer\Options\CodexAppServerProviderOptions.cs`
  - `src\MeAiUtility.MultiProvider.CodexAppServer\Threading\ICodexThreadStore.cs`
  - `src\MeAiUtility.MultiProvider.CodexAppServer\Threading\FileCodexThreadStore.cs`
  - `src\MeAiUtility.MultiProvider.CodexAppServer\Threading\ICodexThreadRegistry.cs`
  - `src\MeAiUtility.MultiProvider.CodexAppServer\Threading\CodexThreadRegistry.cs`
  - `src\MeAiUtility.MultiProvider.CodexAppServer\Threading\CodexThreadDescriptor.cs`
  - `src\MeAiUtility.MultiProvider.CodexAppServer\Threading\CodexThreadRecord.cs`
  - `tests\MeAiUtility.MultiProvider.CodexAppServer.Tests\CodexThreadReuseTests.cs`
  - `tests\MeAiUtility.MultiProvider.CodexAppServer.Tests\CodexThreadStoreAndRegistryTests.cs`
  - `tests\MeAiUtility.MultiProvider.CodexAppServer.Tests\ConfigurationTests\CodexAppServerServiceExtensionsTests.cs`
  - `tests\MeAiUtility.MultiProvider.CodexAppServer.Tests\Fakes\StubCodexThreadStore.cs`
- Files intentionally not inspected:
  - `src\MeAiUtility.MultiProvider.CodexAppServer\Stdio\*`（selected scope 外）
  - `tests\MeAiUtility.MultiProvider.CodexAppServer.Tests\Stdio\*`（selected scope 外）
  - 他 provider の source / tests（selected scope 外）
- Decisions made:
  - TP-001〜TP-005, TP-007, TP-008 の substitute binding は production interface / concrete implementation / entrypoint の 3 点が確認できたため `Bound`
  - TP-006 は production DI test が存在し成功しているため `Done`
  - `RC-003` は DI registration と ChatClient production ctor の接続不整合により `NotImplementedOrMismatch`
- Do not redo unless new evidence appears:
  - selected tests（32 cases across net8.0 / net10.0）は pass している
  - `RC-001`, `RC-002`, `RC-004` の selected fields / behaviors は selected scope 内で production evidence がある
  - blocking の主因は `RC-003` の production DI binding mismatch に集中している
- Remaining work:
  - `VK-001` contract-mismatch の修正（registered `ICodexThreadStore` を production `CodexAppServerChatClient` path に結線するか、contracts/tests を実装に合わせて再定義する）
  - `VK-002` manual-only contention check
- Recommended next step:
  - `coverage-gap-resolution-slice.agent.md` に `VK-001` を対象 gap として渡し、`CodexAppServerChatClient` と `AddCodexAppServer` の binding 契約を解消する
