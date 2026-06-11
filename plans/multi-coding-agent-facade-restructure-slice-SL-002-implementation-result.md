# Slice Implementation Result: SL-002

## Verdict

- Status: PARENT_PLAN_PARTIAL_WITH_FIX_CANDIDATES
- Reason: SL-002 の bounded acceptance は満たした。`CodexAppServerAgentClient` public API、typed request/response/streaming、Codex JSON-RPC production path、thread reuse/store/registry、DI/config entrypoint、fake transport tests を新 `MultiCodingAgentFacade.CodexAppServer` に移植した。Parent Plan 全体は SL-003 以降を待つため完了ではない。

## Changed files

- `src/MultiCodingAgentFacade.CodexAppServer/MultiCodingAgentFacade.CodexAppServer.csproj`
- `src/MultiCodingAgentFacade.CodexAppServer/Abstractions/ICodexProcessRunner.cs`
- `src/MultiCodingAgentFacade.CodexAppServer/Abstractions/ICodexTransport.cs`
- `src/MultiCodingAgentFacade.CodexAppServer/Abstractions/ICodexTransportDiagnostics.cs`
- `src/MultiCodingAgentFacade.CodexAppServer/Abstractions/ICodexTransportFactory.cs`
- `src/MultiCodingAgentFacade.CodexAppServer/Configuration/CodexAppServerServiceExtensions.cs`
- `src/MultiCodingAgentFacade.CodexAppServer/Options/CodexAppServerOptions.cs`
- `src/MultiCodingAgentFacade.CodexAppServer/Options/CodexRuntimeOptions.cs`
- `src/MultiCodingAgentFacade.CodexAppServer/Stdio/DefaultCodexTransportFactory.cs`
- `src/MultiCodingAgentFacade.CodexAppServer/Stdio/StdioCodexTransport.cs`
- `src/MultiCodingAgentFacade.CodexAppServer/Stdio/SystemCodexProcessRunner.cs`
- `src/MultiCodingAgentFacade.CodexAppServer/Threading/*`
- `src/MultiCodingAgentFacade.CodexAppServer/CodexAppServerAgentClient.cs`
- `src/MultiCodingAgentFacade.CodexAppServer/CodexAppServerStreamingUpdate.cs`
- `src/MultiCodingAgentFacade.CodexAppServer/CodexAppServerTurnRequest.cs`
- `src/MultiCodingAgentFacade.CodexAppServer/CodexAppServerTurnResponse.cs`
- `src/MultiCodingAgentFacade.CodexAppServer/CodexProcessExitedException.cs`
- `src/MultiCodingAgentFacade.CodexAppServer/CodexReasoningEffort.cs`
- `src/MultiCodingAgentFacade.CodexAppServer/CodexRpcSession.cs`
- `tests/MultiCodingAgentFacade.CodexAppServer.Tests/MultiCodingAgentFacade.CodexAppServer.Tests.csproj`
- `tests/MultiCodingAgentFacade.CodexAppServer.Tests/CodexAppServerAgentClientTests.cs`
- `tests/MultiCodingAgentFacade.CodexAppServer.Tests/Fakes/ScriptedCodexTransport.cs`
- `tests/MultiCodingAgentFacade.CodexAppServer.Tests/Fakes/StubCodexThreadStore.cs`

## Covered IDs

| ID | Kind | Status | Notes |
| --- | --- | --- | --- |
| RC-SL002-001 | Runtime contract | Done | `IChatClient` public surface を保持せず、typed Codex API を追加した。 |
| RC-SL002-002 | Runtime contract | Done | `Prompt`、model、working directory、approval policy、sandbox mode、network access、reasoning effort を JSON-RPC params へマップする。 |
| RC-SL002-003 | Runtime contract | PartiallyDone | delta/completed/error は typed response/update/exception へ接続済み。approval/user input は lower-level session handler を移植したが dedicated xUnit は SL-004 へ残る。 |
| RC-SL002-004 | Runtime contract | Done | thread reuse policy、thread id/key/name、file store、registry を新 API へ接続した。 |
| RC-SL002-005 | Runtime contract | Done | `MultiCodingAgentFacade:CodexAppServer` と `AddCodexAppServerAgentRuntime` を実装し、production services を DI 登録した。 |
| RC-SL002-006 | Runtime contract | Done | timeout/cancellation/process-exit/stderr path を fail-fast で伝播し、catch 経路は `Exception.ToString()` をログへ出すよう監査・修正した。 |
| XC-001 | Cross-slice contract | Consumed | SL-001 Core semantics を消費した。 |
| XC-002 | Cross-slice contract | ProducerReady | Codex typed API と JSON-RPC mapping を SL-004/SL-006 へ渡せる。 |
| XC-004 | Cross-slice contract | ProducerPartial | Codex public type names、project path、DI method、config section を SL-005/SL-006 へ供給できる。 |
| XC-005 | Cross-slice contract | ProducerPartial | fake transport/store tests と production transport/store/DI binding の対応を残した。SL-004 で追加 test migration を行う。 |

## Checks run

- `dotnet restore tests\MultiCodingAgentFacade.CodexAppServer.Tests\MultiCodingAgentFacade.CodexAppServer.Tests.csproj`: passed
- `dotnet build tests\MultiCodingAgentFacade.CodexAppServer.Tests\MultiCodingAgentFacade.CodexAppServer.Tests.csproj --no-restore`: passed
- `dotnet test tests\MultiCodingAgentFacade.CodexAppServer.Tests\MultiCodingAgentFacade.CodexAppServer.Tests.csproj --no-build --no-restore`: passed
- `dotnet restore MultiCodingAgentFacade.slnx`: passed with warnings
- `dotnet build MultiCodingAgentFacade.slnx --no-restore`: passed with warnings
- `dotnet test MultiCodingAgentFacade.slnx --no-restore --no-build`: passed
- Codex source/test denylist audit for `Microsoft.Extensions.AI`, `IChatClient`, `ChatOptions`, `ChatResponse`, `MeAiUtility.MultiProvider`, `MultiProvider:CodexAppServer`, `ConversationExecutionOptions`, `ExtensionParameters`: passed
- Codex source catch audit for `Exception.ToString()` logging: passed

## Checks not run

- real Codex CLI / app-server E2E: ManualOnly / out of scope for SL-002
- repository-wide old-name final audit: deferred to SL-004/SL-006 because old migration source remains intentionally present
- full approval/user-input dedicated behavioral tests: deferred to SL-004, while production session code is present

## Production binding evidence

- `CodexAppServerServiceExtensions.AddCodexAppServerAgentRuntime` registers `CodexAppServerAgentClient`, `DefaultCodexTransportFactory`, `SystemCodexProcessRunner`, `FileCodexThreadStore`, and `CodexThreadRegistry`.
- `CodexAppServerOptions.SectionName` is `MultiCodingAgentFacade:CodexAppServer`.
- test fakes live under `tests/MultiCodingAgentFacade.CodexAppServer.Tests/Fakes` only.
- `src/MultiCodingAgentFacade.CodexAppServer` does not reference old `MeAiUtility.MultiProvider.CodexAppServer` project.

## Remaining Work

- SL-003: move Copilot runtime implementation from old source to typed `GitHubCopilotAgentClient` API.
- SL-004: migrate broader tests, add dedicated approval/user-input/thread-store/DI tests, and run repository-wide old-name/dependency denylist.
- SL-005: update README/samples/CI/release references with Codex API names.
- SL-006: final cross-slice verification and residual decision.
- Residual warning: `GitHub.Copilot.SDK 0.2.1-preview.1` pulls `Nerdbank.MessagePack 1.0.2`, producing NU1902/NU1903 warnings outside Codex runtime.
- Residual warning: build uses .NET 11 preview SDK on this machine and prints NETSDK1057 preview-SDK messages.

## Handoff to parent

SL-002 is ready for parent aggregation. Do not mark parent Plan complete. SL-003 remains authorized and is the next implementation slice.
