# Architecture Interface Registry Rules

## Purpose

The Architecture Interface Registry records cross-system contracts that other
scripts, prefabs, editor tools, tests, or future systems are allowed to depend
on.

**The goal is NOT to catalogue every variable.**
**The goal is to record what other systems are allowed to rely on.**

Registry entries describe both the shape of a contract and its semantics. A
symbol being visible in source is not, by itself, permission to depend on it.
Conversely, a prefab hierarchy, unit, coordinate space, lifecycle rule, or
ownership boundary can be a contract even when it is not represented by a
public C# symbol.

## Must-register categories

Register a contract when it belongs to any of these categories and is relied
on, or is intended to be relied on, outside its defining implementation:

- public APIs used across components or systems;
- internal APIs used across classes or components;
- serialized configuration fields when they form a prefab or data contract;
- shared runtime state;
- events and callbacks;
- ScriptableObject data used across systems;
- prefab hierarchy contracts;
- Transform references;
- sockets;
- unit conventions;
- coordinate-space conventions;
- layer, tag, and query-filter conventions;
- Source of Truth declarations;
- Write Ownership declarations;
- lifecycle and update semantics; and
- Deprecated / Replacement relationships.

## Normally do NOT register

Do not normally register:

- function-local variables;
- one-off temporary collections;
- private implementation helpers with no external dependency;
- pure implementation details; or
- runtime debug fields that no other system, test, or tool depends on.

If an item in this list later becomes something another system is allowed to
rely on, it has become a contract and must be registered at that point.

## Stable IDs

Every entry has an immutable ID in this format:

```text
<SYSTEM>-<KIND>-<NUMBER>
```

`<NUMBER>` is a zero-padded, monotonically assigned number within the selected
system and kind, for example `MOV-API-001`. IDs are never reused, including
after an entry is Removed. Renaming a symbol does not rename its ID when the
contract retains its identity.

### System prefixes

| Prefix | System |
|---|---|
| `PRJ` | Project-wide convention |
| `MOV` | Movement / sailing / navigation |
| `ART` | Combat Art / ship spatial integration |
| `CMB` | Combat Gameplay |

### Kind prefixes

| Prefix | Kind |
|---|---|
| `CONV` | Convention |
| `API` | Cross-class or cross-system API |
| `FLD` | Serialized/configuration field contract |
| `STA` | Shared runtime state |
| `EVT` | Event or callback |
| `SO` | ScriptableObject data contract |
| `PFB` | Prefab hierarchy contract |
| `REF` | Transform or other object reference |
| `SCK` | Socket contract |
| `LYR` | Layer, tag, or query-filter contract |
| `TOOL` | Editor/tooling contract |

Choose the system that owns the contract and the most specific applicable
kind. Related aspects may be described in one entry when they are inseparable;
otherwise give independently changeable contracts separate IDs.

## Status values

| Status | Meaning |
|---|---|
| `Planned` | Approved contract that is not yet available for consumers. |
| `Active` | Implemented contract on which consumers may rely. |
| `Deprecated` | Still available for compatibility, but new consumers must use the Replacement. |
| `Removed` | No longer available; retained in the Registry for history and ID stability. |

Planned is not equivalent to implemented. Deprecated entries identify a
Replacement whenever one exists. Removed entries remain documented and must
not silently disappear from Registry history.

## Entry fields

Each entry must provide the following semantic fields when applicable. Use
`N/A` with a short reason when a field does not apply; do not leave ambiguity by
silently omitting meaningful fields.

| Field | Required meaning |
|---|---|
| ID | Stable Registry ID. |
| Symbol / Contract Name | Exact code symbol or a precise human-readable contract name. |
| Kind | Contract classification matching the ID kind. |
| Owner | System/component responsible for defining and maintaining the contract. |
| Purpose / Meaning | What the contract represents and what consumers may rely on. |
| Access | Visibility or supported access path. |
| Type | C# type, asset/prefab type, hierarchy shape, or conceptual type. |
| Unit | Explicit unit; use an approved unit or `Scalar / N/A`. |
| Coordinate Space | Explicit space; use an approved space or `Scalar / N/A`. |
| Source of Truth | The authoritative representation from which other values are derived. |
| Writable By | The exclusive or enumerated writers permitted to mutate the contract. |
| Known Consumers | Known dependent systems, components, tools, prefabs, or tests. |
| Lifecycle / Update Timing | Creation, application, update, reset, and destruction timing. |
| Side Effects | Observable effects of reading, invoking, or writing the contract. |
| Status | `Planned`, `Active`, `Deprecated`, or `Removed`. |
| Do Not Interpret As | Explicitly excluded meanings that could otherwise be confused with the contract. |
| Replacement | Successor ID/symbol for Deprecated or Removed entries, or `N/A`. |
| Evidence | Source, prefab, test, or architecture references that substantiate the entry. |

Evidence is a verification aid, not the identity of the contract. Moving a
source file does not by itself create a new Registry ID.

## Source of Truth

Source of Truth identifies the single authoritative representation of a fact
at a given point in its lifecycle. Caches, adapters, UI displays, animation
parameters, telemetry, derived properties, and copied command payloads are not
co-authoritative unless the entry explicitly defines a handoff of authority.

Consumers must read the stated Source of Truth, or a documented derived access
path, rather than infer the fact from presentation state or a coincidentally
similar value. When authority transfers during a lifecycle, the entry must name
the transition and timing.

## Write Ownership

Write Ownership states who is permitted to create, mutate, reset, or replace a
contract value. Public readability, a reachable Transform, or a public setter
does not imply write permission. Any additional writer is an ownership change
and requires Registry review and an update in the same logical change.

Combat may read movement-owned spatial state through registered contracts, but
Combat must never gain implicit permission to write Movement Root position or heading.
Any future explicit coordination must preserve a named movement command/ownership
boundary; it cannot arise merely because Combat can reach the Transform.

## Units and coordinate spaces

Every numeric or spatial entry must state its unit and coordinate space. Use
the following canonical labels where applicable:

### Units

- `m`
- `m/s`
- `s`
- `deg`
- `Scalar / N/A`

Compound units that are essential to the contract, such as `deg/s`, must be
written explicitly rather than shortened to an incorrect listed unit. Do not
use bare `units`, undocumented scale factors, or an implied angle convention.

### Coordinate spaces

- `World Space`
- `Ship Root Local Space`
- `Shot / Dispersion Plane Space`
- `Scalar / N/A`

If conversion is supported, document the input space, output space, and the
Transform or basis that performs the conversion. Unit or space changes are
semantic contract changes even when the underlying C# type is unchanged.

## Lifecycle and update timing

An entry must say when its value exists, becomes valid, changes, and becomes
invalid. Canonical lifecycle descriptions include:

- `Static prefab/configuration data`
- `Applied in Awake`
- `Updated every frame in Update`
- `Updated only on command`
- `Runtime transient`
- `Editor-only`

Add the relevant ordering, reset, enable/disable, destruction, or handoff
details whenever consumers need them. Do not claim a frame ordering that is not
enforced by script execution order or another documented mechanism.

## Registry update rule

Any task that adds, removes, renames, changes the semantics of, changes the
unit or coordinate space of, or changes the ownership or lifecycle of a
registered-category contract must update the corresponding Registry file in
the same logical change.

When no Registry-visible contract changes, the task completion report must say:

```text
Registry impact: none.
```

Documentation and implementation must not knowingly disagree. A contract
change is incomplete until both sides of the change are updated.

## Required block for future Codex prompts

Include this block in future implementation-task prompts:

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

## Required completion-report format

Use the following headings in completion reports for changes with Registry
impact. Write `None` for categories with no entries.

```text
Registry Impact

Added:
Changed:
Deprecated:
Removed:
```

## Validation and automated tests

Registry documentation does not replace executable testing.

For behavior changes:

- deterministic math/state logic should use EditMode tests where practical;
- runtime integration should use PlayMode tests where practical;
- existing relevant regression suites must still be run;
- compilation != Test Runner PASS; and
- manual Unity validation remains necessary for visual/spatial/interaction behavior.

For documentation-only Registry tasks, do not create artificial Unity tests.
Validation is:

- source/prefab spot-check;
- ownership/source-of-truth review;
- registry readback; and
- confirm no executable production code changed.
