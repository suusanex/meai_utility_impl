# Slice Plan: SL-002 Codex typed runtime

## Goal

`CodexAppServerAgentClient`、`CodexAppServerTurnRequest`、`CodexAppServerTurnResponse`、`CodexAppServerStreamingUpdate` を作り、existing Codex JSON-RPC implementation へ接続する。

## Non-goals

- Copilot SDK migration.
- README 全面改訂。
- real Codex CLI E2E。

## Parent requirements covered

FR-006, FR-008, FR-010.

## Parent acceptance conditions covered

AC-007 and Codex part of AC-008.

## Affected components / modules

- `src/MeAiUtility.MultiProvider.CodexAppServer`
- `CodexAppServerChatClient.cs`
- `CodexRpcSession.cs`
- `Options/CodexAppServerProviderOptions.cs`
- `Options/CodexRuntimeOptions.cs`
- `Threading/*`
- `tests/MeAiUtility.MultiProvider.CodexAppServer.Tests`

## Expected implementation scope

- Replace `IChatClient` public surface with Codex typed client surface.
- Preserve initialize / initialized / thread/start / turn/start / delta / completed / error / approval / user input handling.
- Preserve thread reuse and file thread store.
- Bind `MultiCodingAgentFacade:CodexAppServer` options.
- Ensure default `approvalPolicy=never`, `sandboxMode=workspace-write`, `networkAccess=false`.
- Fail fast for unsupported or invalid request fields.

## Cross-slice dependencies

Consumes Core exceptions/logging/trace and config naming from SL-001. Produces Codex public API for SL-004 and SL-005.

## Related Cross-slice Contract IDs

XC-001, XC-002, XC-005.

## Cross-slice contract excerpt

- XC ID: XC-002
- This slice role: Consumer/Producer
- Mechanism: typed request -> runtime options -> JSON-RPC params
- Required fields / state / identifiers: prompt, model id, reasoning effort, working directory, approval policy, sandbox mode, network access, timeout seconds, auto approve, thread reuse policy, thread id/key/name/store path, diagnostics capture
- Owned by this slice: Codex typed API and JSON-RPC mapping
- Consumed by this slice: Core runtime exception/logging semantics from SL-001
- Deferred / unresolved fields: real app-server protocol drift remains manual/verification residual

## Recommended next profile

`standard-slice`

## Immediate next agent

`slice-prep`

## Required inputs for next agent

- Parent Plan
- change-risk-triage
- slice decomposition
- existing Codex plan artifacts under `plans/codex-*`
- Codex source and tests

## Stop condition

SL-002 implementation may stop when typed Codex API is wired to production JSON-RPC path and fake transport tests cover request validation and params serialization.
