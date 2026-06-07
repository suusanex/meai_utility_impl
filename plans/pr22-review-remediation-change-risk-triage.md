# Change Risk Triage

## Recommended profile

`full-coverage`

## Reasoning

PR #22 の review remediation は、旧構成削除、solution / CI 切替、README / sample / migration note、opt-in integration tests、Copilot / Codex runtime response model、Copilot SDK provider override validation、permission handling、old-name / dependency audit を同時に扱う。
これは既知 gap の小修正ではなく、複数の runtime boundary と public API surface が相互接続する残件群である。

既存の `plans/multi-coding-agent-facade-restructure-change-risk-triage.md` も parent restructure を `full-coverage` と診断しており、今回の review comment はその診断を弱めるのではなく、未達 acceptance condition と追加 runtime contract gap を具体化している。
`contract-kernel` 1本に圧縮すると、旧構成削除と CI / docs の structural completion、Copilot / Codex の runtime metadata propagation、provider validation / permission policy が互いに抜け落ちやすい。

したがって minimum sufficient response は `full-coverage`。
Immediate next agent は `plan-slice-decomposition.agent.md`。

## High-risk boundaries

| Boundary | Producer | Consumer | Mechanism | Risk type |
| --- | --- | --- | --- | --- |
| 旧構成削除から new solution build graph への接続 | `MeAiUtility.sln` / `src/MeAiUtility.MultiProvider*` / `tests/MeAiUtility.MultiProvider*` 削除作業 | `MultiCodingAgentFacade.sln` / CI restore-build-test | project references + workflow commands | stale reference / dependency deletion / false CI target |
| old-name / old-dependency audit | source / tests / README / workflows | CI grep / audit step | repository scan with allowlist | false positive / false negative / docs migration exception |
| README / samples から public typed API への接続 | `README.md` / `src/MultiCodingAgentFacade.Samples/Program.cs` | Copilot / Codex consumers | docs + compile-time sample code | public API mismatch / user misguidance |
| Copilot typed request / response to SDK session | `GitHubCopilotAgentClient` / `GitHubCopilotAgentRequest` / `GitHubCopilotAgentResponse` / streaming updates | `GitHubCopilotSdkWrapper` / GitHub Copilot SDK | method call + SDK session config + SDK events | external SDK / metadata loss / streaming correlation |
| Copilot provider override validation | `ProviderOverrideOptions` / request and configured options | `BuildSdkSessionConfig` / SDK provider config | option binding + validation + SDK config mapping | delayed failure / invalid auth or provider combination |
| Copilot permission handling | Copilot SDK permission request | user-facing option / logging / README policy | SDK `OnPermissionRequest` callback | unsafe default / silent auto approval |
| Copilot model capability propagation | SDK model list response | `CopilotModelInfo` / caller reasoning effort selection | SDK model metadata mapping | capability loss / invalid user selection |
| Codex JSON-RPC turn result propagation | `CodexRpcSession` notifications and responses | `CodexAppServerTurnResponse` / streaming updates | stdio JSON-RPC + result model | thread / turn / status / error metadata loss |
| opt-in integration tests to production runtime binding | environment variables / runtime availability | Copilot SDK and Codex App Server smoke tests | xUnit skip + production client paths | placeholder success / fake-only success |

## Selected runtime contracts to cover

| Contract ID | Boundary | What is at risk | Why selected | Triage status | Next action |
| --- | --- | --- | --- | --- | --- |
| RC-PR22-001 | 旧構成削除 -> `MultiCodingAgentFacade.sln` / CI | old projects, old solution, old MEAI/OpenAI dependencies, and stale references may remain while CI still passes the wrong target | review の最上位 Blocking であり、mergeability の前提 | Deferred | `plan-slice-decomposition` で structure / CI / audit slice として分解する |
| RC-PR22-002 | README / sample -> public typed API | docs or samples may continue to teach old `IChatClient` / provider switching or marker-only usage | user-facing surface and AI agent guidance are acceptance-critical | Deferred | docs/sample slice; compile-time sample verification and migration note guardrail |
| RC-PR22-003 | Copilot typed API -> SDK wrapper -> SDK events | response / streaming metadata, request / trace correlation, finish status, diagnostics, model capability values may be lost | external SDK and streaming boundary with thin current model | Deferred | Copilot runtime slice with implementation-contract and runtime-contract-kernel |
| RC-PR22-004 | Codex typed API -> `CodexRpcSession` -> app-server JSON-RPC | thread id, turn id, status, request / trace correlation, diagnostics, error summary may be parsed then discarded | cross-process JSON-RPC with durable thread/turn semantics | Deferred | Codex runtime slice with result model contract and JSON-RPC mapping |
| RC-PR22-005 | Provider override options -> SDK provider config | invalid provider type / base URL / auth / Azure API version may reach SDK as obscure failure | review calls for fail-fast and current code maps null `BaseUrl` to empty string | Deferred | Copilot validation slice or Copilot runtime slice sub-scope |
| RC-PR22-006 | Copilot permission request -> user-visible approval policy | fixed `ApproveAll` can silently approve actions without option/log/docs alignment | safety policy and user expectation boundary | Deferred | Copilot permission handling slice with README / logging linkage |
| RC-PR22-007 | opt-in integration tests -> production runtime path | placeholder test or fake-only path may pass without exercising opt-in production binding | explicit Blocking item and production-binding guardrail | Deferred | test/integration slice with skip reason and smoke path requirements |

## Candidate runtime contracts not selected

| Contract ID | Boundary | Why not selected | Candidate status | Suggested next action |
| --- | --- | --- | --- | --- |
| RC-PR22-X01 | GitHub repository rename | PR review does not request rename and existing parent Plan marks it post-merge/out of scope | OutOfScopeForThisPass | Keep as separate post-merge task |
| RC-PR22-X02 | NuGet publish / package archive | review focuses on Release / repo completion, not publish policy redesign | OutOfScopeForThisPass | Leave to separate packaging Plan if needed |
| RC-PR22-X03 | always-on real Copilot / Codex E2E in CI | review explicitly allows not running real runtimes constantly | OutOfScopeForThisPass | Implement opt-in smoke tests with skip reason instead |
| RC-PR22-X04 | old-name occurrences inside planning artifacts / migration notes | some old names are legitimate when documenting migration or source evidence | Deferred | audit slice must define scoped grep allowlist rather than blanket deletion |

## Risk trigger scan

| Risk trigger | Present / Absent / Unclear | Notes |
| --- | --- | --- |
| Cross-process or cross-service sequence | Present | Codex App Server stdio JSON-RPC and turn lifecycle |
| Queue / event / webhook / background worker | Absent | No queue/webhook; streaming updates are runtime event stream, not a background worker queue |
| External API or SDK | Present | GitHub Copilot SDK and Codex CLI / app-server |
| Authentication or authorization | Present | Copilot logged-in user / tokens, provider override credentials, permission approval handling |
| Durable state / retry / replay / idempotency | Present | Codex thread reuse and thread store; retry semantics appear in Codex error notifications |
| Startup wiring / DI / configuration | Present | new solution, DI registration, appsettings sections, CI workflows |
| Production implementation split from test substitute | Present | SDK wrapper fakes, Codex fake transport, placeholder integration test risk |
| Multiple runtime participants coordinating state | Present | typed client, SDK wrapper/session, JSON-RPC transport, thread store, CI/docs consumers |
| Observable behavior spanning more than one component | Present | review completion spans source, tests, docs, samples, workflows, runtime metadata |

## Suggested next agent

Immediate next agent: `plan-slice-decomposition.agent.md`

Required inputs:

- `plans/pr22-review-remediation-plan.md`
- `plans/pr22-review-remediation-change-risk-triage.md`
- PR #22 review comment `https://github.com/suusanex/meai_utility_impl/pull/22#issuecomment-4641587916`
- Existing parent artifacts:
  - `plans/multi-coding-agent-facade-restructure-plan.md`
  - `plans/multi-coding-agent-facade-restructure-change-risk-triage.md`
  - `plans/multi-coding-agent-facade-restructure-slice-decomposition.md`
- Current-state evidence from source tree and CI / README / sample / integration placeholder files.

Minimum required downstream flow:

1. `plan-slice-decomposition.agent.md` for PR review remediation.
2. `slice-prep` for executable slices.
3. Per-slice `change-risk-triage`; when implementation-realization risk is Present or Unclear, run `implementation-contract-kernel`.
4. `runtime-contract-kernel` for selected RC-PR22 contracts.
5. `test-design-kernel` for selected runtime and production-binding checks.
6. `implementation-handoff-review`.
7. bounded implementation pass.
8. per-slice `verification-kernel`.
9. final `cross-slice-verification-kernel`.
10. `residual-decision-gate` for any intentionally deferred review item.

Recommended slice axes:

- SL-A: structural deletion / solution / dependency graph / old project removal.
- SL-B: CI and old-name / old-dependency audit with scoped allowlist.
- SL-C: README, migration note, and real-use sample.
- SL-D: Copilot response / streaming / model capability / provider override / permission handling.
- SL-E: Codex turn result / streaming metadata propagation.
- SL-F: opt-in integration tests and production-binding verification.

## Out of scope for this triage

- Method-level implementation design.
- Test point mapping and exact test names.
- Full runtime evidence.
- Production code, tests, README, or workflow edits.
- Actual CI execution and real runtime opt-in smoke execution.
- GitHub repository rename and NuGet publish policy.

## Reflection-unnecessary findings

今回の review comment から、現時点で「反映不要」と判断する指摘はない。
ただし次の items は review の意図を狭めて扱う。

| Review item | Triage handling | Reason |
| --- | --- | --- |
| 実 Copilot / 実 Codex integration | Always-on CI requirement ではなく opt-in smoke + skip reason として扱う | review comment 自体が常時 CI 実行は不要と明記しているため |
| old-name / old-dependency 混入チェック | planning artifacts / migration note の合法的な旧名説明を許容する scoped audit として扱う | blanket grep は migration note まで false positive にするため |
| repository rename | 今回の PR remediation には含めない | existing parent Plan で post-merge / out of scope とされ、review comment も rename を required fix にしていないため |

## Handoff Packet

- Profile used: triage-only
- Recommended process profile: `full-coverage`
- Source artifacts:
  - `plans/pr22-review-remediation-plan.md`
  - PR #22 review comment `https://github.com/suusanex/meai_utility_impl/pull/22#issuecomment-4641587916`
  - `plans/multi-coding-agent-facade-restructure-plan.md`
  - `plans/multi-coding-agent-facade-restructure-change-risk-triage.md`
  - `AGENTS.md`
- Selected contracts / IDs: RC-PR22-001, RC-PR22-002, RC-PR22-003, RC-PR22-004, RC-PR22-005, RC-PR22-006, RC-PR22-007
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
  - full bodies of all old provider files, because deletion scope is already established by directory and solution evidence
  - full bodies of all new unit tests, because test design belongs to downstream guardrail artifacts
  - generated outputs under `bin/` / `obj/`
- Decisions made:
  - Recommend `full-coverage`.
  - Treat all Blocking and Major sections as remediation requirements.
  - Mark no PR review item as reflection-unnecessary.
  - Narrow real runtime validation to opt-in smoke tests with skip reason.
  - Require scoped old-name audit rather than naive blanket grep.
- Do not redo unless new evidence appears:
  - Current PR state has old and new project structures side by side.
  - CI currently targets `MeAiUtility.sln`.
  - README currently describes old `MeAiUtility.MultiProvider` / MEAI provider switching.
  - Integration test currently contains placeholder `Assert.True(true)`.
  - Copilot and Codex response / streaming models currently lack required metadata.
- Remaining work:
  - Create PR review remediation slice decomposition.
  - Prepare slice-level guardrail artifacts before implementation.
  - Reconcile new slice axes with existing `multi-coding-agent-facade-restructure-*` slice artifacts.
- Recommended next step: `plan-slice-decomposition.agent.md` using this triage, remediation Plan, PR review comment, and existing parent restructure artifacts.
- Required downstream guardrails: runtime contract identification, participant/boundary mapping, test point mapping, stub/fake/in-memory usage check, production implementation binding, production wiring/entrypoint verification, explicit unresolved status for anything not completed.
