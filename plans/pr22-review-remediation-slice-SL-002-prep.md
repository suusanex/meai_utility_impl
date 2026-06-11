# Slice Preparation Result: SL-002

## Verdict

- Status: READY_FOR_PARENT_REVIEW
- Reason: SL-002 の bounded scope、親 Plan / triage / decomposition / slice artifact に基づく per-slice risk、implementation contract、runtime contract、test design の下書きは完了。実装許可はまだ出せない。SL-001 の削除結果、SL-003 の migration note path、SL-006 の opt-in skip reason は source evidence 未確定なので、parent review gate で Deferred / dependency として扱う必要がある。

## Agent metadata

- Agent type: slice-prep
- Model: gpt-5.4
- Reasoning effort: medium
- Parent authorization artifact: ユーザー依頼「PR22 remediation の slice-prep。対象は SL-002 のみ。実装禁止」と `plans/pr22-review-remediation-slice-SL-002.md`
- Delegation evidence: `plans/pr22-review-remediation-slice-SL-002-prep.md` を slice-prep artifact として作成。production code、tests、README、workflow は編集していない。

## Generated / drafted artifacts

- Per-slice change-risk-triage:
  - Recommended profile: `standard-slice`
  - Implementation-realization risk: Present
  - Reason: SL-002 は CI target と repository audit を扱うため、見た目は workflow 修正に近いが、旧名 denylist / allowlist の設計を誤ると stale public API を見逃す、または migration note / planning artifacts の合法的な旧名説明で CI を落とす。さらに SL-001 の削除結果、SL-003 の migration note、SL-006 の opt-in integration project を消費する cross-slice 性質がある。
  - Parent triage alignment: parent triage の RC-PR22-001 / RC-PR22-007 と、decomposition の XC-PR22-001 / XC-PR22-005 / XC-PR22-006 に整合。単独で final old-name PASS は出さない。
  - Current-state evidence:
    - `.github/workflows/ci.yml` は `dotnet restore MeAiUtility.sln`、`dotnet build MeAiUtility.sln`、`dotnet test MeAiUtility.sln` を実行している。
    - repository root には `MeAiUtility.sln` / `MeAiUtility.slnx` と `MultiCodingAgentFacade.sln` / `MultiCodingAgentFacade.slnx` が併存している。
    - `src/MeAiUtility.MultiProvider*` と `tests/MeAiUtility.MultiProvider*` が現存している。
    - `MultiCodingAgentFacade.sln` / `.slnx` は `tests/MultiCodingAgentFacade.IntegrationTests` を含む。
    - `tests/MultiCodingAgentFacade.IntegrationTests/OptInIntegrationPlaceholderTests.cs` は placeholder `Assert.True(true)` を含む。これは SL-006 所有で、SL-002 は CI 対象に含める前提のみを扱う。
    - `.github/workflows/release.yml` にも旧 solution / old package artifact が残る。SL-002 artifact の affected components は `ci.yml` 中心だが、parent AC-003 の「workflow」範囲に含めるか parent review で明示が必要。
- Implementation-contract-kernel:
  - Drafted.
  - IC-SL002-001: CI restore / build / test command は `MultiCodingAgentFacade.sln` または parent が承認した `.slnx` を source of truth とし、`MeAiUtility.sln` / `MeAiUtility.slnx` を実行対象にしない。
  - IC-SL002-002: CI の test target は `MultiCodingAgentFacade.IntegrationTests` を solution 経由で含む。ただし real Copilot / real Codex credentials がない通常 CI で失敗しない skip design は SL-006 から消費する。
  - IC-SL002-003: old-name audit は denylist と allowlist を明示し、source / tests / README / workflow の stale public old API を検出する。planning artifacts、historical specs、migration note は同列に扱わない。
  - IC-SL002-004: audit 対象から `bin/` / `obj/` など generated output を除外する。`GitHub.Copilot.SDK` の推移依存として出る `Microsoft.Extensions.AI` と、production source の直接依存や README の案内は区別する。
  - IC-SL002-005: audit failure は fail-fast にし、CI 上で違反 term、path、allowlist 判定理由を観測できる形にする。失敗時フォールバックで PASS にしない。
  - IC-SL002-006: allowlist exact path は SL-003 の migration note location と親 review gate で確定するまで Deferred。source evidence のない migration path を仮の Done として埋めない。
  - IC-SL002-007: `release.yml` を SL-002 の edit scope に含めるかは parent review requirement。現在の source evidence では旧名が残るが、slice artifact の affected components は `.github/workflows/ci.yml` なので、scope 拡大は parent 承認なしに行わない。
- Implementation-contract-review-kernel:
  - Required.
  - Review requirement:
    - `MultiCodingAgentFacade.sln` と `MultiCodingAgentFacade.slnx` のどちらを CI 標準 target にするか。parent Plan は `MultiCodingAgentFacade.sln` 基準を要求しているため、既定は `.sln`。
    - old-name denylist の exact terms と path allowlist の境界。特に `README.md` の migration note、`plans/`、`specs/002-meai-multi-provider/`、`.github/workflows/release.yml` の扱い。
    - audit を workflow shell step として置くか、repository-local audit script / test として置くか。繰り返し実行性と CI 可読性を優先するなら script/test 化が望ましいが、production code ではなく guardrail artifact として扱う。
    - SL-006 未実装時に integration placeholder が残る状態で `dotnet test MultiCodingAgentFacade.sln` を CI target にしてよいか。実装順序によっては SL-002 と SL-006 の直列化が必要。
    - `release.yml` を「CI」ではなく release flow として別 slice / parent residual にするか、AC-003 の workflow 混入チェックとして SL-002 に含めるか。
- Runtime-contract-kernel:
  - Drafted.
  - RC-SL002-001: CI command target contract
    - Boundary: `.github/workflows/ci.yml` -> `dotnet restore/build/test`
    - Participants: GitHub Actions runner、.NET SDK 8/10、`MultiCodingAgentFacade.sln`
    - Required fields / identifiers: solution path、TFM matrix、restore/build/test command、`--no-restore` / `--no-build` の整合
    - Expected behavior: CI は旧 solution ではなく new solution graph を restore/build/test する。
    - Failure behavior: solution path 不一致、旧 solution target、missing project reference は CI failure として観測される。
  - RC-SL002-002: scoped old-name audit contract
    - Boundary: repository scan -> CI audit result
    - Participants: audit step/script、source/test/docs/workflow files、denylist / allowlist
    - Required fields / identifiers: denylist terms、scanned paths、excluded generated paths、allowlisted historical/migration/plans paths
    - Expected behavior: stale public old API / dependency references を fail-fast で検出し、合法的な migration / historical references は理由付きで許可する。
    - Failure behavior: allowlist 外の denylist hit は path と term を出して CI failure。
  - RC-SL002-003: integration project inclusion contract
    - Boundary: new solution test command -> `tests/MultiCodingAgentFacade.IntegrationTests`
    - Participants: solution graph、xUnit integration project、SL-006 opt-in smoke tests
    - Required fields / identifiers: integration test project path、opt-in environment variable names、skip reason
    - Expected behavior: credentials なし通常 CI では skip reason を出して壊れず、opt-in enabled では production client path を使う。SL-002 は project inclusion だけを所有し、skip test body は SL-006 が所有する。
    - Failure behavior: placeholder pass が残る場合は final PASS 不可。SL-002 単独では Done 扱いしない。
  - RC-SL002-004: generated output and transitive dependency classification contract
    - Boundary: audit scan -> dependency/reference classification
    - Participants: source files、project files、generated `bin/` / `obj/`、NuGet transitive dependencies
    - Required fields / identifiers: excluded directory list、direct `PackageReference` / source `using` / README guidance、transitive package evidence
    - Expected behavior: generated output は scan 対象から除外し、source graph の直接旧依存と推移依存を区別する。
    - Failure behavior: `bin/` / `obj/` だけの hit で CI を落とさない。source / workflow / README の stale hit は許可理由なしに PASS にしない。
  - RC-SL002-005: workflow scope contract
    - Boundary: `.github/workflows/*` -> old-name audit
    - Participants: `ci.yml`、`release.yml`、parent review gate
    - Required fields / identifiers: scanned workflow files、allowed / denied old workflow references、release packaging policy
    - Expected behavior: SL-002 は `ci.yml` を所有する。`release.yml` は old references があるため、parent review で SL-002 scope に含めるか別 residual として明示する。
    - Failure behavior: `release.yml` を未分類のまま final old-name PASS にしない。
- Test-design-kernel:
  - Drafted.
  - TP-SL002-001: CI workflow target test
    - Verify: `.github/workflows/ci.yml` の restore/build/test が `MultiCodingAgentFacade.sln` を参照し、`MeAiUtility.sln` を参照しない。
    - Evidence: static workflow scan plus CI run result.
  - TP-SL002-002: new solution graph test
    - Verify: `dotnet restore MultiCodingAgentFacade.sln`、`dotnet build MultiCodingAgentFacade.sln -f net8.0/net10.0`、`dotnet test MultiCodingAgentFacade.sln -f net8.0/net10.0` が通常 CI 条件で通る。
    - Notes: SL-006 の placeholder replacement が未完の場合、test の意味は partial。parent review で実装順序を固定する。
  - TP-SL002-003: denylist positive detection test
    - Verify: audit は source / tests / README / workflow の allowlist 外 `MeAiUtility`、`MultiProvider`、`IChatClient`、`Microsoft.Extensions.AI`、`OpenAI`、`AzureOpenAI`、`AddMultiProviderChat`、`ProviderFactory`、`ProviderRegistry`、`MultiProviderOptions`、`ConversationExecutionOptions`、`ExtensionParameters` を検出して fail できる。
    - Notes: term set は parent Plan FR-004 / AC-003 を source とする。`OpenAI` は `OpenAI Codex App Server` など legitimate product name と衝突しうるため、exact rule は review required。
  - TP-SL002-004: allowlist classification test
    - Verify: planning artifacts、historical specs、migration note の合法的 old-name references は audit output で allow reason を持つ。
    - Notes: migration note path は SL-003 未確定なので Deferred。
  - TP-SL002-005: generated output exclusion test
    - Verify: `bin/` / `obj/` / restore artifacts の old terms や transitive dependency artifacts は stale public API として扱わない。
  - TP-SL002-006: integration inclusion and skip-observability test
    - Verify: `MultiCodingAgentFacade.IntegrationTests` が new solution test target に含まれ、credentials なしでは skip reason が観測できる。
    - Notes: actual skip tests are SL-006 owned。SL-002 は CI が project を落とさず含めることを確認する。
  - TP-SL002-007: workflow scope regression test
    - Verify: `.github/workflows/release.yml` の扱いが parent review gate で分類されている。SL-002 scope に含めるなら audit 対象、含めないなら residual /別 slice として記録。
  - TP-SL002-008: cross-slice verification handoff
    - Verify: XC-PR22-001 / XC-PR22-005 / XC-PR22-006 を slice 内で Done にせず、final cross-slice-verification-kernel が old solution deletion、docs migration allowlist、opt-in smoke skip reason まで再確認する。

## Bounded parent Plan pass / Guardrail Focus

SL-002 は parent Plan の FR-004 と FR-013、AC-003 / AC-005 / AC-015 の audit 部分だけを扱う。

Guardrail Focus:

- CI command target が `MeAiUtility.sln` から `MultiCodingAgentFacade.sln` へ切り替わること。
- old-name / old-dependency audit が stale public old API を検出し、migration / historical / planning references を source evidence 付きで区別すること。
- `MultiCodingAgentFacade.IntegrationTests` を CI test target に含めても、real runtime credentials 不在で通常 CI を壊さない設計を SL-006 から消費すること。
- final old-name PASS、old solution deletion PASS、README / migration note consistency PASS は cross-slice verification へ Deferred。

Bounded parent Plan pass で確認した source evidence:

- `plans/pr22-review-remediation-plan.md`: FR-004 / AC-005 は `MultiCodingAgentFacade.sln` 基準の restore / build / test と旧名混入チェックを要求。
- `plans/pr22-review-remediation-change-risk-triage.md`: RC-PR22-001 / RC-PR22-007 が CI / old graph / opt-in integration を cross-slice risk として選択。
- `plans/pr22-review-remediation-slice-decomposition.md`: SL-002 は XC-PR22-001 / XC-PR22-005 / XC-PR22-006 に関連し、final old-name PASS は cross-slice verification に残す。
- `plans/pr22-review-remediation-slice-SL-002.md`: SL-002 の non-goals は runtime API 実装、README 全面改訂、旧構成削除、real runtime smoke 実行。
- `.github/workflows/ci.yml`: 現在は旧 `MeAiUtility.sln` を target にしている。
- `.github/workflows/release.yml`: 現在も旧 `MeAiUtility.sln` と旧 package artifact を参照している。SL-002 scope に含めるかは未承認。
- `MultiCodingAgentFacade.sln` / `.slnx`: new source/test projects と integration test project を含む。
- `tests/MultiCodingAgentFacade.IntegrationTests/OptInIntegrationPlaceholderTests.cs`: placeholder pass が残る。SL-006 ownership。

## Non-goals

- production code の編集。
- tests の作成・編集。
- README 本文全面改訂、migration note 作成。
- workflow 編集。
- 旧 `MeAiUtility` / `MultiProvider` project 削除そのもの。
- Copilot / Codex runtime API 実装。
- real Copilot / real Codex smoke 実行。
- cross-slice contract の最終 Done 判定。
- source evidence のない allowlist path / skip env var / migration note location の仮決め。

## RC / TP / XC ledger

| ID | Kind | Owned / Consumed / Deferred | Notes |
| --- | --- | --- | --- |
| RC-PR22-001 | Parent runtime contract | Consumed / Partial | SL-001 の old graph deletion を消費し、SL-002 は CI target / audit 側を扱う。final old graph PASS は cross-slice verification。 |
| RC-PR22-007 | Parent runtime contract | Consumed / Deferred | opt-in integration project を CI に含める前提を扱う。skip reason / production smoke body は SL-006 所有。 |
| RC-SL002-001 | Runtime contract | Owned | CI command target を `MultiCodingAgentFacade.sln` 基準へ切り替える contract。 |
| RC-SL002-002 | Runtime contract | Owned | scoped old-name audit の denylist / allowlist / failure semantics。 |
| RC-SL002-003 | Runtime contract | Consumed / Deferred | integration project inclusion と skip observability。SL-006 output が必要。 |
| RC-SL002-004 | Runtime contract | Owned | generated output / transitive dependency を stale source reference と区別する audit semantics。 |
| RC-SL002-005 | Runtime contract | Deferred | `.github/workflows/release.yml` を SL-002 scope に含めるか parent review が必要。 |
| IC-SL002-001 | Implementation contract | Owned | CI は旧 solution を実行対象にしない。 |
| IC-SL002-002 | Implementation contract | Consumed / Deferred | integration tests が credentials なし CI を壊さない skip design を SL-006 から消費。 |
| IC-SL002-003 | Implementation contract | Owned | denylist / allowlist を明示し、stale public API と migration / history を区別。 |
| IC-SL002-004 | Implementation contract | Owned | `bin/` / `obj/` generated output を audit 対象から除外。 |
| IC-SL002-005 | Implementation contract | Owned | audit failure は fail-fast。違反 term / path / allow reason を観測可能にする。 |
| IC-SL002-006 | Implementation contract | Deferred | migration note path は SL-003 未確定。仮 path で Done にしない。 |
| IC-SL002-007 | Implementation contract | Deferred | `release.yml` ownership は parent review requirement。 |
| TP-SL002-001 | Test point | Owned | `ci.yml` target が new solution で、旧 solution を参照しないこと。 |
| TP-SL002-002 | Test point | Owned / Deferred | `dotnet restore/build/test MultiCodingAgentFacade.sln`。SL-006 未完時の意味は partial。 |
| TP-SL002-003 | Test point | Owned | denylist positive detection。 |
| TP-SL002-004 | Test point | Consumed / Deferred | allowlist classification。SL-003 migration note location が必要。 |
| TP-SL002-005 | Test point | Owned | generated output exclusion。 |
| TP-SL002-006 | Test point | Consumed / Deferred | integration inclusion and skip-observability。SL-006 output が必要。 |
| TP-SL002-007 | Test point | Deferred | `release.yml` workflow scope classification。 |
| TP-SL002-008 | Test point | Deferred | final cross-slice verification handoff。 |
| XC-PR22-001 | Cross-slice contract | Consumed / Deferred | SL-001 の new solution graph / deleted old paths を消費。SL-001 result はこの prep 時点で未提供。 |
| XC-PR22-005 | Cross-slice contract | Consumed / Deferred | SL-006 の opt-in env vars / skip reason / production client path を消費。 |
| XC-PR22-006 | Cross-slice contract | Owned / Consumed / Deferred | SL-002 は audit rule / CI command を所有し、SL-001 deleted paths / SL-003 migration note / SL-006 test project を消費。final old-name PASS は cross-slice verification。 |

## Production binding requirements

- CI workflow は実際の repository workflow `.github/workflows/ci.yml` に binding される必要がある。local-only script や chat 上の audit 定義だけでは PASS 不可。
- CI restore / build / test は `MultiCodingAgentFacade.sln` または parent 承認済み solution target を実行する。旧 `MeAiUtility.sln` を残しても CI が見なければよい、という扱いは不可。
- audit は実 repository の source / tests / README / workflow に binding される必要がある。sample 文字列だけを検査する fake audit は不可。
- audit は generated output を除外しつつ、source / project / workflow / README の直接旧依存や旧 API 案内を検出する。
- `MultiCodingAgentFacade.IntegrationTests` は new solution graph 経由で CI test target に含まれる必要がある。ただし opt-in test 本体と skip reason は SL-006 から消費するため、SL-002 単独で production-binding smoke を Done にしない。
- `release.yml` の扱いは parent review gate で固定する。未分類の workflow old-name references を final PASS に混ぜない。

## Cross-slice risks to parent-review

- SL-001 未完了のまま SL-002 を実装すると、old solution / old project path が現存するため audit allowlist が過剰になりやすい。SL-002 実装は SL-001 result 後が安全。
- SL-003 migration note path が未確定のため、old-name allowlist の exact path を SL-002 が先に固定すると docs slice と衝突しうる。
- SL-006 の opt-in skip reason が未確定のため、`dotnet test MultiCodingAgentFacade.sln` が placeholder pass を含む false PASS になる risk がある。
- `.github/workflows/release.yml` は現在旧 solution / old package artifact を参照している。SL-002 が `ci.yml` だけを触ると AC-003 の workflow audit で残件になる可能性がある。
- `OpenAI` / `AzureOpenAI` は denylist term として必要だが、`OpenAI Codex App Server` のような legitimate product naming と衝突する。audit rule は単純な blanket grep では危険。
- `Microsoft.Extensions.AI` は generated output / transitive dependency と source-level public guidance を分けないと false positive / false negative の両方を起こす。

## Unresolved items

- SL-001 result: old solution / old project deletion result と deleted old project path list は未提供。
- SL-003 output: migration note path と old-name allowlist location は未確定。
- SL-006 output: opt-in environment variable names、skip reason、production client smoke path は未確定。
- CI target: parent Plan 既定は `MultiCodingAgentFacade.sln`。`.slnx` を使う場合は parent review approval が必要。
- Audit implementation mechanism: workflow inline shell、repo-local script、または audit test のどれにするか parent review が必要。
- `.github/workflows/release.yml`: SL-002 に含めるか、別 residual / out-of-scope とするか parent review が必要。
- `OpenAI` / `AzureOpenAI` denylist exact matching rule: false positive を避けるため review が必要。

## Stop condition

slice-prep はここで停止。実装、テスト作成、README 編集、workflow 編集、production code 編集は行っていない。Parent review gate が `Can implement now? = Yes` とし、SL-001 / SL-003 / SL-006 依存の扱いを明示するまで、SL-002 を slice-impl に渡してはいけない。

## Handoff to Agent Usage Ledger

- Run ID: pr22-remediation-SL-002-slice-prep-2026-06-07
- Phase: slice-prep
- Slice: SL-002
- Edit allowed: No
- Outcome: READY_FOR_PARENT_REVIEW。作成 artifact は `plans/pr22-review-remediation-slice-SL-002-prep.md`。編集 scope はこの prep artifact のみに限定。
