# Slice Plan: SL-003 Copilot typed runtime

## Goal

`GitHubCopilotAgentClient`、`GitHubCopilotAgentRequest`、`GitHubCopilotAgentResponse`、`GitHubCopilotStreamingUpdate` を作り、existing GitHub Copilot SDK wrapper へ接続する。

## Non-goals

- Codex runtime migration.
- real Copilot authenticated E2E。
- NuGet publish。

## Parent requirements covered

FR-005, FR-007, FR-009.

## Parent acceptance conditions covered

AC-006 and Copilot part of AC-008.

## Affected components / modules

- `src/MeAiUtility.MultiProvider.GitHubCopilot`
- `GitHubCopilotChatClient.cs`
- `GitHubCopilotSdkWrapper.cs`
- `GitHubCopilotCliSdkWrapper.cs`
- `Abstractions/ICopilotSdkWrapper.cs`
- `Options/GitHubCopilotProviderOptions.cs`
- `tests/MeAiUtility.MultiProvider.GitHubCopilot.Tests`

## Expected implementation scope

- Replace `IChatClient` public surface with Copilot typed client surface.
- Preserve model list API.
- Preserve SDK wrapper session config mapping for model, reasoning, streaming, attachments, tools, MCP servers, agent, provider override, infinite sessions, skills, diagnostics.
- Preserve streaming behavior without fake whitespace streaming.
- Fail fast on unknown model id, unsupported reasoning, invalid timeout, invalid attachments, invalid auth/provider override.

## Cross-slice dependencies

Consumes Core exceptions/logging/trace and config naming from SL-001. Produces Copilot public API for SL-004 and SL-005.

## Related Cross-slice Contract IDs

XC-001, XC-003, XC-005.

## Cross-slice contract excerpt

- XC ID: XC-003
- This slice role: Consumer/Producer
- Mechanism: typed request -> `CopilotSessionConfig` / SDK invocation
- Required fields / state / identifiers: prompt, model id, reasoning effort, streaming, working directory, config directory, client name, timeout, attachments, skill directories, disabled skills, available/excluded tools, MCP servers, agent, provider override, infinite sessions
- Owned by this slice: Copilot typed API and SDK mapping
- Consumed by this slice: Core runtime exception/logging semantics from SL-001
- Deferred / unresolved fields: SDK permission hooks/user-input details if SDK does not expose stable API

## Recommended next profile

`standard-slice`

## Immediate next agent

`slice-prep`

## Required inputs for next agent

- Parent Plan
- change-risk-triage
- slice decomposition
- existing Copilot plan artifacts under `plans/github-copilot-*` and `plans/copilot-*`
- Copilot source and tests

## Stop condition

SL-003 implementation may stop when typed Copilot API is wired to production SDK wrapper and unit tests cover model validation, request mapping, streaming support behavior, and secret-safe logging.
