# Plan Kernel

## 目的

`meai_utility_impl` を `MeAiUtility.MultiProvider` から `MultiCodingAgentFacade` へ再編し、GitHub Copilot SDK と Codex App Server を .NET から安全に呼び出す runtime-specific facade ライブラリへ移行する。
旧来の `IChatClient` / provider switching / OpenAI / Azure OpenAI / OpenAI compatible provider は互換維持せず削除し、Copilot と Codex の typed client / typed request / typed response / streaming API を公開面にする。

## 非目標

- GitHub repository rename はこの Plan の対象外。開発完了・マージ後に実施する。
- NuGet publish 強化、旧 package archive、新 package ownership 設計は対象外。配布は従来通り Release zip の直接配布を前提にする。
- OpenAI / Azure OpenAI / OpenAI compatible provider の後継実装は作らない。
- `Microsoft.Extensions.AI.IChatClient` adapter / bridge は残さない。
- 旧 namespace / assembly / DI extension / options の互換 wrapper は残さない。

## 機能要件

- FR-001: repository 内の公開名を `MultiCodingAgentFacade` へ揃え、solution / project / namespace / assembly / package metadata / README / CI から旧公開名を除去する。
- FR-002: source project を `MultiCodingAgentFacade.Core`、`MultiCodingAgentFacade.GitHubCopilot`、`MultiCodingAgentFacade.CodexAppServer`、`MultiCodingAgentFacade.Samples` に再構成する。
- FR-003: test project を `MultiCodingAgentFacade.Core.Tests`、`MultiCodingAgentFacade.GitHubCopilot.Tests`、`MultiCodingAgentFacade.CodexAppServer.Tests`、`MultiCodingAgentFacade.IntegrationTests` に再構成する。
- FR-004: OpenAI / Azure OpenAI / OpenAI compatible / provider switching / MEAI adapter / embedding 関連コードとテストを削除する。
- FR-005: Copilot runtime 用に `GitHubCopilotAgentClient`、`GitHubCopilotAgentRequest`、`GitHubCopilotAgentResponse`、`GitHubCopilotStreamingUpdate` を公開する。
- FR-006: Codex runtime 用に `CodexAppServerAgentClient`、`CodexAppServerTurnRequest`、`CodexAppServerTurnResponse`、`CodexAppServerStreamingUpdate` を公開する。
- FR-007: Copilot と Codex の DI registration を runtime 別にし、旧 `AddMultiProviderChat`、旧 `AddGitHubCopilotProvider`、旧 `AddCodexAppServer` とは異なる新 entrypoint を提供する。
- FR-008: configuration section を `MultiCodingAgentFacade:GitHubCopilot` と `MultiCodingAgentFacade:CodexAppServer` に変更する。
- FR-009: Copilot SDK wrapper の SDK client 作成、model list、session config、streaming、diagnostics、secret-safe logging、provider override、skills/tools/MCP/agent/infinite sessions mapping を維持する。
- FR-010: Codex App Server の stdio transport、JSON-RPC sequence、approval/user-input handling、thread reuse、thread store、sandbox/approval/network policy、process diagnostics を維持する。
- FR-011: 共通 Core は例外、ログ、trace/request id、secret masking、file attachment、timeout/cancellation、diagnostic helper など本当に横断的なものに限定する。
- FR-012: README、migration note、samples、CI/release workflow を新目的に合わせて全面更新する。
- FR-013: `net8.0;net10.0` multi-target を維持する。.NET 11 は GA 前のため対象にしない。
- FR-014: package split は行わず、Release zip 配布では単一製品 `MultiCodingAgentFacade` として扱う。ただし source/test project は責務分離のため複数 project に分ける。

## 受け入れ条件

- AC-001: source public API に `IChatClient`、`ChatOptions`、`ChatResponse`、`ChatResponseUpdate` が残っていない。
- AC-002: source project から `Microsoft.Extensions.AI.*` dependency が消えている。
- AC-003: OpenAI / Azure OpenAI / OpenAI compatible provider project と関連 test project が存在しない。
- AC-004: `ProviderFactory`、`ProviderRegistry`、`MultiProviderOptions`、`ExtensionParameters`、`ConversationExecutionOptions`、`IProviderCapabilities` が存在しない。
- AC-005: solution は `MultiCodingAgentFacade.sln` / `MultiCodingAgentFacade.slnx` を基準に restore / build / test できる。
- AC-006: Copilot typed client で non-streaming、streaming、model list、request validation が可能である。
- AC-007: Codex typed client で non-streaming turn、streaming turn、thread reuse、sandbox / approval / network access が request に反映される。
- AC-008: Copilot / Codex の unit tests と stub/fake transport tests が新 namespace / new API で通る。
- AC-009: opt-in integration tests は実 runtime がない環境では skip し、secret を出力しない。
- AC-010: README と migration note は `MultiCodingAgentFacade` の目的、旧 MultiProvider ではないこと、OpenAI/Azure を提供しないこと、Release zip 配布方針を説明している。
- AC-011: public namespace、assembly metadata、logging category、ActivitySource、PackageId に旧 `MeAiUtility` / `MultiProvider` 名が残っていない。
- AC-012: CI は新 solution を restore / build / test し、旧名混入チェックを含む。

## 影響コンポーネント / モジュール

- `MeAiUtility.sln` / `MeAiUtility.slnx`: 新 solution 名へ置換。
- `Directory.Build.props`: package metadata、dependency version、deterministic build 方針の更新。
- `src/MeAiUtility.MultiProvider`: Core 抽出元。provider selection と MEAI wrapper は削除対象。
- `src/MeAiUtility.MultiProvider.GitHubCopilot`: Copilot 実装移植元。
- `src/MeAiUtility.MultiProvider.CodexAppServer`: Codex 実装移植元。
- `src/MeAiUtility.MultiProvider.OpenAI`: 削除対象。
- `src/MeAiUtility.MultiProvider.AzureOpenAI`: 削除対象。
- `src/MeAiUtility.MultiProvider.Samples`: 新 sample へ作り直し。
- `tests/MeAiUtility.MultiProvider.*`: 新 test project へ移植または削除。
- `.github/workflows/ci.yml` / `.github/workflows/release.yml`: 新 solution / Release zip 配布に更新。
- `README.md`: 全面改訂。
- `plans/`: Plan網羅チェック・残件判定フロー成果物を保持。

## 実装スコープ

実装は full-coverage decomposition 後の slice 単位で行う。最初に新 solution / project skeleton と Core 抽出を行い、その後 Codex、Copilot、docs/CI の順で bounded pass として進める。
キミの決定により、public naming は AI と人間が読みやすい明示名を採用し、必要なら完成後に訂正する。

## 既知の high-risk boundaries

| Boundary candidate | Risk |
| --- | --- |
| Copilot typed client -> `ICopilotSdkWrapper` -> GitHub Copilot SDK session | external SDK、auth、streaming event、SDK config mapping の contract mismatch |
| Codex typed client -> `CodexRpcSession` -> stdio JSON-RPC app-server | cross-process JSON-RPC、thread/turn state、approval/sandbox serialization mismatch |
| New DI/config section -> runtime client/options -> production implementation | startup wiring / options binding / section rename による stub-only success |
| Core exception/logging/diagnostics -> Copilot/Codex consumers | old provider semantics と runtime semantics の混在 |
| Project rename/dependency deletion -> tests/CI/release zip | stale project reference、旧 package dependency、旧名混入 |

詳細 contract selection は `change-risk-triage` に委ねる。

## 今回の対象外

- GitHub repository rename 実行。
- NuGet publish 方針変更。
- 実 runtime を必要とする manual E2E の実行。
- OpenAI / Azure OpenAI の代替実装。
- full runtime evidence の作成。

## change-risk-triage への引き継ぎ

この Plan は広範囲の破壊的再編であり、runtime-specific public API、external SDK、cross-process JSON-RPC、DI/config、solution/CI/docs が相互接続する。
`change-risk-triage` は `full-coverage` 要否を確認し、parent-level runtime contract candidates を slice decomposition へ渡すこと。

## 実装実現性の残留事項

| Item | Status | Notes |
| --- | --- | --- |
| package split / monolithic | Consumed | Release zip 配布では単一製品として扱う。source/test project は責務別に分割する。 |
| public client / request / response naming | Consumed | `GitHubCopilotAgentClient` / `GitHubCopilotAgentRequest` / `GitHubCopilotAgentResponse` / `GitHubCopilotStreamingUpdate`、`CodexAppServerAgentClient` / `CodexAppServerTurnRequest` / `CodexAppServerTurnResponse` / `CodexAppServerStreamingUpdate` を初期採用する。 |
| `net10.0` 維持 | Consumed | `net8.0;net10.0` を維持する。 |
| repository rename | DeferredWithReason | 開発完了・マージ後に行うためこの Plan の対象外。 |
| NuGet publish / package archive | DeferredWithReason | 配布方法強化は対象外。Release zip 直接配布を維持する。 |
| runtime priority | Consumed | Codex 先行で設計するが、Copilot も同一 PR の完了条件に含める。 |
| Copilot advanced options typed coverage | DeferredWithReason | slice 内で既存 wrapper mapping を確認し、未対応 key は runtime-specific advanced options として扱う。 |
| diagnostics / health check API | DeferredWithReason | 必須 API にはしない。既存 diagnostics を維持し、正式 health check は residual candidate とする。 |

## Handoff Packet

- Profile used: plan-kernel
- Plan artifact: `plans/multi-coding-agent-facade-restructure-plan.md`
- Source artifacts:
  - `D:/Data/git/copilot-worktrees/personal-project-driver/main/evidence/MultiCodingAgentFacade_requirements.md`
  - `D:/Data/git/copilot-worktrees/personal-project-driver/main/workitems/ready/multi-coding-agent-facade-restructure-repo-inventory-and-plan-01.yaml`
  - user supplied product decisions in this thread
  - `AGENTS.md`
- Selected contracts / IDs: このエージェントでは選択しない。最終選択は change-risk-triage が行う
- Implementation-realization residuals: see `実装実現性の残留事項`
- Files inspected:
  - `AGENTS.md`
  - `Directory.Build.props`
  - `MeAiUtility.slnx`
  - `README.md`
  - `.github/workflows/ci.yml`
  - `.github/workflows/release.yml`
  - selected source/test/project file inventory under `src/` and `tests/`
- Files intentionally not inspected:
  - all individual test bodies, because this Plan only needs inventory-level grouping
  - full README body beyond old-surface scan, because rewrite belongs to docs slice
  - generated build outputs, because none are source of truth
- Decisions made:
  - Treat target repo work as full Plan網羅チェック・残件判定フロー, not direct broad implementation.
  - Use source/test project responsibility split while keeping product distribution as single Release zip product.
  - Use Codex-first execution order without excluding Copilot from the same PR.
- Do not redo unless new evidence appears:
  - Product decisions listed above are consumed and should not block inventory/decomposition.
  - Repository rename and NuGet publish are out of scope.
- Remaining work:
  - Run `change-risk-triage` on this Plan.
  - If `full-coverage`, run `plan-slice-decomposition`.
  - Prepare and implement slices through the standard guardrail flow.
- Recommended next step: `change-risk-triage.agent.md` with this Plan, the requirements document, target repo inventory, and product decisions.
