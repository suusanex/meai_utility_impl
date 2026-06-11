# Slice Plan: SL-003 README / migration note / sample

## Goal

README と sample を `MultiCodingAgentFacade` の typed runtime API に合わせ、旧 MultiProvider からの migration note、Copilot / Codex quickstart、DI、appsettings、streaming / non-streaming、安全上の注意を整える。

## Non-goals

- runtime API 実装。
- CI audit 実装。
- real runtime E2E 実行。
- NuGet publish 方針変更。

## Parent requirements covered

FR-003, FR-006, FR-011, FR-012.

## Parent acceptance conditions covered

AC-004, AC-007, AC-013, AC-014, AC-015 の docs / sample 部分。

## Affected components / modules

- `README.md`
- `src/MultiCodingAgentFacade.Samples/Program.cs`
- sample project file
- migration note path if separated

## Expected implementation scope

- old `IChatClient` / provider switching docs を削除する。
- Copilot / Codex quickstarts、DI 登録例、appsettings 例を追加する。
- non-streaming / streaming examples を sample と docs に揃える。
- sandbox / approval / working directory / skills / disabled skills の注意を説明する。
- AdvancedOptions は typed property を主 API とし、残す場合は unstable / escape hatch と明記する。
- provider override と permission handling の説明を runtime implementation に合わせる。

## Internal high-risk boundary candidates

- docs が runtime slice の未確定 API を先取りして stale になる。
- sample が marker-only のまま残る。
- migration note の旧名説明が SL-002 audit と衝突する。

## Cross-slice dependencies

SL-004 / SL-005 の public API と policy を消費する。SL-002 の scoped audit allowlist と調整する。

## Related Cross-slice Contract IDs

XC-PR22-002, XC-PR22-003, XC-PR22-004, XC-PR22-006.

## Cross-slice contract excerpt

- XC ID: XC-PR22-002
- This slice role: Consumer/Producer
- Mechanism: README / sample compile-time references
- Required fields / state / identifiers: public client names, request/response/streaming type names, DI extension names, config sections, migration note allowed old terms
- Owned by this slice: docs and sample projection
- Consumed by this slice: runtime API and policy outputs from SL-004 / SL-005, solution graph from SL-001
- Deferred / unresolved fields: public API naming correction if runtime implementation changes
- Authoritative source: `plans/pr22-review-remediation-slice-decomposition.md` の `Cross-slice contracts`

## Implementation-realization risks

Present. Current README describes old `MeAiUtility.MultiProvider`, and current sample only prints runtime markers.

## Recommended process profile

`standard-slice`

## Immediate next agent

`slice-prep`

## Required inputs for next agent

- `plans/pr22-review-remediation-plan.md`
- `plans/pr22-review-remediation-change-risk-triage.md`
- `plans/pr22-review-remediation-slice-decomposition.md`
- SL-004 / SL-005 outputs or prepared public API contracts
- SL-002 audit allowlist expectations
- current README and sample

## Stop condition

README and sample no longer instruct users to use `IChatClient`, old provider switching, OpenAI/Azure provider, or marker-only sample flow. Final docs must consume runtime outputs rather than inventing unimplemented fields.
