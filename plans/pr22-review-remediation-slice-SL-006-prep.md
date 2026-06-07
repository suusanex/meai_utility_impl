# Slice Preparation Result: SL-006

## Verdict

- Status: READY_FOR_PARENT_REVIEW
- Reason: SL-006 の slice-prep artifact として、per-slice risk、implementation contract draft、runtime contract draft、test design draft を作成した。実装開始可否は別判定であり、SL-004 / SL-005 の public runtime API 出力、SL-002 の CI 取り込み条件、SL-003 の README 手順が未確定のため、parent review gate では `Can implement now? = No` から始めるのが安全。

## Agent metadata

- Agent type: slice-prep
- Model: gpt-5.4
- Reasoning effort: medium
- Parent authorization artifact: user request for PR22 remediation slice-prep, `plans/pr22-review-remediation-slice-SL-006.md`
- Delegation evidence: SL-006 のみを対象にし、production code / tests / README / workflow を編集しない条件で `plans/pr22-review-remediation-slice-SL-006-prep.md` の作成を委譲された。

## Generated / drafted artifacts

- Per-slice change-risk-triage:
  - Recommended profile: `standard-slice`
  - implementation-realization risk: Present
  - Reasoning: placeholder `Assert.True(true)` を opt-in integration smoke に置き換えるだけに見えても、実際には xUnit skip semantics、CI credentials なし実行、secret-safe output、production DI/client construction、Copilot SDK と Codex App Server の real runtime boundary を同時に扱う。fake-only success や source evidence のない env var 名確定は parent AC-006 / AC-015 の false PASS につながる。
  - High-risk boundaries:
    - disabled opt-in path -> CI test result: skip reason が観測できず placeholder pass になる risk。
    - enabled opt-in path -> production client path: fake / stub を通って real binding gap を見逃す risk。
    - runtime credentials / token / config -> test output: secret leakage risk。
    - xUnit v2.9.3 project -> dynamic skip behavior: NUnit `Assert.Ignore` の旧先例をそのまま使えない risk。
    - SL-004 / SL-005 API -> integration smoke assertions: public response field が未確定のままテストが stale になる risk。
  - Selected RC IDs: RC-PR22-007, RC-PR22-003(consumed), RC-PR22-004(consumed)
  - Next action: parent review gate で SL-004 / SL-005 output availability と opt-in env var naming decision を確認してから implementation authorization を出す。

- Implementation-contract-kernel:
  - Contract ID: IC-SL006-001
  - Scope: `tests/MultiCodingAgentFacade.IntegrationTests` の placeholder を廃止し、credentials なし CI で理由付き skip、opt-in enabled 時に production DI/client construction path を使う Copilot / Codex smoke を追加する実装契約。
  - Participants:
    - Integration test project: `tests/MultiCodingAgentFacade.IntegrationTests/MultiCodingAgentFacade.IntegrationTests.csproj`
    - Current placeholder: `OptInIntegrationPlaceholderTests.cs`
    - Copilot production DI: `AddGitHubCopilotAgentRuntime`, `GitHubCopilotAgentClient`, `GitHubCopilotSdkWrapper`, `ICopilotSdkWrapper`
    - Codex production DI: `AddCodexAppServerAgentRuntime`, `CodexAppServerAgentClient`, `SystemCodexProcessRunner`, `DefaultCodexTransportFactory`, `FileCodexThreadStore`
    - Consumed runtime API outputs: SL-004 Copilot response / permission / provider policy, SL-005 Codex turn response / status / diagnostics model
  - Required behavior:
    - `Assert.True(true)` だけの placeholder pass を残さない。
    - opt-in disabled または required runtime/config がない場合、test output から reason が分かる skip / not-run equivalent にする。
    - enabled smoke は production service extension または production constructor path を通し、integration test 専用 fake / stub を success path に注入しない。
    - smoke prompt は短く、外部状態を変更しない内容に限定する。
    - timeout は bounded にし、長時間 hang を避ける。
    - output に token、bearer、API key、config dir の秘匿値、prompt 以外の秘密情報を出さない。
    - 実 Copilot / 実 Codex の実行 evidence は環境依存のため ManualOnly として扱える。ただし opt-in enabled 時に production binding を使わない success は不可。
  - Explicit non-requirements:
    - real runtime を通常 CI で常時実行しない。
    - SL-004 / SL-005 の runtime API や response model をこの slice で実装しない。
    - README 全面改訂をこの slice で行わない。
  - Source evidence:
    - integration project は xUnit `2.9.3` と `xunit.runner.visualstudio` `3.1.4` を参照している。
    - current placeholder は `OptInIntegrationPlaceholderTests.RealRuntimeIntegrationIsDeferredToOptInTests` の `Assert.True(true)`。
    - production DI extension names は現行 source に `AddGitHubCopilotAgentRuntime` と `AddCodexAppServerAgentRuntime` として存在する。
    - 旧 `MeAiUtility.MultiProvider.IntegrationTests` に opt-in E2E の先例はあるが、旧 namespace / NUnit / `MEAI_*` env var 名なので、そのまま authoritative value として採用しない。

- Implementation-contract-review-kernel:
  - Review requirement: Required
  - Reason: skip semantics、env var naming、production path 判定、secret masking、SL-004 / SL-005 output consumption は non-trivial。特に xUnit v2.9.3 で選ぶ skip mechanism が CI runner 上で本当に skip reason を観測可能にするか、implementation 前に親レビューまたは slice-impl の handoff review で確認する必要がある。
  - Review checklist:
    - opt-in env var / config key の exact names は source evidence または parent-approved decision として記録されているか。
    - Copilot smoke は `ICopilotSdkWrapper` を fake に差し替えず、production `GitHubCopilotSdkWrapper` 経由になっているか。
    - Codex smoke は fake transport / stub thread store を success path に使わず、production app-server transport pathを通るか。ただし isolated no-credential path の skip 判定は production runtime 起動前でよい。
    - disabled path は PASS ではなく skip / explicit not-run として見えるか。
    - stdout / test output に secret が含まれないか。
    - SL-004 / SL-005 がまだ未実装の field を SL-006 が fabricated assertion で固定していないか。

- Runtime-contract-kernel:
  - Contract ID: RC-PR22-007-SL006
  - Boundary: opt-in integration tests -> production DI/client path -> optional real Copilot SDK / Codex App Server runtime
  - Runtime participants:
    - xUnit test runner in CI
    - `MultiCodingAgentFacade.IntegrationTests`
    - `GitHubCopilotAgentClient` and production `GitHubCopilotSdkWrapper`
    - `CodexAppServerAgentClient` and production stdio app-server transport
    - external Copilot / Codex runtime, only when opt-in configuration is present
  - Required fields / state / identifiers:
    - opt-in environment variable names: Deferred, parent-approved exact names required before implementation
    - skip reason: Owned by SL-006, must be observable without credentials
    - production client type: `GitHubCopilotAgentClient`, `CodexAppServerAgentClient`
    - config section: current evidence is `MultiCodingAgentFacade:GitHubCopilot` and `MultiCodingAgentFacade:CodexAppServer`
    - secret masking: Owned by SL-006 for test output discipline
    - smoke prompt / timeout: Owned by SL-006, must be short and bounded
  - Error / recovery expectation:
    - missing opt-in/config/runtime -> skip with reason, not placeholder pass
    - opt-in enabled but runtime failure -> visible test failure with secret-safe diagnostic summary
    - cancellation/timeout -> visible failure or controlled timeout, not hang
  - Verification requirement:
    - no placeholder `Assert.True(true)` remains in integration project
    - credentials absent path is observable as skip / not-run with reason
    - enabled path constructs production clients through public DI or production constructor path
    - real runtime execution evidence can remain ManualOnly if environment is unavailable
  - Status: Drafted / Deferred for implementation authorization until parent review

- Test-design-kernel:
  - TP-SL006-001: placeholder removal
    - Verify integration tests no longer contain placeholder pass such as `Assert.True(true)` for real runtime deferral.
    - Evidence target: `tests/MultiCodingAgentFacade.IntegrationTests`.
  - TP-SL006-002: Copilot disabled opt-in skip reason
    - With Copilot opt-in not enabled, Copilot smoke reports a clear skip / not-run reason and does not call real SDK.
    - Exact env var name is Deferred pending parent-approved naming.
  - TP-SL006-003: Codex disabled opt-in skip reason
    - With Codex opt-in not enabled, Codex smoke reports a clear skip / not-run reason and does not launch Codex app-server.
    - Exact env var name is Deferred pending parent-approved naming.
  - TP-SL006-004: Copilot production binding
    - When opt-in is enabled and required config exists, the Copilot smoke constructs `GitHubCopilotAgentClient` via `AddGitHubCopilotAgentRuntime` or equivalent production path and does not inject fake `ICopilotSdkWrapper`.
    - Real execution may be ManualOnly if credentials/runtime are unavailable in CI.
  - TP-SL006-005: Codex production binding
    - When opt-in is enabled and required config exists, the Codex smoke constructs `CodexAppServerAgentClient` via `AddCodexAppServerAgentRuntime` or equivalent production path and does not inject fake transport for success.
    - Real execution may be ManualOnly if app-server/runtime is unavailable in CI.
  - TP-SL006-006: secret-safe output
    - Test output includes skip/failure diagnostics without echoing token/API key/bearer values or full secret-bearing config.
  - TP-SL006-007: consumed response metadata compatibility
    - Once SL-004 / SL-005 are available, smoke assertions use public response fields without inventing fields. If only minimal text response is available at implementation time, parent review must decide whether to block or assert only production binding.
  - TP-SL006-008: CI credentials-free compatibility
    - Normal `dotnet test` of the integration project succeeds without credentials by reporting opt-in tests as skipped / not-run equivalent, not as placeholder PASS.

## Bounded parent Plan pass / Guardrail Focus

- Bounded parent Plan pass:
  - FR-005: `tests/MultiCodingAgentFacade.IntegrationTests/OptInIntegrationPlaceholderTests.cs` の placeholder を廃止し、設定や環境変数がない場合は skip reason を示す Copilot SDK / Codex App Server opt-in smoke test に置き換える。
  - AC-006: integration tests は placeholder `Assert.True(true)` を含まず、opt-in 条件不足時に skip reason を観測できる。
  - AC-015: Required fixes before merge の integration / production binding 部分が、実装済み、Deferred、ManualOnly のいずれかとして追跡できる。
- Guardrail Focus:
  - RC-PR22-007 を slice-local primary contract とし、RC-PR22-003 / RC-PR22-004 は SL-004 / SL-005 から消費する。
  - XC-PR22-005 は SL-006 が production-binding smoke と skip reason を owning し、real runtime execution evidence は環境がなければ ManualOnly として残す。
  - XC-PR22-003 / XC-PR22-004 の runtime metadata field は SL-006 内で補完しない。
  - cross-slice verification 前に XC を Done 扱いにしない。

## Non-goals

- always-on real Copilot / real Codex CI。
- Copilot / Codex runtime API 実装。
- README 全面改訂。
- SL-004 / SL-005 の public response fields、permission policy、provider override validation、Codex result model の確定。
- old-name audit / CI workflow 実装。
- repository rename、NuGet publish、package archive 設計。

## RC / TP / XC ledger

| ID | Kind | Owned / Consumed / Deferred | Notes |
| --- | --- | --- | --- |
| RC-PR22-007 | Runtime Contract | Owned | opt-in integration と production-binding smoke の primary parent RC。 |
| RC-PR22-003 | Runtime Contract | Consumed | Copilot API / metadata / permission / provider policy は SL-004 から消費する。 |
| RC-PR22-004 | Runtime Contract | Consumed | Codex result / status / diagnostics model は SL-005 から消費する。 |
| XC-PR22-003 | Cross-slice Contract | Consumed | Copilot production smoke は SL-004 の public API と policy に従う。 |
| XC-PR22-004 | Cross-slice Contract | Consumed | Codex production smoke は SL-005 の public API と result model に従う。 |
| XC-PR22-005 | Cross-slice Contract | Owned / Deferred | skip reason と production-binding smoke は owning。real runtime execution evidence と final consistency は Deferred。 |
| TP-SL006-001 | Test Point | Owned | placeholder `Assert.True(true)` removal。 |
| TP-SL006-002 | Test Point | Owned / Deferred | Copilot disabled opt-in skip。env var exact name は Deferred。 |
| TP-SL006-003 | Test Point | Owned / Deferred | Codex disabled opt-in skip。env var exact name は Deferred。 |
| TP-SL006-004 | Test Point | Owned / Consumed | Copilot production binding。SL-004 output を消費する。 |
| TP-SL006-005 | Test Point | Owned / Consumed | Codex production binding。SL-005 output を消費する。 |
| TP-SL006-006 | Test Point | Owned | secret-safe output。 |
| TP-SL006-007 | Test Point | Consumed / Deferred | response metadata assertion は SL-004 / SL-005 の確定後。 |
| TP-SL006-008 | Test Point | Owned / Consumed | credentials-free CI compatibility。SL-002 CI target が消費する。 |

## Production binding requirements

- Integration smoke は `MultiCodingAgentFacade.IntegrationTests` から production package/project references を使う。
- Copilot enabled path は `AddGitHubCopilotAgentRuntime` または production `GitHubCopilotAgentClient` construction を通し、success path で fake `ICopilotSdkWrapper` を注入しない。
- Codex enabled path は `AddCodexAppServerAgentRuntime` または production `CodexAppServerAgentClient` construction を通し、success path で fake transport / stub thread store を注入しない。
- disabled path は production runtime を起動しなくてよいが、skip reason が observable であること。
- smoke prompt は外部状態を変更しない短い内容にする。
- opt-in enabled failure は secret-safe diagnostic を出して fail する。credential missing と opt-in disabled は区別する。
- exact opt-in env var names はこの prep では fabricated value として固定しない。parent review または implementation contract review で確定する。

## Cross-slice risks to parent-review

- SL-004 / SL-005 の implementation result がない状態では、SL-006 が assertions に使う response fields を確定できない。
- SL-002 が integration test project を CI に含めるため、SL-006 の disabled path は credentials なしで deterministic に skip / not-run にならないと CI を壊す。
- SL-003 が README integration 手順を説明するため、SL-006 の env var names / skip reason 文言 / ManualOnly扱いと不一致になる risk がある。
- 旧 integration tests には opt-in E2E の参考実装があるが、旧 `MeAiUtility.MultiProvider` namespace、NUnit attributes、`MEAI_*` env var を含むため、新実装の source of truth としてそのまま扱うと旧 surface 残存になる。
- xUnit v2.9.3 で採用する dynamic skip mechanism が runner により skip reason を表示できるか、implementation 前に確認が必要。

## Unresolved items

- SL-004 / SL-005 の public runtime API outputs が未提供。SL-006 implementation authorization はこれらの確定後に行うこと。
- opt-in environment variable exact names は未確定。旧 `MEAI_*` 名の流用は parent-approved decision がない限り不可。
- xUnit runtime skip mechanism の exact implementation は未確定。xUnit v2.9.3 / runner 3.1.4 で compile し、CI result として skip reason を観測できる必要がある。
- README integration instructions は SL-003 の ownership。SL-006 は手順の source of truth を勝手に更新しない。
- real Copilot / real Codex smoke の実実行 evidence は環境依存で ManualOnly。実行不可そのものは parent PASS を妨げないが、skip reason と production binding design は必須。

## Stop condition

SL-006 の slice-prep として、per-slice change-risk-triage、implementation-contract-kernel draft、implementation-contract-review requirement、runtime-contract-kernel draft、test-design-kernel draft をこの artifact に記録して停止する。実装、テスト作成、README / workflow / source code 編集、implementation-handoff-review は行わない。

## Handoff to Agent Usage Ledger

- Run ID: SL-006-prep-2026-06-07
- Phase: slice-prep
- Slice: SL-006
- Edit allowed: No
- Outcome: READY_FOR_PARENT_REVIEW; artifact created at `plans/pr22-review-remediation-slice-SL-006-prep.md`; production code / tests / README / workflow edits not performed.
