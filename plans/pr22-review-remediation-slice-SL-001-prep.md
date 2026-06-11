# Slice Preparation Result: SL-001

## Verdict

- Status: READY_FOR_PARENT_REVIEW
- Reason: SL-001 の scope / non-goals / 親要件 / cross-slice contract を確認し、旧構成削除と new solution graph 固定に必要な per-slice triage、implementation contract draft、runtime contract draft、test design draft を作成した。実装・テスト作成・README・workflow・source code 編集は行っていない。

## Agent metadata

- Agent type: slice-prep
- Model: gpt-5.4
- Reasoning effort: medium
- Parent authorization artifact: ユーザー指示「PR22 remediation の slice-prep。対象は SL-001 のみ。実装禁止。」および `plans/pr22-review-remediation-slice-SL-001.md`
- Delegation evidence: `plans/pr22-review-remediation-slice-SL-001-prep.md` を slice-prep artifact として作成。write scope はこの artifact のみに限定。

## Generated / drafted artifacts

- Per-slice change-risk-triage: 下記「Per-slice change-risk-triage draft」
- Implementation-contract-kernel: 下記「Implementation-contract-kernel draft」
- Implementation-contract-review-kernel: 下記「Implementation-contract-review-kernel draft / review requirement」
- Runtime-contract-kernel: 下記「Runtime-contract-kernel draft」
- Test-design-kernel: 下記「Test-design-kernel draft」

### Per-slice change-risk-triage draft

#### Recommended profile

`standard-slice`

#### Reasoning

SL-001 は旧 `MeAiUtility` / `MeAiUtility.MultiProvider*` の削除と `MultiCodingAgentFacade.sln` / `.slnx` の authoritative 化に限定される。runtime API や docs / CI audit には踏み込まないため full-coverage を slice 内で再展開する必要はない。

ただし implementation-realization risk は `Present`。削除対象が root solution、source projects、test projects にまたがり、`MultiCodingAgentFacade.sln(x)` が全新 project を保持していない場合は下流 slice が false PASS になる。加えて workflow / README / audit allowlist は別 slice owner のため、この slice は cross-slice contract を完了扱いにせず、削除済み path list と new solution graph state を producer output として親へ返す必要がある。

#### High-risk boundaries

| Boundary | Producer | Consumer | Mechanism | Risk type |
| --- | --- | --- | --- | --- |
| 旧 solution / project deletion -> new solution graph | SL-001 implementation | `MultiCodingAgentFacade.sln`, `MultiCodingAgentFacade.slnx`, downstream slices | file deletion + project reference graph | stale reference / missing project / false source of truth |
| deleted old path list -> scoped audit | SL-001 implementation output | SL-002 | deletion report / repo inventory | audit false negative / fabricated path list |
| new solution graph -> README / migration note | SL-001 implementation output | SL-003 | authoritative solution/project identifiers | docs mismatch / stale migration explanation |
| source graph old dependency removal -> runtime slices | SL-001 implementation output | SL-004, SL-005 indirectly | remaining source/test project references | old public surface remains reachable |

#### Selected runtime / structural contracts

| Contract ID | Boundary | Triage status | Next action |
| --- | --- | --- | --- |
| RC-PR22-001 | 旧構成削除 -> `MultiCodingAgentFacade.sln` / CI | SelectedForSlice | SL-001 runtime contract draft で producer fields を固定する |
| RC-PR22-002 | README / sample -> public typed API | Deferred | SL-003 が消費。SL-001 は solution graph identifiers のみ producer |
| RC-PR22-X04 | planning artifacts / migration note の合法的旧名 | Deferred | SL-002 / SL-003 / final verification で scoped audit として扱う |

#### Implementation-realization risk

Present。

- 旧 root solution `MeAiUtility.sln` / `MeAiUtility.slnx` が現状で存在する。
- 旧 source directories `src/MeAiUtility.MultiProvider*` が現状で存在する。
- 旧 test directories `tests/MeAiUtility.MultiProvider*` が現状で存在する。
- `MultiCodingAgentFacade.sln` / `.slnx` は新 project 群を列挙しているが、削除後の restore / build / test で graph integrity を確認する必要がある。
- workflow / README / full old-name audit はこの slice の non-goal であり、stale reference を見つけても SL-002 / SL-003 へ unresolved として渡す。

### Implementation-contract-kernel draft

#### Contract goal

旧 `MeAiUtility` / `MultiProvider` 構成を source graph から削除し、`MultiCodingAgentFacade.sln` / `MultiCodingAgentFacade.slnx` を下流 slice が信頼できる solution graph source として扱える状態にする。

#### Scope

Owned by SL-001:

- root の `MeAiUtility.sln` / `MeAiUtility.slnx` を削除対象として扱う。
- `src/MeAiUtility.MultiProvider*` と `tests/MeAiUtility.MultiProvider*` を削除対象として扱う。
- `MultiCodingAgentFacade.sln` / `.slnx` に残る source / test project path が実在することを確認する。
- new solution graph が `src/MultiCodingAgentFacade.Core`、`CodexAppServer`、`GitHubCopilot`、`Samples`、対応 test projects、`IntegrationTests` を含むことを確認する。
- 旧 MEAI / OpenAI / AzureOpenAI dependency が new source graph の project reference / package reference として残っていないことを確認する。生成物 `bin/` / `obj/` は判定対象外。

Not owned by SL-001:

- `.github/workflows/ci.yml` の切替と scoped audit 実装。
- `README.md` / migration note / sample の更新。
- Copilot / Codex runtime API の response model 変更。
- opt-in integration smoke の実装。
- `specs/`, `tasks.md`, `plans/` など履歴・計画 artifact 内の旧名説明の削除。

#### Required inputs

| Input | Source | Fabrication allowed? | Notes |
| --- | --- | --- | --- |
| old root solution paths | current repo inventory | No | `MeAiUtility.sln`, `MeAiUtility.slnx` |
| old source project paths | current repo inventory | No | `src/MeAiUtility.MultiProvider*` |
| old test project paths | current repo inventory | No | `tests/MeAiUtility.MultiProvider*` |
| new solution files | current repo inventory | No | `MultiCodingAgentFacade.sln`, `MultiCodingAgentFacade.slnx` |
| new project path list | `MultiCodingAgentFacade.sln(x)` and current repo inventory | No | 実在確認が必要 |

#### Implementation invariants

| ID | Requirement | Verification hint |
| --- | --- | --- |
| IC-SL001-001 | root に `MeAiUtility.sln` / `MeAiUtility.slnx` が残らない | `Test-Path` / `rg --files` |
| IC-SL001-002 | `src/MeAiUtility.MultiProvider*` と `tests/MeAiUtility.MultiProvider*` が残らない | directory inventory |
| IC-SL001-003 | `MultiCodingAgentFacade.sln` と `.slnx` は残り、列挙 project path が実在する | solution text inspection + filesystem check |
| IC-SL001-004 | new solution graph は旧 project path を参照しない | `rg` against `MultiCodingAgentFacade.sln(x)` |
| IC-SL001-005 | new source/test project files の `ProjectReference` / direct `PackageReference` に旧 `MeAiUtility.MultiProvider` / old provider surface が残らない | `rg` against `src/MultiCodingAgentFacade*` and `tests/MultiCodingAgentFacade*`, excluding `bin/` / `obj/` |
| IC-SL001-006 | stale references found outside SL-001 ownership are not silently fixed or ignored | unresolved item として SL-002 / SL-003 / parent review へ渡す |

#### Output expected from slice implementation

- deleted old root solution list。
- deleted old source project directory list。
- deleted old test project directory list。
- remaining new solution files and project list。
- verification commands / results。
- stale references outside SL-001 ownershipがあれば、owner slice と理由。

### Implementation-contract-review-kernel draft / review requirement

Review requirement: required。

理由: 削除範囲が広く、source graph owner と downstream CI / docs / audit owner の境界が non-trivial。親レビューまたは slice-impl 開始時の `implementation-handoff-review` では、次を確認すること。

| Review item | Required decision |
| --- | --- |
| Delete boundary | `MeAiUtility.sln(x)` と `src/tests/MeAiUtility.MultiProvider*` の削除だけに限定され、new `MultiCodingAgentFacade*` project を削除対象に混ぜていないこと |
| Source-of-truth boundary | `MultiCodingAgentFacade.sln` と `.slnx` の両方を残し、片方だけを source of truth として扱わないこと |
| Generated output boundary | `bin/` / `obj/` の old dependency hit を production residue と誤判定しないこと |
| Downstream ownership | workflow / README / scoped audit / migration note は SL-002 / SL-003 へ deferred で渡されること |
| No compatibility wrapper | 旧 namespace / `IChatClient` / `AddMultiProviderChat` 互換 wrapper を復活させないこと |

### Runtime-contract-kernel draft

#### RC-SL001-001: old graph deletion -> authoritative new solution graph

| Field | Value |
| --- | --- |
| Parent RC | RC-PR22-001 |
| Related XC | XC-PR22-001, XC-PR22-006 |
| Contract owner in this slice | Producer |
| Participants | repository root solution files, source project directories, test project directories, `MultiCodingAgentFacade.sln`, `MultiCodingAgentFacade.slnx` |
| Mechanism | filesystem deletion + solution/project reference graph |
| Required identifiers | `MultiCodingAgentFacade.sln`, `MultiCodingAgentFacade.slnx`, remaining source project paths, remaining test project paths, deleted old project paths |
| Error expectation | stale old project reference must be observable by build/test or inventory check; it must not be papered over by compatibility wrapper |
| Recovery expectation | references outside SL-001 owner scope are recorded as unresolved for owner slice instead of ad hoc edits |
| Verification requirement | source inventory, solution graph inspection, restore/build/test of new solution where environment permits |
| Status | DraftedForParentReview |

#### Field continuity

| Field / state / identifier | Producer | Consumer | Storage / handoff | Fabrication allowed? | Slice status |
| --- | --- | --- | --- | --- | --- |
| `MultiCodingAgentFacade.sln` / `.slnx` | SL-001 | SL-002, SL-003, final verification | remaining root solution files | No | Owned |
| remaining source project paths | SL-001 | SL-002, final verification | solution files + source inventory | No | Owned |
| remaining test project paths | SL-001 | SL-002, SL-006, final verification | solution files + test inventory | No | Owned |
| deleted old project path list | SL-001 | SL-002, final verification | slice-impl result / deletion report | No | Owned |
| CI command target | SL-002 | final verification | workflow | No | Deferred |
| audit allowlist / migration note path | SL-002 / SL-003 | final verification | audit config / README or docs | No | Deferred |

### Test-design-kernel draft

#### Test strategy

SL-001 の test design は structural / graph verification に限定する。UnitTest / IntegrationTest の新規作成は要求しない。実装 slice では、削除後の repository inventory と `MultiCodingAgentFacade.sln(x)` graph を確認し、可能なら new solution の restore / build / test を実行する。

#### Test points

| Test Point ID | Kind | Target | Purpose | Expected evidence |
| --- | --- | --- | --- | --- |
| TP-SL001-001 | Inventory | root files | `MeAiUtility.sln` / `MeAiUtility.slnx` が存在しないこと | `rg --files -g '*.sln' -g '*.slnx'` または `Test-Path` 結果 |
| TP-SL001-002 | Inventory | `src/`, `tests/` | `MeAiUtility.MultiProvider*` directories が存在しないこと | directory inventory |
| TP-SL001-003 | SolutionGraph | `MultiCodingAgentFacade.sln`, `.slnx` | new solution graph が old project path を参照せず、listed project paths が実在すること | solution text inspection / path existence check |
| TP-SL001-004 | BuildGraph | `MultiCodingAgentFacade.sln` or `.slnx` | new solution graph で restore / build / test が可能なこと | `dotnet restore`, `dotnet build`, `dotnet test` の結果。環境要因で不可なら理由 |
| TP-SL001-005 | DependencyResidue | new source/test project files | direct `ProjectReference` / `PackageReference` に旧 `MeAiUtility.MultiProvider` / old OpenAI-Azure provider surface が残らないこと | `rg` 結果。`bin/` / `obj/` は除外 |
| TP-SL001-006 | DeferredResidue | workflow/docs/specs/plans | SL-001 非所有の stale reference を owner slice へ渡せること | unresolved item list |

#### Stub / fake / OS mutation policy

- UnitTest / CI IntegrationTest が実 OS 環境を変更する設計は不要。
- 実 runtime、実 Copilot、実 Codex、credential を使う test は SL-001 の対象外。
- verification は filesystem inventory と .NET build graph に限定し、実 OS レジストリ / SetupAPI / service / device には触れない。

## Bounded parent Plan pass / Guardrail Focus

- Bounded parent Plan pass: PR #22 remediation のうち、FR-001 / FR-002 と AC-001 / AC-002 / AC-003 structural 部分。
- Guardrail Focus coverage: RC-PR22-001 と XC-PR22-001 / XC-PR22-006 のうち、旧構成削除と new solution graph producer state。
- Parent stop condition alignment: repository source graph no longer contains old solution / old project directories, and downstream slices can treat `MultiCodingAgentFacade.sln(x)` as authoritative。
- This prep pass stop condition: 実装前 kernel draft を作成し、親レビューへ返す。production code / tests / README / workflow は編集しない。

## Non-goals

- CI audit 実装。
- README 改訂。
- sample 改訂。
- Copilot / Codex runtime model 変更。
- provider override validation / permission handling 実装。
- opt-in integration smoke 実装。
- `specs/`, `tasks.md`, `plans/` の履歴・計画 artifact から旧名を削除すること。
- cross-slice contract を SL-001 内で Done 扱いすること。

## RC / TP / XC ledger

| ID | Kind | Owned / Consumed / Deferred | Notes |
| --- | --- | --- | --- |
| RC-PR22-001 | Parent RC | Owned | 旧構成削除と new solution graph 固定を SL-001 が producer として扱う |
| RC-PR22-002 | Parent RC | Deferred | README / sample との整合は SL-003。SL-001 は solution identifiers のみ producer |
| RC-PR22-X04 | Parent RC candidate | Deferred | planning / migration 旧名許容は SL-002 / SL-003 / final verification |
| XC-PR22-001 | Cross-slice contract | Owned/Produced | `MultiCodingAgentFacade.sln(x)`、remaining project paths、deleted old paths を producer output にする。完了判定は final cross-slice verification |
| XC-PR22-006 | Cross-slice contract | Deferred/Produced input | deleted old path list は SL-001 が供給。audit rule / allowlist は SL-002 / SL-003 |
| TP-SL001-001 | Test point | Owned | old root solution absence |
| TP-SL001-002 | Test point | Owned | old source/test directories absence |
| TP-SL001-003 | Test point | Owned | new solution graph path integrity |
| TP-SL001-004 | Test point | Owned | restore / build / test on new solution where environment permits |
| TP-SL001-005 | Test point | Owned | new source/test project reference residue check, excluding generated outputs |
| TP-SL001-006 | Test point | Deferred bridge | non-owned stale references are routed to owner slice |

## Production binding requirements

- `MultiCodingAgentFacade.sln` / `.slnx` must bind to real source/test project files, not test-only placeholders.
- Remaining new project references must point to `src/MultiCodingAgentFacade.*` / `tests/MultiCodingAgentFacade.*` paths that exist after deletion.
- Old `MeAiUtility.MultiProvider*` projects must not remain as hidden build dependencies, compatibility wrappers, or fallback packages.
- Build/test verification must use the production solution graph. Passing only by inspecting files is insufficient for final slice implementation unless environment prevents .NET execution, in which case the reason must be recorded.
- `bin/` / `obj/` generated artifacts are not production binding evidence and should not be used to decide old dependency residue.

## Cross-slice risks to parent-review

- SL-002 depends on a precise deleted old path list. If SL-001 implementation reports only generic deletion wording, scoped audit cannot distinguish stale source from allowed history.
- SL-003 depends on `MultiCodingAgentFacade.sln(x)` being authoritative. If `.sln` and `.slnx` diverge, docs/sample may target the wrong graph.
- XC-PR22-006 final old-name PASS cannot be decided in SL-001 because migration note path and audit allowlist are not owned here.
- Generated `bin/` / `obj/` may contain old dependency names from previous builds; parent review should ensure implementation verification excludes or cleans generated outputs deliberately instead of treating them as source residue.
- Specs / historical planning documents contain old names by design. Removing them in SL-001 would expand scope and damage source evidence.

## Unresolved items

| Item | Owner | Status | Notes |
| --- | --- | --- | --- |
| CI restore / build / test target switch | SL-002 | Deferred | `.github/workflows/ci.yml` は SL-001 で編集しない |
| scoped old-name / old-dependency audit | SL-002 | Deferred | denylist / allowlist の exact path は SL-002 / SL-003 後に確定 |
| README migration note | SL-003 | Deferred | SL-001 は削除済み path list を供給するだけ |
| sample compile-time API alignment | SL-003 | Deferred | runtime public API は SL-004 / SL-005 消費後 |
| opt-in integration test inclusion | SL-006 / SL-002 | Deferred | placeholder removal と CI inclusion は別 slice |
| real Copilot / Codex smoke execution | SL-006 / ManualOnly | Deferred | 環境依存。SL-001 対象外 |

## Stop condition

READY_FOR_PARENT_REVIEW として停止する。親レビューが承認するまで implementation-execution / file deletion / tests / README / workflow edits へ進まない。

## Handoff to Agent Usage Ledger

- Run ID: SL-001-prep-20260607T191233+09:00
- Phase: slice-prep
- Slice: SL-001
- Edit allowed: No
- Outcome: READY_FOR_PARENT_REVIEW
