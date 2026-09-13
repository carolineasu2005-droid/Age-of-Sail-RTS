# Project Conventions Registry

This file records only frozen, project-wide conventions. Contract governance,
field definitions, and status semantics are defined in
[`REGISTRY_RULES.md`](REGISTRY_RULES.md).

## PRJ-CONV-001 — World Scale

| Field | Value |
|---|---|
| ID | `PRJ-CONV-001` |
| Symbol / Contract Name | World Scale |
| Kind | Convention |
| Owner | Project Architecture |
| Purpose / Meaning | Spatial scale for gameplay, movement, formations, and future combat integration. **1 Unity Unit = 1 meter.** |
| Access | Project-wide |
| Type | Spatial scale convention |
| Unit | `m` |
| Coordinate Space | `World Space` |
| Source of Truth | This project-wide convention |
| Writable By | Project architecture decisions only |
| Known Consumers | Movement, sailing, navigation, formations, future Combat Art, future Combat Gameplay |
| Lifecycle / Update Timing | Static project convention |
| Side Effects | Distances expressed in Unity world units are interpreted as meters. |
| Status | `Active` |
| Do Not Interpret As | A mesh-import guarantee or permission for individual assets to use arbitrary scale. |
| Replacement | N/A |
| Evidence | Frozen Phase 0.5 architecture requirement; movement source and `Docs/ShipMovementSystemArchitecture.md` use Unity world distances consistently. |

## PRJ-CONV-002 — Ship Local Axes

| Field | Value |
|---|---|
| ID | `PRJ-CONV-002` |
| Symbol / Contract Name | Ship Root Local Axes |
| Kind | Convention |
| Owner | Project Architecture |
| Purpose / Meaning | Defines the ship-relative basis: **+Z = Bow / Forward**, **+X = Starboard**, and **+Y = Up**. |
| Access | Project-wide |
| Type | Orthogonal Transform-axis convention |
| Unit | `Scalar / N/A` |
| Coordinate Space | `Ship Root Local Space` |
| Source of Truth | The authoritative Ship Root Transform basis |
| Writable By | Project architecture decisions only; movement may rotate the basis by rotating the authoritative Root. |
| Known Consumers | Movement, sailing, navigation, formations, ship presentation, future Combat Art, future Combat Gameplay |
| Lifecycle / Update Timing | Static project convention; the Root's world orientation changes at runtime. |
| Side Effects | `transform.forward` is the physical bow direction and `transform.right` is starboard. |
| Status | `Active` |
| Do Not Interpret As | Course over ground; leeway may make actual velocity differ from bow/Heading. |
| Replacement | N/A |
| Evidence | `ShipSailingSpeed`, `ShipDestinationController`, and `ShipTurning`; `Docs/ShipMovementSystemArchitecture.md` heading and course conventions. |

## PRJ-CONV-003 — Authoritative Ship Transform

| Field | Value |
|---|---|
| ID | `PRJ-CONV-003` |
| Symbol / Contract Name | Formal Ship Root = Gameplay Root = Movement Root = Ship Entity Transform |
| Kind | Convention |
| Owner | Movement / sailing / navigation architecture |
| Purpose / Meaning | One authoritative Root represents the ship entity. It owns world position, ship heading, `transform.forward`, navigation position, formation position, movement rotation, and the later Combat local-angle reference. |
| Access | The Transform of the ship entity GameObject that hosts the movement components |
| Type | Unity `Transform` identity and ownership contract |
| Unit | Position: `m`; heading: `deg` |
| Coordinate Space | Position and heading: `World Space`; Combat local-angle basis: `Ship Root Local Space` |
| Source of Truth | The authoritative Ship Root Transform |
| Writable By | Movement-owned translation and rotation components. Position is currently integrated by `ShipSailingSpeed`; heading is currently rotated by `ShipTurning`. |
| Known Consumers | Sailing speed, turning, destination navigation, formation geometry/control, selection/camera reads, ship presentation, future Combat Art and Combat Gameplay reads |
| Lifecycle / Update Timing | Runtime Transform state; movement updates position and rotation during `Update`. |
| Side Effects | Root movement moves its child presentation hierarchy; Root rotation changes physical bow/Heading and the ship-relative basis. |
| Status | `Active` |
| Do Not Interpret As | `VisualRoot`, mesh orientation, Course, `ActualVelocity`, or permission for Combat to write pose. Combat must never gain implicit permission to write Movement Root position or heading. |
| Replacement | N/A |
| Evidence | `ShipSailingSpeed` writes its own `transform.position`; `ShipTurning` rotates its own Transform; navigation and formation code read component `transform.position` / `transform.forward`; all current proxy ship prefabs host the movement stack on the prefab Root. |

## PRJ-CONV-004 — VisualRoot Boundary

| Field | Value |
|---|---|
| ID | `PRJ-CONV-004` |
| Symbol / Contract Name | `VisualRoot` |
| Kind | Convention |
| Owner | Ship presentation architecture |
| Purpose / Meaning | Presentation/render hierarchy only. It may contain nested presentation children. |
| Access | Child hierarchy beneath the authoritative Ship Root |
| Type | Unity prefab hierarchy boundary |
| Unit | `Scalar / N/A` |
| Coordinate Space | `Ship Root Local Space` |
| Source of Truth | Current ship prefab hierarchy for presentation; never authoritative for gameplay pose. |
| Writable By | Presentation systems, within the presentation hierarchy and without changing authoritative gameplay state. |
| Known Consumers | Ship meshes, renderers, and future presentation-only animation or effects |
| Lifecycle / Update Timing | Static prefab hierarchy; presentation state may be runtime transient. |
| Side Effects | Local presentation changes affect rendered appearance without redefining the ship entity's gameplay position or Heading. |
| Status | `Active` |
| Do Not Interpret As | The Formal Ship Root, Gameplay Root, Movement Root, Ship Entity Transform, navigation position, formation position, or Combat angle authority. |
| Replacement | N/A |
| Evidence | `VisualRoot` exists as a child of the Root in `PF_Proxy_Light_v01`, `PF_Proxy_Medium_v01`, and `PF_Proxy_Heavy_v01`. |

## PRJ-CONV-005 — Combat Geometry Physics Queries

| Field | Value |
|---|---|
| ID | `PRJ-CONV-005` |
| Symbol / Contract Name | Combat Geometry Physics Query Boundary |
| Kind | Convention |
| Owner | Combat Art / Combat Gameplay architecture |
| Purpose / Meaning | Gameplay Combat hull volumes use the dedicated Unity Layer named **`CombatGeometry` at index `8`**. These volumes are query-only Trigger colliders. Combat physics callers must filter with a `CombatGeometry` LayerMask and explicitly use `QueryTriggerInteraction.Collide` or an equivalent explicit trigger-query policy. |
| Access | Project-wide Unity physics query convention; layer configuration is serialized in `ProjectSettings/TagManager.asset`. |
| Type | Unity Layer name/index, Trigger collider, LayerMask, and trigger-query policy |
| Unit | Collider dimensions/positions `m`; layer/filter values `Scalar / N/A` |
| Coordinate Space | Collider geometry authored in `Ship Root Local Space`; physics queries execute in `World Space`. |
| Source of Truth | Authored Combat Geometry Collider volumes on validated Combat ship prefabs plus the `CombatGeometry` entry at layer index `8` in `ProjectSettings/TagManager.asset`. Render Mesh, `Renderer.bounds`, `Mesh.bounds`, generic `CollisionRoot/ShipCollider`, and `DebugRoot` are not Combat hit geometry Sources of Truth. |
| Writable By | Project architecture controls the layer name/index and query policy. Combat Art prefab authoring controls region collider calibration and Trigger configuration. Runtime Combat consumers query read-only and must not mutate Movement or generic collision geometry. |
| Known Consumers | `PF_Ship_Gelderland_Combat_v01`, `ShipCombatGeometry`, Combat Geometry prefab/physics tests, `ShipCombatGeometryGizmos`. |
| Planned Consumers | Projectile hit queries, Combat Line of Fire, Combat Debug, Hit Context, validator. |
| Lifecycle / Update Timing | Static project and prefab configuration. Trigger geometry follows its authoritative parent Ship Root pose; queries evaluate on demand after normal physics transform synchronization. |
| Side Effects | Explicit filtered queries can report Combat hull Trigger hits without making the volumes physical collision-force participants. |
| Status | `Active` |
| Do Not Interpret As | Permission for Movement, Formation, Selection, Camera, or ordinary ship collision to use this layer as spatial truth; permission to depend on global `Physics.queriesHitTriggers`; or a general-purpose collision layer. |
| Replacement | N/A |
| Evidence | `ProjectSettings/TagManager.asset` layer index `8`; Gelderland `CombatGeometry/MainHull/{Bow,Midship,Stern}` Trigger BoxColliders; focused EditMode and PlayMode tests using a layer mask and `QueryTriggerInteraction.Collide`. |
