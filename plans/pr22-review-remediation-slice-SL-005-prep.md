# Slice Preparation Result: SL-005

## Verdict

- Status: READY_FOR_PARENT_REVIEW
- Reason: SL-005 は Codex App Server の JSON-RPC 通知 / response model / streaming update の field continuity を扱う bounded slice として準備可能。implementation-realization risk は Present であり、`CodexRpcSession.ExecuteTurnAsync` の戻り値変更、公開 response/update shape、error / cancellation propagation、request / trace correlation の出所に non-trivial な判断があるため、implementation-contract-kernel と implementation-contract-review-kernel の下書きを含めて親レビューへ渡す。

## Agent metadata

- Agent type: slice-prep
- Model: gpt-5.4
- Reasoning effort: medium
- Parent authorization artifact: ユーザー依頼「PR22 remediation の slice-prep。対象は SL-005 のみ。実装禁止。」および `plans/pr22-review-remediation-slice-SL-005.md`
- Delegation evidence: `plans/pr22-review-remediation-plan.md`、`plans/pr22-review-remediation-change-risk-triage.md`、`plans/pr22-review-remediation-slice-decomposition.md`、`plans/pr22-review-remediation-slice-SL-005.md`、`AGENTS.md`、SL-005 関連 Codex source/tests の read-only 確認に基づき、この prep artifact のみを作成。production code / tests / README / workflow は編集していない。

## Generated / drafted artifacts

- Per-slice change-risk-triage:
  - Recommended profile: `standard-slice`
  - Implementation-realization risk: Present
  - Reason: 現行 `CodexAppServerTurnResponse` は `Text` のみ、`CodexAppServerStreamingUpdate` は `Kind` / `TextDelta` / `FinalText` のみ、`CodexRpcSession.ExecuteTurnAsync` は `Task<string>` を返す。いっぽう `CodexRpcSession` は JSON-RPC の `threadId`、`turn.id`、`turn.status`、`error.message`、`item/agentMessage/delta` の `turnId` を読み取っており、公開 response/update へ渡す前に破棄している。
  - High-risk boundaries:
    - JSON-RPC `thread/start` / `turn/start` / `turn/completed` / `error` / `thread/status/changed` から typed result model への continuity。
    - `CodexRpcSession.ExecuteTurnAsync` の戻り値を `string` から result model に変える public / internal API 影響。
    - streaming delta と completed update の metadata 一貫性。delta 発生時点と final result 確定時点で得られる fields が異なる。
    - failed / interrupted / retry / timeout / user-input-required の扱い。例外へ変換するだけでは error summary / correlation を利用者が取得できない可能性がある。
    - `Threading/*` の thread reuse / thread store touch timing。既存 thread id と新規 thread id のどちらが response に残るかが曖昧になると downstream docs / opt-in smoke が壊れる。
  - Runtime contract candidates selected:
    - RC-PR22-004: Codex typed API -> `CodexRpcSession` -> app-server JSON-RPC
    - RC-SL005-001: `CodexRpcSession.ExecuteTurnAsync` returns typed turn result
    - RC-SL005-002: streaming update carries available turn correlation metadata
    - RC-SL005-003: failure / interrupted / user-input-required status preserves error summary and correlation without fabricating unknown fields
  - Risk not selected for this slice:
    - Copilot SDK / provider override / permission handling: SL-004 owns。
    - README / sample projection: SL-003 consumes this slice output。
    - real Codex app-server opt-in smoke: SL-006 owns。
- Implementation-contract-kernel:
  - Required because implementation-realization risk is Present.
  - Contract name: `IC-SL005-CodexTurnResultPropagation`
  - Participants:
    - Producer: Codex app-server JSON-RPC messages read by `ICodexTransport`
    - Mapper: `CodexRpcSession`
    - Public consumer: `CodexAppServerAgentClient.ExecuteTurnAsync` / `StreamTurnAsync`
    - Downstream consumers: SL-003 README/sample, SL-006 opt-in integration smoke
  - Proposed production shape constraints:
    - `CodexRpcSession.ExecuteTurnAsync` should return a typed result model rather than `string`.
    - The result model must carry at least final text, thread id, turn id, status, request id, trace id, diagnostics summary, and error summary where source evidence exists.
    - `CodexAppServerTurnResponse` must expose the same stable correlation / status fields needed by AC-009 and AC-010.
    - `CodexAppServerStreamingUpdate` must expose thread / turn / status / request / trace correlation for delta and completed updates where known. Unknown values must remain nullable or explicitly absent; implementation must not synthesize fake identifiers to pass tests.
    - `turn/completed` with `status = completed` should return a successful result with `FinalText`.
    - `turn/completed` with `status = failed` must preserve `ErrorSummary` and status. Whether public `ExecuteTurnAsync` throws, returns a failed result, or both via exception metadata is a contract decision requiring review.
    - `error` notification with `willRetry = false` must preserve the error message as `ErrorSummary`; retrying errors may be diagnostics-only unless a final failure follows.
    - `thread/status/changed` with `waitingOnUserInput` must not become an opaque operation failure without status / error summary.
    - Exception logging must continue to log `Exception.ToString()` per `AGENTS.md` if exceptions are thrown or swallowed.
  - Source-backed fields:
    - `thread id`: `thread/start` result and notification `params.threadId`
    - `turn id`: `turn/start` result `turn.id`, notification `params.turnId`, `turn/completed.params.turn.id`
    - `status`: `turn/completed.params.turn.status`, `thread/status/changed.params.status.type`
    - `final text`: aggregated deltas or `turn.items[].text`
    - `text delta`: `item/agentMessage/delta.params.delta`
    - `error summary`: `turn.error.message` or `error.params.error.message`
  - Source-unclear fields:
    - `trace id`: existing Core `AgentTelemetry` can produce trace id, but current Codex client does not start telemetry. Parent/impl review must decide whether SL-005 wires `AgentTelemetry` or uses another established trace source.
    - `request id`: JSON-RPC request ids exist inside `CodexRpcSession`, while public request correlation may be `AgentTelemetry.RequestId`. Parent/impl review must decide naming and whether to expose JSON-RPC ids separately from public request id.
    - `diagnostics summary`: current transport diagnostics expose command / args / exit code / stderr tail only for process exit. Parent/impl review must decide the stable, secret-safe summary shape.
- Implementation-contract-review-kernel:
  - Review requirement: Required before slice-impl.
  - Non-trivial decisions to review:
    - Whether failed Codex turns return a result model with failed status, throw `RuntimeOperationException`, or throw while carrying correlation / error information through exception properties. This affects existing tests and caller semantics.
    - Whether `request id` means public facade request id, JSON-RPC envelope id, or both. Do not collapse these without explicit naming.
    - Whether `trace id` should be produced by `AgentTelemetry.Start` in `CodexAppServerAgentClient`, and whether it must be shared by non-streaming and streaming paths.
    - Whether streaming `Completed` update should include `FinalText` plus final status / diagnostics, and whether delta updates should include partially-known status or leave status null until final.
    - Whether a new public enum for Codex turn status is introduced or status remains a string. If enum is used, unknown app-server statuses must fail fast or map to an explicit unknown value by reviewed policy.
    - Whether diagnostics summary includes stderr tail from `ICodexTransportDiagnostics`; if yes, secret masking expectations must be stated before implementation.
  - Required reviewers / gate:
    - Parent review gate must confirm SL-005 can own Codex public result/update shape without conflicting with SL-003 and SL-006.
    - If shared Core telemetry types are changed, parent review must check shared ownership risk with SL-004 before parallel implementation.
- Runtime-contract-kernel:
  - RC-PR22-004 / RC-SL005-001:
    - Boundary: app-server JSON-RPC -> `CodexRpcSession` typed turn result -> `CodexAppServerTurnResponse`
    - Required participants: `ICodexTransport`, `CodexRpcSession`, `CodexAppServerAgentClient`, `CodexAppServerTurnResponse`
    - Required fields: thread id, turn id, status, trace id, request id, diagnostics summary, error summary, final text
    - Success condition: completed turn retains thread / turn / status / correlation fields through the public response; `ExecuteTurnAsync` no longer discards metadata by returning only text.
    - Failure condition: failed / interrupted / unsupported interaction paths retain source-backed status and error summary; opaque exceptions without correlation are not sufficient.
    - Fabrication rule: no generated placeholder thread id / turn id / request id / trace id may be asserted as Done without a source path from JSON-RPC or telemetry.
  - RC-SL005-002:
    - Boundary: `item/agentMessage/delta` and final `turn/completed` notifications -> `CodexAppServerStreamingUpdate`
    - Required participants: `CodexRpcSession.HandleNotificationAsync`, `CodexAppServerAgentClient.StreamTurnAsync`, streaming channel/update model
    - Required fields: kind, text delta, final text, thread id, turn id, status, trace id, request id, diagnostics summary, error summary
    - Success condition: delta update exposes source-backed thread id / turn id with each delta, and completed update exposes final status / final text / correlation.
    - Failure condition: final metadata exists only in non-streaming response, or streaming completed update remains metadata-free.
  - RC-SL005-003:
    - Boundary: `turn/completed.status`, `error.willRetry`, `thread/status/changed` -> public error behavior
    - Required participants: `CodexRpcSession`, `RuntimeOperationException` / result model, logger
    - Required fields: status, error summary, trace id, request id, diagnostics summary where available
    - Success condition: fail-fast remains visible, but diagnostic / correlation fields are not lost before caller-visible response or exception.
    - Failure condition: `RuntimeOperationException` message contains only a text message while source-backed thread / turn / status is discarded.
- Test-design-kernel:
  - Scope: test design only。ここでは tests を作成・編集しない。
  - TP-SL005-001: non-streaming completed turn returns `CodexAppServerTurnResponse` with `Text`, `ThreadId`, `TurnId`, `Status`, `TraceId`/`RequestId` per reviewed contract. Fake transport should emit `thread/start`, `turn/start`, `item/agentMessage/delta`, `turn/completed`.
  - TP-SL005-002: streaming delta update carries `TextDelta`, `ThreadId`, `TurnId`, and request/trace correlation where source-backed; completed update carries `FinalText` and final `Status`.
  - TP-SL005-003: failed `turn/completed` with `error.message` preserves `Status = failed` and `ErrorSummary = model overload` through reviewed public behavior.
  - TP-SL005-004: non-retry `error` notification preserves `ErrorSummary` and does not become an uncorrelated generic operation failure.
  - TP-SL005-005: `thread/status/changed` waiting-on-user-input path remains fail-fast but exposes a caller-visible summary/status according to reviewed contract.
  - TP-SL005-006: `ThreadReusePolicy.ReuseByThreadId` and `ReuseOrCreateByKey` response metadata use the actual resolved thread id; tests must not hard-code fabricated ids beyond scripted transport source values.
  - TP-SL005-007: request/trace id source test follows the reviewed contract. If telemetry is used, assert non-empty stable facade request id / trace id; if JSON-RPC ids are exposed separately, assert they are named separately.
  - TP-SL005-008: diagnostics summary test uses fake transport diagnostics or source-backed error payload only; no secret-like strings should be required in expected output.
  - Required checks for slice-impl:
    - `dotnet test MultiCodingAgentFacade.sln --filter MultiCodingAgentFacade.CodexAppServer.Tests` or repo-equivalent targeted test command after parent authorization.
    - At minimum, Codex App Server unit tests must compile against new response/update/result models.
  - Tests must use fake/stub transport and thread store. CI tests must not launch real Codex or require credentials; real app-server E2E remains SL-006 / ManualOnly.

## Bounded parent Plan pass / Guardrail Focus

- Bounded parent Plan pass:
  - Covered FR: FR-008
  - Covered AC: AC-009, AC-010
  - Covered parent RC: RC-PR22-004
  - Slice-local guardrail focus: JSON-RPC field continuity、typed turn result、streaming metadata、error summary / diagnostics preservation、production client mapping。
- Guardrail Focus coverage:
  - `CodexAppServerTurnResponse` must not remain `Text` only。
  - `CodexAppServerStreamingUpdate` must not remain metadata-free。
  - `CodexRpcSession.ExecuteTurnAsync` must not return `string` if that discards turn result metadata。
  - field continuity must be traceable from app-server JSON-RPC or established telemetry source。
  - production path (`CodexAppServerAgentClient` -> `CodexRpcSession` -> transport) must be the binding target; fake-only helper behavior is insufficient。

## Non-goals

- Copilot SDK handling。
- README 全面改訂。
- CI old-name audit。
- real Codex app-server E2E。
- SL-003 docs/sample や SL-006 opt-in smoke の実装。
- OpenAI / Azure OpenAI / old MultiProvider surface の削除。
- production code / tests / README / workflow の編集。

## RC / TP / XC ledger

| ID | Kind | Owned / Consumed / Deferred | Notes |
| --- | --- | --- | --- |
| RC-PR22-004 | Parent RC | Owned | SL-005 は Codex JSON-RPC result propagation を所有する。最終 Done 判定は cross-slice verification へ Deferred。 |
| RC-SL005-001 | Slice RC | Owned | `CodexRpcSession.ExecuteTurnAsync` の typed turn result 化。 |
| RC-SL005-002 | Slice RC | Owned | streaming update の thread / turn / status / correlation metadata。 |
| RC-SL005-003 | Slice RC | Owned | failed / interrupted / user-input-required / retry error の summary と correlation。 |
| TP-SL005-001 | Test Point | Deferred | non-streaming completed turn response metadata。実装時にテスト作成。 |
| TP-SL005-002 | Test Point | Deferred | streaming delta/completed metadata。実装時にテスト作成。 |
| TP-SL005-003 | Test Point | Deferred | failed turn result/error summary。実装時にテスト作成。 |
| TP-SL005-004 | Test Point | Deferred | non-retry error notification。実装時にテスト作成。 |
| TP-SL005-005 | Test Point | Deferred | waiting-on-user-input status。実装時にテスト作成。 |
| TP-SL005-006 | Test Point | Deferred | thread reuse field continuity。実装時にテスト作成。 |
| TP-SL005-007 | Test Point | Deferred | request/trace id source contract。review 後にテスト確定。 |
| TP-SL005-008 | Test Point | Deferred | diagnostics summary and secret-safe behavior。review 後にテスト確定。 |
| XC-PR22-002 | Cross-slice Contract | Produced for SL-003 / Deferred | SL-005 は Codex public API type/field を生産する。README/sample 反映は SL-003。 |
| XC-PR22-004 | Cross-slice Contract | Owned as producer / Deferred | Codex result model and JSON-RPC field propagation。完了判定は cross-slice verification。 |
| XC-PR22-005 | Cross-slice Contract | Produced for SL-006 / Deferred | opt-in smoke が消費する production client response fields。real app-server protocol drift は SL-006 / ManualOnly。 |
| AC-009 | Parent AC | Owned partially | response/update fields の提供。docs/test smoke との一致は cross-slice。 |
| AC-010 | Parent AC | Owned | production path の typed turn result propagation。 |

## Production binding requirements

- `CodexAppServerAgentClient.ExecuteTurnAsync` must map the `CodexRpcSession` typed result into `CodexAppServerTurnResponse`; response metadata must not be produced only inside tests.
- `CodexAppServerAgentClient.StreamTurnAsync` must map source-backed metadata into `CodexAppServerStreamingUpdate`; completed update must carry final state required by the reviewed contract.
- `CodexRpcSession` must preserve JSON-RPC fields read from production transport. Implementation must avoid adding test-only bypasses that skip `ICodexTransport`.
- Thread id must come from resolved thread source: existing `ThreadId`, persisted thread record, or `thread/start` result. The response must not use a hard-coded fallback.
- Turn id must come from `turn/start` result or notification payload. If app-server omits it, the behavior must be fail-fast or explicitly absent by reviewed contract, not silently fabricated.
- Trace/request correlation must come from reviewed production source, likely Core telemetry and/or JSON-RPC request ids with distinct naming.
- Diagnostics summary must be secret-safe and source-backed. Transport stderr tail, JSON-RPC error payload, and captured diagnostics events require explicit masking/summary policy before Done.
- Unit tests must use fakes/stubs but exercise production `CodexAppServerAgentClient` and `CodexRpcSession` code paths.

## Cross-slice risks to parent-review

- SL-003 consumes the Codex public API. If SL-003 implementation starts before this contract is reviewed, README/sample may invent fields or names.
- SL-006 consumes production client fields. If request/trace id naming changes after SL-006 starts, opt-in smoke tests may bind to stale response shape.
- SL-004 may also need Core telemetry / diagnostics changes. If both SL-004 and SL-005 edit shared Core telemetry or diagnostics models, parent review should serialize or assign ownership.
- `request id` ambiguity can create false continuity: JSON-RPC envelope id is not necessarily the same as public facade request id.
- Real app-server protocol drift is explicitly Deferred / ManualOnly. SL-005 should use source-backed scripted protocol fixtures, while SL-006 handles opt-in runtime confirmation.
- Error behavior can create an acceptance gap: throwing on failed turns may be fine, but only if caller-visible exception/result still carries status, error summary, and correlation.

## Unresolved items

- `trace id` source: existing `AgentTelemetry` is available, but current Codex path does not use it. Needs implementation-contract review.
- `request id` source and naming: public facade request id vs JSON-RPC request id must be distinguished or explicitly mapped. Needs implementation-contract review.
- `diagnostics summary` shape and secret masking policy are not established by source evidence. Needs implementation-contract review.
- Public status type: string vs enum, and unknown status handling, needs review.
- Failed turn public behavior: return failed result vs throw with metadata, needs review.
- Final streaming `Completed` update shape: whether it includes `FinalText` and full final metadata, needs review.

## Stop condition

この slice-prep は `READY_FOR_PARENT_REVIEW` で停止する。実装、テスト作成、README、workflow、source code 編集は行わない。親レビューで implementation-contract decisions と shared ownership risk を確認した後にのみ slice-impl へ渡す。

## Handoff to Agent Usage Ledger

- Run ID: SL-005-prep-2026-06-07-codex
- Phase: slice-prep
- Slice: SL-005
- Edit allowed: No
- Outcome: READY_FOR_PARENT_REVIEW。作成 artifact は `plans/pr22-review-remediation-slice-SL-005-prep.md`。production code / tests / README / workflow の編集なし。
