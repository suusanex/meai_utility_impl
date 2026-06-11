# Slice Implementation Result: SL-001

## Verdict

- Status: PARENT_PLAN_PARTIAL_WITH_FIX_CANDIDATES
- Reason: SL-001 の承認範囲で新 solution/project graph と Core foundation は実装され、restore/build/test/audit は通った。一方で旧 `MeAiUtility.MultiProvider*` source/test directories は SL-002/SL-003 の移植元として残しており、parent acceptance condition 全体の旧名削除 PASS は SL-004/SL-006 へ残る。

## Changed files

- `Directory.Build.props`
- `MultiCodingAgentFacade.sln`
- `MultiCodingAgentFacade.slnx`
- `src/MultiCodingAgentFacade.Core/MultiCodingAgentFacade.Core.csproj`
- `src/MultiCodingAgentFacade.Core/Exceptions/RuntimeFacadeException.cs`
- `src/MultiCodingAgentFacade.Core/Exceptions/RuntimeExceptions.cs`
- `src/MultiCodingAgentFacade.Core/Diagnostics/AgentTelemetry.cs`
- `src/MultiCodingAgentFacade.Core/Diagnostics/LoggingExtensions.cs`
- `src/MultiCodingAgentFacade.Core/Options/FileAttachment.cs`
- `src/MultiCodingAgentFacade.Core/Options/ReasoningEffortLevel.cs`
- `src/MultiCodingAgentFacade.CodexAppServer/MultiCodingAgentFacade.CodexAppServer.csproj`
- `src/MultiCodingAgentFacade.CodexAppServer/CodexAppServerRuntimeMarker.cs`
- `src/MultiCodingAgentFacade.GitHubCopilot/MultiCodingAgentFacade.GitHubCopilot.csproj`
- `src/MultiCodingAgentFacade.GitHubCopilot/GitHubCopilotRuntimeMarker.cs`
- `src/MultiCodingAgentFacade.Samples/MultiCodingAgentFacade.Samples.csproj`
- `src/MultiCodingAgentFacade.Samples/Program.cs`
- `tests/MultiCodingAgentFacade.Core.Tests/MultiCodingAgentFacade.Core.Tests.csproj`
- `tests/MultiCodingAgentFacade.Core.Tests/RuntimeFacadeExceptionTests.cs`
- `tests/MultiCodingAgentFacade.CodexAppServer.Tests/MultiCodingAgentFacade.CodexAppServer.Tests.csproj`
- `tests/MultiCodingAgentFacade.CodexAppServer.Tests/CodexAppServerRuntimeMarkerTests.cs`
- `tests/MultiCodingAgentFacade.GitHubCopilot.Tests/MultiCodingAgentFacade.GitHubCopilot.Tests.csproj`
- `tests/MultiCodingAgentFacade.GitHubCopilot.Tests/GitHubCopilotRuntimeMarkerTests.cs`
- `tests/MultiCodingAgentFacade.IntegrationTests/MultiCodingAgentFacade.IntegrationTests.csproj`
- `tests/MultiCodingAgentFacade.IntegrationTests/OptInIntegrationPlaceholderTests.cs`

## Covered IDs

| ID | Kind | Status | Notes |
| --- | --- | --- | --- |
| RC-SL001-001 | Runtime contract | Done | `MultiCodingAgentFacade.slnx` と `MultiCodingAgentFacade.sln` の project graph を作成し、restore/build を確認した。 |
| RC-SL001-002 | Runtime contract | Done | Core exception/telemetry は `RuntimeName` / `MultiCodingAgentFacade` semantics にした。 |
| RC-SL001-003 | Runtime contract | PartiallyDone | new source graph には provider factory/registry/options/reflection discovery を入れていない。旧 source は移植元として残留。 |
| RC-SL001-004 | Runtime contract | PartiallyDone | new solution graph から OpenAI/Azure/OpenAI compatible project を除外した。旧 directories は移植元・残留整理対象として残る。 |
| XC-001 | Cross-slice contract | ProducerReady | Core ownership、shared public naming、`RuntimeName` semantics を downstream へ渡せる。 |
| XC-004 | Cross-slice contract | ProducerPartial | solution/project paths は提供済み。docs/CI/release references は SL-005。 |
| TP-SL001-001 | Test point | Done | `dotnet restore MultiCodingAgentFacade.slnx`、`dotnet build MultiCodingAgentFacade.slnx --no-restore`、`dotnet build MultiCodingAgentFacade.sln --no-restore` を実行。 |
| TP-SL001-002 | Test point | Done | source-only audit で new graph source に OpenAI/Azure/OpenAICompatible 文字列なし。 |
| TP-SL001-003 | Test point | Done | source-only audit で new graph source に `IChatClient` / `ChatOptions` / `ChatResponse` / `ChatResponseUpdate` なし。 |
| TP-SL001-004 | Test point | Done | source-only audit で new graph source に `ProviderFactory` 等 denylist なし。 |
| TP-SL001-005 | Test point | PartiallyDone | direct project references から `Microsoft.Extensions.AI.*` は除去済み。ただし `GitHub.Copilot.SDK` の推移依存として `Microsoft.Extensions.AI.Abstractions` が restore artifacts に現れる。 |
| TP-SL001-006 | Test point | Done | source-only audit で reflection discovery pattern なし。 |

## Checks run

- `dotnet restore MultiCodingAgentFacade.slnx`: passed with warnings
- `dotnet build MultiCodingAgentFacade.slnx --no-restore`: passed with warnings
- `dotnet test MultiCodingAgentFacade.slnx --no-restore --no-build`: passed
- `dotnet build MultiCodingAgentFacade.sln --no-restore`: passed with warnings
- source-only denylist audit for old MEAI/public chat/provider terms: passed
- source-only denylist audit for OpenAI/Azure/OpenAICompatible: passed
- source-only reflection discovery audit: passed

## Checks not run

- real Codex CLI / app-server E2E: ManualOnly / out of scope for SL-001
- real GitHub Copilot authenticated E2E: ManualOnly / out of scope for SL-001
- full old-name audit across entire repository: deferred to SL-004/SL-006 because old migration source remains intentionally present

## Production binding evidence

- `src/MultiCodingAgentFacade.CodexAppServer/MultiCodingAgentFacade.CodexAppServer.csproj` references `..\MultiCodingAgentFacade.Core\MultiCodingAgentFacade.Core.csproj`.
- `src/MultiCodingAgentFacade.GitHubCopilot/MultiCodingAgentFacade.GitHubCopilot.csproj` references `..\MultiCodingAgentFacade.Core\MultiCodingAgentFacade.Core.csproj`.
- `MultiCodingAgentFacade.slnx` and `MultiCodingAgentFacade.sln` include only `MultiCodingAgentFacade.*` project paths.
- `Directory.Build.props` no longer defines `MicrosoftExtensionsAiAbstractionsVersion` or `MicrosoftExtensionsAiOpenAiVersion`.

## Remaining Work

- SL-002: move Codex runtime implementation from old source to typed `CodexAppServerAgentClient` API.
- SL-003: move Copilot runtime implementation from old source to typed `GitHubCopilotAgentClient` API.
- SL-004: migrate real tests and add full old-name/dependency denylist gate.
- SL-005: update README/samples/CI/release references.
- SL-006: final cross-slice verification and residual decision.
- Residual warning: `GitHub.Copilot.SDK 0.2.1-preview.1` pulls `Nerdbank.MessagePack 1.0.2`, producing NU1902/NU1903 vulnerability warnings.
- Residual warning: build uses .NET 11 preview SDK on this machine and prints NETSDK1057 preview-SDK messages.

## Handoff to parent

SL-001 is ready for parent aggregation. Do not mark parent Plan complete. Authorize SL-002 and SL-003 prep next only after parent updates execution state.
