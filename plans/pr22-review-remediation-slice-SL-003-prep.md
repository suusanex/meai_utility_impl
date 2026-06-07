# Slice Preparation Result: SL-003

## Verdict

- Status: READY_FOR_PARENT_REVIEW
- Reason: SL-003 の slice-prep として、per-slice risk、implementation contract draft、runtime contract draft、test design draft を作成した。実装前提となる SL-004 / SL-005 の public API / runtime policy output は未確定のため、SL-003 は親レビューで `Can implement now? = No` または runtime slice output 待ちとして扱う必要がある。

## Agent metadata

- Agent type: slice-prep
- Model: gpt-5.4
- Reasoning effort: medium
- Parent authorization artifact: ユーザー依頼「PR22 remediation の slice-prep。対象は SL-003 のみ。実装禁止」と `plans/pr22-review-remediation-slice-SL-003.md`
- Delegation evidence: 入力 source artifact として `plans/pr22-review-remediation-plan.md`、`plans/pr22-review-remediation-change-risk-triage.md`、`plans/pr22-review-remediation-slice-decomposition.md`、`plans/pr22-review-remediation-slice-SL-003.md`、`AGENTS.md` を読み、write scope を本 artifact のみに限定した。

## Generated / drafted artifacts

- Per-slice change-risk-triage:
  - Recommended profile: `standard-slice`
  - Implementation-realization risk: Present
  - Reasoning: SL-003 は README / migration note / sample を扱う docs/sample slice だが、実装対象は public API projection と compile-time sample であり、SL-004 / SL-005 の runtime output を先取りすると stale docs またはコンパイル不能 sample になる。現状 README は `MeAiUtility.MultiProvider`、`IChatClient`、OpenAI / Azure OpenAI provider switching、`AddMultiProviderChat` など旧 surface を案内しており、現状 sample は runtime marker を表示するだけで実利用 API を示していない。
  - Risk triggers:
    - Public API / docs mismatch: Present
    - Production sample compile path: Present
    - Cross-slice dependency on runtime API names: Present
    - CI audit allowlist collision with migration note: Present
    - Real runtime E2E dependency: Absent for this slice
    - Source evidence missing / fabricated type-name risk: Present
  - Selected slice-local contract focus:
    - RC-SL003-001: README / sample が runtime public API と一致し、旧 provider switching を案内しないこと。
    - RC-SL003-002: Copilot permission / provider override / AdvancedOptions policy を SL-004 output に基づき説明すること。
    - RC-SL003-003: Codex sandbox / approval / working directory / skills の説明を SL-005 output と現行 runtime conventions に基づき説明すること。
    - RC-SL003-004: migration note の旧名説明が SL-002 scoped audit の allowlist と衝突しないこと。
- Implementation-contract-kernel:
  - Draft status: Required / Drafted
  - Participants:
    - Producer: SL-004 Copilot runtime contract remediation、SL-005 Codex runtime contract remediation、SL-001 solution graph、SL-002 scoped audit
    - Consumer / owner in this slice: `README.md`、`src/MultiCodingAgentFacade.Samples/Program.cs`、`src/MultiCodingAgentFacade.Samples/MultiCodingAgentFacade.Samples.csproj`、migration note path if separated
  - Contract:
    - README / sample は SL-004 / SL-005 が確定した public client names、request/response/streaming type names、DI extension names、config section names だけを使う。
    - SL-004 / SL-005 output が未確定の field は、実装時に仮名や空値で埋めず `DeferredToRuntimeSliceOutput` として残す。
    - sample は marker-only ではなく、Copilot / Codex の request 作成、non-streaming、streaming、timeout、working directory、skills / disabled skills または当該 runtime の discovery policy、例外処理を compile-time example として示す。ただし real runtime 実行を必須にしない。
    - README は `IChatClient`、`AddMultiProviderChat`、旧 `MeAiUtility.MultiProvider` provider switching、OpenAI / Azure OpenAI provider surface を通常利用手順として案内しない。migration note 内の旧名説明は historical / migration context として明確に限定する。
    - permission handling、provider override、AdvancedOptions は SL-004 の実装方針に従い、typed property を主 API、AdvancedOptions を残す場合は unstable / escape hatch として説明する。
    - Codex sandbox / approval / working directory / skills は SL-005 の request / option shape と Codex runtime discovery policy に基づいて説明する。Codex に存在しない typed skills API を作ったことにしない。
  - Failure handling:
    - public API name が source / runtime slice output から traceable でない場合、SL-003 実装は停止または parent review へ戻す。
    - sample が compile しない場合、docs-only success として扱わない。
    - migration note allowlist が SL-002 audit と不整合な場合、cross-slice risk として parent review へ戻す。
  - Production binding:
    - sample project は `MultiCodingAgentFacade.Samples.csproj` の production project references を使い、test fake / marker-only output で代替しない。
    - README の code snippet は production public API と一致させる。擬似コードを使う場合は runnable sample と混同しない形に限定する。
- Implementation-contract-review-kernel:
  - Draft status: Required / Review requirement drafted
  - Review focus:
    - SL-004 / SL-005 output より先に README / sample が未確定 API を発明していないか。
    - README の旧名は migration note / historical context に閉じ、quickstart や API reference に残っていないか。
    - `AdvancedOptions` が typed API の代替として無制限な主 API に見えていないか。
    - permission / approval policy が source、logs/options、README で矛盾していないか。
    - sample が `CodexAppServerRuntimeMarker` / `GitHubCopilotRuntimeMarker` 表示だけで終わらず、production client path を参照する compile-time example になっているか。
    - migration note の old-name allowlist 候補を SL-002 へ渡し、SL-003 単独で old-name audit PASS を宣言していないか。
  - Required reviewer decision before implementation:
    - SL-004 / SL-005 の public API contracts が parent review で READY になるまで、SL-003 の final README / sample 実装は authorization しない。
    - 先行して作業する場合も、文書構成の骨組みと source evidence のある現行型名確認までに限定する。
- Runtime-contract-kernel:
  - RC-SL003-001 / parent RC-PR22-002 / XC-PR22-002:
    - Boundary: runtime public APIs -> README / sample compile-time references
    - Participants: `GitHubCopilotAgentClient`、`GitHubCopilotAgentRequest`、`GitHubCopilotAgentResponse`、`GitHubCopilotStreamingUpdate`、`AddGitHubCopilotAgentRuntime`、`CodexAppServerAgentClient`、`CodexAppServerTurnRequest`、`CodexAppServerTurnResponse`、`CodexAppServerStreamingUpdate`、`AddCodexAppServerAgentRuntime`、README、sample
    - Required continuity: public type names, request/response/update fields, DI extension names, config section names, streaming and non-streaming examples
    - Current evidence: GitHub Copilot response currently has `Text`, `ModelId`, `TraceId`, `RequestId`; GitHub Copilot streaming update currently has `Kind`, `TextDelta`, `FinalText`, `DeltaCount`, `AccumulatedLength`; Codex turn response currently has only `Text`; Codex streaming update currently has `Kind`, `TextDelta`, `FinalText`; sample currently prints runtime marker names only.
    - Required runtime output before SL-003 implementation: SL-004 / SL-005 must provide final response/update fields and public API naming, or parent must authorize a strictly source-evidence-based docs update with unresolved placeholders excluded.
    - Verification: sample compile and docs/API grep; final cross-slice verification owns `XC-PR22-002` PASS.
  - RC-SL003-002 / parent RC-PR22-006 / XC-PR22-003:
    - Boundary: Copilot provider override / permission policy -> README safety guidance and sample options
    - Participants: `ProviderOverrideOptions`, `GitHubCopilotOptions`, `GitHubCopilotSdkWrapper`, README, sample
    - Required continuity: provider override fields, fail-fast validation outcome, permission approval policy, logging / diagnostics safety, typed-first AdvancedOptions policy
    - Current evidence: `ProviderOverrideOptions` has `Type`, `BaseUrl`, `ApiKey`, `BearerToken`, `AzureApiVersion`; current SDK mapping maps null `BaseUrl` to empty string; current permission handler uses `ApproveAll`; `CopilotModelInfo` is currently `ModelId` + `SupportsReasoningEffort`.
    - Required runtime output before docs finalization: SL-004 must define fail-fast validation, permission default / explicit option, supported/default reasoning effort shape, and AdvancedOptions policy.
    - Verification: README and sample describe the same option names and safety behavior as production code; final PASS deferred to cross-slice verification.
  - RC-SL003-003 / parent RC-PR22-004 / XC-PR22-004:
    - Boundary: Codex JSON-RPC result metadata -> README / sample turn response guidance
    - Participants: `CodexRpcSession`, `CodexAppServerAgentClient`, `CodexAppServerTurnResponse`, `CodexAppServerStreamingUpdate`, README, sample
    - Required continuity: thread id, turn id, status, trace id, request id, diagnostics summary, error summary, final text, deltas
    - Current evidence: `CodexRpcSession.ExecuteTurnAsync` currently returns `Task<string>` and internal `TurnCompletion` stores `Status`, `Text`, `ErrorMessage`; public `CodexAppServerTurnResponse` currently exposes only `Text`.
    - Required runtime output before docs finalization: SL-005 must provide final public result model and streaming update field names.
    - Verification: README and sample do not document missing metadata as already available unless source output exists; final PASS deferred to cross-slice verification.
  - RC-SL003-004 / parent RC-PR22-001, RC-PR22-007 / XC-PR22-006:
    - Boundary: migration note / docs old-name explanation -> scoped old-name audit
    - Participants: README or separated migration note, SL-002 audit rule, final verification
    - Required continuity: migration note path, allowed old terms, denylist terms, scanned paths
    - Current evidence: README currently begins with `MeAiUtility.MultiProvider` and contains many old surface references; SL-002 owns audit rule and final old-name PASS.
    - Verification: SL-003 records exact migration note location and intended allowed old terms for SL-002; SL-003 does not declare audit PASS.
- Test-design-kernel:
  - TP-SL003-001: README old-surface removal check
    - Target: `README.md`
    - Design: quickstart / API reference / install / config sections must not instruct `IChatClient`, `AddMultiProviderChat`, `ProviderFactory`, `ProviderRegistry`, `MultiProviderOptions`, `ConversationExecutionOptions`, `ExtensionParameters`, OpenAI / Azure OpenAI provider switching as active surface. Migration note may mention old terms only in a clearly bounded section.
    - Expected evidence: `rg` denylist with section-aware allowlist, plus reviewer check for migration note context.
  - TP-SL003-002: sample compile-time production path check
    - Target: `src/MultiCodingAgentFacade.Samples/Program.cs` and sample project
    - Design: sample references production clients / DI extension methods and demonstrates non-streaming / streaming shape without requiring real runtime credentials during compile.
    - Expected evidence: `dotnet build MultiCodingAgentFacade.sln` or sample project build, no marker-only output as the sole behavior.
  - TP-SL003-003: docs-to-Copilot policy continuity check
    - Target: README / sample vs SL-004 output
    - Design: provider override, permission approval, model reasoning effort, diagnostics / metadata, AdvancedOptions wording match production API and tests.
    - Expected evidence: reviewer trace from SL-004 result to README/sample snippets; no invented option names.
  - TP-SL003-004: docs-to-Codex metadata continuity check
    - Target: README / sample vs SL-005 output
    - Design: thread/turn/status/request/trace/diagnostics/error guidance matches production response/update model.
    - Expected evidence: reviewer trace from SL-005 result to README/sample snippets; no invented fields.
  - TP-SL003-005: migration note / scoped audit handoff check
    - Target: README or separated migration note path, SL-002 audit allowlist
    - Design: migration note old-name occurrences are intentionally bounded and passed to SL-002; active guidance remains old-name clean.
    - Expected evidence: path and allowed terms listed for parent / SL-002; final audit PASS remains cross-slice.
  - TP-SL003-006: AGENTS.md policy conformance check
    - Target: docs and sample source comments if any
    - Design: docs are Japanese; source code logs / executable strings remain English; comments/XML comments, if added later, are Japanese; no reflection is introduced.
    - Expected evidence: review of changed docs/sample in slice-impl verification.

## Bounded parent Plan pass / Guardrail Focus

- Covered parent FR: FR-003, FR-006, FR-011, FR-012.
- Covered parent AC: AC-004, AC-007, AC-013, AC-014, AC-015 の docs / sample 部分。
- Guardrail Focus:
  - README / sample は runtime public API の consumer であり、SL-004 / SL-005 の producer output を先取りしない。
  - sample は production binding の compile-time evidence として扱い、test fake / runtime marker-only output を成功扱いしない。
  - migration note の旧名説明は SL-002 audit と連動させ、SL-003 単独で audit PASS を宣言しない。
  - cross-slice contracts `XC-PR22-002`、`XC-PR22-003`、`XC-PR22-004`、`XC-PR22-006` は slice 内で Done にしない。

## Non-goals

- runtime API 実装。
- CI audit 実装。
- real runtime E2E 実行。
- NuGet publish 方針変更。
- SL-004 / SL-005 の public API naming を SL-003 が決定すること。
- SL-002 の scoped audit rule を SL-003 が確定すること。
- README / sample / production code / tests / workflow の編集。

## RC / TP / XC ledger

| ID | Kind | Owned / Consumed / Deferred | Notes |
| --- | --- | --- | --- |
| RC-PR22-002 | Parent RC | Consumed / Partially owned projection | README / sample が runtime public API を消費する。最終 public API は SL-004 / SL-005 owned。 |
| RC-PR22-003 | Parent RC | Consumed | Copilot metadata / model capability は SL-004 owned。SL-003 は docs/sample projection のみ。 |
| RC-PR22-004 | Parent RC | Consumed | Codex result metadata は SL-005 owned。SL-003 は docs/sample projection のみ。 |
| RC-PR22-006 | Parent RC | Consumed / Partially owned docs | permission handling の production policy は SL-004 owned。SL-003 は README safety guidance を owns。 |
| RC-PR22-007 | Parent RC | Deferred / Consumed | opt-in skip reason は SL-006 owned。SL-003 は README guidance を後続で消費する可能性あり。 |
| RC-SL003-001 | Slice RC | Owned | README / sample public API continuity。 |
| RC-SL003-002 | Slice RC | Consumed / Owned docs | Copilot permission / provider override / AdvancedOptions policy documentation。 |
| RC-SL003-003 | Slice RC | Consumed / Owned docs | Codex sandbox / approval / working directory / skills documentation。 |
| RC-SL003-004 | Slice RC | Owned handoff / Deferred final PASS | migration note と SL-002 scoped audit の接続。 |
| XC-PR22-001 | Cross-slice contract | Consumed | SL-001 の solution graph を README / sample 前提として消費する。 |
| XC-PR22-002 | Cross-slice contract | Consumed / Producer projection | SL-004 / SL-005 public API を README / sample に投影する。最終 PASS は cross-slice verification。 |
| XC-PR22-003 | Cross-slice contract | Consumed | Copilot runtime policy / metadata を README / sample が消費する。 |
| XC-PR22-004 | Cross-slice contract | Consumed | Codex result metadata を README / sample が消費する。 |
| XC-PR22-006 | Cross-slice contract | Producer / Consumer / Deferred | migration note path / allowed old terms を SL-002 audit へ渡す。最終 audit PASS は deferred。 |
| TP-SL003-001 | Test Point | Owned design | README old-surface removal check。 |
| TP-SL003-002 | Test Point | Owned design | sample compile-time production path check。 |
| TP-SL003-003 | Test Point | Consumed / Owned design | docs-to-Copilot policy continuity check。 |
| TP-SL003-004 | Test Point | Consumed / Owned design | docs-to-Codex metadata continuity check。 |
| TP-SL003-005 | Test Point | Owned design / Deferred final PASS | migration note / scoped audit handoff check。 |
| TP-SL003-006 | Test Point | Owned design | AGENTS.md policy conformance check。 |

## Production binding requirements

- `src/MultiCodingAgentFacade.Samples/Program.cs` は production project references を通じて `GitHubCopilotAgentClient` / `CodexAppServerAgentClient` または SL-004 / SL-005 で確定した public runtime entrypoint を参照する。
- sample は compile-time example であり、実 Copilot / 実 Codex credentials を CI 常時要求しない。
- sample は marker-only 表示を主成果にしない。runtime marker は補助情報として残す場合でも、request 作成、client path、non-streaming / streaming、timeout、working directory、安全設定の例を伴う必要がある。
- README の runnable snippet は source evidence のある API だけを使う。未確定 API は仮置きしない。
- README は permission / provider override / AdvancedOptions を production code の option / validation / logging behavior と一致させる。
- migration note に残す旧名は SL-002 の scoped audit へ渡せるよう、場所と目的を明示する。

## Cross-slice risks to parent-review

- SL-004 / SL-005 の public API contracts が未準備のまま SL-003 を実装すると、README / sample が存在しない field や option を発明する。
- SL-002 audit allowlist が未確定のまま migration note を書くと、合法的な旧名説明が CI false positive になるか、逆に allowlist 過剰で stale public API を見逃す。
- sample が compile-time production path ではなく marker-only または pseudo-code に寄ると、AC-007 を満たしたように見える false PASS になる。
- Copilot permission policy が SL-004 source と README でずれると、AC-013 の安全上の説明が破綻する。
- AdvancedOptions を便利な escape hatch として強調しすぎると、AC-014 の typed API 主体方針に反する。

## Unresolved items

- SL-004 / SL-005 の slice-prep / implementation output が存在しないため、final public response/update fields、DI names、permission policy、provider override validation outcome、AdvancedOptions policy は未確定。
- SL-002 の scoped audit allowlist が未確定のため、migration note の exact path / allowed old terms は未確定。
- SL-001 の deletion result が未確定のため、README migration note で述べる deleted old project path list は producer output 待ち。
- SL-006 の opt-in smoke / skip reason output が未確定のため、README の integration test guidance は後続消費扱い。
- Human decision は現時点では不要。ただし parent review が SL-003 の先行実装を許可する場合、runtime output 未確定部分を除外する明示条件が必要。

## Stop condition

SL-003 prep artifact を作成し、実装・テスト作成・README・workflow・source code 編集を行わず停止する。cross-slice contract は Done 扱いにしない。次は parent review gate で SL-003 の implementation authorization を判断する。

## Handoff to Agent Usage Ledger

- Run ID: SL-003-prep-2026-06-07
- Phase: slice-prep
- Slice: SL-003
- Edit allowed: No
- Outcome: READY_FOR_PARENT_REVIEW。Artifact: `plans/pr22-review-remediation-slice-SL-003-prep.md`
