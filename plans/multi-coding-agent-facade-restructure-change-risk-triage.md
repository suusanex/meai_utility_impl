# Change Risk Triage

## 推奨プロファイル

`full-coverage`

## 理由

この再編は public API、project/solution rename、dependency deletion、Copilot external SDK binding、Codex stdio JSON-RPC binding、DI/config、tests、README、CI/release を同時に変更する。
1つの implementation pass に流すと、runtime-specific typed API と既存 runtime implementation の接続、旧 `IChatClient` / `MultiProvider` 削除、production wiring、test migration が互いにずれた状態で false PASS になる可能性が高い。

`full-coverage` は「全部を一気に実装する」という意味ではなく、実装前に bounded slice へ分解する診断として扱う。Immediate next agent は `plan-slice-decomposition.agent.md`。

## High-risk boundaries

| Boundary | Producer | Consumer | Mechanism | Risk type |
| --- | --- | --- | --- | --- |
| Copilot typed request to SDK invocation | `GitHubCopilotAgentClient` / request model | `GitHubCopilotSdkWrapper.BuildInvocation` / GitHub Copilot SDK | method call + SDK session config | External SDK / implementation-realization / config mapping |
| Codex typed turn request to app-server JSON-RPC | `CodexAppServerAgentClient` / request model | `CodexRpcSession` / `codex app-server` | stdio JSON-RPC | Cross-process sequence / state transition / timeout |
| Runtime DI/config registration | `IServiceCollection` extension and options binding | application startup resolving runtime clients | DI + `MultiCodingAgentFacade:*` config | Startup wiring / production binding |
| Core diagnostics and exceptions | Core exception/logging/trace helpers | Copilot and Codex runtime clients | shared library dependency | semantic mismatch between provider and runtime concepts |
| Project rename and dependency removal | new solution/project files | tests / CI / release zip | project references + workflows | stale references / dependency audit failure |
| Test substitute to production implementation | stub wrappers / fake transports | production SDK/transport/client wiring | test projects and DI | stub-only success |

## 対象とする runtime contracts

| Contract ID | Boundary | What is at risk | Why selected | Triage status | Next action |
| --- | --- | --- | --- | --- | --- |
| RC-001 | Copilot typed API -> SDK wrapper -> SDK session | request fields such as model, reasoning, streaming, tools, MCP, agent, provider override, attachments, skills may not reach SDK | Copilot is one of two primary runtime values and uses external SDK | Deferred | plan-slice-decomposition で Copilot slice と XC に分解する |
| RC-002 | Codex typed API -> JSON-RPC session -> app-server | thread/turn ids, sandbox, approval, network, auto approve, status, errors may mismatch protocol | Cross-process runtime with durable thread state | Deferred | plan-slice-decomposition で Codex slice と XC に分解する |
| RC-003 | New DI/config -> runtime clients/options | new section and old provider registration may diverge | User-facing entrypoint and production wiring risk | Deferred | project/core/API slice と runtime slices に分ける |
| RC-004 | New project/namespace/dependency graph -> tests/CI/release | old MEAI/OpenAI/MultiProvider references may remain or CI may test wrong solution | Completion depends on structural cleanup and quality gates | Deferred | structure/docs/CI slices に分ける |
| RC-005 | Shared Core diagnostics/exceptions -> runtime-specific callers | old `ProviderName` semantics may leak into new runtime API | Cross-runtime shared surface must remain minimal and semantic | Deferred | Core slice and runtime slices に分ける |

## 選択されなかった候補 runtime contracts

| Contract ID | Boundary | Why not selected | Candidate status | Suggested next action |
| --- | --- | --- | --- | --- |
| RC-X01 | Real Copilot authenticated E2E | opt-in/manual environment dependent | ManualOnly | integration sliceで skip / manual evidence として扱う |
| RC-X02 | Real Codex CLI / app-server E2E | opt-in/manual environment dependent | ManualOnly | integration sliceで skip / manual evidence として扱う |
| RC-X03 | Repository rename on GitHub | explicitly out of scope | OutOfScopeForThisPass | merge後の別作業 |
| RC-X04 | NuGet publish ownership | explicitly out of scope | OutOfScopeForThisPass | Release zip distribution only |

## Risk trigger スキャン

| Risk trigger | Present / Absent / Unclear | Notes |
| --- | --- | --- |
| Cross-process or cross-service sequence | Present | Codex stdio JSON-RPC and subprocess lifecycle |
| Queue / event / webhook / background worker | Absent | No queue/webhook; streaming events are runtime updates, not background worker queue |
| External API or SDK | Present | GitHub Copilot SDK and Codex CLI/app-server |
| Authentication or authorization | Present | Copilot token/logged-in user and Codex CLI authentication delegation |
| Durable state / retry / replay / idempotency | Present | Codex thread store and thread reuse |
| Startup wiring / DI / configuration | Present | new options sections and runtime-specific DI |
| Production implementation split from test substitute | Present | SDK wrapper mocks and Codex fake transport are used heavily |
| Multiple runtime participants coordinating state | Present | typed client, wrapper/session, process transport, thread store |
| Observable behavior spanning more than one component | Present | public API, runtime implementation, tests, docs, CI all co-vary |

## 実装実現性リスク

| Trigger | Status | Evidence | Required next step |
| --- | --- | --- | --- |
| Plan names a specific external SDK or API | Present | GitHub Copilot SDK and `codex app-server` are explicit requirements | slice-specific implementation-contract branch before runtime contract where API surface is moved |
| Plan names a package, release, binary artifact, or local lib folder | Present | `GitHub.Copilot.SDK`, Release zip distribution, new solution/project names | structure and dependency slices must confirm references |
| Plan names a namespace, type, method, extension method, provider ID, or config section | Present | new client/request/response names and `MultiCodingAgentFacade:*` sections | implementation-contract for public API naming and DI surface |
| Existing code contains a similar but different implementation path | Present | current `GitHubCopilotChatClient` and `CodexAppServerChatClient` implement `IChatClient` | avoid nearest-neighbor preservation of old API |
| Implementation requires DI/startup/configuration wiring | Present | runtime-specific DI and options binding required | DI/config slice and verification |
| The affected production address is not known from current evidence | Present | final project paths and public types are not yet created | plan-slice-decomposition and implementation-contract |
| Plan contains remaining work about API surface inspection or dependency confirmation | Present | advanced options and health check API are deferred | slice-local residual tracking |

## 推奨する次の agent

Immediate next agent: `plan-slice-decomposition.agent.md`

Minimum required downstream flow:

1. `plan-slice-decomposition.agent.md`
2. For executable slices, slice-prep / per-slice triage and implementation-contract as needed
3. per-slice runtime-contract-kernel and test-design-kernel
4. implementation-handoff-review
5. bounded implementation pass
6. per-slice verification-kernel
7. final cross-slice-verification-kernel
8. residual-decision-gate

## full-coverage 時の分割方針

`plan-slice-decomposition` は parent acceptance conditions AC-001 から AC-012 を保持し、次の slice 軸で分解すること。

- project / solution / dependency / Core extraction
- Codex typed API and runtime migration
- Copilot typed API and runtime migration
- tests and integration/opt-in migration
- docs / migration note / samples / CI / release
- final old-name and dependency audit

Cross-slice contracts must keep public API naming, shared Core exception/diagnostics semantics, DI/config section names, test substitute to production binding, and old-name removal traceable.

## 今回の triage の対象外

- Individual method-level runtime contract detail
- Test point mapping
- Full runtime evidence
- Actual code migration
- Manual real runtime E2E

## Handoff Packet

- Profile used: triage-only
- Recommended process profile: `full-coverage`
- Source artifacts:
  - `plans/multi-coding-agent-facade-restructure-plan.md`
  - `D:/Data/git/copilot-worktrees/personal-project-driver/main/evidence/MultiCodingAgentFacade_requirements.md`
  - `AGENTS.md`
- Selected contracts / IDs: RC-001, RC-002, RC-003, RC-004, RC-005
- Files inspected:
  - `AGENTS.md`
  - `Directory.Build.props`
  - `MeAiUtility.slnx`
  - `README.md`
  - `.github/workflows/ci.yml`
  - `.github/workflows/release.yml`
  - source/test/project inventory under `src/` and `tests/`
- Files intentionally not inspected:
  - full bodies of every unit test, because decomposition only needs ownership and boundary grouping
  - generated outputs and package artifacts
- Decisions made:
  - Recommend `full-coverage`.
  - Do not connect to Full autonomous Plan-first flow.
  - Treat product decisions supplied by user as consumed.
- Implementation realization risk summary: Present across public API, SDK binding, JSON-RPC binding, DI/config, dependency deletion, project rename.
- Do not redo unless new evidence appears:
  - `full-coverage` diagnosis
  - parent-level RC candidates
  - out-of-scope status for repository rename and NuGet publish
- Remaining work:
  - Create slice decomposition and executable slice artifacts.
  - Prepare slice-local guardrail artifacts before implementation.
- Recommended next step: `plan-slice-decomposition.agent.md` using this triage and the parent Plan.
- Required downstream guardrails: runtime contract identification, participant/boundary mapping, test point mapping, stub/fake/in-memory usage check, production implementation binding, production wiring/entrypoint verification, explicit unresolved status.
- Full-coverage handling: `plan-slice-decomposition.agent.md` へ進める。Full autonomous Plan-first flow へは接続しない。
