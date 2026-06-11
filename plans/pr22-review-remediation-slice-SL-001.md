# Slice Plan: SL-001 旧構成削除と solution graph 固定

## Goal

旧 `MeAiUtility.sln` / `MeAiUtility.slnx`、旧 `src/MeAiUtility.MultiProvider*`、旧 `tests/MeAiUtility.MultiProvider*` を削除し、`MultiCodingAgentFacade.sln` / `.slnx` を solution graph の source of truth にする。

## Non-goals

- CI audit 実装。
- README 改訂。
- Copilot / Codex runtime model 変更。
- integration smoke 実装。

## Parent requirements covered

FR-001, FR-002.

## Parent acceptance conditions covered

AC-001, AC-002, AC-003 の structural 部分。

## Affected components / modules

- `MeAiUtility.sln`
- `MeAiUtility.slnx`
- `src/MeAiUtility.MultiProvider*`
- `tests/MeAiUtility.MultiProvider*`
- `MultiCodingAgentFacade.sln`
- `MultiCodingAgentFacade.slnx`
- `Directory.Build.props`

## Expected implementation scope

- 旧 solution / project / test directories を削除する。
- new solution graph の source / test project 参照を確認する。
- 旧 MEAI / OpenAI / AzureOpenAI dependency が source graph に残らないようにする。
- 削除により stale reference が出た場合は、SL-002 / SL-003 / SL-006 へ渡す unresolved item として記録する。

## Internal high-risk boundary candidates

- 旧 directory 削除後に workflow / README / sample / test project reference が stale になる。
- `MultiCodingAgentFacade.sln` が全 source / test project を含まず、CI false PASS になる。

## Cross-slice dependencies

SL-002 が CI target と audit scope として消費する。SL-003 が migration note で削除済み旧構成として説明する。

## Related Cross-slice Contract IDs

XC-PR22-001, XC-PR22-006.

## Cross-slice contract excerpt

- XC ID: XC-PR22-001
- This slice role: Producer
- Mechanism: solution / project reference graph
- Required fields / state / identifiers: `MultiCodingAgentFacade.sln`, `MultiCodingAgentFacade.slnx`, remaining source project paths, remaining test project paths, deleted old project paths
- Owned by this slice: old solution / old project deletion and new solution graph
- Consumed by this slice: parent Plan deletion requirements
- Deferred / unresolved fields: CI command and audit allowlist are owned by SL-002
- Authoritative source: `plans/pr22-review-remediation-slice-decomposition.md` の `Cross-slice contracts`

## Implementation-realization risks

Present. 削除対象が広く、source / test / docs / workflow の stale reference が連鎖しやすい。

## Recommended process profile

`standard-slice`

## Immediate next agent

`slice-prep`

## Required inputs for next agent

- `plans/pr22-review-remediation-plan.md`
- `plans/pr22-review-remediation-change-risk-triage.md`
- `plans/pr22-review-remediation-slice-decomposition.md`
- current solution / project files
- source / test inventory

## Stop condition

repository source graph no longer contains old solution / old project directories, and downstream slices can treat `MultiCodingAgentFacade.sln(x)` as authoritative. Production runtime behavior changes are outside this slice.
