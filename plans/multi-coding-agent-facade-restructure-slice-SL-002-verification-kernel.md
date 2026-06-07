# Verification Kernel: SL-002

## Verdict

- Status: PARENT_PLAN_PARTIAL_WITH_FIX_CANDIDATES
- Reason: SL-002 の bounded verification は PASS。Codex typed runtime は build/test/audit を通過した。Parent Plan 全体は SL-003/SL-004/SL-005/SL-006 の結果を待つ。

## Verification evidence

| Check | Result | Notes |
| --- | --- | --- |
| `dotnet restore tests\MultiCodingAgentFacade.CodexAppServer.Tests\MultiCodingAgentFacade.CodexAppServer.Tests.csproj` | Pass | Codex test project restore。 |
| `dotnet build tests\MultiCodingAgentFacade.CodexAppServer.Tests\MultiCodingAgentFacade.CodexAppServer.Tests.csproj --no-restore` | Pass | net8.0/net10.0、0 warnings / 0 errors。preview SDK message は出力。 |
| `dotnet test tests\MultiCodingAgentFacade.CodexAppServer.Tests\MultiCodingAgentFacade.CodexAppServer.Tests.csproj --no-build --no-restore` | Pass | net8.0/net10.0 で 4 tests pass。 |
| `dotnet restore MultiCodingAgentFacade.slnx` | PassWithWarnings | Copilot SDK 推移依存の NU1902/NU1903 warning。 |
| `dotnet build MultiCodingAgentFacade.slnx --no-restore` | PassWithWarnings | 0 errors。NETSDK1057 preview SDK message と Copilot-side NU1902/NU1903 warning。 |
| `dotnet test MultiCodingAgentFacade.slnx --no-restore --no-build` | Pass | net8.0/net10.0 の全 test project が成功。 |
| Codex old API / old name denylist audit | Pass | source-only audit。`bin/obj` は除外。 |
| Codex exception logging catch audit | Pass | catch-and-wrap / catch-and-drop path は `Exception.ToString()` をログへ出す。 |

## Covered IDs

| ID | Kind | Status | Notes |
| --- | --- | --- | --- |
| RC-SL002-001 | Runtime contract | Verified | New public Codex API exists; old `IChatClient` surface absent from new Codex source/test. |
| RC-SL002-002 | Runtime contract | Verified | request mapping test covers initialize/thread/start/turn/start fields including sandbox/network/effort. |
| RC-SL002-003 | Runtime contract | PartialVerified | streaming delta/completed and runtime error are tested. approval/user-input handler remains production-code-present but dedicated tests are deferred. |
| RC-SL002-004 | Runtime contract | PartialVerified | public thread types/store/registry are present; dedicated persistence/registry tests are deferred to SL-004. |
| RC-SL002-005 | Runtime contract | Verified | production DI/config entrypoint exists and references production services. |
| RC-SL002-006 | Runtime contract | Verified | timeout/error wrapping and catch logging paths audited. |
| XC-002 | Cross-slice contract | ProducerReady | Codex typed API and JSON-RPC mapping ready for SL-004/SL-006 consumers. |
| XC-005 | Cross-slice contract | ProducerPartial | fake transport/store test substitution exists; SL-004 should broaden binding tests. |

## Remaining Work

| Item | Status | Notes |
| --- | --- | --- |
| Approval/user-input dedicated tests | Deferred | Existing session code was migrated, but focused xUnit coverage belongs to SL-004. |
| Thread store persistence tests | Deferred | Production file store exists; broader tests belong to SL-004. |
| Real Codex CLI / app-server E2E | ManualOnly | Not required by SL-002 bounded verification. |
| Repository-wide old-name audit | Deferred | Old source remains migration source until later slices. |
| Copilot SDK vulnerability warnings | ResidualDeferred | NU1902/NU1903 are outside Codex runtime and should be considered in SL-003/SL-006. |
| .NET 11 preview SDK message | LocalEnvironment | NETSDK1057 is emitted by the local SDK; target frameworks remain `net8.0;net10.0`. |

## Handoff Packet

- Profile used: verification-kernel
- Source artifacts:
  - `plans/multi-coding-agent-facade-restructure-plan.md`
  - `plans/multi-coding-agent-facade-restructure-slice-SL-002.md`
  - `plans/multi-coding-agent-facade-restructure-slice-SL-002-prep.md`
  - `plans/multi-coding-agent-facade-restructure-parent-review-gate.md`
  - `plans/multi-coding-agent-facade-restructure-slice-SL-002-implementation-result.md`
- Selected contracts / IDs: RC-SL002-001..RC-SL002-006, XC-001, XC-002, XC-004, XC-005
- Files inspected: new Codex source/test project graph and Codex catch/logging paths
- Decisions made:
  - Public Codex enum is `CodexReasoningEffort` with `None` / `Minimal` / `Low` / `Medium` / `High` / `XHigh`.
  - `ICodexThreadRegistry` is part of Codex runtime public surface.
  - Config section is `MultiCodingAgentFacade:CodexAppServer`.
  - DI entrypoint is `AddCodexAppServerAgentRuntime`.
- Do not redo unless new evidence appears:
  - Codex implementation does not depend on `Microsoft.Extensions.AI` in new source/test.
  - fake transport/store are test-only.
  - production binding is present in `CodexAppServerServiceExtensions`.
- Recommended next step: implement SL-003 using the already authorized parent review gate.
