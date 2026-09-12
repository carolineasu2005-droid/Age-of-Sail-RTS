# Development Workflow Gate

This workflow is the required review gate for future Codex implementation
tasks. It keeps architecture decisions, registered contracts, implementation,
tests, and Unity validation synchronized.

## Standard development sequence

```text
Architecture Decision
→ Registry Update
→ Codex Implementation
→ Automated Tests
→ Registry Diff Review
→ Manual Unity Test
→ Commit
```

Registry Update and implementation may occur in the same logical change. This
sequence defines responsibility and review order; it does not require separate
commits for each stage.

### 1. Architecture Decision

- Define the requested behavior and system ownership before editing.
- Identify existing Sources of Truth, writers, units, coordinate spaces, and
  lifecycle boundaries.
- Resolve conflicts with existing Registry entries explicitly. Do not silently
  create a second authority.
- Keep deferred and no-go systems outside the change.

### 2. Registry Update

- Identify every registered-category contract added, removed, renamed, or
  changed in semantics, unit, space, ownership, or lifecycle.
- Update the corresponding Registry file in the same logical change.
- Add stable IDs and evidence for new contracts; never reuse an old ID.
- Keep unimplemented contracts `Planned`. Promote an entry to `Active` only
  when its implementation and evidence exist.
- If no Registry-visible contract changes, record exactly:

```text
Registry impact: none.
```

### 3. Codex Implementation

- Implement only the approved scope and contracts.
- Preserve Source of Truth and Write Ownership boundaries.
- Do not expose speculative APIs or serialize implementation details merely
  because they might be useful later.
- Combat and Combat Art may read the registered Movement Root contracts but
  must not write Movement Root position or Heading.

### 4. Automated Tests

- Add or update the smallest relevant executable coverage for behavior changes.
- Run the affected existing regression suites as well as new tests.
- Record the actual Unity Test Runner result; compilation alone is insufficient.
- Documentation-only changes do not require artificial Unity tests.

### 5. Registry Diff Review

Before manual validation, review implementation and Registry diffs together:

- every changed cross-system contract is represented;
- symbol names and access match source;
- units and coordinate spaces match calculations and storage;
- Source of Truth and Writable By agree with actual readers/writers;
- lifecycle and update timing match source;
- Active entries have source/prefab/test evidence as applicable;
- Planned entries are not presented as implemented; and
- Deprecated/Removed entries retain stable IDs and replacement history.

If source and an existing Active entry disagree, current source is authoritative
for verification. Correct the Registry documentation; do not change behavior
merely to make code match stale documentation unless behavior change is itself
approved in scope.

### 6. Manual Unity Test

- Perform the task's specified Unity scene/prefab interaction checks.
- Record the scene/prefab, steps, observed result, and any limitation.
- Do not claim manual validation when Unity was not opened and exercised.

### 7. Commit

Commit only after the implementation, automated-test evidence, Registry diff,
and required manual validation are reviewable. Use a commit message describing
the logical change rather than an individual workflow step.

## Required future Codex task prompt

Every future Codex task prompt must contain all of these sections:

```text
Goal

Scope

Existing Contracts

Do Not Change

Registry Requirement

Acceptance Criteria

Automated Tests

Manual Unity Test

Expected Files

Expected Result
```

The `Existing Contracts` section must cite relevant Registry IDs and state the
Source of Truth and Write Ownership being preserved. `Expected Files` is a
scope boundary, not permission to modify unrelated dirty-worktree files.

The `Registry Requirement` section must include:

```text
Registry Requirement

If this task adds, removes, renames, changes the semantics of,
changes the unit/coordinate space of, or changes the ownership/lifecycle of any:

- public/internal cross-system API
- serialized configuration field used as a contract
- shared runtime state
- event/callback
- ScriptableObject field used across systems
- prefab hierarchy contract
- Transform reference
- socket
- layer/tag/query-filter convention
- unit or coordinate-space convention

update the corresponding file under Docs/Architecture/Registry
in the same logical change.

Do not register function-local implementation details.

If no registry-visible contract changed, explicitly report:
"Registry impact: none."
```

## Required completion report

Every completion report must include all of these sections:

```text
Summary

Files Changed

Behavior Change

Tests

Manual Test Required

Registry Impact

Known Limitations

Deferred / No-Go Items
```

The report must distinguish tests that passed, failed, or were not run. Under
`Manual Test Required`, state the exact remaining Unity validation or `None`
with justification. Under `Registry Impact`, use this format when contracts
changed:

```text
Registry Impact

Added:
Changed:
Deprecated:
Removed:
```

When no Registry-visible contract changed, retain the section and write:

```text
Registry Impact

Registry impact: none.
```

## Automated-test policy

The repository already has EditMode and PlayMode regression suites. Their
assembly definitions are:

- `Assets/Assets/Game/Tests/EditMode/AgeOfSailRTS.EditModeTests.asmdef`
- `Assets/Assets/Game/Tests/PlayMode/AgeOfSailRTS.PlayModeTests.asmdef`

Run the complete suites from **Window > General > Test Runner**, using **Run
All** in both the EditMode and PlayMode tabs. Keep the Console visible and
investigate failures before accepting a gameplay change, as required by
`Assets/Assets/Game/Tests/README.md`.

### Prefer EditMode tests for

- deterministic math;
- geometry;
- state transitions;
- scoring;
- angle/range calculations;
- dispersion sampling;
- reload logic; and
- ownership/data-contract logic where executable.

### Prefer PlayMode tests for

- runtime integration;
- movement + combat coexistence;
- collider interaction;
- projectile flight;
- VFX spawn;
- formation blocking; and
- AI firing behavior.

Test choice follows behavior, not folder convenience. A change can require both
modes. Relevant existing regression tests must be run before accepting gameplay
changes.

Always distinguish:

```text
Compile success
!=
Unity Test Runner PASS
```

Compilation verifies that assemblies build. A Unity Test Runner PASS requires
the relevant EditMode and/or PlayMode tests to execute successfully and report
no failures.

## Manual Unity-test policy

Automated coverage does not replace manual Unity validation. Manual tests are
still required for:

- spatial correctness;
- visual alignment;
- interaction feel;
- prefab hierarchy integration; and
- RTS-camera readability.

The task prompt must specify the intended manual scene/prefab setup and pass
criteria. If the environment cannot run the manual test, the completion report
must leave it explicitly outstanding.

## Phase 0.5 verification record

Verified against the current local repository at source revision
`79f61c7183442ce1db35d08fbad4095047060bef` on 2026-09-12. This is a
documentation/source consistency review, not a Unity Test Runner execution.

| Verification item | Authoritative evidence | Result |
|---|---|---|
| Required Phase 0.5 files | `REGISTRY_RULES.md`, `PROJECT_CONVENTIONS.md`, `Movement.registry.md`, `CombatArt.registry.md`, `Combat.registry.md`, and this workflow file | PASS |
| Root position ownership | `ShipSailingSpeed.MoveShip` writes Root `transform.position`; `MOV-STA-001` | PASS |
| Root Heading ownership | `ShipTurning.Update` rotates the Root; `ShipHeadingController` sends rudder commands and does not write position; `MOV-STA-002`, `MOV-API-002`–`005` | PASS |
| CurrentSpeed semantics | `ShipSailingSpeed.UpdateCurrentSpeed` and `ApplyTurningDrag`; `MOV-STA-003` distinguishes the forward scalar from ground-motion magnitude | PASS |
| ActualVelocity semantics | `ShipSailingSpeed.MoveShip` composes `ForwardVelocity + LeewayVelocity`; `MOV-STA-011`–`013` | PASS |
| CourseSpeed / CourseHeading | `courseSpeed = actualVelocity.magnitude`; CourseHeading uses horizontal ActualVelocity and falls back to Heading near zero; `MOV-STA-014`–`016` | PASS |
| Wind From / Wind Flow | `GlobalWind.WindFromDirection` derives from `windFromDegrees`; `WindFlowDirection` is its negation; `MOV-FLD-001`, `MOV-STA-026`, `MOV-STA-027` | PASS |
| ShipMovementProfile ownership | Profile asset is authored configuration; `ShipMovementProfileController.Awake/ApplyProfile` copies values into runtime components; `MOV-SO-001`–`009` | PASS |
| ManeuverPlanner state/commands | Planner declarations and command/delegation/cancel paths match `MOV-STA-019`–`023` and `MOV-API-006`–`011` | PASS |
| DestinationController state/commands | Destination properties, set/clear, arrival, and planner cancellation paths match `MOV-STA-024`–`025` and `MOV-API-012`–`013` | PASS |
| Formation Root dependency | `FormationGeometrySnapshot` reads each `ShipDestinationController.transform.position` and `.forward` | PASS |
| Combat Art implementation status | Every ART Registry entry is `Planned`; no ART entry is marked Active | PASS |
| Combat implementation status | Every CMB Registry entry is `Planned`; no CMB entry is marked Active | PASS |
| Existing test structure | EditMode and PlayMode test assemblies, fixtures, and `Tests/README.md` were inspected | PASS |

### Verification correction

`MOV-STA-007` was clarified during this review: its signed relative-wind value
is a scalar angle derived from World-space directions about World +Y. This is a
documentation-only coordinate-space clarification; runtime semantics and code
are unchanged.

No other mismatch was found in the Active Movement contracts. Future source
changes invalidate this snapshot only where they alter a registered contract;
such changes must update the Registry in the same logical change.
