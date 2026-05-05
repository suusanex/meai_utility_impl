# Coverage Gap Triage

## Scope

This triage uses the caller-selected Source A only: `plans/codex-app-server-verification-kernel.md`.

Unresolved items were normalized from these Source A sections only:
- `## Runtime contract verification`
- `## Stub-to-Production Binding`
- `## Test observations`
- `## Unresolved items`

`## Stub-to-Production Binding` had no unresolved rows because all selected rows were `Bound`.

Reference artifacts used only for mapping/classification context:
- `plans/codex-app-server-plan.md`
- `plans/codex-app-server-test-design-kernel.md`
- `plans/codex-app-server-runtime-contract-kernel.md`
- `plans/codex-app-server-provider-change-risk-triage.md`

Selected source type: `verification-kernel`
Selected IDs reviewed: `RC-002-b`, `RC-002-c`, `RC-002-f`, `TP-001-a`, `TP-001-b`, `TP-001-c`, `TP-001-d`, `TP-001-e`, `TP-002-a`, `TP-002-b-1`, `TP-002-b-2`, `TP-002-b-3`, `TP-002-c-1`, `TP-002-c-2`, `TP-002-d`, `TP-002-e-1`, `TP-002-e-2`, `TP-002-f-1`, `TP-002-f-2`, `TP-003-a-1`, `TP-003-a-2`, `TP-003-b`, `TP-003-c-1`, `TP-003-c-2`, `TP-003-d`, `U-001`〜`U-011`.

## Gap classification

| ID | Current status | Plan requirement / contract | Gap type | Suggested next action | Recommended target profile |
| --- | --- | --- | --- | --- | --- |
| RC-002-b | NotImplementedOrMismatch | RC-002-b — `turn/completed` required fields and status handling | ContractMismatch | Runtime contract verification: update `CodexRpcSession` handling for `turn/completed` so required `params.turn.id` is represented/validated in production handling. | fix-slice |
| RC-002-c | NotImplementedOrMismatch | RC-002-c — `error` notification required correlation fields and retry behavior | ContractMismatch | Runtime contract verification: update `CodexRpcSession` handling for `error` notifications so required `threadId` / `turnId` are represented/validated. | fix-slice |
| RC-002-f | NotImplementedOrMismatch | RC-002-f — `thread/status/changed` required fields and wait-state behavior | ContractMismatch | Runtime contract verification: update `CodexRpcSession` handling so required `threadId` / `status.type` are represented/validated alongside existing `activeFlags` logic. | fix-slice |
| TP-001-a | not run in this pass | RC-001-a — `initialize` request fields | TestOracleMissing | Test observations: add concrete assertion for `initialize` payload / `clientInfo.name` / `clientInfo.version` in the cited Codex provider test. | fix-slice |
| TP-001-b | not run in this pass | RC-001-b — `initialized` notification shape | TestOracleMissing | Test observations: add concrete assertion for `initialized` notification shape (`method`, no params) in the cited Codex provider test. | fix-slice |
| TP-001-c | not run in this pass | RC-001-c — `thread/start` request and error-response behavior | TestOracleMissing | Test observations: add the missing selected error-response assertion for `thread/start` → `ProviderException`. | fix-slice |
| TP-001-d | not run in this pass | RC-001-d — `turn/start` request fields / effort mapping | AlreadyCoveredButDocumentationStale | Test observations: documentation-only update to record that the cited test already contains direct assertions for `threadId`, text input, and `effort=="high"`. Do not change production/test code unless new evidence appears. | fix-slice |
| TP-001-e | not run in this pass | RC-001-e — `sandboxPolicy` / `networkAccess` placement | AlreadyCoveredButDocumentationStale | Test observations: documentation-only update to record that the cited test already asserts missing top-level `networkAccess` and present `sandboxPolicy.networkAccess`. | fix-slice |
| TP-002-a | not run in this pass | RC-002-a — delta aggregation | AlreadyCoveredButDocumentationStale | Test observations: documentation-only update to record that the cited test already asserts final aggregated text `Hello World`. | fix-slice |
| TP-002-b-1 | not run in this pass | RC-002-b — `turn/completed(status:"completed")` success path | AlreadyCoveredButDocumentationStale | Test observations: documentation-only update to record existing concrete assertion for completed-turn success path. | fix-slice |
| TP-002-b-2 | not run in this pass | RC-002-b — `turn/completed(status:"failed")` error path | AlreadyCoveredButDocumentationStale | Test observations: documentation-only update to record existing concrete assertion for `ProviderException("model overload")`. | fix-slice |
| TP-002-b-3 | missing | RC-002-b — `turn/completed(status:"interrupted")` cancellation path | TestOracleMissing | Test observations: add a selected test for interrupted completion returning `OperationCanceledException`. | fix-slice |
| TP-002-c-1 | not run in this pass | RC-002-c — `error(willRetry:true)` continue path | AlreadyCoveredButDocumentationStale | Test observations: documentation-only update to record existing concrete retryable-error assertion. | fix-slice |
| TP-002-c-2 | not run in this pass | RC-002-c — `error(willRetry:false)` fail path | AlreadyCoveredButDocumentationStale | Test observations: documentation-only update to record existing concrete non-retryable error assertion. | fix-slice |
| TP-002-d | missing | RC-002-d — EOF / process exit faults pending request | TestOracleMissing | Test observations: add an EOF-specific test (or equivalent scripted EOF fake path) so pending turn fault behavior is concretely verified. | fix-slice |
| TP-002-e-1 | not run in this pass | RC-002-e — approval request with `AutoApprove=false` | AlreadyCoveredButDocumentationStale | Test observations: documentation-only update to record existing concrete assertion for `decision:"cancel"` plus thrown `ProviderException`. | fix-slice |
| TP-002-e-2 | not run in this pass | RC-002-e — approval request with `AutoApprove=true` | AlreadyCoveredButDocumentationStale | Test observations: documentation-only update to record existing concrete assertion for `decision:"acceptForSession"` plus successful completion. | fix-slice |
| TP-002-f-1 | not run in this pass | RC-002-f — `waitingOnUserInput` error path | AlreadyCoveredButDocumentationStale | Test observations: documentation-only update to record existing concrete assertion for `ProviderException("User input required")`. | fix-slice |
| TP-002-f-2 | missing | RC-002-f — `waitingOnApproval` continuation path | TestOracleMissing | Test observations: add the missing selected test that preserves wait state until later approval request handling. | fix-slice |
| TP-003-a-1 | not run in this pass | RC-003-a — `MultiProviderOptions.Validate()` accepts Codex section | AlreadyCoveredButDocumentationStale | Test observations: documentation-only update to record existing direct `Throws.Nothing` assertion. | fix-slice |
| TP-003-a-2 | missing | RC-003-a — `MultiProviderOptions.Validate()` rejects null Codex section | TestOracleMissing | Test observations: add the missing direct null-section validation test. | fix-slice |
| TP-003-b | not run in this pass | RC-003-b — explicit `CodexAppServer` provider discovery / mapping | TestOracleMissing | Test observations: replace/broaden the current artifact so it verifies actual `DiscoverProviderType()` / `ProviderFactory.Create()` mapping instead of bypassing it via pre-registered `FakeClient`. | fix-slice |
| TP-003-c-1 | not run in this pass | RC-003-c — singleton registration of `CodexAppServerChatClient` | AlreadyCoveredButDocumentationStale | Test observations: documentation-only update to record existing singleton assertion. | fix-slice |
| TP-003-c-2 | not run in this pass | RC-003-c — singleton registration of `ICodexTransportFactory` | AlreadyCoveredButDocumentationStale | Test observations: documentation-only update to record existing singleton assertion. | fix-slice |
| TP-003-d | not run in this pass | RC-003-d — `codex.*` extension prefix allowed | AlreadyCoveredButDocumentationStale | Test observations: documentation-only update to record existing `Set/Get("codex.workingDirectory")` assertion. | fix-slice |
| U-001 | missing-test | RC-001-a — `initialize` request fields | TestOracleMissing | Unresolved items: add missing concrete assertion for `initialize` payload / `clientInfo` fields in `CodexAppServerChatClientTests.cs`. | fix-slice |
| U-002 | missing-test | RC-001-b — `initialized` notification shape | TestOracleMissing | Unresolved items: add missing concrete assertion for `initialized` notification shape in `CodexAppServerChatClientTests.cs`. | fix-slice |
| U-003 | missing-test | RC-001-c — `thread/start` error-response behavior | TestOracleMissing | Unresolved items: add selected `thread/start` error-response assertion in `CodexAppServerChatClientTests.cs`. | fix-slice |
| U-004 | contract-mismatch | RC-002-b — `turn/completed` required `params.turn.id` | ContractMismatch | Unresolved items: fix `CodexRpcSession` to read/validate required `turn.id`. | fix-slice |
| U-005 | contract-mismatch | RC-002-c — `error` notification required `threadId` / `turnId` | ContractMismatch | Unresolved items: fix `CodexRpcSession` to read/validate required `threadId` / `turnId`. | fix-slice |
| U-006 | missing-test | RC-002-b — interrupted completion path | TestOracleMissing | Unresolved items: add selected interrupted-path test in `CodexAppServerChatClientTests.cs`. | fix-slice |
| U-007 | missing-test | RC-002-d — EOF / pending-fault path | TestOracleMissing | Unresolved items: add EOF-specific test and any needed fake support in `ScriptedCodexTransport`. | fix-slice |
| U-008 | contract-mismatch | RC-002-f — `thread/status/changed` required `threadId` / `status.type` | ContractMismatch | Unresolved items: fix `CodexRpcSession` to read/validate required `threadId` / `status.type`. | fix-slice |
| U-009 | missing-test | RC-002-f — `waitingOnApproval` continuation path | TestOracleMissing | Unresolved items: add selected continuation-path test in `CodexAppServerChatClientTests.cs`. | fix-slice |
| U-010 | missing-test | RC-003-a — null Codex section validation | TestOracleMissing | Unresolved items: add direct null-section validation test in `MultiProviderOptionsTests.cs`. | fix-slice |
| U-011 | missing-test | RC-003-b — actual provider mapping observation | TestOracleMissing | Unresolved items: add/replace provider-factory test so it verifies Codex mapping instead of fake-client bypass. | fix-slice |

## Recommended fix slices

| Slice | Included ID(s) / gap type(s) | Why grouped | Target files / addresses | Recommended agent | Recommended profile | Preconditions / human decision needed |
| --- | --- | --- | --- | --- | --- | --- |
| Contract alignment in `CodexRpcSession` | `verification-kernel.md#Runtime contract verification: RC-002-b / ContractMismatch`; `RC-002-c / ContractMismatch`; `RC-002-f / ContractMismatch`; `verification-kernel.md#Unresolved items: U-004 / ContractMismatch`; `U-005 / ContractMismatch`; `U-008 / ContractMismatch` | Same module, same runtime sequence family, same production contract-handling gap | `src/MeAiUtility.MultiProvider.CodexAppServer/CodexRpcSession.cs` | `coverage-gap-resolution-slice.agent.md` | `fix-slice` | なし |
| Startup request/notification test completion | `verification-kernel.md#Test observations: TP-001-a / TestOracleMissing`; `TP-001-b / TestOracleMissing`; `TP-001-c / TestOracleMissing`; `verification-kernel.md#Unresolved items: U-001 / TestOracleMissing`; `U-002 / TestOracleMissing`; `U-003 / TestOracleMissing` | Same test file and same startup/session-init contract family | `tests/MeAiUtility.MultiProvider.CodexAppServer.Tests/CodexAppServerChatClientTests.cs` | `coverage-gap-resolution-slice.agent.md` | `fix-slice` | なし |
| Turn interruption / EOF / approval-wait test completion | `verification-kernel.md#Test observations: TP-002-b-3 / TestOracleMissing`; `TP-002-d / TestOracleMissing`; `TP-002-f-2 / TestOracleMissing`; `verification-kernel.md#Unresolved items: U-006 / TestOracleMissing`; `U-007 / TestOracleMissing`; `U-009 / TestOracleMissing` | Same read-loop / scripted transport behavior family; likely one bounded test pass | `tests/MeAiUtility.MultiProvider.CodexAppServer.Tests/CodexAppServerChatClientTests.cs`; `tests/MeAiUtility.MultiProvider.CodexAppServer.Tests/Fakes/ScriptedCodexTransport.cs` | `coverage-gap-resolution-slice.agent.md` | `fix-slice` | なし |
| Options / provider-factory test completion | `verification-kernel.md#Test observations: TP-003-a-2 / TestOracleMissing`; `TP-003-b / TestOracleMissing`; `verification-kernel.md#Unresolved items: U-010 / TestOracleMissing`; `U-011 / TestOracleMissing` | Same DI/options wiring area; narrow test-only scope | `tests/MeAiUtility.MultiProvider.Tests/Options/MultiProviderOptionsTests.cs`; `tests/MeAiUtility.MultiProvider.Tests/Configuration/ProviderFactoryTests.cs` | `coverage-gap-resolution-slice.agent.md` | `fix-slice` | なし |
| Runtime/test documentation refresh | `verification-kernel.md#Test observations: TP-001-d / AlreadyCoveredButDocumentationStale`; `TP-001-e / AlreadyCoveredButDocumentationStale`; `TP-002-a / AlreadyCoveredButDocumentationStale`; `TP-002-b-1 / AlreadyCoveredButDocumentationStale`; `TP-002-b-2 / AlreadyCoveredButDocumentationStale`; `TP-002-c-1 / AlreadyCoveredButDocumentationStale`; `TP-002-c-2 / AlreadyCoveredButDocumentationStale`; `TP-002-e-1 / AlreadyCoveredButDocumentationStale`; `TP-002-e-2 / AlreadyCoveredButDocumentationStale`; `TP-002-f-1 / AlreadyCoveredButDocumentationStale` | Same artifact family: cited Codex provider tests already contain evidence; only coverage/verification recording is stale | `plans/codex-app-server-verification-kernel.md` or downstream coverage document update only | `coverage-gap-resolution-slice.agent.md` | `fix-slice` | なし |
| DI/options documentation refresh | `verification-kernel.md#Test observations: TP-003-a-1 / AlreadyCoveredButDocumentationStale`; `TP-003-c-1 / AlreadyCoveredButDocumentationStale`; `TP-003-c-2 / AlreadyCoveredButDocumentationStale`; `TP-003-d / AlreadyCoveredButDocumentationStale` | Same artifact family: DI/options tests already cited as concrete evidence; only recording is stale | `plans/codex-app-server-verification-kernel.md` or downstream coverage document update only | `coverage-gap-resolution-slice.agent.md` | `fix-slice` | なし |

## Human decisions required

なし

## Handoff Packet

- Profile used: triage-only
- Source artifact type: verification-kernel
- Source artifact: `plans/codex-app-server-verification-kernel.md`
- Reference artifacts:
  - `plans/codex-app-server-plan.md`
  - `plans/codex-app-server-test-design-kernel.md`
  - `plans/codex-app-server-runtime-contract-kernel.md`
  - `plans/codex-app-server-provider-change-risk-triage.md`
- Items reviewed:
  - Runtime contract verification: `RC-002-b`, `RC-002-c`, `RC-002-f`
  - Test observations: `TP-001-a`, `TP-001-b`, `TP-001-c`, `TP-001-d`, `TP-001-e`, `TP-002-a`, `TP-002-b-1`, `TP-002-b-2`, `TP-002-b-3`, `TP-002-c-1`, `TP-002-c-2`, `TP-002-d`, `TP-002-e-1`, `TP-002-e-2`, `TP-002-f-1`, `TP-002-f-2`, `TP-003-a-1`, `TP-003-a-2`, `TP-003-b`, `TP-003-c-1`, `TP-003-c-2`, `TP-003-d`
  - Unresolved items: `U-001`〜`U-011`
- Downstream selectors:
  - `plans/codex-app-server-verification-kernel.md#Runtime contract verification: RC-002-b / ContractMismatch`
  - `plans/codex-app-server-verification-kernel.md#Runtime contract verification: RC-002-c / ContractMismatch`
  - `plans/codex-app-server-verification-kernel.md#Runtime contract verification: RC-002-f / ContractMismatch`
  - `plans/codex-app-server-verification-kernel.md#Test observations: TP-001-a / TestOracleMissing`
  - `plans/codex-app-server-verification-kernel.md#Test observations: TP-001-b / TestOracleMissing`
  - `plans/codex-app-server-verification-kernel.md#Test observations: TP-001-c / TestOracleMissing`
  - `plans/codex-app-server-verification-kernel.md#Test observations: TP-001-d / AlreadyCoveredButDocumentationStale`
  - `plans/codex-app-server-verification-kernel.md#Test observations: TP-001-e / AlreadyCoveredButDocumentationStale`
  - `plans/codex-app-server-verification-kernel.md#Test observations: TP-002-a / AlreadyCoveredButDocumentationStale`
  - `plans/codex-app-server-verification-kernel.md#Test observations: TP-002-b-1 / AlreadyCoveredButDocumentationStale`
  - `plans/codex-app-server-verification-kernel.md#Test observations: TP-002-b-2 / AlreadyCoveredButDocumentationStale`
  - `plans/codex-app-server-verification-kernel.md#Test observations: TP-002-b-3 / TestOracleMissing`
  - `plans/codex-app-server-verification-kernel.md#Test observations: TP-002-c-1 / AlreadyCoveredButDocumentationStale`
  - `plans/codex-app-server-verification-kernel.md#Test observations: TP-002-c-2 / AlreadyCoveredButDocumentationStale`
  - `plans/codex-app-server-verification-kernel.md#Test observations: TP-002-d / TestOracleMissing`
  - `plans/codex-app-server-verification-kernel.md#Test observations: TP-002-e-1 / AlreadyCoveredButDocumentationStale`
  - `plans/codex-app-server-verification-kernel.md#Test observations: TP-002-e-2 / AlreadyCoveredButDocumentationStale`
  - `plans/codex-app-server-verification-kernel.md#Test observations: TP-002-f-1 / AlreadyCoveredButDocumentationStale`
  - `plans/codex-app-server-verification-kernel.md#Test observations: TP-002-f-2 / TestOracleMissing`
  - `plans/codex-app-server-verification-kernel.md#Test observations: TP-003-a-1 / AlreadyCoveredButDocumentationStale`
  - `plans/codex-app-server-verification-kernel.md#Test observations: TP-003-a-2 / TestOracleMissing`
  - `plans/codex-app-server-verification-kernel.md#Test observations: TP-003-b / TestOracleMissing`
  - `plans/codex-app-server-verification-kernel.md#Test observations: TP-003-c-1 / AlreadyCoveredButDocumentationStale`
  - `plans/codex-app-server-verification-kernel.md#Test observations: TP-003-c-2 / AlreadyCoveredButDocumentationStale`
  - `plans/codex-app-server-verification-kernel.md#Test observations: TP-003-d / AlreadyCoveredButDocumentationStale`
  - `plans/codex-app-server-verification-kernel.md#Unresolved items: U-001 / TestOracleMissing`
  - `plans/codex-app-server-verification-kernel.md#Unresolved items: U-002 / TestOracleMissing`
  - `plans/codex-app-server-verification-kernel.md#Unresolved items: U-003 / TestOracleMissing`
  - `plans/codex-app-server-verification-kernel.md#Unresolved items: U-004 / ContractMismatch`
  - `plans/codex-app-server-verification-kernel.md#Unresolved items: U-005 / ContractMismatch`
  - `plans/codex-app-server-verification-kernel.md#Unresolved items: U-006 / TestOracleMissing`
  - `plans/codex-app-server-verification-kernel.md#Unresolved items: U-007 / TestOracleMissing`
  - `plans/codex-app-server-verification-kernel.md#Unresolved items: U-008 / ContractMismatch`
  - `plans/codex-app-server-verification-kernel.md#Unresolved items: U-009 / TestOracleMissing`
  - `plans/codex-app-server-verification-kernel.md#Unresolved items: U-010 / TestOracleMissing`
  - `plans/codex-app-server-verification-kernel.md#Unresolved items: U-011 / TestOracleMissing`
- Items intentionally not reviewed:
  - `plans/codex-app-server-implementation-coverage-of-integration-test.md` — caller explicitly selected Source A only
  - Source A rows with success statuses (`Done`, `Bound`) — outside unresolved-item normalization scope
- Decisions made:
  - Classified `RC-002-b`, `RC-002-c`, `RC-002-f` as `ContractMismatch` because production handling exists but does not represent required contract fields.
  - Classified test-observation rows with explicit “artifact contains direct assertions” evidence as `AlreadyCoveredButDocumentationStale`.
  - Classified test-observation rows marked `missing`, or `not run in this pass` plus notes showing the selected assertion is absent/misaligned, as `TestOracleMissing`.
  - Kept `Unresolved items` rows as independent classification rows because Source A section-specific handling is required.
- Do not redo unless new evidence appears:
  - Source A already establishes that production binding / DI wiring for the selected `Bound` items is confirmed.
  - Source A already establishes that the main blocking production gaps are the `RC-002-b`, `RC-002-c`, `RC-002-f` contract mismatches.
  - Source A notes already contain enough evidence to treat `TP-001-d`, `TP-001-e`, `TP-002-a`, `TP-002-b-1`, `TP-002-b-2`, `TP-002-c-1`, `TP-002-c-2`, `TP-002-e-1`, `TP-002-e-2`, `TP-002-f-1`, `TP-003-a-1`, `TP-003-c-1`, `TP-003-c-2`, `TP-003-d` as documentation-stale rather than missing-test gaps.
- Remaining work:
  - Production: align `CodexRpcSession` with required correlation / status fields for `RC-002-b`, `RC-002-c`, `RC-002-f`
  - Tests: add missing selected assertions/tests for `TP-001-a`, `TP-001-b`, `TP-001-c`, `TP-002-b-3`, `TP-002-d`, `TP-002-f-2`, `TP-003-a-2`, `TP-003-b`
  - Documentation: refresh verification/coverage recording for rows classified as `AlreadyCoveredButDocumentationStale`
- Recommended next step:
  1. Run `coverage-gap-resolution-slice.agent.md` with selectors for `RC-002-b`, `RC-002-c`, `RC-002-f`, `U-004`, `U-005`, `U-008` (highest priority, blocking production mismatch slice).
  2. Run `coverage-gap-resolution-slice.agent.md` with selectors for `TP-001-a`, `TP-001-b`, `TP-001-c`, `U-001`, `U-002`, `U-003`.
  3. Run `coverage-gap-resolution-slice.agent.md` with selectors for `TP-002-b-3`, `TP-002-d`, `TP-002-f-2`, `U-006`, `U-007`, `U-009`.
  4. Run `coverage-gap-resolution-slice.agent.md` with selectors for `TP-003-a-2`, `TP-003-b`, `U-010`, `U-011`.
  5. Run documentation-only `coverage-gap-resolution-slice.agent.md` passes for the `AlreadyCoveredButDocumentationStale` selectors.
