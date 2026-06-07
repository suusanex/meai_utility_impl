# Slice Preparation Result: SL-001

## Verdict

- Status: READY_FOR_PARENT_REVIEW
- Reason: SL-001 の bounded scope、既存 project/source inventory、implementation-realization risk、slice-local RC/TP を整理できた。実装前に parent review gate で Core の公開例外名・project skeleton 範囲・旧名 denylist の扱いを承認する必要があるが、現時点で blocking human decision はない。

## Generated / drafted artifacts

- Per-slice change-risk-triage: `standard-slice`。implementation-realization risk は `Present`。既存 Core が `Microsoft.Extensions.AI`、`IChatClient`、provider switching、`ProviderName` semantics、reflection discovery に依存しているため。
- Implementation-contract-kernel: Drafted。新 solution/project graph、Core 抽出対象、削除対象、dependency denylist、no-reflection、production binding requirement を定義。
- Implementation-contract-review-kernel: Required。Core shared exception names、`RuntimeName` semantics、SL-001 で作る runtime project skeleton の範囲が cross-slice producer contract になるため parent review が必要。
- Runtime-contract-kernel: Drafted。`RC-SL001-001` から `RC-SL001-004` を slice-local contract として定義。
- Test-design-kernel: Drafted。restore/build、project graph denylist、old API/source denylist、no-reflection audit、production project reference audit を test point に分解。

## Bounded parent Plan pass / Guardrail Focus

SL-001 は FR-001, FR-002, FR-003, FR-004, FR-011, FR-013, FR-014 の土台だけを扱う。AC-002, AC-003, AC-004, AC-005, AC-011 は「土台」までで、parent acceptance condition 全体の完了扱いは SL-004/SL-006 へ deferred。

確認した source evidence:

- `MeAiUtility.sln` は旧 `MeAiUtility.MultiProvider.*`、OpenAI/Azure/Copilot/Samples/tests を含む。
- `MeAiUtility.slnx` は現状 Codex/Core/Codex test のみで、`MeAiUtility.sln` と不一致。
- `Directory.Build.props` に `MicrosoftExtensionsAiAbstractionsVersion` / `MicrosoftExtensionsAiOpenAiVersion` が残る。
- `src/MeAiUtility.MultiProvider` は `Microsoft.Extensions.AI.Abstractions` に依存し、`IChatClient` factory/registry を持つ。
- `ProviderFactory` は assembly scan / reflection discovery を使うため、AGENTS.md の reflection 原則禁止にも抵触する。
- `MultiProviderException` / telemetry は `ProviderName` / `ProviderId` / `MeAiUtility.MultiProvider` semantics を持つ。

## Non-goals

- Codex / Copilot typed runtime behavior の詳細移植。
- `CodexAppServerAgentClient` / `GitHubCopilotAgentClient` の request mapping 完成。
- README 全面改訂。
- samples の完成。
- real runtime E2E。
- cross-slice contract の完了判定。
- `Microsoft.Extensions.AI` 依存除去の最終 PASS 判定。SL-001 では構造的土台と削除方針まで。

## RC / TP / XC ledger

| ID | Kind | Owned / Consumed / Deferred | Notes |
| --- | --- | --- | --- |
| RC-SL001-001 | Runtime contract | Owned | `MultiCodingAgentFacade.sln` / `.slnx` と source/test project graph が `net8.0;net10.0` で restore/build 可能な構造を持つ。 |
| RC-SL001-002 | Runtime contract | Owned | Core shared semantics は provider ではなく runtime を表す。`ProviderName` は `RuntimeName` 相当に置換する。 |
| RC-SL001-003 | Runtime contract | Owned | `ProviderFactory`、`ProviderRegistry`、`MultiProviderOptions`、`IProviderCapabilities`、`ExtensionParameters`、`ConversationExecutionOptions` は production public surface から削除対象。 |
| RC-SL001-004 | Runtime contract | Owned | OpenAI / AzureOpenAI / OpenAI compatible project と MEAI adapter dependency を新 project graph から外す。 |
| IC-SL001-001 | Implementation contract | Owned | Core へ移す候補は例外、logging/trace/request id、secret masking、file attachment、timeout/cancellation helper に限定する。 |
| IC-SL001-002 | Implementation contract | Owned | `ProviderOverrideOptions` は Copilot-specific として扱う。Core へ残す場合は parent review 必須。 |
| IC-SL001-003 | Implementation contract | Owned | reflection discovery は削除する。残す判断をする場合は理由コメントとチャット説明が必要。 |
| TP-SL001-001 | Test point | Owned | 新 solution / slnx の restore/build。 |
| TP-SL001-002 | Test point | Owned | source project graph に OpenAI/Azure provider project が残らないことを audit。 |
| TP-SL001-003 | Test point | Owned | Core public surface に `IChatClient` / `ChatOptions` / `ChatResponse` / `ChatResponseUpdate` が残らないことを audit。 |
| TP-SL001-004 | Test point | Owned | `ProviderFactory` 等 AC-004 denylist が production source に残らないことを audit。 |
| TP-SL001-005 | Test point | Owned | `Microsoft.Extensions.AI.*` dependency が SL-001 対象 Core/project graph から消えることを audit。 |
| TP-SL001-006 | Test point | Owned | reflection discovery が残らないことを audit。 |
| XC-001 | Cross-slice contract | Owned / Producer | Core ownership、shared public naming、`RuntimeName` semantics を SL-002/SL-003/SL-004/SL-005 へ渡す。 |
| XC-002 | Cross-slice contract | Deferred | Codex request -> JSON-RPC mapping は SL-002。SL-001 は Core semantics のみ供給。 |
| XC-003 | Cross-slice contract | Deferred | Copilot request -> SDK mapping は SL-003。SL-001 は Core semantics のみ供給。 |
| XC-004 | Cross-slice contract | Owned / Producer | solution name、project paths、target frameworks、Release zip 単一製品方針の土台を供給。 |

## Production binding requirements

- 新 project references は production runtime projects から `MultiCodingAgentFacade.Core` へ向くこと。test project だけが Core を参照する状態を PASS にしない。
- `MeAiUtility.MultiProvider` project を test-only compatibility shim として残さない。
- `OpenAI` / `AzureOpenAI` / `OpenAICompatible` provider projects は新 solution/project graph から除外または削除し、残す場合は explicit blocked reason が必要。
- `Directory.Build.props` から MEAI/OpenAI package version properties を削除または production graph 非参照にする。
- Core exception/logging は AGENTS.md に従い、例外を捨てる場合は `Exception.ToString()` を trace/log に出す設計を維持する。
- `ActivitySource` / logging category / namespace / assembly metadata は `MultiCodingAgentFacade` semantics にする。

## Cross-slice risks to parent-review

- Core exception の exact type names が未確定。`MultiProviderException` の単純 rename では `ProviderName` semantics が残る。
- SL-001 で runtime project skeleton まで作るか、既存 runtime project rename だけに留めるかを parent review で明確化する必要がある。
- `ProviderOverrideOptions` は現在 Core にあるが、Copilot-specific secret handling に見える。Core に残すと FR-011 の「本当に横断的なものに限定」に反する可能性がある。
- `MeAiUtility.sln` と `MeAiUtility.slnx` の現状差分が大きい。どちらを source of truth として新 solution に移すか、実装指示で固定する必要がある。
- old-name denylist は docs/migration/history で allowlist が必要になる。SL-001 では production source/project graph に限定するのが安全。

## Unresolved items

- exact shared exception names: parent review required。
- exact Core namespace layout: parent review required。
- SL-001 の project creation 範囲: `Core` only か、`GitHubCopilot` / `CodexAppServer` / `Samples` skeleton まで含むか parent review required。
- health check API: decomposition 通り deferred。SL-001 では作らない。
- real runtime evidence: ManualOnly / out of scope。

## Stop condition

slice-prep はここで停止。実装・ファイル編集は行っていない。Parent review gate が `Can implement now? = Yes` を出すまで、SL-001 を slice-impl に渡してはいけない。
