# Slice Preparation Result: SL-003

## Verdict

- Status: READY_FOR_PARENT_REVIEW
- Reason: SL-003 の bounded scope、旧 Copilot 実装、旧 Copilot tests、SL-001 の新 Core foundation を照合し、per-slice risk / implementation contract / runtime contract / test design を準備できた。実装前に parent review gate で public request shape、DI entrypoint 名、Copilot-specific `ProviderOverrideOptions` / operation phase の所有場所を承認する必要はあるが、現時点で blocking human decision はない。

## Generated / drafted artifacts

- Per-slice change-risk-triage: `standard-slice`。implementation-realization risk は `Present`。旧実装が `IChatClient` / `ChatOptions` / `ConversationExecutionOptions` に依存し、GitHub Copilot SDK、streaming event、DI/config、secret-safe logging、test substitute と production wrapper の接続を同時に移す必要があるため。
- Implementation-contract-kernel: Drafted。typed API、wrapper contract、request field mapping、exception/logging semantics、DI/config production binding、削除対象 surface を定義。
- Implementation-contract-review-kernel: Required。`GitHubCopilotAgentRequest` の exact shape、new DI method name、Copilot-specific option type ownership、operation phase を exception property として残すか log stage に寄せるかが non-trivial。
- Runtime-contract-kernel: Drafted。`RC-SL003-001` から `RC-SL003-006` を slice-local contract として定義。
- Test-design-kernel: Drafted。typed API request mapping、streaming、model validation、DI/config、secret-safe logging、old API denylist、production binding を test point に分解。

## Bounded parent Plan pass / Guardrail Focus

SL-003 は FR-005, FR-007, FR-009 と AC-006、AC-008 の Copilot 部分のみを扱う。SL-001 の `MultiCodingAgentFacade.Core`、`RuntimeFacadeException`、`RuntimeName`、`AgentTelemetry`、`FileAttachment`、`ReasoningEffortLevel` を消費する。Codex runtime、docs/CI/release、full old-name audit、real authenticated Copilot E2E は扱わない。

確認した source evidence:

- 旧 `GitHubCopilotChatClient` は `IChatClient` を実装しつつ、model list、request validation、streaming wrapper、attachments、skill directories、disabled skills、timeout、provider override、stage logging を持つ。
- 旧 `ICopilotSdkWrapper` は `SupportsStreaming`、`ListModelsAsync`、`SendAsync`、`SendStreamingAsync`、`CopilotSessionConfig`、`CopilotStreamingUpdate` を持つ。
- 旧 `GitHubCopilotSdkWrapper` は `GitHub.Copilot.SDK` alias、session config mapping、SDK event streaming、CLI diagnostics、secret-aware provider override logging を持つ。
- 旧 DI は `MultiProvider:GitHubCopilot` section と `AddGitHubCopilotProvider` / `AddGitHubCopilotSdkWrapper` を使うため、FR-008/FR-007 に合わせて新 section / new entrypoint が必要。
- 新 `src/MultiCodingAgentFacade.GitHubCopilot` は marker と project skeleton のみ。Core 参照と `GitHub.Copilot.SDK` package は存在する。

## Non-goals

- Codex runtime migration。
- README / migration note / samples / CI / release zip の全面更新。
- real GitHub Copilot authenticated E2E。
- NuGet publish。
- embedding API の後継作成。
- `Microsoft.Extensions.AI.IChatClient` adapter / bridge / compatibility wrapper。
- cross-slice contract の完了判定。

## RC / TP / XC ledger

| ID | Kind | Owned / Consumed / Deferred | Notes |
| --- | --- | --- | --- |
| RC-SL003-001 | Runtime contract | Owned | `GitHubCopilotAgentClient` / request / response / streaming update / model list API が `IChatClient` なしで動作する。 |
| RC-SL003-002 | Runtime contract | Owned | typed request fields が `CopilotSessionConfig` / SDK invocation へ落ちる。対象は model, reasoning, streaming, timeout, attachments, skills, tools, MCP, agent, provider override, infinite sessions。 |
| RC-SL003-003 | Runtime contract | Owned | streaming は SDK/wrapper event 由来 update を使い、final text の空白分割疑似 streaming を使わない。 |
| RC-SL003-004 | Runtime contract | Owned | model list と unknown model / unsupported reasoning / invalid timeout / invalid attachment は fail-fast する。 |
| RC-SL003-005 | Runtime contract | Owned | DI/config は `MultiCodingAgentFacade:GitHubCopilot` と production wrapper に接続し、provider-only/default wrapper は fail-fast。 |
| RC-SL003-006 | Runtime contract | Owned | exceptions/logging は `RuntimeName=GitHubCopilot`、`RuntimeFacadeException` 系、`Exception.ToString()` trace、secret-safe logging を維持する。 |
| IC-SL003-001 | Implementation contract | Owned | public API は typed Copilot API に限定し、`IChatClient` / `ChatOptions` / `ChatResponse` / `ChatResponseUpdate` を new source public surface に出さない。 |
| IC-SL003-002 | Implementation contract | Owned | `ProviderOverrideOptions` と `InfiniteSessionOptions` は Copilot-specific として `MultiCodingAgentFacade.GitHubCopilot` 側に置く。Core へ戻さない。 |
| IC-SL003-003 | Implementation contract | Owned | `GitHubCopilotEmbeddingAdapter` は移植しない。parent FR-004 の embedding 関連削除方針に従う。 |
| IC-SL003-004 | Implementation contract | Owned | old `CopilotRuntimeException.Operation` 相当は parent review 後、Copilot-specific property か stage logging として保持する。Core の `ProviderName` semantics は復活させない。 |
| TP-SL003-001 | Test point | Owned | new Copilot public source に `IChatClient` / MEAI chat surface が残らないことを audit。 |
| TP-SL003-002 | Test point | Owned | non-streaming typed request が wrapper invocation へ正しく mapping される。 |
| TP-SL003-003 | Test point | Owned | streaming update が wrapper deltas を順次返し、unsupported wrapper では fail-fast する。 |
| TP-SL003-004 | Test point | Owned | model list、unknown model、unsupported reasoning、invalid timeout、invalid attachments を検証する。 |
| TP-SL003-005 | Test point | Owned | DI/config section と production wrapper binding、default wrapper fail-fast を検証する。 |
| TP-SL003-006 | Test point | Owned | provider override secret、GitHub token、prompt 本文、attachment path の logging leakage を検証する。 |
| TP-SL003-007 | Test point | Owned | exceptions は `RuntimeName` と trace を持ち、wrap 時に `Exception.ToString()` が trace/log に出る。 |
| TP-SL003-008 | Test point | Deferred | opt-in real Copilot E2E は通常 CI では skip / ManualOnly。SL-004/Integration 側へ引き継ぐ。 |
| XC-001 | Cross-slice contract | Consumed | SL-001 の Core exception/logging/`RuntimeName` semantics を消費する。 |
| XC-003 | Cross-slice contract | Owned / Producer | Copilot typed API と SDK mapping を SL-004/SL-006 へ渡す。ただし SL-003 内で完了扱いしない。 |
| XC-005 | Cross-slice contract | Producer / Deferred | production runtime client と test substitute の binding evidence を SL-004 へ渡す。 |

## Production binding requirements

- `src/MultiCodingAgentFacade.GitHubCopilot` の production client が production `ICopilotSdkWrapper` / `GitHubCopilotSdkWrapper` を使うこと。test-only fake client を production API として扱わない。
- config section は `MultiCodingAgentFacade:GitHubCopilot`。旧 `MultiProvider:GitHubCopilot` を新 entrypoint の source of truth にしない。
- `AddGitHubCopilotProvider` 互換名は移植しない。新 DI entrypoint は parent review で承認された名前にする。
- `GitHub.Copilot.SDK` は runtime project の SDK binding として維持可。ただし public API に SDK 型を漏らさない。
- `Microsoft.Extensions.AI` public chat surface を new source に持ち込まない。SDK の推移依存として restore artifacts に現れる可能性は SL-006 で分類する。
- `RuntimeFacadeException` 系に wrap する場合、例外を捨てる箇所では `Exception.ToString()` を trace/log に出す。
- provider override の `ApiKey` / `BearerToken` / GitHub token / prompt 本文は log に出さない。出す場合は presence flag か masked value のみ。
- `GitHubCopilotEmbeddingAdapter` と embedding registration は移植しない。

## Cross-slice risks to parent-review

- SL-002 と SL-003 がそれぞれ DI/config extension を作るため、shared naming convention がずれると SL-005/SL-006 で docs/CI が false PASS になる。
- `ProviderOverrideOptions` は SL-001 で Core に置かない判断済み。SL-003 で Copilot-specific として復活させる範囲を parent review で固定する必要がある。
- old Copilot tests は NUnit/Moq/MEAI surface 前提。SL-003 が直接 test migration しすぎると SL-004 の責務を奪うため、slice-local tests は production binding と request mapping に限定する。
- `GitHub.Copilot.SDK 0.2.1-preview.1` の推移依存・脆弱性 warning は SL-001 検証で residual。SL-003 で SDK upgrade を勝手に行うべきではない。
- SDK permission hooks / user-input details は stable API evidence が限定的。unsupported option は silently ignored ではなく fail-fast または explicit deferred にする。

## Unresolved items

- exact `GitHubCopilotAgentRequest` property shape: `Prompt` 単体か message list を持つかは parent review required。
- exact DI entrypoint name: `AddGitHubCopilot...` 系の新名は parent review required。
- operation phase preservation: old `CopilotOperation` 相当を Copilot-specific exception property として残すか、stage logging に寄せるか parent review required。
- SDK event taxonomy: delta / progress / disconnected / final の完全 semantics は ManualOnly / verification residual candidate。
- real authenticated Copilot E2E: out of scope for SL-003 automated pass。

## Stop condition

slice-prep はここで停止。実装・ファイル編集は行っていない。Parent review gate が `Can implement now? = Yes` を出すまで、SL-003 を slice-impl に渡してはいけない。
