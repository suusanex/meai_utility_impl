# Slice Plan: SL-004 Test migration and quality gates

## Goal

New API and project graph に合わせて tests を移植し、旧 dependency / old public name 混入を検出する quality gate を作る。

## Non-goals

- Runtime implementation itself.
- README rewrite.
- Manual real-runtime E2E execution.

## Parent requirements covered

FR-003, FR-004, FR-012.

## Parent acceptance conditions covered

AC-001, AC-002, AC-003, AC-004, AC-008, AC-009, AC-011.

## Affected components / modules

- old `tests/MeAiUtility.MultiProvider.*`
- new `tests/MultiCodingAgentFacade.*`
- `.github/workflows/ci.yml`
- old-name/dependency audit script or test

## Expected implementation scope

- Remove OpenAI/Azure/OpenAI compatible/MultiProvider/MEAI tests.
- Migrate Copilot and Codex tests to typed APIs.
- Keep fake/stub transport tests CI-safe.
- Ensure opt-in integration tests skip without credentials/runtime.
- Add old-name/dependency denylist check with explicit allowlist for migration/history docs if needed.

## Cross-slice dependencies

Consumes production public APIs from SL-001, SL-002, SL-003.

## Related Cross-slice Contract IDs

XC-004, XC-005.

## Cross-slice contract excerpt

- XC ID: XC-005
- This slice role: Consumer
- Mechanism: tests assert production implementation and DI entrypoints
- Required fields / state / identifiers: public type names, DI extension names, config sections, fake transport/wrapper binding, old dependency denylist
- Owned by this slice: quality gates and test mapping
- Consumed by this slice: production types from SL-001..SL-003
- Deferred / unresolved fields: manual real runtime validation

## Recommended next profile

`standard-slice`

## Immediate next agent

`slice-prep`

## Required inputs for next agent

- Implemented SL-001..SL-003 outputs
- Parent Plan and decomposition
- current tests and workflow files

## Stop condition

SL-004 implementation may stop when automated tests exercise new APIs and old dependency/name checks fail on stale public source.
