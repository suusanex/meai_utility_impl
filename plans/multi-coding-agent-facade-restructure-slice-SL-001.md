# Slice Plan: SL-001 Project/Core foundation

## Goal

`MultiCodingAgentFacade` の solution/project/Core foundation を作り、旧 provider switching と MEAI dependency を削除できる構造へ移す。

## Non-goals

- Codex / Copilot runtime behavior の詳細移植。
- README 全面改訂。
- real runtime E2E。

## Parent requirements covered

FR-001, FR-002, FR-003, FR-004, FR-011, FR-013, FR-014.

## Parent acceptance conditions covered

AC-002, AC-003, AC-004, AC-005, AC-011 の土台。

## Affected components / modules

- `MeAiUtility.sln`
- `MeAiUtility.slnx`
- `Directory.Build.props`
- `src/MeAiUtility.MultiProvider`
- `src/MeAiUtility.MultiProvider.OpenAI`
- `src/MeAiUtility.MultiProvider.AzureOpenAI`
- project files under `src/`
- project files under `tests/`

## Expected implementation scope

- `MultiCodingAgentFacade.sln` / `MultiCodingAgentFacade.slnx` を作成する。
- `MultiCodingAgentFacade.Core` project を作成し、共有例外、diagnostics、logging、file attachment、trace/request id helper を移す。
- OpenAI / Azure OpenAI / OpenAI compatible provider project を削除する。
- Provider factory / registry / `MultiProviderOptions` / `IProviderCapabilities` / `ExtensionParameters` / `ConversationExecutionOptions` の削除方針を実装へ反映する。
- `net8.0;net10.0` を維持する。

## Cross-slice dependencies

Produces Core naming and project graph consumed by SL-002, SL-003, SL-004, SL-005.

## Related Cross-slice Contract IDs

XC-001, XC-002, XC-003, XC-004.

## Cross-slice contract excerpt

- XC ID: XC-001
- This slice role: Producer
- Mechanism: source project and namespace ownership
- Required fields / state / identifiers: `MultiCodingAgentFacade.Core`, `MultiCodingAgentFacade.GitHubCopilot`, `MultiCodingAgentFacade.CodexAppServer`, shared exception names, `RuntimeName`
- Owned by this slice: Core ownership, shared public naming, project graph
- Consumed by this slice: requirements and product decisions
- Deferred / unresolved fields: exact health check API remains deferred

## Recommended next profile

`standard-slice`

## Immediate next agent

`slice-prep`

## Required inputs for next agent

- `plans/multi-coding-agent-facade-restructure-plan.md`
- `plans/multi-coding-agent-facade-restructure-change-risk-triage.md`
- `plans/multi-coding-agent-facade-restructure-slice-decomposition.md`
- current solution/project files
- source inventory under `src/MeAiUtility.MultiProvider*`

## Stop condition

SL-001 implementation may stop when the new project graph and Core foundation are present, old provider switching source is removed or blocked with explicit reason, and downstream runtime slices can reference stable Core and project names.
