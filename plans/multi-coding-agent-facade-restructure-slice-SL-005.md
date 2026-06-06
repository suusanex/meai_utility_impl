# Slice Plan: SL-005 Docs, samples, CI, release zip

## Goal

README、migration note、samples、CI/release workflow を `MultiCodingAgentFacade` の目的、typed runtime clients、Release zip 配布に合わせる。

## Non-goals

- NuGet publish.
- GitHub repository rename.
- Runtime code changes.

## Parent requirements covered

FR-012, FR-014.

## Parent acceptance conditions covered

AC-005, AC-010, AC-012.

## Affected components / modules

- `README.md`
- sample project(s)
- `.github/workflows/ci.yml`
- `.github/workflows/release.yml`
- migration note

## Expected implementation scope

- Rewrite README around `MultiCodingAgentFacade`.
- Explain it is not MultiProvider, does not provide `IChatClient`, and does not provide OpenAI/Azure/OpenAI compatible providers.
- Add Copilot and Codex quickstarts.
- Add configuration, DI, request/response/streaming, diagnostics/security defaults, troubleshooting, integration test instructions.
- Add migration note from old API to typed runtime clients.
- Update CI to new solution.
- Update Release zip module list; do not add NuGet publishing scope.

## Cross-slice dependencies

Consumes final public API names, project paths, config sections from SL-001, SL-002, SL-003.

## Related Cross-slice Contract IDs

XC-004.

## Cross-slice contract excerpt

- XC ID: XC-004
- This slice role: Consumer
- Mechanism: docs/workflows reference production project and public API
- Required fields / state / identifiers: solution name, project paths, DI methods, config section names, Release zip module list
- Owned by this slice: docs and workflow references
- Consumed by this slice: outputs from SL-001..SL-003
- Deferred / unresolved fields: repository rename post-merge note

## Recommended next profile

`standard-slice`

## Immediate next agent

`slice-prep`

## Required inputs for next agent

- Implemented SL-001..SL-003 outputs
- Public API names
- Release zip decision
- Parent Plan and decomposition

## Stop condition

SL-005 implementation may stop when docs and workflows no longer instruct users to use `IChatClient`, old provider switching, OpenAI/Azure provider, or NuGet publish flow.
