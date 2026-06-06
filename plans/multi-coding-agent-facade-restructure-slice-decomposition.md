# Plan Slice Decomposition

## 親 Plan の要約

`MeAiUtility.MultiProvider` を互換維持なしで `MultiCodingAgentFacade` へ再編する。OpenAI / Azure OpenAI / OpenAI compatible / MEAI / provider switching を削除し、GitHub Copilot SDK と Codex App Server の runtime-specific typed clients を提供する。

## full-coverage 判定の理由

変更範囲が public API、project/solution、dependencies、external SDK、cross-process JSON-RPC、DI/config、tests、README、CI/release にまたがる。slice に分けず実装すると、旧 API の残存、stub-only success、runtime contract mismatch、stale CI のいずれかを見落としやすい。

## 分割方針

- 最初に project / Core / public naming の所有者を作り、旧 provider switching の受け皿を消す。
- Codex を先に移植するが、Copilot も同一 PR の acceptance conditions に含める。
- Runtime slice は Codex と Copilot を分ける。ただし public API naming、Core exception/diagnostics、DI/config section は cross-slice contract として保持する。
- Tests/docs/CI は最後にまとめて整合させるが、各 runtime slice の production binding を test-only success として完了扱いしない。

## Slice 一覧

| Slice ID | Name | Goal | Recommended profile | Immediate next agent | Depends on | Can run in parallel? |
| --- | --- | --- | --- | --- | --- | --- |
| SL-001 | Project/Core foundation | 新 solution/project 構成、Core 抽出、旧 provider switching 削除の土台を作る | standard-slice | slice-prep | none | No |
| SL-002 | Codex typed runtime | Codex typed client/request/response/streaming を既存 JSON-RPC 実装へ接続する | standard-slice | slice-prep | SL-001 | No |
| SL-003 | Copilot typed runtime | Copilot typed client/request/response/streaming/model list を既存 SDK wrapper へ接続する | standard-slice | slice-prep | SL-001 | No |
| SL-004 | Test migration and quality gates | 新 API の unit/stub/fake tests、旧依存混入チェック、opt-in integration 整理を行う | standard-slice | slice-prep | SL-001, SL-002, SL-003 | No |
| SL-005 | Docs, samples, CI, release zip | README/migration/samples/CI/release を新目的と Release zip 配布に更新する | standard-slice | slice-prep | SL-001, SL-002, SL-003 | Partially |
| SL-006 | Final audit and residual gate | 旧名/旧依存/API/README/CI の cross-slice verification と residual decision を行う | contract-kernel | cross-slice-verification-kernel | SL-001..SL-005 | No |

## Slice 詳細

### SL-001: Project/Core foundation

- Goal: `MultiCodingAgentFacade` の solution/project/Core foundation を作り、旧 provider switching と MEAI dependency を削除できる構造へ移す。
- Non-goals: Codex/Copilot runtime behavior の詳細移植、README 全面改訂、real runtime E2E。
- Parent requirements covered: FR-001, FR-002, FR-003, FR-004, FR-011, FR-013, FR-014.
- Parent acceptance conditions covered: AC-002, AC-003, AC-004, AC-005, AC-011 の土台。
- Affected components / modules: `MeAiUtility.slnx`, `MeAiUtility.sln`, `Directory.Build.props`, `src/MeAiUtility.MultiProvider`, `src/MeAiUtility.MultiProvider.OpenAI`, `src/MeAiUtility.MultiProvider.AzureOpenAI`, project files.
- Expected implementation scope: new project paths creation, shared Core type migration, old provider factory/registry/options deletion, OpenAI/Azure project removal, project references update.
- Internal high-risk boundary candidates: project reference graph and dependency deletion.
- Cross-slice dependencies: produces Core/public naming/config section conventions consumed by SL-002, SL-003, SL-004, SL-005.
- Related Cross-slice Contract IDs: XC-001, XC-002, XC-003, XC-004.
- Cross-slice contract excerpt:
  - XC ID: XC-001
  - This slice role: Producer
  - Mechanism: source project and namespace ownership
  - Required fields / state / identifiers: `MultiCodingAgentFacade.Core`, `MultiCodingAgentFacade.GitHubCopilot`, `MultiCodingAgentFacade.CodexAppServer`, shared exception names, `RuntimeName`
  - Owned by this slice: Core ownership, shared public naming
  - Consumed by this slice: user product decisions
  - Deferred / unresolved fields: exact health check API remains deferred
- Implementation-realization risks: Present. Existing Core has provider-specific types and MEAI references.
- Recommended process profile: standard-slice
- Immediate next agent: slice-prep
- Required inputs for next agent: parent Plan, triage, this decomposition, source/test inventory, `Directory.Build.props`, current project files.
- Stop condition for this slice: new project graph builds structurally and old provider switching source is removed or explicitly marked for downstream deletion without runtime implementation being falsely complete.

### SL-002: Codex typed runtime

- Goal: `CodexAppServerAgentClient` familyを作り、existing `CodexRpcSession` / stdio transport / thread reuse を typed request API へ接続する。
- Non-goals: Copilot SDK migration、README 全面改訂、real Codex CLI E2E。
- Parent requirements covered: FR-006, FR-008, FR-010.
- Parent acceptance conditions covered: AC-007, AC-008 の Codex 部分。
- Affected components / modules: `src/MeAiUtility.MultiProvider.CodexAppServer`, `CodexRpcSession`, `Options/CodexAppServerProviderOptions.cs`, `Threading/*`, Codex tests.
- Expected implementation scope: typed request/response/streaming update model, request validation, `MultiCodingAgentFacade:CodexAppServer` binding, thread reuse preservation, user input/approval failure semantics.
- Internal high-risk boundary candidates: typed request to JSON-RPC thread/start and turn/start params.
- Cross-slice dependencies: consumes Core exceptions/diagnostics and config naming from SL-001; tests in SL-004.
- Related Cross-slice Contract IDs: XC-001, XC-002, XC-005.
- Cross-slice contract excerpt:
  - XC ID: XC-002
  - This slice role: Consumer/Producer
  - Mechanism: typed request -> runtime options -> JSON-RPC params
  - Required fields / state / identifiers: prompt, model id, reasoning effort, working directory, approval policy, sandbox mode, network access, timeout seconds, auto approve, thread reuse policy, thread id/key/name/store path, diagnostics capture
  - Owned by this slice: Codex request/response/streaming model and runtime mapping
  - Consumed by this slice: Core exception/logging/trace semantics from SL-001
  - Deferred / unresolved fields: real app-server protocol drift remains verification/manual-only
- Implementation-realization risks: Present. Existing implementation is `IChatClient` and `ChatOptions` based.
- Recommended process profile: standard-slice
- Immediate next agent: slice-prep
- Required inputs for next agent: Codex existing plan artifacts, `CodexAppServerChatClient.cs`, `CodexRpcSession.cs`, Codex options/threading/tests.
- Stop condition for this slice: typed Codex API is wired to production JSON-RPC path and fake transport tests cover request validation and params serialization.

### SL-003: Copilot typed runtime

- Goal: `GitHubCopilotAgentClient` familyを作り、existing SDK wrapper/model list/streaming diagnostics を typed request API へ接続する。
- Non-goals: Codex runtime migration、real Copilot authenticated E2E。
- Parent requirements covered: FR-005, FR-007, FR-009.
- Parent acceptance conditions covered: AC-006, AC-008 の Copilot 部分。
- Affected components / modules: `src/MeAiUtility.MultiProvider.GitHubCopilot`, `ICopilotSdkWrapper`, `GitHubCopilotSdkWrapper`, `GitHubCopilotProviderOptions`, Copilot tests.
- Expected implementation scope: typed request/response/streaming update model, model list API, request validation, SDK wrapper config mapping, runtime-specific advanced options.
- Internal high-risk boundary candidates: typed request to SDK session config and model validation.
- Cross-slice dependencies: consumes Core exceptions/diagnostics and config naming from SL-001; tests in SL-004.
- Related Cross-slice Contract IDs: XC-001, XC-003, XC-005.
- Cross-slice contract excerpt:
  - XC ID: XC-003
  - This slice role: Consumer/Producer
  - Mechanism: typed request -> `CopilotSessionConfig` / SDK invocation
  - Required fields / state / identifiers: prompt, model id, reasoning effort, streaming, working directory, config directory, client name, timeout, attachments, skill directories, disabled skills, available/excluded tools, MCP servers, agent, provider override, infinite sessions
  - Owned by this slice: Copilot typed API and SDK mapping
  - Consumed by this slice: Core exception/logging/trace semantics from SL-001
  - Deferred / unresolved fields: SDK permission hooks/user-input details if SDK does not expose stable API
- Implementation-realization risks: Present. Existing implementation is `IChatClient` and `ChatOptions` based.
- Recommended process profile: standard-slice
- Immediate next agent: slice-prep
- Required inputs for next agent: Copilot existing plan artifacts, `GitHubCopilotChatClient.cs`, `GitHubCopilotSdkWrapper.cs`, wrapper abstraction/options/tests.
- Stop condition for this slice: typed Copilot API is wired to production SDK wrapper and unit tests cover model validation, request mapping, streaming support behavior, and secret-safe logging.

### SL-004: Test migration and quality gates

- Goal: old test projectsを削除・移植し、新 API と旧依存混入チェックを検証する。
- Non-goals: runtime implementation itself, README rewrite.
- Parent requirements covered: FR-003, FR-004, FR-012.
- Parent acceptance conditions covered: AC-001, AC-002, AC-003, AC-004, AC-008, AC-009, AC-011.
- Affected components / modules: `tests/MeAiUtility.MultiProvider.*`, new `tests/MultiCodingAgentFacade.*`, CI test command, old-name audit.
- Expected implementation scope: Core/Codex/Copilot unit tests, fake/stub transport tests, opt-in integration skip behavior, old name/dependency guard test or script.
- Internal high-risk boundary candidates: test substitute to production implementation binding.
- Cross-slice dependencies: consumes runtime API from SL-002/SL-003 and project graph from SL-001.
- Related Cross-slice Contract IDs: XC-004, XC-005.
- Cross-slice contract excerpt:
  - XC ID: XC-005
  - This slice role: Consumer
  - Mechanism: tests assert production implementation and DI entrypoints
  - Required fields / state / identifiers: public type names, DI extension names, config sections, fake transport/wrapper binding, old dependency denylist
  - Owned by this slice: quality gates and test mapping
  - Consumed by this slice: production types from SL-001..SL-003
  - Deferred / unresolved fields: manual real runtime validation
- Implementation-realization risks: Present. Current tests are old namespace and MEAI-centric.
- Recommended process profile: standard-slice
- Immediate next agent: slice-prep
- Required inputs for next agent: implemented slices SL-001..SL-003, current tests, workflow files.
- Stop condition for this slice: normal automated tests exercise new API and old dependency/name checks fail on stale public source.

### SL-005: Docs, samples, CI, release zip

- Goal: README、migration note、samples、CI/release workflow を `MultiCodingAgentFacade` の目的と Release zip 配布に合わせる。
- Non-goals: NuGet publish, repository rename, runtime code changes.
- Parent requirements covered: FR-012, FR-014.
- Parent acceptance conditions covered: AC-005, AC-010, AC-012.
- Affected components / modules: `README.md`, sample project(s), `.github/workflows/ci.yml`, `.github/workflows/release.yml`, migration note.
- Expected implementation scope: new quickstarts, security defaults, troubleshooting, migration note, Release zip content update, CI new solution command.
- Internal high-risk boundary candidates: docs/CI may cite stale project names or old provider switching.
- Cross-slice dependencies: consumes final public API names, project paths, config sections from SL-001..SL-003.
- Related Cross-slice Contract IDs: XC-004.
- Cross-slice contract excerpt:
  - XC ID: XC-004
  - This slice role: Consumer
  - Mechanism: docs/workflows reference production project and public API
  - Required fields / state / identifiers: solution name, project paths, DI methods, config section names, Release zip module list
  - Owned by this slice: docs and workflow references
  - Consumed by this slice: outputs from SL-001..SL-003
  - Deferred / unresolved fields: repository rename post-merge note
- Implementation-realization risks: Present. README currently explains old MultiProvider.
- Recommended process profile: standard-slice
- Immediate next agent: slice-prep
- Required inputs for next agent: implemented public API names and project graph.
- Stop condition for this slice: docs and CI no longer tell users to use `IChatClient`, old provider switching, OpenAI/Azure provider, or NuGet publish flow.

### SL-006: Final audit and residual gate

- Goal: all slices後に parent acceptance condition と cross-slice contracts を検証し、FixNow / residual / human-required / deferred を明示する。
- Non-goals: new implementation.
- Parent requirements covered: all.
- Parent acceptance conditions covered: all.
- Affected components / modules: all changed source, tests, docs, workflows.
- Expected implementation scope: no code; verification and residual decision only.
- Internal high-risk boundary candidates: cross-slice completeness.
- Cross-slice dependencies: all slices.
- Related Cross-slice Contract IDs: XC-001..XC-005.
- Cross-slice contract excerpt:
  - XC ID: XC-001..XC-005
  - This slice role: Consumer
  - Mechanism: final cross-slice verification
  - Required fields / state / identifiers: all parent ACs, old-name denylist, production binding evidence
  - Owned by this slice: final PASS/residual classification
  - Consumed by this slice: slice verification results
  - Deferred / unresolved fields: manual-only real runtime evidence
- Implementation-realization risks: Present.
- Recommended process profile: contract-kernel
- Immediate next agent: cross-slice-verification-kernel
- Required inputs for next agent: all slice prep/impl/verification artifacts.
- Stop condition for this slice: residual-decision-gate has classified all remaining work.

## Cross-slice contracts

| Cross-slice Contract ID | Producer slice | Consumer slice | Runtime participants | Mechanism | Required fields / state / identifiers | Error / retry / recovery expectation | Verification requirement | Status |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| XC-001 | SL-001 | SL-002, SL-003, SL-004, SL-005 | Core project and runtime projects | project reference + namespace | Core exception names, `RuntimeName`, trace/request id helpers, logging helpers, file attachment model | runtime-specific exceptions must not leak old provider semantics | verify namespace and public API after all slices | Deferred |
| XC-002 | SL-002 | SL-004, SL-006 | Codex client, `CodexRpcSession`, fake transport | typed request to JSON-RPC | prompt, model id, effort, wd, approval, sandbox, network, timeout, auto approve, thread reuse fields | fail-fast validation; timeout/cancellation releases transport | fake transport tests and production binding verification | Deferred |
| XC-003 | SL-003 | SL-004, SL-006 | Copilot client, SDK wrapper, SDK session | typed request to SDK config | model, reasoning, streaming, paths, tools, MCP, agent, provider override, infinite sessions, attachments, skills | fail-fast validation; unsupported options not ignored | wrapper tests and production binding verification | Deferred |
| XC-004 | SL-001, SL-002, SL-003 | SL-005, SL-006 | solution/projects/docs/workflows | references and commands | solution name, project paths, DI methods, config sections, Release zip content | stale reference must fail audit | docs/CI audit and build/test command verification | Deferred |
| XC-005 | SL-002, SL-003 | SL-004, SL-006 | production runtime clients and test substitutes | unit tests + fake/stub injection | fake transport/wrapper maps to production interfaces and DI entrypoints | tests must not pass against test-only API | verification-kernel checks production binding | Deferred |

## Cross-slice field continuity

| Field / state / identifier | Required by | Source artifact / owner | Producer XC | Intermediate storage / artifact | Consumer XC | Fabrication allowed? | Status | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Public product name `MultiCodingAgentFacade` | all slices | requirements / user decision | XC-001 | project metadata/docs | XC-004 | No | Done | fixed by requirement |
| Package/distribution shape | SL-005 | user decision | XC-004 | release workflow / README | XC-004 | No | Done | Release zip single product; no NuGet scope |
| Target frameworks `net8.0;net10.0` | SL-001, SL-004 | user decision and requirement | XC-004 | project files | XC-004 | No | Done | .NET 10 retained |
| `GitHubCopilotAgentClient` type name | SL-003, SL-004, SL-005 | this Plan | XC-003 | source + docs | XC-004 | No | Deferred | may be corrected after implementation review |
| `CodexAppServerAgentClient` type name | SL-002, SL-004, SL-005 | this Plan | XC-002 | source + docs | XC-004 | No | Deferred | may be corrected after implementation review |
| `RuntimeName` exception field | SL-002, SL-003, SL-004 | SL-001 | XC-001 | Core exception model | XC-005 | No | Deferred | replaces `ProviderName` |
| Codex thread id/key/name/store path | SL-002, SL-004 | existing Codex thread store | XC-002 | Codex request/runtime options | XC-005 | No | Deferred | must remain traceable |
| Copilot provider override secrets | SL-003, SL-004 | existing Copilot options/wrapper | XC-003 | request/options + logging | XC-005 | No | Deferred | log presence flags only |
| Old-name denylist | SL-004, SL-006 | requirements checklist | XC-004 | audit test/script | XC-004 | No | Deferred | allowlist only for migration/history docs |

## Parent contract mapping

| Parent Contract ID | Disposition | Slice ID | Cross-slice Contract ID | Notes |
| --- | --- | --- | --- | --- |
| RC-001 | InternalToSlice | SL-003 | XC-003, XC-005 | Copilot typed API and SDK wrapper binding |
| RC-002 | InternalToSlice | SL-002 | XC-002, XC-005 | Codex typed API and JSON-RPC binding |
| RC-003 | CrossSlice | SL-001, SL-002, SL-003 | XC-001, XC-002, XC-003 | DI/config spans foundation and runtime slices |
| RC-004 | CrossSlice | SL-001, SL-004, SL-005 | XC-004, XC-005 | project/dependency/tests/CI/release |
| RC-005 | CrossSlice | SL-001, SL-002, SL-003 | XC-001 | Core diagnostics/exceptions semantics |

## Execution order

1. SL-001 Project/Core foundation.
2. SL-002 Codex typed runtime.
3. SL-003 Copilot typed runtime.
4. SL-004 Test migration and quality gates.
5. SL-005 Docs, samples, CI, release zip. Some docs drafting can start after API names are stable, but final docs must wait for SL-002/SL-003.
6. SL-006 Final audit and residual gate.

SL-002 and SL-003 both consume SL-001 public/Core decisions and should not be implemented in parallel until SL-001 parent review is READY. SL-004 must wait for production APIs so tests do not define a test-only surface.

## Final cross-slice verification requirements

- Verify AC-001..AC-012 against final source/docs/workflows.
- Verify XC-001: Core public semantics do not mention provider switching and expose runtime-specific diagnostics.
- Verify XC-002: Codex request fields reach JSON-RPC params and thread store state without `ChatOptions`.
- Verify XC-003: Copilot request fields reach SDK wrapper/session config and unsupported options fail fast.
- Verify XC-004: solution, workflows, release zip, README, migration note use new names and no stale provider switching path.
- Verify XC-005: tests bind to production runtime clients and DI entrypoints, not test-only replacements.
- ManualOnly: real GitHub Copilot SDK authenticated E2E and real Codex CLI app-server E2E may remain opt-in skip with clear reason.

## Human decisions required

No blocking product decisions remain for inventory/decomposition.

Deferred human/manual items:

- GitHub repository rename after merge.
- NuGet publish/package archive remains out of scope.
- Real runtime credential/environment validation remains opt-in/manual.
- Public type names may be corrected after review if they prove unclear.

## 今回の decomposition の対象外

- Implementation code.
- Runtime contract details inside each slice.
- Test point mapping.
- Full runtime evidence.
- Manual real-runtime validation.

## Handoff Packet

- Profile used: plan-slice-decomposition
- Parent Plan artifact: `plans/multi-coding-agent-facade-restructure-plan.md`
- Change Risk Triage artifact: `plans/multi-coding-agent-facade-restructure-change-risk-triage.md`
- Slice Decomposition artifact: `plans/multi-coding-agent-facade-restructure-slice-decomposition.md`
- Slice artifacts:
  - `plans/multi-coding-agent-facade-restructure-slice-SL-001.md`
  - `plans/multi-coding-agent-facade-restructure-slice-SL-002.md`
  - `plans/multi-coding-agent-facade-restructure-slice-SL-003.md`
  - `plans/multi-coding-agent-facade-restructure-slice-SL-004.md`
  - `plans/multi-coding-agent-facade-restructure-slice-SL-005.md`
- Slice IDs: SL-001, SL-002, SL-003, SL-004, SL-005, SL-006
- Cross-slice Contract IDs: XC-001, XC-002, XC-003, XC-004, XC-005
- Cross-slice field continuity items: public product name, distribution shape, target frameworks, public type names, runtime exception field, Codex thread fields, Copilot provider override secrets, old-name denylist
- Source artifacts:
  - `plans/multi-coding-agent-facade-restructure-plan.md`
  - `plans/multi-coding-agent-facade-restructure-change-risk-triage.md`
  - requirements and workitem artifacts from management repo
- Files inspected:
  - `AGENTS.md`
  - `Directory.Build.props`
  - `MeAiUtility.slnx`
  - `README.md`
  - `.github/workflows/ci.yml`
  - `.github/workflows/release.yml`
  - source/test/project inventory under `src/` and `tests/`
- Files intentionally not inspected:
  - every individual method body in source/tests; slice-prep owns detailed inspection per slice
  - generated outputs
- Decisions made:
  - Use full-coverage decomposition.
  - Codex is ordered before Copilot but both are required in one PR.
  - Single Release zip product; source projects remain split by responsibility.
- Do not redo unless new evidence appears:
  - slice boundaries and parent contract mapping.
- Remaining work:
  - Run slice-prep for SL-001 first.
  - Parent review must authorize implementation before any code changes.
- Recommended next step: Use `token-aware-full-coverage-3layer` parent orchestration. Start with slice execution table and SL-001 slice-prep. Do not implement directly from this decomposition.
- Required downstream guardrails:
  - Each slice reads parent Plan and this decomposition.
  - Executable slice artifacts are bounded Plans.
  - Slice scope and non-goals are binding.
  - Parent RC to slice/XC mapping is authoritative.
  - Slice-local runtime contract identification, participant mapping, test point mapping, stub usage identification, production binding, production wiring verification, and explicit unresolved status must be preserved.
  - Cross-slice contracts are verified only in final cross-slice verification.
  - Cross-slice field continuity must not be fabricated.
