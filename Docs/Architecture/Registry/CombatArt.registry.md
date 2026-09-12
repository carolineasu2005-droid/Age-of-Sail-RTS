# Combat Art / Ship Spatial Integration Registry

**Registry status:** Foundation / Partially Active

This Registry records approved Combat Art and ship-spatial boundaries. The
minimal `ShipArtDefinition` component, its explicit reference accessors,
Length/Beam queries, and the dedicated Phase 1 placeholder-prefab integration
are `Active`. Formal-art calibration, sockets, geometry, exposure, VFX hooks,
and tooling remain `Planned` until implementation evidence is added.

Governing dependencies:

- `PRJ-CONV-001` defines 1 Unity Unit = 1 meter.
- `PRJ-CONV-002` defines +Z Bow, +X Starboard, and +Y Up.
- `PRJ-CONV-003` / `MOV-REF-001` define the Formal Ship Root.
- `MOV-STA-001` and `MOV-STA-002` retain position and Heading authority.
- `PRJ-CONV-004` keeps `VisualRoot` presentation-only.

Combat Art may read the Movement Root and build child-space references from it.
It must not write Movement Root position or Heading.
Movement remains the writer of authoritative Root movement state:
`ShipSailingSpeed` owns Root translation and `ShipTurning` owns Root Heading,
as registered by `MOV-STA-001` and `MOV-STA-002`.

## Planned ship hierarchy

```text
Formal Ship Root
├── VisualRoot
├── CollisionRoot
├── DebugRoot
├── ArtReferences
├── CombatSockets
├── CombatGeometry
└── Exposure
```

This is the approved full target hierarchy. `PF_Ship_Gelderland_Combat_v01`
currently implements the inherited `VisualRoot`, `CollisionRoot`, and
`DebugRoot` plus a distinct `ArtReferences` hierarchy. `CombatSockets`,
`CombatGeometry`, and `Exposure` remain Planned.

| ID | Contract | Kind | Owner | Purpose / Meaning | Access / Type | Unit | Coordinate Space | Source of Truth | Writable By | Known / Planned Consumers | Lifecycle | Side Effects | Status | Do Not Interpret As | Replacement | Evidence |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `ART-PFB-001` | Combat-ready ship hierarchy | Prefab hierarchy | Combat Art | Establishes the complete named sibling-root target beneath the Formal Ship Root. | Planned prefab hierarchy | `Scalar / N/A` | `Ship Root Local Space` | Future validated combat-ready ship prefab | Combat Art prefab authoring only; never Combat runtime pose logic | Known: none. Planned: Combat Art, Combat Gameplay, validator. | Static prefab/configuration data when implemented | N/A until implemented | `Planned` | The partial Phase 1 placeholder hierarchy, a current-prefab guarantee, or permission to move the Formal Ship Root. | N/A | Approved Phase 0.5 / Change 3 architecture; complete-hierarchy implementation evidence: none. |
| `ART-PFB-004` | `PF_Ship_Gelderland_Combat_v01` | Prefab Variant contract | Combat Art | Dedicated Gelderland Combat placeholder that inherits the complete Medium Movement stack while adding `ShipArtDefinition` on the authoritative Root and a distinct `ArtReferences` child. Current placeholder anchors produce approximately 45 m Length and 11 m Beam from Medium proxy source evidence. | Prefab Variant of `PF_Proxy_Medium_v01` at `Assets/Assets/Game/Ship/Proxy/PF_Ship_Gelderland_Combat_v01.prefab` | Position/dimensions `m` | Anchors: `Ship Root Local Space`; inherited Root pose: `World Space` | The variant asset plus inherited Medium source prefab; `ShipArtDefinition` serialized references select the spatial anchors | Combat Art prefab authoring may recalibrate variant-local placeholders; Movement alone writes Root pose at runtime | Known: `ShipArtDefinition`, EditMode prefab tests, focused PlayMode integration test. Planned: Combat Gameplay and validator. | Static prefab configuration; inherited children and added anchors move with Root at runtime | Instantiation adds no automatic hierarchy mutation; Root Movement behavior is inherited unchanged | `Active` | Final Gelderland art/dimensions, a new Movement Root, or the generic source used to rebuild Light/Heavy proxies. | N/A | Prefab variant GUID `45f76b674dece124096d84bb473f8322`; base prefab GUID `f767a1e80c8cd1843a0e947ca3c36def`; Combat Art placeholder tests. |

## ShipArtDefinition

| ID | Contract | Kind | Owner | Purpose / Meaning | Access / Type | Unit / Space | Source of Truth | Writable By | Known / Planned Consumers | Lifecycle | Status | Do Not Interpret As | Replacement | Evidence |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `ART-SO-001` | `ShipArtDefinition` data asset concept | ScriptableObject data | Combat Art | Historical planned asset shape superseded before implementation by the root-local component contract `ART-API-001`. | No implemented API | `Scalar / N/A` | Registry history | Nobody | None | Never implemented | `Removed` | An available asset type or an alternate source of spatial truth. | `ART-API-001` | Superseded by Combat Art Phase 1 / Change 1A. |
| `ART-API-001` | `ShipArtDefinition` | `MonoBehaviour` component and read contract | Combat Art | Passive root-local holder for explicit ship-art references. It derives spatial dimensions without owning or mutating Movement pose. | Addable to the authoritative Ship Root; eight public getter-only `Transform` accessors; no update loop | References: `Scalar / N/A`; derived positions/dimensions use `Ship Root Local Space` and `m` | Private serialized fields on the component; the component's own `transform` is the spatial root | Combat Art prefab/scene authoring assigns fields; runtime consumers are read-only | Known: `PF_Ship_Gelderland_Combat_v01` and EditMode/PlayMode contract tests. Planned: Combat Gameplay spatial reads and validator. | Static configuration with on-demand read queries; reads have no hierarchy or Movement side effects | `Active` | A Movement Root replacement, runtime movement controller, automatic hierarchy builder, or final formal-art calibration. | N/A | `ShipArtDefinition.cs`; component on the variant Root; Combat Art contract, prefab, and integration tests. |
| `ART-API-002` | `TryGetLength(out float length)` | Query API | Combat Art | Derives Length as the absolute difference between Bow and Stern reference positions on the root-local Z axis. Returns unavailable with output `0` when either reference is missing. | `public bool TryGetLength(out float length)` | `m`; `Ship Root Local Space`, +Z Bow | `BowReference` and `SternReference`, converted with the component root's `InverseTransformPoint` | Derived read-only value; nobody writes it | Known: Combat placeholder variant plus EditMode and focused PlayMode tests. Planned: Combat Gameplay spatial reads and validator/debug visualization. | On-demand query; no side effects | `Active` | Renderer, mesh, collider, world-axis, or final Gelderland length data. | N/A | `ShipArtDefinition.TryGetLength`; contract, prefab, and root-motion integration tests. |
| `ART-API-003` | `TryGetBeam(out float beam)` | Query API | Combat Art | Derives Beam as the absolute difference between Starboard and Port reference positions on the root-local X axis. Returns unavailable with output `0` when either reference is missing. | `public bool TryGetBeam(out float beam)` | `m`; `Ship Root Local Space`, +X Starboard | `PortReference` and `StarboardReference`, converted with the component root's `InverseTransformPoint` | Derived read-only value; nobody writes it | Known: Combat placeholder variant plus EditMode and focused PlayMode tests. Planned: Combat Gameplay spatial reads and validator/debug visualization. | On-demand query; no side effects | `Active` | Renderer, mesh, collider, world-axis, or final Gelderland beam data. | N/A | `ShipArtDefinition.TryGetBeam`; contract, prefab, and root-motion integration tests. |

## ArtReferences

`ArtReferences` is the formal cross-system spatial-anchor hierarchy on the
Combat placeholder variant. It is separate from inherited `DebugRoot`, which
remains historical Movement/debug support. Consumers must use the assigned
`ShipArtDefinition` references rather than search render children, infer
geometry from a mesh, or consume DebugRoot points as Combat contracts.

| ID | Contract | Kind | Owner | Purpose / Meaning | Unit | Coordinate Space | Source of Truth | Writable By | Known / Planned Consumers | Lifecycle | Status | Do Not Interpret As | Evidence |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `ART-REF-001` | `ArtReferences/{Waterline,Center,Bow,Stern,Port,Starboard,Deck}` | Reference-container hierarchy contract | Combat Art | Direct child of the authoritative Root organizing seven ordinary Transform anchors. It is semantically separate from `DebugRoot`. | `Scalar / N/A` | `Ship Root Local Space` | `PF_Ship_Gelderland_Combat_v01` variant-local hierarchy | Combat Art prefab authoring; runtime consumers do not replace or move anchors | Known: `ShipArtDefinition` and Combat Art prefab tests. Planned: Combat Gameplay and validator. | Static prefab configuration; anchors inherit Root movement/rotation | `Active` | A Movement debug hierarchy, runtime Combat state, automatic builder output, or final formal-art calibration. | Combat placeholder prefab and structure tests. |
| `ART-REF-002` | `VisualRoot` reference | Transform reference | Combat Art | Points to the inherited presentation/render hierarchy defined by `PRJ-CONV-004`; assignment is optional at the API level and assigned on the Combat placeholder. | `Scalar / N/A` | `Ship Root Local Space` | `ShipArtDefinition.visualRoot`; public read via `VisualRoot` | Combat Art prefab/scene authoring; no public runtime setter | Known: `PF_Ship_Gelderland_Combat_v01` and EditMode contract/prefab tests. Planned: Combat Art presentation and validator. | Static serialized reference; reading has no side effects | `Active` | Gameplay pose, Combat geometry, or a source for Root Heading. | Variant assignment to inherited `VisualRoot`; Combat Art prefab tests. |
| `ART-REF-003` | `WaterlineReference` | Transform reference | Combat Art | Represents the intended ship waterline reference plane/height; only its spatial anchor is implemented and the exact assigned transform awaits calibration. | Position `m` | `Ship Root Local Space` | `ShipArtDefinition.waterlineReference`; public read via `WaterlineReference` | Combat Art prefab/scene authoring; no public runtime setter | Known: Combat placeholder variant and EditMode contract/prefab tests. Planned: Combat Art/VFX and validator. | Static serialized reference; reading has no side effects | `Active` | Buoyancy, sinking logic, final waterline height, or Movement Root position. | Variant assignment to `ArtReferences/Waterline`; contract and prefab tests. |
| `ART-REF-004` | `CenterReference` | Transform reference | Combat Art | Stable semantic center of the gameplay-relevant ship body/art reference for use as a common spatial anchor. | Position `m` | `Ship Root Local Space` | `ShipArtDefinition.centerReference`; public read via `CenterReference` | Combat Art prefab/scene authoring; no public runtime setter | Known: Combat placeholder variant and EditMode contract/prefab tests. Planned: Combat Art, Combat Gameplay, and validator. | Static serialized reference; reading has no side effects | `Active` | `Renderer.bounds.center`, center of mass, or collider center. | Variant assignment to `ArtReferences/Center`; contract and prefab tests. |
| `ART-REF-005` | `BowReference` | Transform reference | Combat Art | Explicit bow reference aligned to the +Z ship convention and used with Stern to derive Length. | Position `m` | `Ship Root Local Space` | `ShipArtDefinition.bowReference`; public read via `BowReference` | Combat Art prefab/scene authoring; no public runtime setter | Known: Combat placeholder variant, Length query, and EditMode/PlayMode tests. Planned: Combat Gameplay and validator. | Static serialized reference; reading has no side effects | `Active` | Final bow dimension or muzzle socket. | Variant assignment to `ArtReferences/Bow`; query, prefab, and integration tests. |
| `ART-REF-006` | `SternReference` | Transform reference | Combat Art | Explicit stern reference opposite the +Z bow direction and used with Bow to derive Length. | Position `m` | `Ship Root Local Space` | `ShipArtDefinition.sternReference`; public read via `SternReference` | Combat Art prefab/scene authoring; no public runtime setter | Known: Combat placeholder variant, Length query, and EditMode/PlayMode tests. Planned: Combat Gameplay and validator. | Static serialized reference; reading has no side effects | `Active` | Final stern dimension or propulsion implementation. | Variant assignment to `ArtReferences/Stern`; query, prefab, and integration tests. |
| `ART-REF-007` | `PortReference` | Transform reference | Combat Art | Explicit port-side reference opposite the +X Starboard axis and used with Starboard to derive Beam. | Position `m` | `Ship Root Local Space` | `ShipArtDefinition.portReference`; public read via `PortReference` | Combat Art prefab/scene authoring; no public runtime setter | Known: Combat placeholder variant, Beam query, and EditMode/PlayMode tests. Planned: Combat Gameplay and validator. | Static serialized reference; reading has no side effects | `Active` | A Port muzzle collection or broadside arc definition. | Variant assignment to `ArtReferences/Port`; query, prefab, and integration tests. |
| `ART-REF-008` | `StarboardReference` | Transform reference | Combat Art | Explicit starboard-side reference aligned with +X and used with Port to derive Beam. | Position `m` | `Ship Root Local Space` | `ShipArtDefinition.starboardReference`; public read via `StarboardReference` | Combat Art prefab/scene authoring; no public runtime setter | Known: Combat placeholder variant, Beam query, and EditMode/PlayMode tests. Planned: Combat Gameplay and validator. | Static serialized reference; reading has no side effects | `Active` | A Starboard muzzle collection or broadside arc definition. | Variant assignment to `ArtReferences/Starboard`; query, prefab, and integration tests. |
| `ART-REF-009` | `DeckReference` | Transform reference | Combat Art | Primary deck-height semantic anchor; exact assigned transform awaits formal-art calibration. | Position `m` | `Ship Root Local Space` | `ShipArtDefinition.deckReference`; public read via `DeckReference` | Combat Art prefab/scene authoring; no public runtime setter | Known: Combat placeholder variant and EditMode contract/prefab tests. Planned: Combat Art, Combat Gameplay, and validator. | Static serialized reference; reading has no side effects | `Active` | Crew, Boarding, Deck VFX, collision surface, or target exposure. | Variant assignment to `ArtReferences/Deck`; contract and prefab tests. |
| `ART-CONV-005` | `DebugRoot` / `ArtReferences` separation | Convention | Combat Art / Movement boundary | `DebugRoot` remains historical Movement/debug support. Combat Art consumers use the distinct `ArtReferences` anchors wired through `ShipArtDefinition`; coincident placeholder positions do not merge their identities or ownership. | `Scalar / N/A` | `Ship Root Local Space` | This Registry contract and the distinct Transform objects in `PF_Ship_Gelderland_Combat_v01` | Architecture changes only; Combat Art may recalibrate ArtReferences without redefining DebugRoot | Known: Combat placeholder prefab and structure tests. Planned: Combat Gameplay and validator. | Active throughout placeholder and formal-art integration | `Active` | Permission to bind Combat contracts permanently to Movement debug points. | Distinct `ArtReferences` and `DebugRoot` prefab hierarchies; identity assertions in EditMode tests. |

## Muzzle Sockets

```text
CombatSockets
└── Muzzles
    ├── Port
    └── Starboard
```

| ID | Contract | Kind | Owner | Purpose / Meaning | Unit | Coordinate Space | Source of Truth | Writable By | Planned Consumers | Lifecycle | Status | Do Not Interpret As | Evidence |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `ART-PFB-002` | `CombatSockets/Muzzles` hierarchy | Prefab hierarchy | Combat Art | Separates Combat socket organization from render and collision hierarchies. | `Scalar / N/A` | `Ship Root Local Space` | Future validated ship prefab | Combat Art prefab authoring | Combat Art, Combat Gameplay, validator | Static prefab/configuration data | `Planned` | A cannon inventory, reload owner, or guarantee of socket count/order. | Approved Phase 0.5 / Change 3 architecture; implementation evidence: none. |
| `ART-SCK-001` | Port muzzle sockets | Socket collection | Combat Art | Planned emission transforms for participating Port cannons. Count, ordering, and exact transforms remain TBD and placeholder-capable. | Position `m`; orientation `deg` | `Ship Root Local Space` | Future children/references beneath `CombatSockets/Muzzles/Port` | Combat Art prefab authoring | Combat Gameplay shot spawning, Combat VFX, validator | Static prefab/configuration data | `Planned` | Port broadside state, a per-gun reload design, or final Gelderland socket data. | Approved Phase 0.5 / Change 3 architecture; implementation evidence: none. |
| `ART-SCK-002` | Starboard muzzle sockets | Socket collection | Combat Art | Planned emission transforms for participating Starboard cannons. Count, ordering, and exact transforms remain TBD and placeholder-capable. | Position `m`; orientation `deg` | `Ship Root Local Space` | Future children/references beneath `CombatSockets/Muzzles/Starboard` | Combat Art prefab authoring | Combat Gameplay shot spawning, Combat VFX, validator | Static prefab/configuration data | `Planned` | Starboard broadside state, a per-gun reload design, or final Gelderland socket data. | Approved Phase 0.5 / Change 3 architecture; implementation evidence: none. |

## Combat Geometry

| ID | Contract | Kind | Owner | Purpose / Meaning | Unit / Space | Source of Truth | Writable By | Planned Consumers | Lifecycle | Status | Do Not Interpret As | Evidence |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `ART-PFB-003` | `CombatGeometry` hierarchy | Prefab hierarchy | Combat Art / spatial integration | Planned dedicated hierarchy for simplified, validated Combat spatial geometry. Exact shapes and dimensions are TBD. | `m`; `Ship Root Local Space` | Future validated Combat geometry data/hierarchy | Combat Art prefab authoring | Combat Gameplay queries, Target Exposure, validator/debug visualization | Static prefab/configuration data | `Planned` | Render Mesh or the existing generic `ShipCollider`. | Approved Phase 0.5 / Change 3 architecture; implementation evidence: none. |
| `ART-CONV-001` | Combat Geometry separation | Convention | Combat Art / Combat Gameplay boundary | Combat Geometry is not Render Mesh. Combat Geometry is not existing generic `ShipCollider`. | `Scalar / N/A` | This Registry contract | Architecture change only | Combat Art authoring, Combat Gameplay queries, validator | Foundation convention | `Planned` | Permission to silently reuse render triangles or generic selection/collision geometry as Combat geometry. | Approved Phase 0.5 / Change 3 architecture; implementation evidence: none. |

## Exposure Reference

| ID | Contract | Kind | Owner | Purpose / Meaning | Unit / Space | Source of Truth | Writable By | Planned Consumers | Lifecycle | Status | Do Not Interpret As | Evidence |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `ART-REF-010` | `Exposure` reference | Transform/spatial reference | Combat Art / spatial integration | Planned basis/reference for deriving target projected geometry from validated Combat geometry. Exact representation and calculation API are TBD. | `m`; space conversion between `World Space`, `Ship Root Local Space`, and projection plane must be explicit when designed | Future validated Exposure configuration | Combat Art prefab/data authoring; runtime calculation owner TBD | Combat Gameplay Exposure consumer, validator/debug visualization | Static reference with runtime projected result; details TBD | `Planned` | Projectile Dispersion, hit probability, Render Mesh bounds, or final formal-art dimensions. | Approved Phase 0.5 / Change 3 architecture; implementation evidence: none. |
| `ART-CONV-002` | Target Exposure ownership boundary | Convention | Combat Art / Combat Gameplay boundary | Target Exposure describes target projected geometry. It must not own or modify Projectile Dispersion. | `Scalar / N/A` | This Registry contract | Architecture change only | Exposure producer and Combat dispersion consumer | Foundation convention | `Planned` | A dispersion curve, random shot sample, or Hit/Miss decision. | Approved Phase 0.5 / Change 3 architecture; implementation evidence: none. |

## VFX integration hooks

| ID | Contract | Kind | Owner | Purpose / Meaning | Unit / Space | Source of Truth | Writable By | Planned Consumers | Lifecycle | Status | Do Not Interpret As | Evidence |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `ART-EVT-001` | Combat Art VFX integration hooks | Event boundary | Combat Gameplay emits; Combat Art/VFX consumes | Planned one-way delivery of resolved spatial/combat events to presentation. Event names, payloads, and dispatch timing are intentionally TBD. | Per payload; explicit units/spaces required when designed | Future authoritative Combat resolution and registered spatial references | Combat Gameplay event producer; VFX is read-only consumer | Combat Art / VFX | Runtime transient after authoritative Combat decisions | `Planned` | A Hit/Miss/Damage decision API or a Movement command channel. | Approved Phase 0.5 / Change 3 architecture; implementation evidence: none. |
| `ART-CONV-003` | VFX authority boundary | Convention | Combat Art | VFX consumes spatial/combat events. VFX must not decide Hit/Miss/Damage. VFX must not modify Movement. | `Scalar / N/A` | This Registry contract | Architecture change only | Combat Gameplay and VFX implementation | Foundation convention | `Planned` | Gameplay authority, damage state, or Root pose ownership. | Approved Phase 0.5 / Change 3 architecture; implementation evidence: none. |

## Combat Art Validator

| ID | Contract | Kind | Owner | Purpose / Meaning | Access / Type | Unit / Space | Source of Truth | Writable By | Planned Consumers | Lifecycle | Status | Do Not Interpret As | Evidence |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `ART-TOOL-001` | Combat Art Validator | Editor tool | Combat Art tooling | Planned Editor-only configuration checker for hierarchy, required references, socket organization, spaces, and geometry separation. Detailed checks await contract implementation. | Editor-only validation command/tool; API TBD | `Scalar / N/A` | Implemented Registry contracts plus inspected asset/prefab configuration | Tooling code may report diagnostics; it must not silently author/repair prefabs | Artists, designers, developers, CI only if explicitly added later | `Editor-only` and on demand | `Planned` | An automatic prefab builder, runtime dependency, or substitute for manual visual/spatial validation. | Approved Phase 0.5 / Change 3 architecture; implementation evidence: none. |

## Formal art dependency

| ID | Contract | Kind | Owner | Purpose / Meaning | Source of Truth | Writable By | Planned Consumers | Lifecycle | Status | Do Not Interpret As | Evidence |
|---|---|---|---|---|---|---|---|---|---|---|---|
| `ART-CONV-004` | Placeholder-capable Phases 1–6 | Convention | Combat Art / project architecture | Phases 1–6 must remain placeholder-capable. Current 45 m Length, 11 m Beam, waterline Y, center Y, and deck Y are temporary Medium-proxy calibration; final transforms, dimensions, sockets, and geometry are recalibrated when formal Gelderland art arrives. | This Registry contract, `PF_Ship_Gelderland_Combat_v01`, and later validated formal Gelderland asset data | Architecture and Combat Art calibration change only | Known: Combat placeholder prefab and tests. Planned: all later Combat Art and Combat Gameplay phases. | Foundation through formal-art integration | `Active` | Placeholder transforms/dimensions/sockets/geometry as final production art data or frozen historical truth. | Combat placeholder variant and tests; formal Gelderland calibration evidence: not yet available. |

The Phase 1 code and placeholder-prefab contracts are now implemented by
`ART-API-001` through `ART-API-003`, `ART-PFB-004`, `ART-REF-001` through
`ART-REF-009`, and `ART-CONV-004` through `ART-CONV-005`. Active placeholder
entries do not assert final art data. All remaining detailed APIs and later
hierarchies are undesigned or Planned; future tasks must promote only
implemented contracts and add source/prefab/test evidence in the same logical
change.
