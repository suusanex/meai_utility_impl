# Plan Slice Decomposition

## 親 Plan の要約

PR #22 review comment `4641587916` で示された blocking / major 指摘を、`MultiCodingAgentFacade` 再編の完了条件として扱う。
現状は新 `MultiCodingAgentFacade.*` project が追加されている一方で旧 `MeAiUtility.MultiProvider` 構成が残っているため、旧構成削除、README / CI / sample / opt-in integration test / runtime response model / provider validation / permission handling を bounded slice に分解する。

## full-coverage 判定の理由

対象は project / solution graph、CI、README、samples、tests、Copilot SDK boundary、Codex JSON-RPC boundary、response / streaming metadata、provider override validation、permission policy、旧名 audit にまたがる。
単一の fix-slice にすると、CI が旧 solution を見続ける、docs が旧 `IChatClient` を案内する、runtime metadata が response model で失われる、または placeholder integration test で false PASS になる可能性が高い。

## 分割方針

- 旧構成削除と solution graph の所有者を先に作る。
- CI / old-name audit は構造削除の後に置き、migration note や planning artifacts の合法的な旧名説明を false positive にしない scoped audit として扱う。
- Copilot と Codex の runtime model は別 slice にし、metadata continuity を cross-slice contract として残す。
- README / sample は public API と runtime policy を消費する slice にする。runtime code の先取り仕様書として扱わない。
- opt-in integration は常時 real runtime CI ではなく、skip reason と production path binding を検証する slice にする。
- cross-slice verification は実装 slice ではなく、全 slice 後の `cross-slice-verification-kernel` へ渡す。

## Slice 一覧

| Slice ID | Name | Goal | Recommended profile | Immediate next agent | Depends on | Can run in parallel? |
| --- | --- | --- | --- | --- | --- | --- |
| SL-001 | 旧構成削除と solution graph 固定 | 旧 `MeAiUtility` / `MultiProvider` project と solution を削除し、新 solution graph を source of truth にする | standard-slice | slice-prep | none | No |
| SL-002 | CI と scoped old-name audit | CI を新 solution へ切り替え、旧名 / 旧依存混入チェックを scoped audit として追加する | standard-slice | slice-prep | SL-001 | Partially |
| SL-003 | README / migration note / sample | README と sample を新 typed runtime API に合わせ、migration note と安全上の注意を整える | standard-slice | slice-prep | SL-001, SL-004, SL-005 | Partially |
| SL-004 | Copilot runtime contract remediation | Copilot response / streaming / model capability / provider override / permission handling を修正する | standard-slice | slice-prep | SL-001 | Partially |
| SL-005 | Codex runtime contract remediation | Codex turn result / streaming metadata / `CodexRpcSession` result model propagation を修正する | standard-slice | slice-prep | SL-001 | Partially |
| SL-006 | opt-in integration と production-binding smoke | placeholder integration test を opt-in smoke tests に置き換え、skip reason と production path binding を確認する | standard-slice | slice-prep | SL-004, SL-005 | Partially |

## Slice 詳細

### SL-001: 旧構成削除と solution graph 固定

- Goal: 旧 `MeAiUtility.sln` / `MeAiUtility.slnx`、旧 `src/MeAiUtility.MultiProvider*`、旧 `tests/MeAiUtility.MultiProvider*` を削除し、`MultiCodingAgentFacade.sln` / `.slnx` を solution graph の source of truth にする。
- Non-goals: CI audit 実装、README 改訂、Copilot / Codex runtime model 変更、integration smoke。
- Parent requirements covered: FR-001, FR-002.
- Parent acceptance conditions covered: AC-001, AC-002, AC-003 の structural 部分。
- Affected components / modules: `MeAiUtility.sln`, `MeAiUtility.slnx`, `src/MeAiUtility.MultiProvider*`, `tests/MeAiUtility.MultiProvider*`, `MultiCodingAgentFacade.sln`, `MultiCodingAgentFacade.slnx`, `Directory.Build.props`.
- Expected implementation scope: 旧 solution / project / tests の削除、new solution graph の参照整合、旧 MEAI / OpenAI / AzureOpenAI dependency の source graph からの削除。
- Internal high-risk boundary candidates: deletion による stale project reference、new solution graph が source / tests を取りこぼす risk。
- Cross-slice dependencies: SL-002 が audit / CI target として消費し、SL-003 が migration note の削除済み事実として消費する。
- Related Cross-slice Contract IDs: XC-PR22-001, XC-PR22-006.
- Cross-slice contract excerpt:
  - XC ID: XC-PR22-001
  - This slice role: Producer
  - Mechanism: solution / project reference graph
  - Required fields / state / identifiers: `MultiCodingAgentFacade.sln`, `MultiCodingAgentFacade.slnx`, remaining source project paths, remaining test project paths, deleted old project paths
  - Owned by this slice: old solution / old project deletion and new solution graph
  - Consumed by this slice: parent Plan deletion requirements
  - Deferred / unresolved fields: CI command and audit allowlist are owned by SL-002
- Implementation-realization risks: Present. Deleting root solution and old directories can expose stale references across workflow, README, samples, and tests.
- Recommended process profile: standard-slice
- Immediate next agent: slice-prep
- Required inputs for next agent: parent Plan, parent triage, this decomposition, `MultiCodingAgentFacade.sln(x)`, `MeAiUtility.sln(x)`, source/test inventory.
- Stop condition for this slice: repository source graph no longer contains old solution / old project directories, and downstream slices can treat `MultiCodingAgentFacade.sln(x)` as authoritative.

### SL-002: CI と scoped old-name audit

- Goal: CI を `MultiCodingAgentFacade.sln` 基準へ切り替え、旧 `MeAiUtility` / `MultiProvider` / `IChatClient` / `Microsoft.Extensions.AI` / OpenAI / AzureOpenAI 混入チェックを scoped audit として追加する。
- Non-goals: runtime API 実装、README 本文全面改訂、旧構成削除そのもの。
- Parent requirements covered: FR-004, FR-013.
- Parent acceptance conditions covered: AC-003, AC-005, AC-015 の audit 部分。
- Affected components / modules: `.github/workflows/ci.yml`, audit script or test, `Directory.Build.props`, `MultiCodingAgentFacade.sln`.
- Expected implementation scope: restore / build / test target 切替、old-name / old-dependency denylist、migration note / planning artifacts / historical references の allowlist 定義、CI green のための command 整理。
- Internal high-risk boundary candidates: blanket grep による migration note false positive、audit scope 漏れによる false negative。
- Cross-slice dependencies: SL-001 の new solution graph を消費し、SL-003 の migration note と allowlist を調整し、SL-006 の opt-in integration test target を消費する。
- Related Cross-slice Contract IDs: XC-PR22-001, XC-PR22-005, XC-PR22-006.
- Cross-slice contract excerpt:
  - XC ID: XC-PR22-006
  - This slice role: Producer/Consumer
  - Mechanism: repository scan / CI audit
  - Required fields / state / identifiers: denylist terms, scanned paths, allowlisted migration/history/plans paths, workflow command target, test project list
  - Owned by this slice: audit rule and CI command
  - Consumed by this slice: deleted old paths from SL-001, migration note location from SL-003, opt-in test project from SL-006
  - Deferred / unresolved fields: final old-name PASS belongs to cross-slice verification
- Implementation-realization risks: Present. Audit design can either miss old public surface or fail legitimate migration documentation.
- Recommended process profile: standard-slice
- Immediate next agent: slice-prep
- Required inputs for next agent: SL-001 output, `.github/workflows/ci.yml`, README/migration note target decision, test project list.
- Stop condition for this slice: CI targets new solution and old-name / old-dependency audit is defined without treating legitimate migration/history references as implementation residue.

### SL-003: README / migration note / sample

- Goal: README と sample を `MultiCodingAgentFacade` の typed runtime API に合わせ、旧 MultiProvider からの migration note、Copilot / Codex quickstart、DI、appsettings、streaming / non-streaming、安全上の注意を整える。
- Non-goals: runtime API 実装、CI audit 実装、real runtime E2E 実行、NuGet publish 方針変更。
- Parent requirements covered: FR-003, FR-006, FR-011, FR-012.
- Parent acceptance conditions covered: AC-004, AC-007, AC-013, AC-014, AC-015 の docs / sample 部分。
- Affected components / modules: `README.md`, `src/MultiCodingAgentFacade.Samples/Program.cs`, sample project file, migration note path if separated.
- Expected implementation scope: old `IChatClient` / provider switching docs の削除、Copilot / Codex quickstarts、DI 登録例、appsettings 例、streaming / non-streaming 例、sandbox / approval / working directory / skills、AdvancedOptions 方針、provider override / permission handling の説明、migration note。
- Internal high-risk boundary candidates: docs が runtime slice の未確定 API を先取りして stale になる risk、sample が marker-only のまま残る risk。
- Cross-slice dependencies: SL-004 / SL-005 の public API と policy を消費し、SL-002 の scoped audit allowlist と調整する。
- Related Cross-slice Contract IDs: XC-PR22-002, XC-PR22-003, XC-PR22-004, XC-PR22-006.
- Cross-slice contract excerpt:
  - XC ID: XC-PR22-002
  - This slice role: Consumer/Producer
  - Mechanism: README / sample compile-time references
  - Required fields / state / identifiers: public client names, request/response/streaming type names, DI extension names, config sections, migration note allowed old terms
  - Owned by this slice: docs and sample projection
  - Consumed by this slice: runtime API and policy outputs from SL-004 / SL-005, solution graph from SL-001
  - Deferred / unresolved fields: public API naming correction if runtime implementation changes
- Implementation-realization risks: Present. README currently describes old `MeAiUtility.MultiProvider` and sample currently only prints runtime markers.
- Recommended process profile: standard-slice
- Immediate next agent: slice-prep
- Required inputs for next agent: SL-004 / SL-005 outputs or prepared public API contracts, SL-002 audit allowlist expectations, current README and sample.
- Stop condition for this slice: README and sample no longer instruct users to use `IChatClient`, old provider switching, OpenAI/Azure provider, or marker-only sample flow.

### SL-004: Copilot runtime contract remediation

- Goal: Copilot response / streaming update / model capability / provider override validation / permission handling / AdvancedOptions policy を review 要件に合わせる。
- Non-goals: Codex JSON-RPC result model、README 全面改訂、CI old-name audit、real Copilot authenticated E2E。
- Parent requirements covered: FR-007, FR-009, FR-010, FR-011, FR-012.
- Parent acceptance conditions covered: AC-008, AC-011, AC-012, AC-013, AC-014.
- Affected components / modules: `src/MultiCodingAgentFacade.GitHubCopilot/GitHubCopilotAgentResponse.cs`, `GitHubCopilotStreamingUpdate.cs`, `GitHubCopilotAgentRequest.cs`, `Abstractions/ICopilotSdkWrapper.cs`, `GitHubCopilotSdkWrapper.cs`, `Options/ProviderOverrideOptions.cs`, `Options/GitHubCopilotOptions.cs`, Copilot tests.
- Expected implementation scope: response metadata fields、streaming correlation fields、SDK metadata extension point、model capability shape、fail-fast provider override validation、permission handler option/logging、AdvancedOptions typed-first policy。
- Internal high-risk boundary candidates: external SDK model list / session config / event stream、permission callback、secret-safe diagnostics。
- Cross-slice dependencies: SL-003 docs/sample と SL-006 opt-in smoke がこの API / policy を消費する。
- Related Cross-slice Contract IDs: XC-PR22-002, XC-PR22-003, XC-PR22-005.
- Cross-slice contract excerpt:
  - XC ID: XC-PR22-003
  - This slice role: Producer
  - Mechanism: typed API -> `CopilotSessionConfig` -> SDK session / event stream
  - Required fields / state / identifiers: `TraceId`, `RequestId`, runtime name, elapsed time, finish status, diagnostics summary, SDK metadata, model id, supported/default reasoning efforts, provider override fields, permission approval policy
  - Owned by this slice: Copilot runtime result and SDK mapping
  - Consumed by this slice: Core exception / diagnostics semantics from existing foundation
  - Deferred / unresolved fields: real SDK behavior remains opt-in/manual for SL-006
- Implementation-realization risks: Present. Current response model is thin, `CopilotModelInfo` is bool-only, and permission handling is fixed `ApproveAll`.
- Recommended process profile: standard-slice
- Immediate next agent: slice-prep
- Required inputs for next agent: parent Plan, triage, this decomposition, current Copilot source/tests, review comment.
- Stop condition for this slice: Copilot public API can carry required metadata and fail-fast / permission policy is represented in production code and slice-local tests.

### SL-005: Codex runtime contract remediation

- Goal: Codex turn response / streaming update / `CodexRpcSession.ExecuteTurnAsync` result model を修正し、thread id / turn id / status / request / trace / diagnostics / error summary を response まで保持する。
- Non-goals: Copilot SDK handling、README 全面改訂、CI old-name audit、real Codex app-server E2E。
- Parent requirements covered: FR-008.
- Parent acceptance conditions covered: AC-009, AC-010.
- Affected components / modules: `src/MultiCodingAgentFacade.CodexAppServer/CodexAppServerTurnResponse.cs`, `CodexAppServerStreamingUpdate.cs`, `CodexAppServerAgentClient.cs`, `CodexRpcSession.cs`, `Threading/*`, Codex tests.
- Expected implementation scope: `CodexRpcSession` turn result model、thread/start result propagation、turn/completed / error / status notification metadata capture、streaming update metadata、client response mapping、fake transport tests。
- Internal high-risk boundary candidates: stdio JSON-RPC notifications と typed response model の field continuity、thread store update timing、error / retry / cancellation semantics。
- Cross-slice dependencies: SL-003 docs/sample と SL-006 opt-in smoke がこの response model を消費する。
- Related Cross-slice Contract IDs: XC-PR22-002, XC-PR22-004, XC-PR22-005.
- Cross-slice contract excerpt:
  - XC ID: XC-PR22-004
  - This slice role: Producer
  - Mechanism: app-server JSON-RPC -> `CodexRpcSession` turn result -> typed response / streaming update
  - Required fields / state / identifiers: thread id, turn id, status, trace id, request id, diagnostics summary, error summary, final text, text delta
  - Owned by this slice: Codex result model and JSON-RPC field propagation
  - Consumed by this slice: existing thread store and transport abstractions
  - Deferred / unresolved fields: app-server protocol drift remains opt-in/manual for SL-006
- Implementation-realization risks: Present. Current `CodexAppServerTurnResponse` has only `Text` and `ExecuteTurnAsync` returns `string`.
- Recommended process profile: standard-slice
- Immediate next agent: slice-prep
- Required inputs for next agent: parent Plan, triage, this decomposition, current Codex source/tests, review comment.
- Stop condition for this slice: Codex production path returns a typed turn result and thread / turn / status metadata is not discarded before public response / streaming update.

### SL-006: opt-in integration と production-binding smoke

- Goal: placeholder integration test を廃止し、Copilot SDK / Codex App Server の opt-in smoke test と skip reason を追加し、production runtime path に対する binding gap を検出する。
- Non-goals: always-on real runtime CI、runtime API 実装、README 全面改訂。
- Parent requirements covered: FR-005.
- Parent acceptance conditions covered: AC-006, AC-015 の integration / production binding 部分。
- Affected components / modules: `tests/MultiCodingAgentFacade.IntegrationTests/OptInIntegrationPlaceholderTests.cs`, integration test project, production DI / client construction path, README integration instructions consumed from SL-003.
- Expected implementation scope: placeholder removal、environment/config gated skip、Copilot SDK opt-in smoke、Codex App Server opt-in smoke、skip reason observability、secret-safe output、production client construction path binding。
- Internal high-risk boundary candidates: fake-only success、real runtime missing environment、secret leakage、test framework skip semantics。
- Cross-slice dependencies: consumes SL-004 Copilot API and SL-005 Codex API; SL-002 CI must include the project without requiring credentials.
- Related Cross-slice Contract IDs: XC-PR22-003, XC-PR22-004, XC-PR22-005.
- Cross-slice contract excerpt:
  - XC ID: XC-PR22-005
  - This slice role: Consumer/Producer
  - Mechanism: opt-in xUnit tests -> production DI/client path -> optional real runtime
  - Required fields / state / identifiers: opt-in environment variable names, skip reason, production client type, config section, secret masking, smoke prompt / timeout
  - Owned by this slice: integration test gating and production-binding smoke
  - Consumed by this slice: public runtime APIs from SL-004 / SL-005
  - Deferred / unresolved fields: real runtime execution evidence is ManualOnly unless environment is available
- Implementation-realization risks: Present. Current integration test is placeholder `Assert.True(true)`.
- Recommended process profile: standard-slice
- Immediate next agent: slice-prep
- Required inputs for next agent: SL-004 and SL-005 outputs, current integration tests, test framework skip pattern, README integration instructions.
- Stop condition for this slice: integration project has no placeholder pass, skips cleanly without runtime credentials, and opt-in smoke tests use production client paths when enabled.

## Cross-slice contracts

| Cross-slice Contract ID | Producer slice | Consumer slice | Runtime participants | Mechanism | Required fields / state / identifiers | Error / retry / recovery expectation | Verification requirement | Status |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| XC-PR22-001 | SL-001 | SL-002, SL-003 | solution graph, CI workflow, docs | project references + workflow commands | `MultiCodingAgentFacade.sln`, `.slnx`, remaining project paths, deleted old project paths | stale old solution reference must fail audit or review | CI command and repo inventory verify no old solution/project source remains | Deferred |
| XC-PR22-002 | SL-004, SL-005 | SL-003 | runtime public APIs, README, sample | typed API references + compile-time sample | client/request/response/update type names, DI method names, config section names, non-streaming/streaming examples | docs must not invent fields or use marker-only behavior | sample compiles and docs match public runtime API | Deferred |
| XC-PR22-003 | SL-004 | SL-003, SL-006 | Copilot client, SDK wrapper, docs, tests | typed request/response + SDK session config | `TraceId`, `RequestId`, runtime name, elapsed time, finish status, diagnostics summary, SDK metadata, model capability values, provider override validation, permission policy | invalid provider override fails before SDK; unsafe permission behavior is visible/configured | Copilot tests, docs, and opt-in smoke reference same policy and fields | Deferred |
| XC-PR22-004 | SL-005 | SL-003, SL-006 | Codex client, `CodexRpcSession`, app-server, docs, tests | JSON-RPC -> result model -> response/update | thread id, turn id, status, trace id, request id, diagnostics summary, error summary, final text, deltas | failed/interrupted/error statuses propagate without losing correlation | fake transport tests, docs, and opt-in smoke reference same response fields | Deferred |
| XC-PR22-005 | SL-004, SL-005 | SL-006, final verification | production clients, fakes, integration tests | production DI/client construction + opt-in execution | production client types, config sections, opt-in env vars, skip reasons, secret masking | missing runtime credentials skip with reason; enabled smoke fails visibly | integration tests prove no placeholder success and no fake-only production binding | Deferred |
| XC-PR22-006 | SL-001, SL-003 | SL-002, final verification | source tree, docs, audit step | scoped repository scan | denylist terms, allowlisted docs/history/plans paths, migration note path, scanned source/workflow paths | audit must not pass stale public old API; must not fail legitimate migration explanation | scoped audit output explains allowed old-name occurrences | Deferred |

## Cross-slice field continuity

| Field / state / identifier | Required by | Source artifact / owner | Producer XC | Intermediate storage / artifact | Consumer XC | Fabrication allowed? | Status | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `MultiCodingAgentFacade.sln` / `.slnx` | SL-002, SL-003 | existing new solution files / SL-001 | XC-PR22-001 | workflow commands, README | XC-PR22-006 | No | Deferred | SL-001 must make these authoritative |
| deleted old project path list | SL-002, final verification | current repo inventory / SL-001 | XC-PR22-001 | audit allowlist / deletion report | XC-PR22-006 | No | Deferred | must not be regenerated from docs prose |
| public runtime type names | SL-003, SL-006 | SL-004 / SL-005 source | XC-PR22-002 | README, sample, integration tests | XC-PR22-005 | No | Deferred | docs consume implementation, not the reverse |
| Copilot `TraceId` / `RequestId` | SL-003, SL-006 | `GitHubCopilotAgentClient` / SL-004 | XC-PR22-003 | response/update models | XC-PR22-005 | No | Deferred | must be produced by runtime request path |
| Copilot elapsed / finish / diagnostics / SDK metadata | SL-003, SL-006 | SDK wrapper / SL-004 | XC-PR22-003 | response/update models | XC-PR22-005 | No | Deferred | fallback empty metadata must not be called Done without policy |
| Copilot supported/default reasoning efforts | SL-003, SL-006 | SDK model list / SL-004 | XC-PR22-003 | `CopilotModelInfo` | XC-PR22-005 | No | Deferred | bool-only model is insufficient |
| Copilot permission policy | SL-003, SL-006 | SDK wrapper options / SL-004 | XC-PR22-003 | options, logs, README | XC-PR22-005 | No | Deferred | fixed `ApproveAll` without option/doc is not acceptable |
| Provider override validation outcome | SL-004, SL-006 | `ProviderOverrideOptions` / SL-004 | XC-PR22-003 | client validation / tests | XC-PR22-005 | No | Deferred | invalid combinations must fail-fast |
| Codex thread id / turn id / status | SL-003, SL-006 | app-server JSON-RPC / SL-005 | XC-PR22-004 | turn result / response/update | XC-PR22-005 | No | Deferred | current string result loses this |
| Codex request id / trace id | SL-003, SL-006 | client request path / SL-005 | XC-PR22-004 | response/update models | XC-PR22-005 | No | Deferred | trace/request correlation must survive |
| Codex diagnostics / error summary | SL-003, SL-006 | transport/session / SL-005 | XC-PR22-004 | response/update models | XC-PR22-005 | No | Deferred | failure path must not discard error summary |
| opt-in skip reason | SL-002, SL-003 | SL-006 integration tests | XC-PR22-005 | test output / README | XC-PR22-006 | No | Deferred | skip reason must be observable without credentials |
| old-name allowlist | SL-002, final verification | SL-003 migration note + plans | XC-PR22-006 | audit config/script | XC-PR22-006 | No | Deferred | source/workflow old names and migration prose differ |

## Parent contract mapping

| Parent Contract ID | Disposition | Slice ID | Cross-slice Contract ID | Notes |
| --- | --- | --- | --- | --- |
| RC-PR22-001 | CrossSlice | SL-001, SL-002 | XC-PR22-001, XC-PR22-006 | old graph deletion plus CI/audit verification |
| RC-PR22-002 | CrossSlice | SL-003, SL-004, SL-005 | XC-PR22-002 | docs/sample consume runtime public API |
| RC-PR22-003 | CrossSlice | SL-004, SL-003, SL-006 | XC-PR22-003, XC-PR22-005 | Copilot runtime metadata and production smoke |
| RC-PR22-004 | CrossSlice | SL-005, SL-003, SL-006 | XC-PR22-004, XC-PR22-005 | Codex result model and production smoke |
| RC-PR22-005 | InternalToSlice | SL-004 | XC-PR22-003 | provider override validation is Copilot runtime sub-scope |
| RC-PR22-006 | CrossSlice | SL-004, SL-003 | XC-PR22-003 | permission handling needs runtime option/log and README explanation |
| RC-PR22-007 | CrossSlice | SL-006, SL-002, SL-003 | XC-PR22-005 | opt-in smoke must be included in CI without credentials and documented |

## Execution order

1. SL-001 を最初に実施する。旧構成削除と new solution graph が安定しないと、CI / docs / tests の正しい参照先が確定しない。
2. SL-004 と SL-005 は SL-001 後に並行準備可能。ただし implementation で shared Core / diagnostics model を同時変更する場合は parent review gate で直列化する。
3. SL-002 は SL-001 後に準備可能。最終 audit allowlist は SL-003 の migration note と調整する。
4. SL-003 は SL-004 / SL-005 の public API contract が READY になるまで実装確定しない。先に下書きしても、final docs/sample は runtime slice の output を消費する。
5. SL-006 は SL-004 / SL-005 の response/client shape が確定してから実装する。
6. 全 slice の verification 後、`cross-slice-verification-kernel` と `residual-decision-gate` を実行する。XC-PR22-* を slice 内で Done にしない。

## Final cross-slice verification requirements

- AC-001..AC-015 を parent Plan から再確認する。
- XC-PR22-001: old solution / old project source が削除され、CI と docs が new solution graph を参照する。
- XC-PR22-002: README / sample が runtime public API と一致し、marker-only sample ではない。
- XC-PR22-003: Copilot response / streaming / model capability / provider override / permission policy が source、tests、docs、opt-in smoke で一貫する。
- XC-PR22-004: Codex thread / turn / status / correlation / diagnostics / error summary が JSON-RPC から public response / update まで traceable。
- XC-PR22-005: integration tests に placeholder pass がなく、opt-in disabled 時は skip reason が観測でき、enabled 時は production client path を使う。
- XC-PR22-006: old-name audit は stale public source / workflow を検出しつつ、migration note / planning artifact の合法的な旧名説明を区別する。
- ManualOnly: real Copilot / real Codex 実行は環境がある場合のみ実施。ない場合は skip reason と ManualOnly residual でよい。
- PASS を妨げる unresolved item: old solution/project remaining, README old API guidance, marker-only sample, placeholder integration, thin response model, no provider override validation, silent fixed Copilot auto approve.

## Human decisions required

現時点で decomposition を止める human decision はない。

Deferred / ManualOnly:

- real Copilot / real Codex smoke の実実行は環境依存。
- repository rename は今回の PR remediation では OutOfScopeForThisPass。
- NuGet publish / package archive redesign は OutOfScopeForThisPass。
- old-name audit の allowlist exact path は SL-002 / SL-003 の成果物で確定する。

## 今回の decomposition の対象外

- production code / tests / README / workflow の修正。
- slice-local runtime contract kernel / test design の詳細化。
- full runtime evidence。
- actual CI / real runtime smoke execution。
- GitHub repository rename。
- NuGet publish policy。

## Handoff Packet

- Profile used: plan-slice-decomposition
- Parent Plan artifact: `plans/pr22-review-remediation-plan.md`
- Change Risk Triage artifact: `plans/pr22-review-remediation-change-risk-triage.md`
- Slice Decomposition artifact: `plans/pr22-review-remediation-slice-decomposition.md`
- Slice artifacts:
  - `plans/pr22-review-remediation-slice-SL-001.md`
  - `plans/pr22-review-remediation-slice-SL-002.md`
  - `plans/pr22-review-remediation-slice-SL-003.md`
  - `plans/pr22-review-remediation-slice-SL-004.md`
  - `plans/pr22-review-remediation-slice-SL-005.md`
  - `plans/pr22-review-remediation-slice-SL-006.md`
- Slice IDs: SL-001, SL-002, SL-003, SL-004, SL-005, SL-006
- Cross-slice Contract IDs: XC-PR22-001, XC-PR22-002, XC-PR22-003, XC-PR22-004, XC-PR22-005, XC-PR22-006
- Cross-slice field continuity items: solution graph identifiers, deleted old project path list, public runtime type names, Copilot correlation and diagnostics metadata, Copilot model capability and permission policy, provider override validation outcome, Codex thread/turn/status/correlation metadata, opt-in skip reason, old-name allowlist
- Source artifacts:
  - PR #22 review comment `https://github.com/suusanex/meai_utility_impl/pull/22#issuecomment-4641587916`
  - `plans/pr22-review-remediation-plan.md`
  - `plans/pr22-review-remediation-change-risk-triage.md`
  - `plans/multi-coding-agent-facade-restructure-plan.md`
  - `plans/multi-coding-agent-facade-restructure-change-risk-triage.md`
  - `plans/multi-coding-agent-facade-restructure-slice-decomposition.md`
  - `AGENTS.md`
- Files inspected:
  - `.github/agents/plan-slice-decomposition.agent.md`
  - `plans/pr22-review-remediation-plan.md`
  - `plans/pr22-review-remediation-change-risk-triage.md`
  - `plans/multi-coding-agent-facade-restructure-slice-decomposition.md`
  - existing slice artifacts `plans/multi-coding-agent-facade-restructure-slice-SL-001.md` through `SL-005.md`
  - current-state files recorded in the parent Plan and triage
- Files intentionally not inspected:
  - full old provider implementation bodies, because deletion scope was already established by parent Plan / triage
  - all unit test bodies, because test point mapping belongs to slice-local `test-design-kernel`
  - generated outputs under `bin/` / `obj/`
- Decisions made:
  - Use six executable remediation slices.
  - Do not create a separate implementation slice for final cross-slice verification.
  - Keep real runtime execution ManualOnly / opt-in.
  - Treat old-name audit as scoped, not blanket grep.
- Do not redo unless new evidence appears:
  - Slice boundaries, parent RC to slice / XC mapping, and execution order.
- Remaining work:
  - Run slice-prep for SL-001 first.
  - Create an agent usage ledger and parent execution table if moving into the 3-layer orchestration.
  - Do not implement directly from this decomposition.
- Recommended next step: use `token-aware-full-coverage-3layer` parent orchestration in `PREP_ONLY` or `DELEGATED_IMPLEMENTATION` mode. Start with slice execution table and SL-001 slice-prep.
- Required downstream guardrails:
  - 各 slice は parent Plan と slice decomposition の両方を source artifact として読むこと。
  - executable slice については `plans/pr22-review-remediation-slice-SL-xxx.md` を bounded Plan として読むこと。
  - 各 slice は自分の slice scope と non-goals を守ること。
  - 親の `RC-PR22-xxx` candidate と slice / `XC-PR22-xxx` の対応は `Parent contract mapping` を source として扱うこと。
  - slice 内の selected runtime contract について、runtime contract identification / participant mapping / test point mapping / stub usage identification / production implementation binding / production wiring verification / explicit unresolved status を保持すること。
  - cross-slice contract は slice 内で勝手に完了扱いにせず、最後に `cross-slice-verification-kernel.agent.md` で確認すること。
  - cross-slice field continuity は slice 内で勝手に補完・推測・空文字化して完了扱いにせず、source artifact または producer contract から traceable でない field は `Deferred` / `NeedsHumanDecision` として保持すること。
  - production binding が slice 間にまたがる場合は `Bound` として扱わず、cross-slice verification まで `Deferred` または `PartiallyDone` とすること。
