# codex-plan-coverage

Compatibility wrapper for older instructions that still name `codex-plan-coverage`.
Prefer `codex-first-cost-router` for new work.

Use this skill only by forwarding into `codex-first-cost-router`, preserving the same source of truth and state artifact.

## Workflow

1. Read the user's request, issue body, PR comment, branch state, or supplied artifact as the source of truth.
2. Locate or create `plans/<slug>/codex-first-state.md`.
3. Let `codex-first-cost-router` choose the next gate and model tier.
4. In the Plan gate, record behavior expansion decision and Plan readiness. If source requirements need black-box behavior cases, route to `black-box-behavior-spec-kernel` and then back to `plan-kernel` for Case-to-Plan mapping.
5. Use existing plan / triage / contract / implementation / verification agents only when the router says that gate is appropriate.
6. Preserve Routing Plan, Edit Permission, Agent Usage Ledger, and DelegationCompliance.
7. Record every gate result back to the state artifact.

## Rules

- Do not ask the user to choose this skill, an agent, `full-coverage`, or a model tier.
- Do not implement before a bounded Plan or equivalent READY decision exists.
- Do not run risk triage or select `full-coverage` unless Plan readiness is `ReadyForRiskTriage`.
- Do not treat `NeedsPlanBehaviorExpansion` as a full-coverage candidate.
- Do not use runtime-contract artifacts as the whole implementation spec.
- Do not treat fake, stub, mock, or in-memory success as production readiness.
- Do not close if `ManualVerificationRequired`, `NeedsHumanDecision`, or `NeedsHigherModelReview` remains.
- Do not close if required delegation evidence is missing or DelegationCompliance is FAIL.
- Record residual work instead of looping until everything looks green.

## Output

Return a concise result with:

- source artifacts used
- gates executed
- state artifact path
- Plan readiness and behavior spec artifact when relevant
- selected model tier
- implementation scope
- verification result
- residual work
- whether closure is safe
