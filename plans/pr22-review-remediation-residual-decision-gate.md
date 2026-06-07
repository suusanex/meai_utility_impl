# Residual Decision Gate: PR22 Review Remediation

## Verdict

- Status: PARENT_PLAN_VERIFIED_WITH_ACCEPTED_RESIDUALS
- Reason: cross-slice verification では parent Plan の AC-001..AC-015 と XC-PR22-001..006 について、local equivalent checks、scoped audit、production DI/client binding、skip reason 観測の範囲で blocking implementation gap は見つかっていない。残った RDG-PR22-CSV-001..006 は、キミの user decisions により real runtime は opt-in、`release.yml` は実装対象かつ実装済み、低影響項目は caller-convenient defaults で分類可能として扱える。したがって FixNow は不要で、残件は ManualOnly accepted residual または separate work / operational verification として parent close を妨げない。

## Inputs

| Field | Value |
| --- | --- |
| Parent Plan | `plans/pr22-review-remediation-plan.md` |
| Change Risk Triage | `plans/pr22-review-remediation-change-risk-triage.md` |
| Slice Decomposition | `plans/pr22-review-remediation-slice-decomposition.md` |
| Agent Usage Ledger | `plans/pr22-review-remediation-agent-usage-ledger.md` |
| Cross-slice Verification | `plans/pr22-review-remediation-cross-slice-verification-kernel.md` |
| Slice Implementation Results | `plans/pr22-review-remediation-slice-SL-001-implementation-result.md` ... `plans/pr22-review-remediation-slice-SL-006-implementation-result.md` |
| Human decision source | user prompt for this residual-decision-gate run |
| Explicit human decisions present? | Yes |

Explicit human decisions consumed:

- `release.yml` は開発対象に含める。SL-002 で実装済みとして扱う。
- opt-in integration は `MCAF` prefix を使い、real runtime は opt-in として扱う。
- lower-impact items は caller-convenient defaults で separate work / accepted residual に分類してよい。

## Parent Plan completion ledger

| Plan item | Type | Implementation status | Verification status | Evidence | Residual status | Blocking? |
| --- | --- | --- | --- | --- | --- | --- |
| FR-001 | FR | Implemented | Verified | SL-001 result; cross-slice AC-001 / AC-002 | None | No |
| FR-002 | FR | Implemented | Verified | SL-001 / SL-002 / SL-003 result; scoped old-name audit pass | RDG-PR22-CSV-004 is separate prompt cleanup | No |
| FR-003 | FR | Implemented | Verified | SL-003 result; cross-slice AC-004 | None | No |
| FR-004 | FR | Implemented | Verified | SL-002 result; `ci.yml` and `release.yml` target `MultiCodingAgentFacade.sln`; audit pass | RDG-PR22-CSV-006 operational evidence deferred | No |
| FR-005 | FR | Implemented | Verified | SL-006 result; integration tests are opt-in and skip with reason | RDG-PR22-CSV-001 / RDG-PR22-CSV-002 ManualOnly accepted | No |
| FR-006 | FR | Implemented | Verified | SL-003 result; sample build / dry-run evidence | None | No |
| FR-007 | FR | Implemented | Verified | SL-004 result; cross-slice AC-008 | RDG-PR22-CSV-001 ManualOnly accepted for real SDK execution | No |
| FR-008 | FR | Implemented | Verified | SL-005 result; cross-slice AC-009 / AC-010 | RDG-PR22-CSV-002 ManualOnly accepted; RDG-PR22-CSV-005 test-hardening deferred | No |
| FR-009 | FR | Implemented | Verified | SL-004 result; cross-slice AC-011 | None | No |
| FR-010 | FR | Implemented | Verified | SL-004 result; cross-slice AC-012 | None | No |
| FR-011 | FR | Implemented | Verified | SL-004 runtime option plus SL-003 README/sample; cross-slice AC-013 | None | No |
| FR-012 | FR | Implemented | Verified | SL-004 fail-fast policy plus SL-003 README; cross-slice AC-014 | None | No |
| FR-013 | FR | Implemented | Verified | SL-002 scoped audit and plan chain references | None | No |
| AC-001 | AC | Implemented | Verified | root solution inventory and workflows use `MultiCodingAgentFacade.sln` / `.slnx` | None | No |
| AC-002 | AC | Implemented | Verified | old `MeAiUtility.MultiProvider*` source/test directories removed | None | No |
| AC-003 | AC | Implemented for active public surface | VerifiedWithAcceptedResidual | active `.github/workflows`, `README.md`, `src`, `tests` audit pass | RDG-PR22-CSV-004 accepted as separate docs/prompt cleanup | No |
| AC-004 | AC | Implemented | Verified | README coverage in SL-003 and cross-slice verification | None | No |
| AC-005 | AC | Implemented | VerifiedWithAcceptedResidual | CI workflow static inspection, local net8/net10 tests, audit pass | RDG-PR22-CSV-006 operational verification deferred | No |
| AC-006 | AC | Implemented | VerifiedWithAcceptedResidual | SL-006 integration tests: placeholder removed, disabled path skips with reason | RDG-PR22-CSV-001 / RDG-PR22-CSV-002 ManualOnly accepted | No |
| AC-007 | AC | Implemented | Verified | sample uses production clients / DI and compile-time examples | None | No |
| AC-008 | AC | Implemented | VerifiedWithAcceptedResidual | Copilot response/update metadata fields and tests | RDG-PR22-CSV-001 ManualOnly accepted | No |
| AC-009 | AC | Implemented | VerifiedWithAcceptedResidual | Codex response/update metadata fields and tests | RDG-PR22-CSV-002 ManualOnly accepted; RDG-PR22-CSV-005 deferred | No |
| AC-010 | AC | Implemented | Verified | `CodexRpcSession.ExecuteTurnAsync` returns typed result and client maps metadata | RDG-PR22-CSV-005 deferred | No |
| AC-011 | AC | Implemented | Verified | `CopilotModelInfo` supported/default reasoning effort fields | None | No |
| AC-012 | AC | Implemented | Verified | typed model provider validation before SDK session creation | None | No |
| AC-013 | AC | Implemented | Verified | permission handling option/default/log/docs/sample alignment | None | No |
| AC-014 | AC | Implemented | Verified | `AdvancedOptions` supported-key fail-fast and README escape-hatch wording | None | No |
| AC-015 | AC | Implemented | VerifiedWithAcceptedResiduals | SL-001..SL-006 results plus cross-slice residual table | RDG-PR22-CSV-001..006 accepted/deferred as below | No |

## Residual decisions

| Residual ID | Decision | Reason | Blocks implementation OK? | Follow-up |
| --- | --- | --- | --- | --- |
| RDG-PR22-CSV-001 | ManualOnly accepted residual | real GitHub Copilot smoke は credentials / local runtime が必要で、キミの decision により real runtime は opt-in。SL-006 は `MCAF_GITHUB_COPILOT_INTEGRATION` disabled path の skip reason と production `GitHubCopilotSdkWrapper` / `GitHubCopilotAgentClient` DI binding を検証済み。fake-only success は見つからない。 | No | credentials がある環境で `MCAF_GITHUB_COPILOT_INTEGRATION=1` を指定して operational smoke を任意実行する。 |
| RDG-PR22-CSV-002 | ManualOnly accepted residual | real Codex App Server smoke は app-server / local runtime credentials が必要で、real runtime は opt-in。SL-006 は `MCAF_CODEX_APP_SERVER_INTEGRATION` disabled path の skip reason と production `SystemCodexProcessRunner` / `DefaultCodexTransportFactory` / `FileCodexThreadStore` / `CodexAppServerAgentClient` DI binding を検証済み。 | No | runtime 環境があるときだけ `MCAF_CODEX_APP_SERVER_INTEGRATION=1` で operational smoke を任意実行する。 |
| RDG-PR22-CSV-003 | DeferToSeparateWork / accepted residual | `Nerdbank.MessagePack 1.0.2` advisory は既存 dependency warning として各 check で観測されているが、restore / build / tests は pass しており、PR22 remediation の old structure removal / runtime API / docs / workflow acceptance を直接阻害していない。security 更新は依存更新として blast radius が別。 | No | dependency/security remediation Plan または issue で version update と warning policy を扱う。 |
| RDG-PR22-CSV-004 | DeferToSeparateWork / accepted residual | `.github/agents/copilot-instructions.md` は `.github/workflows` ではなく agent prompt context。parent AC-003 の通常対象は source / test / README / workflow で、active public surface audit は pass。旧 spec context が agent guidance として紛らわしい可能性はあるため、PR22 merge blocker ではなく docs/prompt cleanup に送る。 | No | separate docs/prompt cleanup で `.github/agents/copilot-instructions.md` の旧 `Microsoft.Extensions.AI` 文脈を整理する。 |
| RDG-PR22-CSV-005 | DeferToSeparateWork / accepted residual | SL-005 の主要 metadata continuity は AC-009 / AC-010 として source/testsで verified。追加の `ThreadReusePolicy.ReuseByThreadId` / `ReuseOrCreateByKey` 専用テストや `ICodexTransportDiagnostics` fake assertion は test hardening であり、parent close 条件ではない。 | No | future test-hardening で Codex thread reuse variants と transport diagnostics assertion を追加する。 |
| RDG-PR22-CSV-006 | DeferToOperationalVerification / accepted residual | hosted CI / release は GitHub Actions context、tag push、release creation が必要。SL-002 は `release.yml` を実装対象に含め、new solution build、artifact pattern、release DLL existence を local equivalent で確認済み。merge前の implementation OK 判定には hosted run を必須にしない。 | No | PR / release operation 側で GitHub Actions run と tag-based release dry evidence を確認する。 |

## Residual decision table

| Residual ID | Source item | Residual type | Options | Recommended option | Explicit human decision | Decision status | Owner / next step |
| --- | --- | --- | --- | --- | --- | --- | --- |
| RDG-PR22-CSV-001 | real GitHub Copilot smoke not run | ManualEnvironmentRequired | FixNow / ManualOnly / NeedsHumanDecision | ManualOnly accepted residual | real runtime is opt-in; `MCAF` prefix accepted | AcceptedResidual | 任意の manual smoke。parent close は進めてよい。 |
| RDG-PR22-CSV-002 | real Codex App Server smoke not run | ManualEnvironmentRequired | FixNow / ManualOnly / NeedsHumanDecision | ManualOnly accepted residual | real runtime is opt-in; `MCAF` prefix accepted | AcceptedResidual | 任意の manual smoke。parent close は進めてよい。 |
| RDG-PR22-CSV-003 | dependency advisory warnings | OutOfScopeForThisPass | FixNow / DeferToSeparateWork / NeedsHumanDecision | DeferToSeparateWork | lower-impact caller-convenient default | AcceptedResidual | dependency/security remediation に分離。 |
| RDG-PR22-CSV-004 | `.github/agents/copilot-instructions.md` old spec context | DocsPromptCleanup | FixNow / DeferToSeparateWork / NeedsHumanDecision | DeferToSeparateWork | lower-impact caller-convenient default | AcceptedResidual | docs/prompt cleanup に分離。 |
| RDG-PR22-CSV-005 | extra Codex dedicated tests | TestHardening | FixNow / DeferToSeparateWork / NeedsHumanDecision | DeferToSeparateWork | lower-impact caller-convenient default | AcceptedResidual | future test-hardening に分離。 |
| RDG-PR22-CSV-006 | hosted CI / release not executed | OperationalVerification | FixNow / ManualOnly / DeferToOperationalVerification / NeedsHumanDecision | DeferToOperationalVerification | `release.yml` is in scope and implemented; lower-impact default for hosted evidence | AcceptedResidual | PR / release operation 側の hosted verification。 |

## FixNow items

なし。

coverage-gap-resolution-slice へ進む条件はない。もしキミが後から hosted real smoke、dependency advisory、agent prompt cleanup、extra tests のいずれかを merge blocker に格上げするなら、その時点で coverage-gap-triage へ渡す。

## Accepted / deferred residuals

| Residual ID | Accepted / deferred as | Close impact | Notes |
| --- | --- | --- | --- |
| RDG-PR22-CSV-001 | ManualOnly accepted residual | Non-blocking | opt-in real GitHub Copilot smoke。 |
| RDG-PR22-CSV-002 | ManualOnly accepted residual | Non-blocking | opt-in real Codex App Server smoke。 |
| RDG-PR22-CSV-003 | Separate dependency/security work | Non-blocking | existing advisory warning。 |
| RDG-PR22-CSV-004 | Separate docs/prompt cleanup | Non-blocking | active public surface audit からは外す。 |
| RDG-PR22-CSV-005 | Future test-hardening | Non-blocking | main metadata continuity は既存 verification で満たす。 |
| RDG-PR22-CSV-006 | Operational hosted verification | Non-blocking | local equivalent evidence を parent close evidence として採用。 |

## Human decision required

| Residual ID | Question | Why human decision is required | Safe default |
| --- | --- | --- | --- |
| none | none | user prompt supplied sufficient decision authority for this gate | Proceed with parent close using accepted residuals |

## Handoff

- Source artifacts:
  - `AGENTS.md`
  - `plans/pr22-review-remediation-plan.md`
  - `plans/pr22-review-remediation-change-risk-triage.md`
  - `plans/pr22-review-remediation-slice-decomposition.md`
  - `plans/pr22-review-remediation-agent-usage-ledger.md`
  - `plans/pr22-review-remediation-cross-slice-verification-kernel.md`
  - `plans/pr22-review-remediation-slice-SL-001-implementation-result.md`
  - `plans/pr22-review-remediation-slice-SL-002-implementation-result.md`
  - `plans/pr22-review-remediation-slice-SL-003-implementation-result.md`
  - `plans/pr22-review-remediation-slice-SL-004-implementation-result.md`
  - `plans/pr22-review-remediation-slice-SL-005-implementation-result.md`
  - `plans/pr22-review-remediation-slice-SL-006-implementation-result.md`
- Decisions made:
  - RDG-PR22-CSV-001 and RDG-PR22-CSV-002 are ManualOnly accepted residuals.
  - RDG-PR22-CSV-003 is separate dependency/security work.
  - RDG-PR22-CSV-004 is separate docs/prompt cleanup, not PR22 AC-003 active public surface FixNow.
  - RDG-PR22-CSV-005 is future test-hardening, not parent close blocker.
  - RDG-PR22-CSV-006 is operational hosted verification, not implementation OK blocker.
- Decisions not made:
  - none for this gate.
- Accepted residuals:
  - RDG-PR22-CSV-001
  - RDG-PR22-CSV-002
  - RDG-PR22-CSV-003
  - RDG-PR22-CSV-004
  - RDG-PR22-CSV-005
  - RDG-PR22-CSV-006
- FixNow items:
  - none
- Manual verification handoff:
  - Optional only. Real runtime smoke can be run by an operator with credentials using `MCAF_GITHUB_COPILOT_INTEGRATION=1` and/or `MCAF_CODEX_APP_SERVER_INTEGRATION=1`.
- Re-plan required:
  - No
- Remaining blocking items:
  - none
- Recommended next step:
  - parent Plan close / final report に進む。FixNow が新たに指定された場合だけ `coverage-gap-triage.agent.md` に戻す。
