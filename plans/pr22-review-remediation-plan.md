# Plan Kernel

## Goal

PR #22 の review comment `4641587916` で示された merge blocking / major 指摘を、`MultiCodingAgentFacade` 再編の完了条件として再固定する。
現状の PR は新 `MultiCodingAgentFacade.*` project を追加している一方で旧 `MeAiUtility.MultiProvider` 構成が残っているため、互換を残さない再編として完了できるよう、旧構成削除、README / CI / sample / opt-in integration test / runtime response model / provider validation / permission handling を bounded な下流 flow に渡す。

## Non-goals

- この Plan Kernel pass では production code、tests、workflow、README を変更しない。
- 実 Copilot / 実 Codex を常時 CI で実行することは要求しない。
- GitHub repository rename、NuGet publish 方針変更、旧 package archive は扱わない。
- OpenAI / Azure OpenAI / OpenAI compatible provider の代替実装は作らない。
- `Microsoft.Extensions.AI.IChatClient` adapter、旧 namespace 互換 wrapper、旧 `AddMultiProviderChat` 互換 entrypoint は残さない。

## Functional requirements

- FR-001: 旧 `MeAiUtility.sln` / `MeAiUtility.slnx` と旧 `src/MeAiUtility.MultiProvider*` / `tests/MeAiUtility.MultiProvider*` project 群を削除し、solution 基準を `MultiCodingAgentFacade.sln` / `MultiCodingAgentFacade.slnx` へ一本化する。
- FR-002: OpenAI / Azure OpenAI / OpenAI compatible provider、`IChatClient`、`AddMultiProviderChat`、`ProviderFactory`、`ProviderRegistry`、`MultiProviderOptions`、`ConversationExecutionOptions`、`ExtensionParameters`、旧 provider switching / MEAI provider surface を source / tests / docs / CI から削除する。
- FR-003: README を `MultiCodingAgentFacade` の新方針へ全面改訂し、Copilot / Codex App Server 向け runtime client、非提供対象、quickstart、DI、appsettings、streaming / non-streaming、sandbox / approval / working directory / skills、migration note を説明する。
- FR-004: CI を `MultiCodingAgentFacade.sln` 基準の restore / build / test に切り替え、旧 `MeAiUtility` / `MultiProvider` / `IChatClient` / `Microsoft.Extensions.AI` / OpenAI / AzureOpenAI 混入チェックを追加する。
- FR-005: `tests/MultiCodingAgentFacade.IntegrationTests/OptInIntegrationPlaceholderTests.cs` の placeholder を廃止し、設定や環境変数がない場合は skip reason を示す Copilot SDK / Codex App Server opt-in smoke test に置き換える。
- FR-006: `src/MultiCodingAgentFacade.Samples/Program.cs` を runtime marker 表示ではなく実利用 sample にし、Copilot / Codex の DI 登録、request 作成、non-streaming、streaming、timeout、working directory、skills、disabled skills、例外処理を示す。
- FR-007: Copilot response / streaming update model に elapsed time、runtime name、diagnostics summary、completion / finish status、SDK metadata extension point、request id / trace id を保持できる shape を追加する。
- FR-008: Codex response / streaming update model に thread id、turn id、status、trace id、request id、diagnostics summary、error summary を保持できる shape を追加し、`CodexRpcSession.ExecuteTurnAsync` を `string` ではなく turn result model を返す形へ変更する。
- FR-009: `CopilotModelInfo` を `ModelId` + bool から、supported reasoning effort values、default reasoning effort、reasoning effort support を表せる model へ拡張する。
- FR-010: `ProviderOverrideOptions` と SDK session config mapping に fail-fast validation を追加し、`Type`、provider type 別の `BaseUrl`、`ApiKey` / `BearerToken`、Azure `AzureApiVersion`、不正な組み合わせを `RuntimeInvalidRequestException` で検出する。
- FR-011: Copilot permission handling の固定 `ApproveAll` を見直し、既定値、明示 option、ログ、auto approve 範囲、README 説明を安全側に揃える。
- FR-012: `AdvancedOptions` の公開 API 方針を決め、typed property を主 API としつつ、残す場合は unstable / escape hatch として README と API comments で扱いを明確にする。
- FR-013: 既存の `plans/multi-coding-agent-facade-restructure-*` 成果物との関係を保持し、今回の PR 指摘 remediation を下流 slice decomposition へ渡す。

## Acceptance conditions

- AC-001: repository root に旧 `MeAiUtility.sln` / `MeAiUtility.slnx` が存在せず、CI と開発手順は `MultiCodingAgentFacade.sln` または `MultiCodingAgentFacade.slnx` を参照する。
- AC-002: `src/MeAiUtility.MultiProvider*` と `tests/MeAiUtility.MultiProvider*` が削除され、旧 project reference が残っていない。
- AC-003: source / test / README / workflow の通常対象から `IChatClient`、`AddMultiProviderChat`、`ProviderFactory`、`ProviderRegistry`、`MultiProviderOptions`、`ConversationExecutionOptions`、`ExtensionParameters`、`Microsoft.Extensions.AI`、OpenAI / AzureOpenAI provider surface が削除されている。
- AC-004: README は `MultiCodingAgentFacade` の目的と非提供範囲を説明し、Copilot / Codex の quickstart、DI、appsettings、streaming / non-streaming、sandbox / approval / working directory / skills、migration note を含む。
- AC-005: CI は `MultiCodingAgentFacade.sln` を restore / build / test し、旧名・旧依存混入チェックを実行する。
- AC-006: integration tests は placeholder `Assert.True(true)` を含まず、opt-in 条件不足時に skip reason を観測できる。
- AC-007: sample は Copilot / Codex の実利用 API を呼ぶ compile-time example になっており、marker 表示だけではない。
- AC-008: Copilot non-streaming response と streaming update は request / trace correlation、runtime name、elapsed / finish / diagnostics / metadata を利用者が取得できる。
- AC-009: Codex turn response と streaming update は thread id、turn id、status、request / trace correlation、diagnostics / error summary を利用者が取得できる。
- AC-010: `CodexRpcSession.ExecuteTurnAsync` の production path が turn result model を返し、client response まで thread / turn / status を失わない。
- AC-011: model list API は supported reasoning effort values と default reasoning effort を表現できる。
- AC-012: invalid provider override は SDK 層へ遅延せず、runtime client 実行前に `RuntimeInvalidRequestException` で fail-fast する。
- AC-013: Copilot approval / permission request は固定 auto approve に見えず、既定値・option・ログ・README が一致している。
- AC-014: `AdvancedOptions` は typed API の代替として無制限に見えない形に整理され、公開する場合は unstable / escape hatch と明記されている。
- AC-015: PR review comment の Required fixes before merge 各項目について、実装済み、別 slice へ Deferred、または明示的な OutOfScope / NeedsHumanDecision の状態が追跡できる。

## Affected components / modules

- `MeAiUtility.sln` / `MeAiUtility.slnx`: 削除対象。
- `MultiCodingAgentFacade.sln` / `MultiCodingAgentFacade.slnx`: CI / restore / build / test の基準。
- `.github/workflows/ci.yml`: new solution target と旧名・旧依存混入チェック。
- `README.md`: 全面改訂、migration note、samples / configuration / approval 説明。
- `src/MeAiUtility.MultiProvider*` / `tests/MeAiUtility.MultiProvider*`: 削除対象。
- `src/MultiCodingAgentFacade.Core`: exceptions / diagnostics / shared options の最小共通 surface。
- `src/MultiCodingAgentFacade.GitHubCopilot`: request / response / streaming update / model info / provider override / permission handling / SDK wrapper。
- `src/MultiCodingAgentFacade.CodexAppServer`: request / response / streaming update / `CodexRpcSession` / thread store / JSON-RPC result mapping。
- `src/MultiCodingAgentFacade.Samples`: 実利用 sample。
- `tests/MultiCodingAgentFacade.*`: new API unit tests、fake / stub transport tests、opt-in integration tests。
- `plans/multi-coding-agent-facade-restructure-*`: 既存 parent Plan / slice 実行結果として参照するが、この pass で書き換えない。

## Expected implementation scope

この Plan は review remediation 用の parent Plan として扱う。
実装はこの pass では行わず、`change-risk-triage` の診断後に full-coverage の slice decomposition へ渡す。
特に旧構成削除、docs/CI、runtime response contract、Copilot provider override / permission handling、Codex turn result propagation は相互依存するため、単一の implementation pass で処置しない。

## Known high-risk boundaries

| Boundary candidate | Risk |
| --- | --- |
| 旧 solution / project deletion -> new solution / CI / release flow | stale reference、CI が旧 solution を見続ける、旧 project 削除で build graph が壊れる |
| README / samples / migration note -> public API | docs が旧 provider switching を案内し、利用者と agent を誤誘導する |
| Copilot request / response / streaming update -> `GitHubCopilotSdkWrapper` -> GitHub Copilot SDK | request / trace correlation、metadata、finish status、permission handling、provider override validation が落ちる |
| Codex request / `CodexRpcSession` -> app-server JSON-RPC -> response / streaming update | thread id / turn id / status / error が production path で失われる |
| model catalog -> reasoning effort selection | SDK から得た supported values を bool に潰し、利用者が valid value を判断できない |
| integration tests / fakes -> production implementation | placeholder / fake-only success により real runtime binding gap を見逃す |
| old-name / dependency audit -> source / tests / docs / workflows | grep 対象を誤ると planning artifacts や migration note の合法的な旧名まで false positive になる |

詳細な contract selection は `change-risk-triage` に委ねる。

## Out of scope for this pass

- production code / tests / docs / workflow の修正。
- full runtime evidence、test point design、implementation contract の詳細化。
- 実 Copilot / 実 Codex を使った manual E2E。
- GitHub repository rename。
- NuGet publish / package archive 設計。

## Handoff to change-risk-triage

`change-risk-triage` は、PR #22 review comment を source requirement として、この remediation が `contract-kernel`、`standard-slice`、`full-coverage`、`fix-slice` のどれに当たるかを診断すること。
特に、既存 parent Plan がすでに `full-coverage` と診断済みである点、今回の comment がその残件をより具体化している点、かつ response model / permission / provider validation など新しい runtime contract gap を追加している点を確認すること。

## Handoff Packet

- Profile used: plan-kernel
- Plan artifact: `plans/pr22-review-remediation-plan.md`
- Source artifacts:
  - PR #22 review comment: `https://github.com/suusanex/meai_utility_impl/pull/22#issuecomment-4641587916`
  - `plans/multi-coding-agent-facade-restructure-plan.md`
  - `plans/multi-coding-agent-facade-restructure-change-risk-triage.md`
  - `AGENTS.md`
- Selected contracts / IDs: none selected by this agent; final selection belongs to change-risk-triage
- Files inspected:
  - `.github/agents/plan-kernel.agent.md`
  - `.github/agents/change-risk-triage.agent.md`
  - `.github/workflows/ci.yml`
  - `Directory.Build.props`
  - `MeAiUtility.sln`
  - `MeAiUtility.slnx`
  - `MultiCodingAgentFacade.sln`
  - `MultiCodingAgentFacade.slnx`
  - `README.md`
  - `src/MultiCodingAgentFacade.GitHubCopilot/GitHubCopilotAgentResponse.cs`
  - `src/MultiCodingAgentFacade.GitHubCopilot/GitHubCopilotStreamingUpdate.cs`
  - `src/MultiCodingAgentFacade.GitHubCopilot/Abstractions/ICopilotSdkWrapper.cs`
  - `src/MultiCodingAgentFacade.GitHubCopilot/GitHubCopilotAgentRequest.cs`
  - `src/MultiCodingAgentFacade.GitHubCopilot/Options/ProviderOverrideOptions.cs`
  - `src/MultiCodingAgentFacade.GitHubCopilot/GitHubCopilotSdkWrapper.cs`
  - `src/MultiCodingAgentFacade.CodexAppServer/CodexAppServerTurnResponse.cs`
  - `src/MultiCodingAgentFacade.CodexAppServer/CodexAppServerStreamingUpdate.cs`
  - `src/MultiCodingAgentFacade.CodexAppServer/CodexRpcSession.cs`
  - `src/MultiCodingAgentFacade.Samples/Program.cs`
  - `tests/MultiCodingAgentFacade.IntegrationTests/OptInIntegrationPlaceholderTests.cs`
- Files intentionally not inspected:
  - every old provider source file body, because directory and rg evidence are enough for deletion-scope planning
  - every new unit test body, because test point mapping belongs to downstream `test-design-kernel`
  - build outputs under `bin/` / `obj/`, because they are not source of truth
- Decisions made:
  - Create a new remediation Plan instead of rewriting the existing parent restructure Plan.
  - Treat all Blocking and Major review sections as valid remediation inputs.
  - Treat real runtime always-on CI as a non-goal; opt-in smoke tests with skip reason remain required.
  - No PR review item is marked reflection-unnecessary in this Plan pass.
- Do not redo unless new evidence appears:
  - Existing PR state has old and new project structures side by side.
  - README and CI still describe / target the old `MeAiUtility.MultiProvider` structure.
  - Current Copilot / Codex response models are too thin for the review requirement.
- Remaining work:
  - Run `change-risk-triage` on this remediation Plan.
  - If `full-coverage`, create or update slice decomposition for review remediation.
  - Then run slice-level guardrail flow before implementation.
- Recommended next step: `change-risk-triage.agent.md` with this Plan, the PR review comment, existing restructure Plan / triage, and selected current-state evidence.
