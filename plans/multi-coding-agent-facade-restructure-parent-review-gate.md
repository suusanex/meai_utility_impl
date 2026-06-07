# Parent Review Gate

## Verdict per slice

| Slice ID | Verdict | Can implement now? | Parallel group | Blocking reason |
| --- | --- | --- | --- | --- |
| SL-001 | READY | Yes | G1-serial | なし。ただし実装範囲はこの gate の authorization に限定する。 |
| SL-002 | READY | Yes | G2-Codex | なし。ただし Core 変更禁止、Codex runtime project/test project に限定する。 |
| SL-003 | READY | Yes | G2-Copilot | なし。ただし Core 変更禁止、Copilot runtime project/test project に限定する。 |
| SL-004 | WAITING_DEPENDENCY | No | none | SL-002/SL-003 の production APIs が未実装。test-only surface で false PASS になる危険がある。 |
| SL-005 | WAITING_DEPENDENCY | No | none | SL-002/SL-003 の public API names、project paths、config sections が未実装。 |
| SL-006 | WAITING_DEPENDENCY | No | none | 実装対象 slice の verification result が未作成。 |

## Cross-slice contract review

| XC ID | Producer | Consumer | Status | Notes |
| --- | --- | --- | --- | --- |
| XC-001 | SL-001 | SL-002, SL-003, SL-004, SL-005 | AuthorizedForProduction | SL-001 は `MultiCodingAgentFacade.Core`、runtime-oriented exception semantics、`RuntimeName`、trace/request id、secret-safe logging helpers の土台を作る。health check API は作らない。 |
| XC-002 | SL-002 | SL-004, SL-006 | AuthorizedForProduction | SL-002 は typed request -> JSON-RPC params、thread reuse、approval/user-input、streaming update、production DI を実装する。 |
| XC-003 | SL-003 | SL-004, SL-006 | AuthorizedForProduction | SL-003 は typed request -> SDK wrapper/session config、model list、streaming update、production DI を実装する。 |
| XC-004 | SL-001, SL-002, SL-003 | SL-005, SL-006 | PartiallyAuthorized | SL-001 は `MultiCodingAgentFacade.slnx` / `MultiCodingAgentFacade.sln` と source/test project skeleton の土台だけ作る。docs/CI/release の最終参照は SL-005。 |
| XC-005 | SL-002, SL-003 | SL-004, SL-006 | Deferred | test substitute to production binding は runtime slices 後に SL-004 で扱う。 |

## Field continuity review

| Field / state / identifier | Required by | Source / producer | Consumer | Status | Notes |
| --- | --- | --- | --- | --- | --- |
| Public product name `MultiCodingAgentFacade` | all slices | parent Plan / SL-001 | SL-002..SL-006 | Authorized | namespace、assembly metadata、solution/project names の基準。 |
| Target frameworks `net8.0;net10.0` | SL-001, SL-004 | parent Plan / SL-001 | SL-004, SL-005 | Authorized | SL-001 の project skeleton はこの multi-target を維持する。 |
| Core namespace layout | SL-002, SL-003 | SL-001 | SL-002, SL-003 | Authorized | `MultiCodingAgentFacade.Core` を root とし、`Exceptions`、`Diagnostics`、`Options` を最小限使う。 |
| Runtime exception field `RuntimeName` | SL-002, SL-003, SL-004 | SL-001 | SL-002, SL-003 | Authorized | `ProviderName` を残さない。base exception name は `RuntimeFacadeException` とする。 |
| ActivitySource / logging category | SL-002, SL-003, SL-005 | SL-001 | SL-002..SL-005 | Authorized | `MultiCodingAgentFacade` semantics にする。 |
| `ProviderOverrideOptions` | SL-003 | SL-003 | SL-004, SL-005 | Deferred | SL-001 Core には置かない。Copilot-specific option として SL-003 へ委譲する。 |
| Old-name denylist | SL-004, SL-006 | parent Plan / SL-001 foundation | SL-004, SL-006 | Partial | SL-001 では production source/project graph のみ対象。docs/history allowlist は SL-004/SL-005/SL-006。 |
| Codex public request shape | SL-002, SL-004, SL-005 | SL-002 | SL-004, SL-005 | Authorized | `CodexAppServerTurnRequest` は `Prompt` を必須とし、Codex-specific option fields を持つ。 |
| Codex reasoning effort | SL-002, SL-004 | SL-002 | SL-004 | Authorized | Codex-specific `CodexReasoningEffort` を使い、`None` / `Minimal` / `Low` / `Medium` / `High` / `XHigh` を表す。 |
| Codex thread registry | SL-002, SL-004, SL-005 | SL-002 | SL-004, SL-005 | Authorized | `ICodexThreadRegistry` は Codex runtime public surface として SL-002 に含める。 |
| Copilot public request shape | SL-003, SL-004, SL-005 | SL-003 | SL-004, SL-005 | Authorized | `GitHubCopilotAgentRequest` は `Prompt` を必須とし、attachments/tools/MCP/agent/provider override など Copilot-specific fields を持つ。 |
| Runtime DI entrypoint names | SL-002, SL-003, SL-005 | SL-002, SL-003 | SL-005, SL-006 | Authorized | `AddCodexAppServerAgentRuntime` と `AddGitHubCopilotAgentRuntime` を採用する。 |
| Copilot operation phase | SL-003, SL-004 | SL-003 | SL-004 | Authorized | Core exception property ではなく Copilot-specific logging stage として保持する。 |

## Implementation authorization

- Authorized slices: SL-001, SL-002, SL-003
- Serialized slices: SL-001 -> SL-002/SL-003 -> SL-004/SL-005 -> SL-006
- Blocked slices: SL-004, SL-005, SL-006
- Human decision required: none

## Implementation progress

| Slice ID | Status | Evidence |
| --- | --- | --- |
| SL-001 | ImplementedPartialVerified | `plans/multi-coding-agent-facade-restructure-slice-SL-001-implementation-result.md` と `plans/multi-coding-agent-facade-restructure-slice-SL-001-verification-kernel.md`。 |
| SL-002 | ImplementedVerified | `plans/multi-coding-agent-facade-restructure-slice-SL-002-implementation-result.md` と `plans/multi-coding-agent-facade-restructure-slice-SL-002-verification-kernel.md`。 |
| SL-003 | ImplementedVerified | `plans/multi-coding-agent-facade-restructure-slice-SL-003-implementation-result.md` と `plans/multi-coding-agent-facade-restructure-slice-SL-003-verification-kernel.md`。 |
| SL-004 | ReadyForPrep | SL-002/SL-003 verification 後に扱う。 |
| SL-005 | ReadyForPrep | SL-002/SL-003 public API 確定後に扱う。 |
| SL-006 | WaitingDependency | SL-001..SL-005 の結果集約後に扱う。 |

## Parent instructions for slice-impl

SL-001 の実装は次に限定する。

1. `MultiCodingAgentFacade.Core`、`MultiCodingAgentFacade.CodexAppServer`、`MultiCodingAgentFacade.GitHubCopilot`、`MultiCodingAgentFacade.Samples` の source project skeleton を作る。
2. test project skeleton は `MultiCodingAgentFacade.Core.Tests`、`MultiCodingAgentFacade.CodexAppServer.Tests`、`MultiCodingAgentFacade.GitHubCopilot.Tests`、`MultiCodingAgentFacade.IntegrationTests` まで作る。ただし runtime API test migration は SL-004 へ残す。
3. Core へ移す production code は shared exceptions、diagnostics/logging、file attachment、reasoning effort、timeout/cancellation helper など横断的なものに限定する。
4. Core base exception は `RuntimeFacadeException` とし、runtime identifier は `RuntimeName` とする。旧 `MultiProviderException` / `ProviderName` semantics を残さない。
5. `ProviderFactory`、`ProviderRegistry`、`MultiProviderOptions`、`IProviderCapabilities`、`ExtensionParameters`、`ConversationExecutionOptions`、reflection discovery は production source から削除する。
6. OpenAI / Azure OpenAI / OpenAI compatible provider projects は新 solution/project graph から外す。残す場合は verification result に blocked reason を明記する。
7. `Directory.Build.props` から `Microsoft.Extensions.AI.*` と OpenAI package version properties を削除する。
8. `MultiCodingAgentFacade.slnx` と `MultiCodingAgentFacade.sln` を作る。CI/source-of-truth は `.slnx` とし、`.sln` は Visual Studio 互換用の同等 graph とする。
9. Codex/Copilot runtime behavior の詳細移植、DI/config entrypoint 実装、README 全面改訂、samples completion、real runtime E2E は行わない。
10. Required checks は少なくとも `dotnet restore MultiCodingAgentFacade.slnx` と `dotnet build MultiCodingAgentFacade.slnx --no-restore`。実行不能なら理由を verification result に残す。

SL-002 の実装は次に限定する。

1. `src/MultiCodingAgentFacade.CodexAppServer` と `tests/MultiCodingAgentFacade.CodexAppServer.Tests` を主な write scope とする。
2. public API は `CodexAppServerAgentClient`、`CodexAppServerTurnRequest`、`CodexAppServerTurnResponse`、`CodexAppServerStreamingUpdate`、`CodexReasoningEffort`、Codex thread types とする。
3. `CodexAppServerTurnRequest.Prompt` を必須にする。`IChatClient` / `ChatOptions` / `Microsoft.Extensions.AI` / `MeAiUtility.MultiProvider` は new Codex source に入れない。
4. `MultiCodingAgentFacade:CodexAppServer` section と `AddCodexAppServerAgentRuntime` を採用する。
5. production DI は `CodexAppServerAgentClient`、`DefaultCodexTransportFactory`、`SystemCodexProcessRunner`、`FileCodexThreadStore`、`CodexThreadRegistry` を束縛する。
6. Codex-specific `CodexReasoningEffort` は `None` / `Minimal` / `Low` / `Medium` / `High` / `XHigh` を持つ。
7. fake transport/store は tests に限定する。

SL-003 の実装は次に限定する。

1. `src/MultiCodingAgentFacade.GitHubCopilot` と `tests/MultiCodingAgentFacade.GitHubCopilot.Tests` を主な write scope とする。
2. public API は `GitHubCopilotAgentClient`、`GitHubCopilotAgentRequest`、`GitHubCopilotAgentResponse`、`GitHubCopilotStreamingUpdate`、model list API とする。
3. `GitHubCopilotAgentRequest.Prompt` を必須にする。`IChatClient` / `ChatOptions` / `Microsoft.Extensions.AI` / `MeAiUtility.MultiProvider` は new Copilot source に入れない。
4. `MultiCodingAgentFacade:GitHubCopilot` section と `AddGitHubCopilotAgentRuntime` を採用する。
5. `ProviderOverrideOptions` は Copilot-specific source に置く。Core へ戻さない。
6. old `CopilotOperation` 相当は exception property ではなく logging stage として保持する。
7. `GitHubCopilotEmbeddingAdapter` は移植しない。

## 最終監査

- `plan-slice-decomposition` から直接実装していない。
- SL-001 は slice-prep と parent review gate を通した。
- READY でない slice は実装承認していない。
- cross-slice contract は slice 内で完了扱いにしていない。
