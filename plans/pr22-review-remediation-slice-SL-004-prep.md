# Slice Preparation Result: SL-004

## Verdict

- Status: READY_FOR_PARENT_REVIEW
- Reason: SL-004 の Goal / Non-goals / Parent requirements / Parent acceptance conditions は source artifact 間で整合している。implementation-realization risk は Present で、Copilot public API、SDK wrapper、provider override validation、permission handling、model capability、AdvancedOptions policy に non-trivial な実装判断があるため、implementation-contract-kernel と implementation-contract-review-kernel を下書きした。実装・テスト・README・workflow・source code 編集は行っていない。

## Agent metadata

- Agent type: slice-prep
- Model: gpt-5.4
- Reasoning effort: medium
- Parent authorization artifact: ユーザー指示「PR22 remediation の slice-prep。対象は SL-004 のみ。実装は行わない」、`plans/pr22-review-remediation-slice-SL-004.md`
- Delegation evidence: `plans/pr22-review-remediation-plan.md`、`plans/pr22-review-remediation-change-risk-triage.md`、`plans/pr22-review-remediation-slice-decomposition.md`、`plans/pr22-review-remediation-slice-SL-004.md`、`AGENTS.md` を source artifact として読み、write scope をこの prep artifact のみに限定した。

## Generated / drafted artifacts

- Per-slice change-risk-triage: 下記「Per-slice change-risk-triage draft」参照。
- Implementation-contract-kernel: 下記「Implementation-contract-kernel draft」参照。
- Implementation-contract-review-kernel: 下記「Implementation-contract-review-kernel draft / review requirement」参照。
- Runtime-contract-kernel: 下記「Runtime-contract-kernel draft」参照。
- Test-design-kernel: 下記「Test-design-kernel draft」参照。

### Per-slice change-risk-triage draft

#### Recommended profile

`standard-slice`

#### Reasoning

SL-004 は Copilot runtime contract に限定された slice だが、次の境界が同時に動く。

- public response / streaming update model
- `GitHubCopilotAgentClient` から `ICopilotSdkWrapper` / `GitHubCopilotSdkWrapper` への request / response mapping
- SDK model list から `CopilotModelInfo` への capability propagation
- `ProviderOverrideOptions` から SDK `ProviderConfig` への fail-fast validation と secret-safe diagnostics
- SDK permission callback の default / option / logging policy
- `AdvancedOptions` の typed-first policy と escape hatch boundary

親 triage の `full-coverage` は維持するが、この slice 単体では Copilot runtime に bounded できるため `standard-slice` が妥当。ただし implementation-realization risk は Present。

#### Risk trigger scan

| Risk trigger | Present / Absent / Unclear | Notes |
| --- | --- | --- |
| External API or SDK | Present | `GitHubCopilotSdkWrapper` が GitHub Copilot SDK の model list、session config、session event stream、permission callback を扱う。 |
| Authentication or authorization | Present | `ProviderOverrideOptions` の `ApiKey` / `BearerToken` と SDK permission callback が関係する。secret value はログ・diagnostics に出さない。 |
| Startup wiring / DI / configuration | Present | `GitHubCopilotOptions` と `AddGitHubCopilotAgentRuntime` 経由の production wrapper registration が消費者になる。 |
| Production implementation split from test substitute | Present | tests は `ScriptedCopilotSdkWrapper` fake を使う。production wrapper 側 validation / SDK mapping のテスト不足が false PASS になり得る。 |
| Observable behavior spanning more than one component | Present | request telemetry、response/update model、SDK wrapper logs、docs/sample、SL-006 opt-in smoke まで跨る。 |
| Durable state / retry / replay / idempotency | Absent | SL-004 では durable state を所有しない。 |
| Real OS mutation | Absent | UnitTest / CI IntegrationTest は実 OS 変更を要求しない方針を維持する。 |

#### Selected runtime contracts

| Contract ID | Boundary | Triage status | Next action |
| --- | --- | --- | --- |
| RC-SL004-001 | Copilot request telemetry -> response / streaming update metadata | Selected | runtime-contract-kernel と tests で `TraceId` / `RequestId` / runtime name / elapsed / finish / diagnostics / SDK metadata の continuity を確認する。 |
| RC-SL004-002 | SDK model list -> `CopilotModelInfo` | Selected | bool-only の `SupportsReasoningEffort` ではなく supported/default reasoning effort values を保持できる shape を実装設計に入れる。 |
| RC-SL004-003 | `ProviderOverrideOptions` -> SDK `ProviderConfig` | Selected | invalid `Type` / provider type 別 `BaseUrl` / auth / Azure API version を SDK call 前に `RuntimeInvalidRequestException` で fail-fast する。 |
| RC-SL004-004 | SDK permission request -> option / default / logging policy | Selected | `ApproveAll` 固定を避け、明示 option と安全側 default / logging を traceable にする。 |
| RC-SL004-005 | typed properties -> `AdvancedOptions` escape hatch | Selected | typed property を主 API とし、`AdvancedOptions` は supported key allowlist と unstable / escape hatch policy を明確にする。 |

#### Implementation-realization risk

Present.

現行証拠:

- `GitHubCopilotAgentResponse` は `Text`, `ModelId`, `TraceId`, `RequestId` のみ。
- `GitHubCopilotStreamingUpdate` は `Kind`, `TextDelta`, `FinalText`, `DeltaCount`, `AccumulatedLength` のみで correlation を持たない。
- `ICopilotSdkWrapper` の `SendAsync` は `Task<string>`、streaming update も SDK metadata / finish status / diagnostics を持たない。
- `CopilotModelInfo` は `ModelId` と `SupportsReasoningEffort` の bool-only。
- `GitHubCopilotSdkWrapper.BuildSdkSessionConfig` は `BaseUrl = invocation.ProviderOverride.BaseUrl ?? string.Empty` としており、provider override validation が SDK config 生成前に十分固定されていない。
- `OnPermissionRequest = CopilotSdk.PermissionHandler.ApproveAll` が固定。
- `GitHubCopilotAgentRequest.AdvancedOptions` はあるが、typed-first policy は public docs / API comments 側へまだ接続されていない。

#### Non-trivial decisions requiring review

- Response / wrapper return type を `string` から typed result へ変えるか、wrapper は最小変更で client 側が metadata を合成するか。
- `CopilotStreamingUpdate` と public `GitHubCopilotStreamingUpdate` を同一 shape に寄せるか、internal/public 分離を維持するか。
- `FinishStatus` / diagnostics / SDK metadata の型名と nullability。
- supported/default reasoning effort values の表現を `ReasoningEffortLevel` に寄せるか、SDK raw string も保持するか。
- provider override `Type` の allowed values と provider type 別 required field の source of truth。
- permission handling default を deny / prompt / explicit approve のどれにするか。実 SDK callback の型制約があるため、implementation 時に SDK API shape の確認が必要。
- `AdvancedOptions` を残す場合の allowlist、例外型、docs/API comments の扱い。

### Implementation-contract-kernel draft

#### Contract goal

Copilot runtime slice は、PR #22 remediation の AC-008 / AC-011 / AC-012 / AC-013 / AC-014 を満たすため、Copilot public API から SDK boundary まで required metadata と policy を落とさない形にする。

#### Participants

| Participant | Role | Source evidence |
| --- | --- | --- |
| `GitHubCopilotAgentRequest` | caller input / typed request / escape hatch | `Prompt`, `ModelId`, `ReasoningEffort`, provider override, working directory, tools, skills, `AdvancedOptions` を持つ。 |
| `GitHubCopilotAgentClient` | telemetry generation / validation / public response mapping | `AgentTelemetry.Start`, `ValidateModelAsync`, `BuildSessionConfig`, `SendTurnAsync`, `StreamTurnAsync` を持つ。 |
| `ICopilotSdkWrapper` | client と SDK wrapper の abstraction boundary | `ListModelsAsync`, `SendAsync`, `SendStreamingAsync`, `CopilotSessionConfig`, `CopilotModelInfo`, `CopilotStreamingUpdate` を定義。 |
| `GitHubCopilotSdkWrapper` | production SDK binding | SDK model list、session config、provider mapping、permission callback、session event parsing、logging を扱う。 |
| `GitHubCopilotAgentResponse` / `GitHubCopilotStreamingUpdate` | public output model | 現在は text/correlation または streaming text/count の薄い shape。 |
| `GitHubCopilotOptions` / `ProviderOverrideOptions` | configured defaults and provider override | provider override と runtime options の source。 |
| `ScriptedCopilotSdkWrapper` | unit test fake | client mapping tests の fake boundary。production SDK mapping の代替完了扱いにしない。 |

#### Required contract outputs

| Output | Required behavior | Notes |
| --- | --- | --- |
| Response metadata | non-streaming response が `TraceId`, `RequestId`, runtime name, elapsed time, finish/completion status, diagnostics summary, SDK metadata extension point を保持できる。 | Runtime name は `GitHubCopilotRuntimeMarker.RuntimeName` を source にする。 |
| Streaming metadata | delta/progress/completed update が request / trace correlation と runtime name を保持でき、completed update が final text と finish/diagnostics を運べる。 | 全 update に同一 `TraceId` / `RequestId` が付くことを test point 化する。 |
| Model capability | `CopilotModelInfo` が model id、supported reasoning effort values、default reasoning effort、reasoning effort support を表現できる。 | SDK が default を返さない場合に fabricated default を入れない。source がない場合は `null` / empty として扱う。 |
| Provider override validation | invalid provider override は SDK session creation 前に `RuntimeInvalidRequestException` で fail-fast する。 | `ApiKey` / `BearerToken` の値は diagnostics/log に出さず、presence only にする。 |
| Permission policy | permission request は固定 silent `ApproveAll` に見えない。default、explicit option、logging が一致する production shape を持つ。 | README は SL-003 が消費するため、この slice は code/API/policy shape を producer として出す。 |
| AdvancedOptions policy | typed properties が主 API。`AdvancedOptions` は supported key allowlist / unstable escape hatch として invalid key を fail-fast する。 | docs/API comments は SL-003 と cross-slice verification へ接続する。 |

#### Required failure semantics

- request validation / provider override validation / unsupported reasoning effort は SDK call 前に fail-fast する。
- `RuntimeInvalidRequestException` / `RuntimeFeatureNotSupportedException` / `RuntimeOperationException` の既存 runtime exception semantics を崩さない。
- AGENTS.md に従い、例外を catch して捨てる場合は `Exception.ToString()` 相当の trace logging を残す。既存 `LogExceptionWithTrace` / explicit `Exception={Exception}` pattern を優先する。
- fallback による silent degradation は行わない。SDK が required metadata を返さない場合は、source がある metadata と source がない metadata を区別する。

#### Implementation constraints

- production code 編集はこの prep では行わない。後続 slice-impl では SL-004 artifact と親 review gate の承認範囲に限定する。
- Codex JSON-RPC result model、README 全面改訂、CI old-name audit、real Copilot authenticated E2E は扱わない。
- source evidence のない field / state / identifier を fabricated value で埋めない。特に SDK metadata / default reasoning effort / finish status は SDK または wrapper result の source を明示する。
- reflection は使用しない。もし SDK API 制約で必要と判断する場合は、AGENTS.md に従い、コードコメントとチャット報告で理由を明示する。
- UnitTest は実 Copilot / 実 OS 変更を要求しない。production binding は fake-only で Done にせず、SL-006 と final cross-slice verification へ渡す。

### Implementation-contract-review-kernel draft / review requirement

#### Review requirement

SL-004 の implementation contract は non-trivial なので、slice-impl 開始前または implementation-handoff-review で次を確認すること。

| Review item | Required reviewer check | Blocking if failed |
| --- | --- | --- |
| Public output shape | `GitHubCopilotAgentResponse` / `GitHubCopilotStreamingUpdate` に必要 metadata が入り、nullability と source が説明できる。 | Yes |
| Wrapper boundary | `ICopilotSdkWrapper.SendAsync` / `SendStreamingAsync` の shape 変更が fake と production wrapper の両方へ反映される、または client 側合成に留める理由が明示される。 | Yes |
| Model capability | `CopilotModelInfo` が supported/default values を失わず、bool-only 互換に戻っていない。 | Yes |
| Provider validation | invalid override が SDK call 前に `RuntimeInvalidRequestException` で落ち、secret-safe logging を守る。 | Yes |
| Permission policy | `ApproveAll` 固定から脱し、default / explicit option / log message が一致する。 | Yes |
| AdvancedOptions policy | typed property 優先、allowlist、invalid key failure、docs handoff が一貫する。 | Yes |
| Cross-slice contract | SL-003 / SL-006 に渡す public API / policy が traceable。XC-PR22-003 / XC-PR22-005 を slice 内で Done にしない。 | Yes |

#### Open review questions for parent

- Permission policy の default 値は、人間の明示選択が必要か、既定 deny にするか、SDK callback 制約上の最小安全策にするか。親 review gate で実装許可前に方針を固定した方がよい。
- Provider override `Type` の allowed values は PR review comment / SDK docs / current SDK API のどれを source of truth にするか。source evidence がなければ implementation-handoff-review で SDK API を確認する。
- SDK metadata extension point は raw dictionary と typed summary のどちらを public API とするか。将来 drift を考えると typed summary + extension dictionary が候補だが、親承認までは draft 扱い。

### Runtime-contract-kernel draft

#### RC-SL004-001: Copilot response / streaming metadata continuity

| Item | Contract |
| --- | --- |
| Runtime participants | `GitHubCopilotAgentClient`, `AgentTelemetry`, `CopilotSessionConfig`, `ICopilotSdkWrapper`, `GitHubCopilotAgentResponse`, `GitHubCopilotStreamingUpdate` |
| Boundary | request telemetry and SDK result -> public non-streaming / streaming outputs |
| Required fields | `TraceId`, `RequestId`, runtime name, model id, elapsed time, finish/completion status, diagnostics summary, SDK metadata extension point, final text / text delta |
| Producer source | `AgentTelemetry.Start` for trace/request, SDK wrapper result/event stream for finish/diagnostics/metadata where available |
| Consumer | SL-003 README/sample, SL-006 opt-in smoke, final cross-slice verification |
| Failure expectation | missing required source metadata must not be silently fabricated; unavailable optional SDK metadata remains explicit null/empty with policy |
| Verification requirement | unit tests prove non-streaming and streaming outputs preserve correlation and final/completed metadata |
| Status | Draft / Deferred to implementation |

#### RC-SL004-002: Copilot model capability propagation

| Item | Contract |
| --- | --- |
| Runtime participants | `GitHubCopilotSdkWrapper.ListModelsAsync`, SDK model object, `CopilotModelInfo`, `GitHubCopilotAgentClient.ValidateModelAsync` |
| Boundary | SDK model list -> public model catalog / reasoning effort validation |
| Required fields | `ModelId`, supported reasoning effort values, default reasoning effort, reasoning effort support |
| Producer source | SDK `ListModelsAsync` result, specifically existing evidence `model.Id` and `model.SupportedReasoningEfforts` |
| Consumer | caller model selection, SL-003 docs/sample, SL-006 smoke |
| Failure expectation | unsupported requested reasoning effort fails with `RuntimeFeatureNotSupportedException`; unknown model fails with `RuntimeInvalidRequestException` |
| Verification requirement | tests cover supported value list, default null/known behavior, unsupported selected effort, unknown model |
| Status | Draft / Deferred to implementation |

#### RC-SL004-003: Provider override fail-fast validation

| Item | Contract |
| --- | --- |
| Runtime participants | `ProviderOverrideOptions`, `GitHubCopilotOptions`, `GitHubCopilotAgentRequest`, `GitHubCopilotAgentClient`, `GitHubCopilotSdkWrapper.BuildInvocation`, SDK `ProviderConfig` |
| Boundary | request/config provider override -> SDK session config |
| Required fields | `Type`, provider type specific `BaseUrl`, `ApiKey`, `BearerToken`, `AzureApiVersion` |
| Producer source | request override or configured options; no fabricated default for missing `BaseUrl` |
| Consumer | SDK session creation |
| Failure expectation | invalid combinations fail before SDK session creation with `RuntimeInvalidRequestException`; secrets are not logged, only presence flags are logged |
| Verification requirement | tests cover missing type, unsupported type, missing required base URL when applicable, missing auth, conflicting auth, Azure API version rules, secret-safe log/diagnostics |
| Status | Draft / Deferred to implementation |

#### RC-SL004-004: Permission handling policy

| Item | Contract |
| --- | --- |
| Runtime participants | `GitHubCopilotOptions`, `CopilotSessionConfig`, `GitHubCopilotSdkWrapper.BuildSdkSessionConfig`, SDK `OnPermissionRequest`, logging |
| Boundary | SDK permission request -> user-visible policy |
| Required fields | approval mode/default, optional allow/auto-approve scope, log event / request id, policy handoff to README |
| Producer source | explicit options or safe default chosen by parent-approved implementation contract |
| Consumer | SDK session config, SL-003 README, SL-006 smoke/final verification |
| Failure expectation | fixed silent `ApproveAll` is not acceptable; policy must be observable in logs and configurable through typed option |
| Verification requirement | tests verify default and explicit approval mode mapping without invoking real SDK; trace logs do not leak secrets |
| Status | Draft / Needs parent review detail before implementation |

#### RC-SL004-005: AdvancedOptions typed-first / escape hatch

| Item | Contract |
| --- | --- |
| Runtime participants | `GitHubCopilotAgentRequest`, `GitHubCopilotAgentClient.CopyAdvancedOptions`, `GitHubCopilotSdkWrapper.EnsureSupportedAdvancedOptions`, `GitHubCopilotOptions` |
| Boundary | public typed request properties + advanced dictionary -> SDK invocation |
| Required fields | supported advanced option keys, typed property precedence, invalid key failure, unstable/escape hatch policy |
| Producer source | existing supported key allowlist in `GitHubCopilotSdkWrapper` plus typed request properties |
| Consumer | SDK invocation, SL-003 docs/API comments |
| Failure expectation | unsupported key fails fast; typed fields should not be silently overridden by accidental duplicate advanced value without defined precedence |
| Verification requirement | tests cover typed property mapping, advanced supported key mapping, unsupported key failure, precedence behavior |
| Status | Draft / Deferred to implementation and docs handoff |

### Test-design-kernel draft

#### Test strategy

SL-004 の test-design は UnitTest 中心とし、実 Copilot authentication / real SDK E2E は SL-006 の opt-in smoke に渡す。fake-only success を production binding 完了扱いにしないため、client-level fake tests と production wrapper static/internal mapping tests を分ける。

#### Test points

| Test Point ID | Target RC | Test type | Design |
| --- | --- | --- | --- |
| TP-SL004-001 | RC-SL004-001 | Unit | `SendTurnAsync` が `Text`, `ModelId`, `TraceId`, `RequestId`, runtime name, elapsed/finish/diagnostics/metadata を response に保持する。`TraceId` / `RequestId` は空でないことを確認する。 |
| TP-SL004-002 | RC-SL004-001 | Unit | `StreamTurnAsync` の delta/progress/completed update が同じ `TraceId` / `RequestId` / runtime name を保持し、completed が final text と completion metadata を保持する。 |
| TP-SL004-003 | RC-SL004-002 | Unit | `CopilotModelInfo` が supported reasoning effort values と default reasoning effort を表現し、`ValidateModelAsync` が unsupported selected effort を fail-fast する。 |
| TP-SL004-004 | RC-SL004-002 | Unit / production wrapper mapping | `GitHubCopilotSdkWrapper.ListModelsAsync` が SDK `SupportedReasoningEfforts` を bool に潰さず list として mapping する。default が SDK から得られない場合は fabricated default を入れない。 |
| TP-SL004-005 | RC-SL004-003 | Unit | request-level / options-level `ProviderOverrideOptions` の invalid combinations が SDK wrapper call 前に `RuntimeInvalidRequestException` になる。 |
| TP-SL004-006 | RC-SL004-003 | Unit / production wrapper mapping | valid provider override が SDK `ProviderConfig` に mapping され、`ApiKey` / `BearerToken` value は log/diagnostics に出ない。 |
| TP-SL004-007 | RC-SL004-004 | Unit / production wrapper mapping | permission approval option の default と explicit mode が SDK `OnPermissionRequest` mapping へ反映され、固定 `ApproveAll` のみになっていない。 |
| TP-SL004-008 | RC-SL004-004 | Unit | permission request / auto approval / denial 相当の log translation が request id と policy を含み、secret を含まない。 |
| TP-SL004-009 | RC-SL004-005 | Unit | supported `AdvancedOptions` key は受理され、unsupported key は fail-fast する。例外型は parent-approved contract に従う。 |
| TP-SL004-010 | RC-SL004-005 | Unit | typed request properties と `AdvancedOptions` の重複時の precedence が明示テストされる。 |
| TP-SL004-011 | XC-PR22-003 / XC-PR22-005 | Contract handoff check | SL-003 / SL-006 が消費する public API / policy fields を一覧化し、cross-slice verification まで Deferred として残す。 |

#### Checks to run by slice-impl

- `dotnet test MultiCodingAgentFacade.sln --filter FullyQualifiedName~MultiCodingAgentFacade.GitHubCopilot.Tests`
- 可能なら `dotnet test MultiCodingAgentFacade.sln` のうち CI 対象 unit tests。環境・lock で実行不能な場合は理由と代替 evidence を残す。

#### Tests not required in this slice

- real Copilot authenticated E2E。
- Codex App Server JSON-RPC tests。
- README sample compile verification。ただし SL-003 / SL-006 が消費する public API field list は handoff に残す。
- workflow audit test。

## Bounded parent Plan pass / Guardrail Focus

SL-004 の bounded parent Plan pass は、親 Plan の FR-007, FR-009, FR-010, FR-011, FR-012 と AC-008, AC-011, AC-012, AC-013, AC-014 に限定する。

Guardrail Focus:

- Copilot response / streaming update の metadata continuity。
- Copilot model capability の supported/default reasoning effort continuity。
- Provider override の SDK call 前 fail-fast validation。
- Permission handling の fixed silent `ApproveAll` からの脱却。
- AdvancedOptions の typed-first / escape hatch policy。
- SL-003 docs/sample と SL-006 opt-in smoke へ渡す cross-slice field continuity。

この prep は implementation-ready の材料を作るだけで、parent acceptance condition の完了判定は行わない。

## Non-goals

- Codex JSON-RPC result model。
- README 全面改訂。
- CI old-name audit。
- real Copilot authenticated E2E。
- production code / tests / README / workflow の編集。
- `XC-PR22-003` / `XC-PR22-005` の Done 判定。
- PR review comment 全体の completion 判定。

## RC / TP / XC ledger

| ID | Kind | Owned / Consumed / Deferred | Notes |
| --- | --- | --- | --- |
| FR-007 | Parent FR | Owned | Copilot response / streaming update に elapsed time、runtime name、diagnostics summary、completion / finish status、SDK metadata extension point、request id / trace id を保持できる shape を追加する範囲。 |
| FR-009 | Parent FR | Owned | `CopilotModelInfo` の supported/default reasoning effort values 対応。 |
| FR-010 | Parent FR | Owned | Provider override fail-fast validation。 |
| FR-011 | Parent FR | Owned / Cross-slice Deferred | runtime option/logging shape は SL-004 が owns、README 説明は SL-003 が consumes。 |
| FR-012 | Parent FR | Owned / Cross-slice Deferred | typed-first policy は SL-004 が producer、README/API comments の説明は SL-003 と parent review で確認。 |
| AC-008 | Parent AC | Owned / Deferred to implementation | Copilot non-streaming response と streaming update の metadata。 |
| AC-011 | Parent AC | Owned / Deferred to implementation | model list API capability shape。 |
| AC-012 | Parent AC | Owned / Deferred to implementation | invalid provider override fail-fast。 |
| AC-013 | Parent AC | Owned / Cross-slice Deferred | approval / permission policy の runtime 実装は SL-004、README 一致は SL-003 / final verification。 |
| AC-014 | Parent AC | Owned / Cross-slice Deferred | AdvancedOptions 方針の runtime/API shape は SL-004、docs は SL-003。 |
| RC-PR22-003 | Parent RC | Owned / Cross-slice Deferred | Copilot typed API -> SDK wrapper -> SDK events。XC-PR22-003 / XC-PR22-005 へ接続。 |
| RC-PR22-005 | Parent RC | Owned | Provider override options -> SDK provider config。 |
| RC-PR22-006 | Parent RC | Owned / Cross-slice Deferred | permission handling needs runtime option/log and README explanation。 |
| RC-SL004-001 | Slice RC | Owned | response / streaming metadata continuity。 |
| RC-SL004-002 | Slice RC | Owned | model capability propagation。 |
| RC-SL004-003 | Slice RC | Owned | provider override validation。 |
| RC-SL004-004 | Slice RC | Owned / Needs parent review detail | permission policy default / explicit option / logging。 |
| RC-SL004-005 | Slice RC | Owned / Cross-slice Deferred | AdvancedOptions typed-first / escape hatch policy。 |
| TP-SL004-001 | Test Point | Deferred | non-streaming response metadata。 |
| TP-SL004-002 | Test Point | Deferred | streaming update correlation and completed metadata。 |
| TP-SL004-003 | Test Point | Deferred | model capability validation。 |
| TP-SL004-004 | Test Point | Deferred | SDK model list mapping。 |
| TP-SL004-005 | Test Point | Deferred | provider override invalid fail-fast。 |
| TP-SL004-006 | Test Point | Deferred | provider override valid mapping and secret-safe diagnostics。 |
| TP-SL004-007 | Test Point | Deferred | permission option -> SDK mapping。 |
| TP-SL004-008 | Test Point | Deferred | permission log translation and secret safety。 |
| TP-SL004-009 | Test Point | Deferred | AdvancedOptions supported/unsupported key behavior。 |
| TP-SL004-010 | Test Point | Deferred | typed property / AdvancedOptions precedence。 |
| TP-SL004-011 | Test Point | Deferred | cross-slice public API / policy handoff。 |
| XC-PR22-002 | Cross-slice Contract | Consumed / Produced / Deferred | SL-004 produces Copilot public API fields consumed by SL-003 README/sample。最終一致は cross-slice verification。 |
| XC-PR22-003 | Cross-slice Contract | Owned as producer / Deferred | Copilot runtime result and SDK mapping。SL-003 / SL-006 が consumer。slice 内で Done にしない。 |
| XC-PR22-005 | Cross-slice Contract | Produced for consumer / Deferred | SL-006 production-binding smoke が consumes。real SDK behavior は ManualOnly / opt-in。 |

## Production binding requirements

- `GitHubCopilotAgentClient` の public non-streaming / streaming path が required metadata を保持すること。fake wrapper だけの response shape 変更で完了扱いにしない。
- `ICopilotSdkWrapper` と `GitHubCopilotSdkWrapper` の production boundary が同じ result/update contract を扱うこと。もし wrapper contract を変えない場合は、production wrapper から得られない metadata を public model に fabricated value として入れない。
- `GitHubCopilotSdkWrapper.ListModelsAsync` が SDK model capability を bool-only に潰さないこと。
- `ProviderOverrideOptions` validation は SDK session creation より前に実行されること。
- permission handling は production `BuildSdkSessionConfig` の `OnPermissionRequest` に反映されること。テスト fake のみの option 追加では不十分。
- DI registration は production `GitHubCopilotSdkWrapper` を使う既存 path と整合すること。ただし DI / README 完了確認は cross-slice verification に残す。

## Cross-slice risks to parent-review

- SL-003 は SL-004 の public API / policy を消費する。SL-004 の API naming / metadata nullability が親 review gate で曖昧なまま SL-003 を実装すると docs/sample が stale になる。
- SL-006 は SL-004 の production client path と permission/provider behavior を opt-in smoke で消費する。SL-004 が fake-only evidence で READY になると、SL-006 で production binding gap が露出する。
- Permission default は安全方針に関わるため、parent review gate で実装方針を明示するのが望ましい。
- Provider override allowed `Type` / Azure requirements は SDK API と PR review 要件の source確認が必要。source evidence がない値をこの prep では固定しない。
- `AdvancedOptions` の docs/API comments は SL-003 と連動するため、SL-004 は runtime/API shape を producer とし、docs completion を主張しない。

## Unresolved items

- Permission handling default の最終方針: Needs parent review detail。
- Provider override `Type` の allowed values と provider type 別 required fields の authoritative source: implementation-handoff-review で SDK API / review requirement を確認。
- SDK default reasoning effort の source: 現時点の source evidence では `SupportedReasoningEfforts` は確認できるが default field は未確認。fabricated default は禁止。
- SDK metadata / finish status の実 SDK source: 現時点の source evidence だけでは exact SDK result fields を固定しない。typed extension point と null policy を draft とする。
- SL-003 README/API comments と SL-006 opt-in smoke の final consistency: cross-slice verification へ Deferred。

## Stop condition

この slice-prep は `READY_FOR_PARENT_REVIEW` で停止する。production code、tests、README、workflow は編集していない。次は親 review gate が SL-004 の implementation authorization、permission policy、provider override allowed values、parallelization 可否を判定する。

## Handoff to Agent Usage Ledger

- Run ID: SL-004-prep-2026-06-07-local
- Phase: slice-prep
- Slice: SL-004
- Edit allowed: No
- Outcome: READY_FOR_PARENT_REVIEW
