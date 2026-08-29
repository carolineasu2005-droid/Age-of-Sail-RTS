# BlenderShipPipeline

Local, repeatable Blender automation for AgeOfSailRTS ship assets. The source of truth is the JSON ship configuration plus the Python generators; `.blend`, render, and export files are generated outputs.

The current production target is the **Dutch Medium Warship — Gelderland Type c.1634**, implemented as a **Formal Base Model / Production Blockout**. It is deliberately a modular, low/medium-density foundation rather than a final detailed or Unity-ready asset.

## Environment and operating rules

- Blender: 5.2.1 LTS
- Fixed executable: `E:\SteamLibrary\steamapps\common\Blender\blender.exe`
- Interface: Blender CLI + official `bpy` API
- Project root: `E:\unity\Unity6\AgeOfSailRTS\AgeOfSailRTS\Tools\BlenderShipPipeline`
- No MCP, third-party Blender plug-ins, GUI click automation, or Blender user-setting changes

Every Python script derives the pipeline root and `config`, `reference`, and `output` locations from its own `__file__`. Builds do not depend on the PowerShell or Blender current working directory.

## Directory layout

```text
BlenderShipPipeline/
├─ README.md
├─ run_blender.ps1
├─ config/
│  └─ ships/
│     └─ gelderland_1634.json
├─ scripts/
│  ├─ build_ship.py
│  ├─ core/
│  │  ├─ paths.py
│  │  ├─ config.py
│  │  └─ validation.py
│  ├─ ship/
│  │  ├─ longitudinal.py
│  │  ├─ hull.py
│  │  ├─ structures.py
│  │  ├─ decks.py
│  │  └─ gunports.py
│  ├─ render/
│  │  └─ turnaround.py
│  ├─ test_connection.py
│  ├─ render_test.py
│  └─ export_test.py
├─ output/
│  ├─ blend/
│  ├─ renders/
│  └─ exports/
└─ reference/
```

## Runner

`run_blender.ps1` validates the fixed Blender executable and requested script, invokes Blender with `--background --python-exit-code 1 --python`, preserves Blender console output, and returns Blender's original exit code. Unhandled Python errors therefore produce a non-zero process result.

From the project root:

```powershell
.\run_blender.ps1 test_connection.py
.\run_blender.ps1 render_test.py
.\run_blender.ps1 export_test.py
.\run_blender.ps1 build_ship.py
```

The same runner can be called from any directory:

```powershell
& "E:\unity\Unity6\AgeOfSailRTS\AgeOfSailRTS\Tools\BlenderShipPipeline\run_blender.ps1" build_ship.py
```

## Gelderland Base Model

### Identity and dimensions

- Identity: low, long, compact early-17th-century Dutch medium warship, c.1634
- Hull main-body length: 36.4 m
- Maximum beam: 8.6 m
- Length/beam ratio: approximately 4.23
- World scale: 1 Blender Unit = 1 meter

These values describe a user-approved production brief. The repository does not claim that every construction heuristic is an independently documented historical measurement.

### Formal coordinate system

- `X`: longitudinal
- `-X`: bow
- `+X`: stern
- `Y`: transverse
- `-Y`: port
- `+Y`: starboard
- `+Z`: up
- Centerline: `Y = 0`

Mesh vertices are generated directly in this system. No whole-object rotation is used to disguise an axis conversion, and production objects retain identity rotation and unit scale.

### Generation flow

```text
Validated ship config
  -> longitudinal regional profiles
  -> semantic cross-sections
  -> port half control mesh
  -> procedural Y mirror with shared centerline
  -> keel / stem / stern / transom / rudder
  -> main, forecastle, and quarter decks
  -> bulwarks and primary wales
  -> mirrored 14 + 14 gunport recesses
  -> validation and parameter-response tests
  -> diagnostic renders
  -> final .blend save
```

The hull uses five interpolated longitudinal characters: Bow, Forward-Mid, Midship, Aft-Mid, and Stern Run. The midship section has semantic Keel/Floor/Bilge/Maximum-Breadth/Upper-Side/Tumblehome/Rail regions. The bow progressively narrows the floor and bilge into a V-shaped entry, while the stern run retains more fullness and terminates at a controlled transom.

The current preset uses 41 longitudinal stations and 13 cross-section rows. The pre-opening control hull is predominantly regular quad strips. Gunports are applied as one mirrored Exact Boolean, followed by a Y-axis bisect/mirror pass that guarantees matching final topology and a merged centerline.

### Object and collection structure

```text
SHIP_Gelderland1634
├─ Structure
│  ├─ Hull_Main
│  ├─ Keel
│  ├─ Stem
│  ├─ Sternpost
│  ├─ Rudder
│  ├─ Transom_Main
│  └─ Stern_Upperworks
├─ Decks
│  ├─ Deck_Main
│  ├─ Deck_Forecastle
│  └─ Deck_Quarter
├─ HullDetails
│  ├─ Bulwark_Port
│  ├─ Bulwark_Starboard
│  ├─ Wales_Main_Port
│  └─ Wales_Main_Starboard
└─ Gunports
   ├─ Gunports_Main_Port
   └─ Gunports_Main_Starboard
```

Major structures remain separately editable, while repeated gunport pocket geometry is aggregated into one object per side rather than 28 unmanaged objects. Diagnostic cameras and lights are removed before the production `.blend` is saved.

### Gunport rule

- Exactly one continuous main gunport tier
- Exactly 14 port openings and 14 starboard openings
- Even spacing between configured usable limits, not over the entire hull length
- Current center spacing: approximately 1.890 m
- Recesses are real hull openings with simplified open-front pocket depth
- No cannon, barrel, carriage, muzzle, or dummy cylinder is generated

### Configuration and provenance

`config/ships/gelderland_1634.json` uses schema version 2 and classifies every configuration leaf under one of three provenance groups:

- `user_locked_historical_baseline`: identity, formal axes and scale, 36.4 m length, 8.6 m beam, c.1634 Dutch medium-warship brief, moderate tumblehome, transom stern, and 14 gunports per side. This label records the approved brief; it is not an external historical-source claim.
- `modeling_heuristic`: exact section fullness, bilge and V-shape values, sheer curves, rake values, deck extents, transom proportions, generation resolution, gunport dimensions, and validation tolerances chosen to create a stable editable blockout.
- `stylization`: structural exaggeration, minimum readable feature size, wale projection, gunport readability, and neutral diagnostic materials/render choices.

Configuration validation rejects missing required fields, invalid ranges, the wrong coordinate convention, a formal count other than 14 per side, unclassified parameters, or parameters classified more than once. The final `.blend` embeds the config SHA-256 and a `SHIP_BUILD_MANIFEST.json` text block.

### Build and outputs

```powershell
.\run_blender.ps1 build_ship.py
```

The formal build writes:

- `output/blend/gelderland_1634_base.blend`
- `output/renders/gelderland_1634/base/port.png`
- `output/renders/gelderland_1634/base/starboard.png`
- `output/renders/gelderland_1634/base/top.png`
- `output/renders/gelderland_1634/base/bow.png`
- `output/renders/gelderland_1634/base/stern.png`
- `output/renders/gelderland_1634/base/perspective.png`
- `output/renders/gelderland_1634/base/rts_perspective.png`

All formal views are 1024×1024 PNGs. Rendering validates image dimensions, non-black content, foreground contrast, and camera crop before the `.blend` is saved.

For an isolated formal-hull diagnostic stage, use the fixed executable directly:

```powershell
& "E:\SteamLibrary\steamapps\common\Blender\blender.exe" --background --python-exit-code 1 --python "E:\unity\Unity6\AgeOfSailRTS\AgeOfSailRTS\Tools\BlenderShipPipeline\scripts\build_ship.py" -- --stage hull
```

This writes seven temporary diagnostic views under `output/renders/gelderland_1634/stage_b_hull/` and does not replace the formal `.blend`.

### Automated validation

The formal build fails with a Python exception when a core check fails. Checks cover:

- Hull dimensions and separate full-blockout dimensions
- Finite coordinates, duplicate vertices/faces, zero-length edges, zero-area faces, normals, manifold incidence, and enclosed volume
- Clean `Y = 0` centerline with no duplicate seam or internal mirror wall
- Hull, bulwark, wale, and gunport symmetry
- Exactly 14 + 14 gunports, even spacing, matching longitudinal positions, and no cannons
- Required modular objects and forbidden asset names/collections
- Identity transforms and unit scale
- Render validity and camera framing
- Isolated response to beam, tumblehome, bow V-shape, transom width, and gunport-count changes

General arbitrary-mesh BVH self-intersection is not used as a blind blocking metric because adjacent coplanar strips can produce false positives. The deterministic profile construction, strict manifold/degeneracy checks, and diagnostic renders are used together to identify obvious intersections.

## Bridge compatibility checks

- `test_connection.py`: clears the scene, creates `Codex_Test_Cube`, a camera, and a light, then saves `output/blend/codex_blender_test.blend`.
- `render_test.py`: frames and lights a test cube, renders with Blender 5.2 EEVEE, validates dimensions and luminance, then saves `output/renders/test_render.png`.
- `export_test.py`: creates `Codex_Export_Test`, saves `output/blend/export_test.blend`, and exports `output/exports/export_test.fbx` using Blender 5.2's bundled official FBX exporter. Registration is process-local and does not modify user preferences.

These tests remain independent of the formal ship builder.

## Phase 3A historical proxy

`output/blend/gelderland_1634_proxy.blend` and the older images directly under `output/renders/gelderland_1634/` are retained as Phase 3A historical outputs and are not overwritten by the formal build.

That proxy used the former prototype convention (`X` transverse, `Y` longitudinal) and proved the station-based concept only. The current formal generator in `scripts/build_ship.py` uses the new production axes described above; it no longer treats the proxy as the production hull.

## Current scope exclusions

The Formal Base Model intentionally does not include:

- Cannons or gun carriages
- Masts, yards, sails, standing/running rigging, shrouds, or ratlines
- Anchors, boats, crew, cargo, rope coils, lanterns, or small fittings
- Figurehead, complex windows, sculpture, ornate stern decoration, cabins, or furniture
- UVs, texture maps, weathering, final PBR materials, LODs, colliders, or Unity prefabs
- Formal Unity FBX export

Those are future modular stages. The current acceptance target is a stable, rebuildable Blender base model with reliable proportions, silhouette, deck architecture, structural bands, and 14 + 14 gunport openings.
