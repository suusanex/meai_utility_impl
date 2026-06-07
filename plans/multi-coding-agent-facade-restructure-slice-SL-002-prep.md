# Slice Preparation Result: SL-002

## Verdict

- Status: READY_FOR_PARENT_REVIEW
- Reason: SL-002 の bounded scope、SL-001 の Core foundation 消費条件、既存 Codex implementation / tests / `plans/codex-*` の runtime contract evidence を整理できた。blocking human decision はない。ただし typed public API と DI/config/exception 置換は non-trivial なので、parent review gate で実装承認が必要。

## Generated / drafted artifacts

- Per-slice change-risk-triage: `standard-slice`。implementation-realization risk は `Present`。既存 Codex 実装は `IChatClient` / `ChatOptions` / `Microsoft.Extensions.AI` / `ProviderException` / `MultiProvider:CodexAppServer` 前提で、新 API は typed client/request/response/streaming へ置換する必要がある。
- Implementation-contract-kernel: Drafted。旧 Codex 実装を migration source として使い、`CodexAppServerAgentClient` family、runtime options、JSON-RPC mapping、thread reuse、DI/config、Core exception mapping、旧名 denylist を固定する。
- Implementation-contract-review-kernel: Required。typed request/response の exact public surface、Core exception への例外変換、`CodexThreadRegistry` の public ownership、config section rename が cross-slice producer contract になるため。
- Runtime-contract-kernel: Drafted。`RC-SL002-001` から `RC-SL002-006` を slice-local contract として定義。
- Test-design-kernel: Drafted。fake transport unit tests、thread store tests、DI/config tests、public surface denylist、production binding audit、streaming update tests を test point に分解。

## Bounded parent Plan pass / Guardrail Focus

SL-002 は FR-006, FR-008, FR-010 と AC-007、AC-008 の Codex 部分だけを扱う。FR-001/AC-011 の旧名除去は「Codex runtime source/test の範囲」に限定して扱い、repository 全体の old-name final audit は SL-004/SL-006 へ deferred。

確認した source evidence:

- 新 `src/MultiCodingAgentFacade.CodexAppServer` は現状 `CodexAppServerRuntimeMarker` と project skeleton のみ。
- 新 Core は `RuntimeFacadeException`、`RuntimeInvalidRequestException`、`RuntimeTimeoutException`、`RuntimeFeatureNotSupportedException`、`ReasoningEffortLevel` を提供済み。
- 旧 `src/MeAiUtility.MultiProvider.CodexAppServer` は JSON-RPC、stdio transport、approval/user-input、thread reuse、thread store/registry を実装済みだが、旧 namespace、旧 exceptions、`Microsoft.Extensions.AI`、`IChatClient` に依存する。
- `plans/codex-app-server-runtime-contract-kernel.md` は Codex protocol schema の主要補正を確定済み。`networkAccess` は `turn/start.sandboxPolicy` 配下、`turn/failed` notification は存在せず `turn/completed.turn.status == "failed"`。
- `plans/codex-app-server-coverage-gap-resolution-slice.md` と `plans/codex-thread-reuse-coverage-gap-resolution-slice.md` により、旧実装側の RC-002-b/c/f と RC-003 の selected gap は修正済み。ただし formal verification 再実行は未実施。
- 新 test project は xUnit skeleton。旧 Codex tests は NUnit なので、SL-002 実装では新 test project の xUnit style へ移植するのが自然。

## Non-goals

- Copilot runtime migration。
- README 全面改訂。
- real Codex CLI / app-server E2E。
- OpenAI / Azure / MEAI adapter 互換。
- `IChatClient` adapter / bridge の温存。
- cross-slice contract の最終完了判定。
- repository 全体の旧名・旧依存 final audit。

## RC / TP / XC ledger

| ID | Kind | Owned / Consumed / Deferred | Notes |
| --- | --- | --- | --- |
| RC-SL002-001 | Runtime contract | Owned | `CodexAppServerAgentClient`、`CodexAppServerTurnRequest`、`CodexAppServerTurnResponse`、`CodexAppServerStreamingUpdate` を public surface とし、`IChatClient` / `ChatOptions` / `ChatResponse*` を公開面から除去する。 |
| RC-SL002-002 | Runtime contract | Owned | typed request fields を initialize / thread/start / turn/start JSON-RPC params へ正しくマップする。 |
| RC-SL002-003 | Runtime contract | Owned | delta / turn completed / failed / interrupted / error / approval / user input notification を typed response / streaming update / Core runtime exceptions へ変換する。 |
| RC-SL002-004 | Runtime contract | Owned | thread reuse policy、thread id/key/name/store path、file thread store、thread registry を typed API へ接続する。 |
| RC-SL002-005 | Runtime contract | Owned | `MultiCodingAgentFacade:CodexAppServer` DI/config から production client、transport factory、thread store、registry が解決される。 |
| RC-SL002-006 | Runtime contract | Owned | timeout/cancellation/process-exit/stderr diagnostics を fail-fast で伝播し、catch-and-wrap 時は `Exception.ToString()` を trace/log に出す。 |
| IC-SL002-001 | Implementation contract | Owned | 旧 Codex source は migration source。旧 public provider surface は保存しない。 |
| IC-SL002-002 | Implementation contract | Owned | `ProviderName` semantics は `RuntimeName` semantics へ置換し、例外は `RuntimeFacadeException` family を使う。 |
| IC-SL002-003 | Implementation contract | Owned | `MultiProvider:CodexAppServer` は `MultiCodingAgentFacade:CodexAppServer` へ変更する。 |
| IC-SL002-004 | Implementation contract | Owned | default thread store path は旧 `MeAiUtility` を含めず、`MultiCodingAgentFacade/CodexAppServer/threads.json` 相当にする。 |
| IC-SL002-005 | Implementation contract | Owned | fake transport / stub store は tests に限定し、production DI は `StdioCodexTransport` / `SystemCodexProcessRunner` / `FileCodexThreadStore` に束縛する。 |
| TP-SL002-001 | Test point | Owned | `dotnet build/test MultiCodingAgentFacade.slnx` と new Codex test project の xUnit tests。 |
| TP-SL002-002 | Test point | Owned | new Codex source/test public surface に `IChatClient` / `ChatOptions` / `Microsoft.Extensions.AI` / `MeAiUtility.MultiProvider` が残らないこと。 |
| TP-SL002-003 | Test point | Owned | request mapping: initialize、initialized、thread/start、turn/start、sandboxPolicy、networkAccess、reasoning effort。 |
| TP-SL002-004 | Test point | Owned | response mapping: delta aggregation、completed、failed、interrupted、retryable/non-retryable error、approval、user input。 |
| TP-SL002-005 | Test point | Owned | streaming turn が `CodexAppServerStreamingUpdate` を逐次返し、consumer cancellation / timeout で cleanup する。 |
| TP-SL002-006 | Test point | Owned | thread reuse/store/registry: AlwaysNew、ReuseByThreadId、ReuseOrCreateByKey、store persistence、registry list/get。 |
| TP-SL002-007 | Test point | Owned | DI/config section `MultiCodingAgentFacade:CodexAppServer` で production services が singleton 解決され、registered store を実使用する。 |
| TP-SL002-008 | Test point | Owned | exceptions/logging: wrap する例外は `RuntimeName == "CodexAppServer"` で、捨てる/変換する前に `Exception.ToString()` をログへ出す。 |
| XC-001 | Cross-slice contract | Consumed | SL-001 の `RuntimeFacadeException` / `RuntimeName` / Core namespace を消費する。 |
| XC-002 | Cross-slice contract | Owned / Producer | Codex typed API と JSON-RPC mapping を SL-004/SL-006 へ渡す。slice 内で完了扱いしない。 |
| XC-004 | Cross-slice contract | ProducerPartial | Codex public type names、DI method、config section を SL-005/SL-006 へ供給する。 |
| XC-005 | Cross-slice contract | Owned / Producer | fake transport/store tests と production transport/store/DI binding の対応を SL-004/SL-006 へ渡す。 |

## Production binding requirements

- `src/MultiCodingAgentFacade.CodexAppServer` に production implementation を置き、旧 `src/MeAiUtility.MultiProvider.CodexAppServer` を参照し続けない。
- `CodexAppServerAgentClient` は `ICodexTransportFactory`、`ICodexThreadStore`、logger/logger factory を production DI から受け取る。constructor 内で registered service を迂回して直接 `new FileCodexThreadStore` する経路を PASS にしない。
- `AddCodexAppServer` 相当の entrypoint は `MultiCodingAgentFacade:CodexAppServer` を読む。
- fake transport / scripted transport / stub thread store は test project に限定する。
- `StdioCodexTransport`、`SystemCodexProcessRunner`、`FileCodexThreadStore` が production graph に存在し、fake-only success になっていないことを verification で確認する。
- 旧 `Microsoft.Extensions.AI.*` dependency、`IChatClient`、`ChatOptions`、`ConversationExecutionOptions`、`ExtensionParameters` を new Codex production source に残さない。
- 例外変換時は AGENTS.md に従い、全ての catch-and-wrap / catch-and-drop path で `Exception.ToString()` を trace/log に出す。

## Cross-slice risks to parent-review

- typed request/response の exact property set は parent review で承認した方がよい。既存 fields は prompt、model id、reasoning effort、working directory、approval policy、sandbox mode、network access、timeout seconds、auto approve、thread reuse fields、diagnostics capture、service name、summary、personality。
- `ICodexThreadRegistry` を public API として SL-002 に含めるか、SL-004/SL-005 へ defer するかは parent review で範囲固定が必要。ただし既存 thread reuse plan では public registry が requirement になっている。
- Core の `ReasoningEffortLevel` は `Low/Medium/High/XHigh` で、Codex schema は `none/minimal/low/medium/high/xhigh`。`none/minimal` を Codex-specific enum/string として扱うか要 review。
- old source の `ClientName` default や error message に `MeAiUtility.MultiProvider` が残るため、SL-002 source-local old-name audit が必要。
- `codex-thread-reuse-verification-kernel.md` の formal verdict は古い `BLOCKED_BY_CONTRACT_MISMATCH` のまま。gap resolution は済んでいるが、SL-002 prep では `Done` 扱いにせず、migration source evidence として扱う。

## Unresolved items

- Exact typed API fields and shapes: parent review required。
- `ICodexThreadRegistry` public inclusion in SL-002 vs later slice: parent review required。
- Codex-specific option representation for `none` / `minimal` reasoning effort: parent review required。
- real app-server protocol drift: ManualOnly / out of scope。
- full old-name/dependency audit across repository: deferred to SL-004/SL-006。
- formal re-verification of old `plans/codex-*` gap resolutions: useful evidence only; SL-002 implementation should verify new code directly。

## Stop condition

slice-prep はここで停止。実装・ファイル編集は行っていない。Parent review gate が `Can implement now? = Yes` を出すまで、SL-002 を slice-impl に渡してはいけない。
