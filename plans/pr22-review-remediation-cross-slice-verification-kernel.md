# Cross-slice Verification Kernel: PR22 Review Remediation

## Verdict

- Status: PARENT_PLAN_NEEDS_RESIDUAL_DECISION
- Reason: parent Plan の AC-001..AC-015 と XC-PR22-001..006 は、slice 実装結果、live repository state、net8.0 / net10.0 の no-build tests、scoped old-name audit、targeted static checks の範囲で cross-slice mismatch / production wiring gap / stub-only success は見つからなかった。一方で、real GitHub Copilot / real Codex App Server smoke、dependency advisory warning、scope 外寄りの `.github/agents/copilot-instructions.md` 旧 spec context、追加専用テスト候補、GitHub hosted release 実行は residual-decision-gate の明示判断に渡す必要がある。

## Inputs

- `AGENTS.md`
- `plans/pr22-review-remediation-plan.md`
- `plans/pr22-review-remediation-change-risk-triage.md`
- `plans/pr22-review-remediation-slice-decomposition.md`
- `plans/pr22-review-remediation-slice-SL-001.md` ... `plans/pr22-review-remediation-slice-SL-006.md`
- `plans/pr22-review-remediation-slice-SL-001-prep.md` ... `plans/pr22-review-remediation-slice-SL-006-prep.md`
- `plans/pr22-review-remediation-parent-review-gate.md`
- `plans/pr22-review-remediation-slice-execution-table.md`
- `plans/pr22-review-remediation-agent-usage-ledger.md`
- `plans/pr22-review-remediation-slice-SL-001-implementation-result.md` ... `plans/pr22-review-remediation-slice-SL-006-implementation-result.md`
- 追加確認対象: `README.md`, `.github/workflows/ci.yml`, `.github/workflows/release.yml`, `tools/Invoke-OldNameAudit.ps1`, `src/MultiCodingAgentFacade.Samples/Program.cs`, `src/MultiCodingAgentFacade.GitHubCopilot/**`, `src/MultiCodingAgentFacade.CodexAppServer/**`, `tests/MultiCodingAgentFacade.IntegrationTests/**`

## Parent acceptance coverage

| AC | Status | Evidence | Notes |
| --- | --- | --- | --- |
| AC-001 | Verified | `rg --files -g '*.sln' -g '*.slnx'` は `MultiCodingAgentFacade.sln` / `.slnx` のみ。`ci.yml` / `release.yml` は `dotnet restore/build/test MultiCodingAgentFacade.sln`。 | 旧 root solution reference は active workflow から消滅。 |
| AC-002 | Verified | `src` は `MultiCodingAgentFacade.CodexAppServer/Core/GitHubCopilot/Samples`、`tests` は `MultiCodingAgentFacade.*` のみ。solution / slnx に旧 path hit なし。 | 旧 project reference は live graph で見つからない。 |
| AC-003 | VerifiedWithResidualDecisionRequired | `pwsh -NoProfile -File tools/Invoke-OldNameAudit.ps1` pass。active `.github/workflows`, `README.md`, `src`, `tests` の targeted grep は README migration / removed-concept context と audit script 自身以外に blocking hit なし。 | `.github/agents/copilot-instructions.md` の旧 spec context は audit scope 外。RDG-PR22-CSV-004 へ渡す。 |
| AC-004 | Verified | README は purpose / non-provided scope / quickstart / DI / appsettings / streaming / non-streaming / sandbox / approval / working directory / skills / migration note を含む。 | SL-003 result と source grep で確認。 |
| AC-005 | Verified | `ci.yml` は net8.0 / net10.0 matrix で restore/build/test と old-name audit を実行。this pass の audit も pass。 | GitHub hosted run 自体は未実行。RDG-PR22-CSV-006 へ渡す。 |
| AC-006 | Verified | integration tests に `Assert.True(true)` / `placeholder` / dynamic skip marker hit なし。net8.0 / net10.0 tests は各 `2 passed, 2 skipped, 0 failed`。 | skip reason は xUnit output で観測。 |
| AC-007 | Verified | sample は `AddGitHubCopilotAgentRuntime` / `AddCodexAppServerAgentRuntime` と production clients を解決し、request / non-streaming / streaming / timeout / working directory / skills / disabled skills を compile-time code として持つ。 | この pass では sample build は再実行せず、SL-003 result と source static evidence を採用。 |
| AC-008 | Verified | `GitHubCopilotAgentResponse` / `GitHubCopilotStreamingUpdate` は `TraceId`, `RequestId`, `RuntimeName`, `ElapsedTime`, `FinishStatus`, `DiagnosticsSummary`, `SdkMetadata` を持ち、tests は metadata mapping を確認。 | real Copilot execution は ManualOnly。 |
| AC-009 | Verified | `CodexAppServerTurnResponse` / `CodexAppServerStreamingUpdate` は thread / turn / status / request / trace / diagnostics / error summary を持つ。 | fake transport tests は production client/session path を通る。real app-server drift は ManualOnly。 |
| AC-010 | Verified | `CodexRpcSession.ExecuteTurnAsync` は `CodexRpcTurnResult` を返し、`CodexAppServerAgentClient` が public response/update へ mapping。 | `string` 返却の metadata loss は解消済み。 |
| AC-011 | Verified | `CopilotModelInfo` は `SupportedReasoningEfforts`, `DefaultReasoningEffort`, `SupportsReasoningEffort` を表現。SL-004 tests が supported/default values を確認。 | SDK default がない場合は fabricated default を入れない設計。 |
| AC-012 | Verified | `ProviderOverride` public concept は source から削除され、`GitHubCopilotModelProviderOptions` / `ModelProvider` validation が SDK session creation 前に fail-fast。 | README は removal / typed option として説明。 |
| AC-013 | Verified | `GitHubCopilotPermissionHandlingMode` / `PermissionHandling` は default `ApproveAll`、`DenyAll` / `NoResult` 選択可能。README と sample は default と安全な sample `DenyAll` を説明 / 使用。 | default 方針は parent review gate の human decision を消費済み。 |
| AC-014 | Verified | `AdvancedOptions` は supported key allowlist で unsupported key fail-fast。README は unstable escape hatch / typed property 主 APIとして説明。 | API comments の追加専用確認はこの pass の対象外。 |
| AC-015 | VerifiedWithResidualDecisionRequired | required fixes は SL-001..SL-006 result で implementation / Deferred / ManualOnly として追跡。cross-slice verification で blocking gap は見つからない。 | residual-decision-gate で RDG-PR22-CSV-001..006 の扱いを確定する必要あり。 |

## Cross-slice contract review

| XC ID | Producer evidence | Consumer evidence | Status | Notes |
| --- | --- | --- | --- | --- |
| XC-PR22-001 | SL-001 result: old root solution / old source-test directories deleted。live inventory: `MultiCodingAgentFacade.sln` / `.slnx` only, `src` / `tests` は new project only。 | SL-002 workflows use `MultiCodingAgentFacade.sln`; SL-003 README/sample use new solution/project naming。 | Verified | stale old solution / old project source reference は active target で見つからない。 |
| XC-PR22-002 | SL-004 / SL-005 public API fields exist in source: Copilot response/update metadata, Codex response/update metadata。 | README / sample consume source-backed clients, DI methods, request/response/update fields; sample project references production projects。 | Verified | docs/sample が marker-only や fabricated field に寄っている証拠なし。 |
| XC-PR22-003 | SL-004 source/tests: Copilot `TraceId`, `RequestId`, `RuntimeName`, elapsed/finish/diagnostics/SDK metadata, model capability, `ModelProvider`, permission policy, AdvancedOptions validation。 | README/sample/integration consume `PermissionHandling`, `RuntimeName`, `RequestId`, `TraceId`; opt-in smoke uses production `GitHubCopilotAgentClient` and asserts public fields。 | VerifiedWithManualOnlyResidual | real Copilot smoke は RDG-PR22-CSV-001。 |
| XC-PR22-004 | SL-005 source/tests: JSON-RPC -> `CodexRpcTurnResult` -> public response/update for thread/turn/status/correlation/diagnostics/error。 | README/sample/integration consume `CodexAppServerTurnResponse`, `CodexAppServerStreamingUpdate`, `RequestId`, `TraceId`, `ThreadId`, `TurnId`, `Status`。 | VerifiedWithManualOnlyResidual | real Codex App Server smoke は RDG-PR22-CSV-002。 |
| XC-PR22-005 | SL-004 / SL-005 production clients and DI extensions exist。SL-006 integration tests resolve production wrapper/transport/thread store and clients without fakes。 | SL-006 opt-in tests use `MCAF_GITHUB_COPILOT_INTEGRATION` / `MCAF_CODEX_APP_SERVER_INTEGRATION`, disabled path skips with reason, enabled path is production client path。SL-002 CI includes solution-level integration tests. | VerifiedWithManualOnlyResidual | fake/stub-only success は見つからない。real execution evidence は ManualOnly。 |
| XC-PR22-006 | SL-001 deleted old path list and SL-003 README migration context are available。SL-002 audit script encodes denylist / allowlist and workflow target. | `tools/Invoke-OldNameAudit.ps1` pass; active source/test/README/workflow targeted grep has no blocking old surface hit。 | VerifiedWithResidualDecisionRequired | `.github/agents/copilot-instructions.md` は scoped audit 外の historical/spec-derived instruction として RDG-PR22-CSV-004。 |

## Field continuity review

| Field / state / identifier | Source | Consumer | Status | Notes |
| --- | --- | --- | --- | --- |
| `MultiCodingAgentFacade.sln` / `.slnx` | SL-001 + live root inventory | SL-002 CI/release, SL-003 README/sample | Verified | root solution filesは新名のみ。 |
| deleted old project path list | SL-001 implementation result / live `src` and `tests` inventory | SL-002 audit, final verification | Verified | 旧 `MeAiUtility.MultiProvider*` directories は live inventory に存在しない。 |
| public runtime type names | SL-004 / SL-005 source | README, sample, SL-006 integration | Verified | `GitHubCopilotAgentClient`, `CodexAppServerAgentClient`, response/update recordsを source-backed に消費。 |
| Copilot `TraceId` / `RequestId` | `GitHubCopilotAgentClient` telemetry -> response/update | README, sample, integration tests | Verified | integration smoke asserts `RequestId` / `TraceId` public fields。 |
| Copilot elapsed / finish / diagnostics / SDK metadata | `ICopilotSdkWrapper` / `GitHubCopilotSdkWrapper` -> response/update | README, sample, GitHubCopilot tests | Verified | source-backed optional metadata。real SDK metadata completeness は ManualOnly 実行の外。 |
| Copilot supported/default reasoning efforts | SDK wrapper model mapping -> `CopilotModelInfo` | model validation tests, README | Verified | bool-only model ではない。 |
| Copilot permission policy | `GitHubCopilotOptions.PermissionHandling`, request `PermissionHandling`, SDK handler mapping | README, sample, integration smoke | Verified | parent-approved default `ApproveAll`; sample/smoke は `DenyAll` を使用。 |
| provider override validation outcome | `GitHubCopilotModelProviderOptions` / `ModelProvider` validation | README, tests, integration smoke | Verified | `ProviderOverride` concept removal後の typed model provider validationに置換。 |
| Codex thread id / turn id / status | `CodexRpcSession` JSON-RPC result / notifications | response/update, README, sample | Verified | `CodexRpcTurnResult` から public modelsへ mapping。 |
| Codex request id / trace id | `AgentTelemetry` and JSON-RPC request id separation | response/update, tests, README/sample | Verified | `RequestId`, `TraceId`, `JsonRpcTurnStartRequestId` が分離されている。 |
| Codex diagnostics / error summary | `CodexRpcSession.BuildDiagnosticsSummary`, turn/error notifications | response/update, tests, README/sample | VerifiedWithResidualCandidate | dedicated `ICodexTransportDiagnostics` fake assertion は RDG-PR22-CSV-005。parent AC blocking ではない。 |
| opt-in skip reason | SL-006 integration tests | CI tests, README/sample | VerifiedWithManualOnlyResidual | disabled path skip reason は no-build testsで観測。enabled real runtime は ManualOnly。 |
| old-name allowlist | SL-003 README migration note + SL-002 audit script | audit / final verification | VerifiedWithResidualDecisionRequired | active scopeは pass。`.github/agents/copilot-instructions.md` の扱いは residual-decision-gate へ。 |

## Checks run

- `dotnet test MultiCodingAgentFacade.sln -f net8.0 --no-build`
  - Result: Passed.
  - Observed: Core 1 passed; CodexAppServer 7 passed; GitHubCopilot 11 passed; Integration 2 passed / 2 skipped.
- `dotnet test MultiCodingAgentFacade.sln -f net10.0 --no-build`
  - Result: Passed.
  - Observed: Core 1 passed; CodexAppServer 7 passed; GitHubCopilot 11 passed; Integration 2 passed / 2 skipped.
- `pwsh -NoProfile -File tools/Invoke-OldNameAudit.ps1`
  - Result: Passed. `Old-name audit passed. Scanned files: 153. Allowed historical/planning hits: 1454.`
- `rg --files -g '*.sln' -g '*.slnx'`
  - Result: `MultiCodingAgentFacade.sln`, `MultiCodingAgentFacade.slnx` only.
- `Get-ChildItem -Name src` / `Get-ChildItem -Name tests`
  - Result: only `MultiCodingAgentFacade.*` source/test directories.
- targeted old solution / old project / old public surface grep for `.github`, `README.md`, `src`, `tests`, `tools`
  - Result: active source/test/workflow blocking hitsなし。README migration / removed concept context、audit script rule text、`.github/agents/copilot-instructions.md` historical spec contextが残存。
- targeted source grep for Copilot / Codex public fields, DI methods, production binding, integration opt-in, project references
  - Result: producer / consumer / wiring evidenceを確認。

## Checks not run

- real GitHub Copilot smoke with `MCAF_GITHUB_COPILOT_INTEGRATION=1`: ManualOnly。local credentials / runtime environment が必要。
- real Codex App Server smoke with `MCAF_CODEX_APP_SERVER_INTEGRATION=1`: ManualOnly。local runtime credentials / app-server environment が必要。
- GitHub Actions hosted CI run: local equivalent commands and static workflow inspectionのみ。
- GitHub release creation / `softprops/action-gh-release` execution: tag push / GitHub release context が必要。
- release artifact zip collection end-to-end on `ubuntu-latest`: this pass では未実行。workflow static evidence と SL-002 result の release build / DLL existence evidenceを採用。
- sample project build / sample dry-run再実行: this pass では未実行。SL-003 result と source static evidenceを採用。
- coverage-gap-resolution-slice / residual-decision-gate: この agent の禁止事項に従い未実行。

## Residual candidates for residual-decision-gate

| ID | Type | Severity | Decision needed | Evidence | Recommended disposition |
| --- | --- | --- | --- | --- | --- |
| RDG-PR22-CSV-001 | ManualEnvironmentRequired | Medium | real GitHub Copilot smoke を merge前必須にするか、ManualOnly accepted residual とするか。 | SL-006 opt-in disabled tests skip with reason; production DI/client path is bound; real execution not run. | ManualOnly residual candidate。 |
| RDG-PR22-CSV-002 | ManualEnvironmentRequired | Medium | real Codex App Server smoke を merge前必須にするか、ManualOnly accepted residual とするか。 | SL-006 opt-in disabled tests skip with reason; production DI/client path is bound; real execution not run. | ManualOnly residual candidate。 |
| RDG-PR22-CSV-003 | OutOfScopeForThisPass | Low/Medium | `Nerdbank.MessagePack 1.0.2` NU1902 / NU1903 advisory warnings をこの PR22 remediation で扱うか、別 dependency remediation に送るか。 | SL-001..SL-006 checksで繰り返し observed。tests は pass。 | Separate dependency/security triage candidate。 |
| RDG-PR22-CSV-004 | NeedsHumanDecision | Low/Medium | `.github/agents/copilot-instructions.md` の旧 spec-derived `Microsoft.Extensions.AI` context を今回の scoped audit / AC-003 の通常対象に含めるか。 | targeted grep: `.github/agents/copilot-instructions.md:7`, `:26` に旧 spec context。SL-002 audit は `.github/workflows` のみを scan。 | residual-decision-gate で accept / defer / separate docs-prompt cleanup / FixNow triage を選ぶ。 |
| RDG-PR22-CSV-005 | ParentAcceptanceConditionUnverified | Low | SL-005 residual tests (`ThreadReusePolicy.ReuseByThreadId` / `ReuseOrCreateByKey` metadata continuity, `ICodexTransportDiagnostics` dedicated assertion) を parent close 条件に含めるか。 | SL-005 implementation result の Residual candidates。current parent AC-009/010 は source/testsで主要 path verified。 | accepted residual candidate または future test-hardening。 |
| RDG-PR22-CSV-006 | ManualEnvironmentRequired | Low | GitHub hosted CI / release workflow 実行を final close の必須 evidence とするか。 | local net8/net10 no-build tests and audit pass; release creation/tag context not run. | Manual/deferred operational verification candidate。 |

## Handoff to residual-decision-gate

| Residual ID | Source item | Residual type | Related XC / RC / TP ID | Required decision or evidence | Suggested next gate |
| --- | --- | --- | --- | --- | --- |
| RDG-PR22-CSV-001 | real GitHub Copilot smoke not run | ManualOnly | XC-PR22-003, XC-PR22-005, RC-PR22-007, TP-SL006-004 | `MCAF_GITHUB_COPILOT_INTEGRATION=1` real smoke evidence, or explicit ManualOnly acceptance/defer。 | residual-decision-gate |
| RDG-PR22-CSV-002 | real Codex App Server smoke not run | ManualOnly | XC-PR22-004, XC-PR22-005, RC-PR22-007, TP-SL006-005 | `MCAF_CODEX_APP_SERVER_INTEGRATION=1` real smoke evidence, or explicit ManualOnly acceptance/defer。 | residual-decision-gate |
| RDG-PR22-CSV-003 | dependency advisory warnings | OutOfScopeForThisPass | AC-015 | Decide separate dependency remediation / accepted known warning / FixNow triage。 | residual-decision-gate, possibly coverage-gap-triage if FixNow selected |
| RDG-PR22-CSV-004 | `.github/agents/copilot-instructions.md` old spec context | NeedsHumanDecision | AC-003, XC-PR22-006 | Decide whether `.github/agents/**` is in PR22 active guidance audit scope。 | residual-decision-gate, possibly coverage-gap-triage if FixNow selected |
| RDG-PR22-CSV-005 | extra Codex dedicated tests | ResidualTestHardening | XC-PR22-004, TP-SL005-006, TP-SL005-008 | Decide if dedicated thread-reuse/diagnostics tests are required before close。 | residual-decision-gate |
| RDG-PR22-CSV-006 | hosted CI / release not executed | ManualOnly | AC-005, XC-PR22-001, XC-PR22-006 | GitHub Actions evidence or accepted local-equivalent evidence。 | residual-decision-gate |

Handoff Packet:

- Profile used: cross-slice-verification-kernel
- Parent Plan artifact: `plans/pr22-review-remediation-plan.md`
- Change Risk Triage artifact: `plans/pr22-review-remediation-change-risk-triage.md`
- Slice Decomposition artifact: `plans/pr22-review-remediation-slice-decomposition.md`
- Slice artifacts: `plans/pr22-review-remediation-slice-SL-001.md` ... `plans/pr22-review-remediation-slice-SL-006.md`
- Slice prep artifacts: `plans/pr22-review-remediation-slice-SL-001-prep.md` ... `plans/pr22-review-remediation-slice-SL-006-prep.md`
- Slice implementation artifacts: `plans/pr22-review-remediation-slice-SL-001-implementation-result.md` ... `plans/pr22-review-remediation-slice-SL-006-implementation-result.md`
- Cross-slice Contract IDs verified: XC-PR22-001, XC-PR22-002, XC-PR22-003, XC-PR22-004, XC-PR22-005, XC-PR22-006
- Parent AC verified: AC-001..AC-015
- Gap / residual IDs: RDG-PR22-CSV-001, RDG-PR22-CSV-002, RDG-PR22-CSV-003, RDG-PR22-CSV-004, RDG-PR22-CSV-005, RDG-PR22-CSV-006
- Files inspected:
  - `AGENTS.md`
  - `README.md`
  - `.github/workflows/ci.yml`
  - `.github/workflows/release.yml`
  - `.github/agents/copilot-instructions.md` via targeted grep
  - `tools/Invoke-OldNameAudit.ps1`
  - `MultiCodingAgentFacade.sln`
  - `MultiCodingAgentFacade.slnx`
  - `src/MultiCodingAgentFacade.Samples/Program.cs`
  - `src/MultiCodingAgentFacade.Samples/MultiCodingAgentFacade.Samples.csproj`
  - `src/MultiCodingAgentFacade.GitHubCopilot/**` public API / DI / wrapper related files via targeted grep/content
  - `src/MultiCodingAgentFacade.CodexAppServer/**` public API / DI / session related files via targeted grep/content
  - `tests/MultiCodingAgentFacade.IntegrationTests/**`
  - `tests/MultiCodingAgentFacade.GitHubCopilot.Tests/**` via targeted grep
  - `tests/MultiCodingAgentFacade.CodexAppServer.Tests/**` via targeted grep
- Files intentionally not inspected:
  - old `MeAiUtility.MultiProvider*` implementation bodies: deleted source graphの確認で十分。
  - generated `bin/` / `obj/`: source of truth ではない。
  - every unit test body: cross-slice scope では targeted evidence のみ確認。
  - PR / GitHub hosted workflow state: connector / remote executionはこの pass の範囲外。
- Decisions made:
  - cross-slice mismatch / production wiring gap / stub-only success は検出しない。
  - real runtime smoke は parent PASS blocker ではなく ManualOnly residual candidate。
  - `.github/agents/copilot-instructions.md` は scoped audit 外だが、active agent guidance と見る余地があるため residual decision に送る。
  - dependency advisory warnings は PR22 remediation acceptance の直接 blocker とは扱わず residual decision に送る。
- Do not redo unless new evidence appears:
  - XC-PR22-001..006 の producer/consumer/mechanism/required fields の照合。
  - net8.0 / net10.0 no-build tests and scoped audit pass。
  - integration placeholder removal and production DI/client binding check。
- Remaining work:
  - residual-decision-gate で RDG-PR22-CSV-001..006 を explicit decision する。
  - FixNow が選ばれた場合のみ coverage-gap-triage へ渡す。coverage-gap-resolution-slice へ直接進まない。
- Recommended next step: `residual-decision-gate.agent.md` にこの artifact と slice implementation results を渡し、ManualOnly / accepted residual / defer / FixNow triage / abort の判断を作る。
