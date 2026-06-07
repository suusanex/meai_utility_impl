# Verification Kernel: SL-001

## Verdict

- Status: PARENT_PLAN_PARTIAL_WITH_FIX_CANDIDATES
- Reason: SL-001 の bounded acceptance は満たしたが、parent Plan 全体の AC-001..AC-012 は runtime migration / tests / docs / final audit を待つ。旧 source directories は downstream migration source として残っているため、全体 PASS ではない。

## Verification evidence

| Check | Result | Notes |
| --- | --- | --- |
| `dotnet restore MultiCodingAgentFacade.slnx` | PassWithWarnings | Copilot SDK 推移依存の `Nerdbank.MessagePack` NU1902/NU1903 warning。 |
| `dotnet build MultiCodingAgentFacade.slnx --no-restore` | PassWithWarnings | 0 errors。NETSDK1057 preview SDK message と NU1902/NU1903 warning。 |
| `dotnet test MultiCodingAgentFacade.slnx --no-restore --no-build` | Pass | net8.0/net10.0 の skeleton tests が成功。 |
| `dotnet build MultiCodingAgentFacade.sln --no-restore` | PassWithWarnings | `.sln` と `.slnx` の同等 graph build を確認。 |
| new source graph old MEAI/chat/provider denylist audit | Pass | source-only audit。`bin/obj` は除外。 |
| new source graph OpenAI/Azure denylist audit | Pass | source-only audit。 |
| new source graph reflection discovery audit | Pass | `GetTypes(`、`GetAssemblies(`、`System.Reflection`、`typeof(` pattern なし。 |

## Covered IDs

| ID | Kind | Status | Notes |
| --- | --- | --- | --- |
| RC-SL001-001 | Runtime contract | Verified | New `.slnx` / `.sln` buildable project graph exists. |
| RC-SL001-002 | Runtime contract | Verified | Core runtime semantics use `RuntimeName`; old `ProviderName` does not appear in new source graph. |
| RC-SL001-003 | Runtime contract | PartialVerified | New source graph does not contain provider switching types. Old source remains outside new graph. |
| RC-SL001-004 | Runtime contract | PartialVerified | New graph excludes OpenAI/Azure provider projects. Old provider directories remain outside new graph. |
| XC-001 | Cross-slice contract | ProducerReady | Downstream slices can consume `MultiCodingAgentFacade.Core`. |
| XC-004 | Cross-slice contract | ProducerPartial | Solution/project naming is available; docs/CI/release still deferred. |

## Remaining Work

| Item | Status | Notes |
| --- | --- | --- |
| Old `MeAiUtility.MultiProvider*` source/test directories | Deferred | Required as migration source for SL-002/SL-003/SL-004; final deletion/audit belongs to later slices. |
| Direct vs transitive `Microsoft.Extensions.AI.*` dependency | PartiallyDone | Direct props/project references removed from new source graph. `GitHub.Copilot.SDK` currently brings MEAI abstractions transitively. |
| Copilot SDK vulnerability warnings | NeedsResidualDecision | NU1902/NU1903 from `Nerdbank.MessagePack 1.0.2`; may require SDK upgrade or explicit residual acceptance. |
| .NET 11 preview SDK warning | ManualOnly | Local SDK emits NETSDK1057. Target frameworks remain `net8.0;net10.0`. |
| Runtime typed APIs | Deferred | SL-002/SL-003. |
| Full test migration and old-name denylist | Deferred | SL-004. |
| Docs/CI/release rewrite | Deferred | SL-005. |

## Handoff Packet

- Profile used: verification-kernel
- Source artifacts:
  - `plans/multi-coding-agent-facade-restructure-plan.md`
  - `plans/multi-coding-agent-facade-restructure-change-risk-triage.md`
  - `plans/multi-coding-agent-facade-restructure-slice-decomposition.md`
  - `plans/multi-coding-agent-facade-restructure-slice-SL-001.md`
  - `plans/multi-coding-agent-facade-restructure-slice-SL-001-prep.md`
  - `plans/multi-coding-agent-facade-restructure-parent-review-gate.md`
  - `plans/multi-coding-agent-facade-restructure-slice-SL-001-implementation-result.md`
- Selected contracts / IDs: RC-SL001-001, RC-SL001-002, RC-SL001-003, RC-SL001-004, XC-001, XC-004
- Files inspected: new `MultiCodingAgentFacade.*` source/test project graph and selected old source inventory from prep
- Files intentionally not inspected: full old runtime method bodies and all legacy tests; downstream slices own detailed migration
- Decisions made:
  - Keep old source outside new graph as migration source.
  - Treat `.slnx` as CI/source-of-truth and `.sln` as Visual Studio compatible graph.
  - Do not create health check API in SL-001.
- Do not redo unless new evidence appears:
  - SL-001 Core exception base name is `RuntimeFacadeException`.
  - Shared runtime identifier field is `RuntimeName`.
  - `ProviderOverrideOptions` is not Core-owned in SL-001.
- Remaining work: see table above.
- Recommended next step: parent should run slice-prep for SL-002 and SL-003 using SL-001 artifacts and implementation result.
