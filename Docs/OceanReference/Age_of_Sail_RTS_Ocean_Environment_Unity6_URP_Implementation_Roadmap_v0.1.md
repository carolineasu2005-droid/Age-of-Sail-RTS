# Age of Sail Naval RTS — Ocean Environment Unity 6 URP Implementation Roadmap v0.1

**Document status:** Execution roadmap draft  
**Current phase:** Blender water breakdown complete; Unity 6 URP prototype migration begins  
**Goal:** Reproduce the core visual character of the current stylized water in Unity with the least necessary complexity, while establishing a technical foundation that can later support fleet combat, shorelines, wakes, and projectile water impacts.

---

## 1. Current Baseline

### 1.1 Confirmed Visual Direction

- **Stylized** and clearly game-oriented rather than photorealistic.
- Large-scale waves define the ocean’s overall form; medium- and high-frequency detail should only support surface material and foam.
- The ocean must serve RTS readability from elevated camera angles. Avoid overly dense whitecaps, high-frequency shimmer, and strong reflections that compete visually with ships.
- The color palette should stay in the blue-to-cyan range. It may be brighter than real ocean water, but the final version should reduce the tropical feel.
- Highlights, wave crests, and foam should appear as **controlled graphic shapes**, not as dense clusters of tiny bright details.

### 1.2 Confirmed Blender Reference Structure

- Large-scale waves are driven primarily by the **Ocean Modifier**.
- The visible water surface is instanced and assigned materials through **Geometry Nodes**.
- A second Geometry Nodes setup generates three named attributes:
  - `接触浮沫` — contact foam
  - `岸边区域` — shoreline region
  - `模糊法向` — blurred normal
- The water material is built from the following major modules:
  - base color
  - crest color
  - Fresnel
  - stylized highlights
  - transmission color
  - wave foam
  - contact foam
  - shoreline transparency
  - distance-based color unification
- The Blender water relies mainly on **procedural noise and geometry attributes**, rather than many external water-detail textures.

### 1.3 Completed Assets

- `Environment_Land_v01.fbx` — may continue to be used as a Unity environment asset.
- `Ocean_ReferenceMesh_v01.fbx` — static wave-shape reference, approximately **51k vertices / 101k triangles**. It is only for visual and scale comparison and is **not** intended as the final runtime ocean.
- Original Blender project — retained as the **LookDev Reference** and should not be modified destructively.
- `Water_Node_Dump.txt` — complete text backup of the node structure.

---

## 2. Overall Technical Direction

The production Unity ocean will **not** directly reproduce Blender’s Ocean Modifier and will **not** use full Alembic or prerendered ocean animation as the runtime solution.

The Unity 6 URP implementation will use the following approach.

### Low-Frequency Shape

**2–4 Gerstner / Gerstner-like vertex waves**

→ Responsible for real geometric displacement and crest positions.

### Mid-Frequency Surface Detail

**Scrolling normals / procedural noise**

→ Responsible for surface liveliness and secondary detail.

### Wave Crests

**Wave height / wave steepness mask**

→ Drives crest color and wave foam.

### Stylized Lighting

**Fresnel + N·L + Ramp / Step**

→ Replaces Blender `Shader to RGB` and layered Glossy logic.

### Shallow Water and Shoreline

**Scene Depth or Shore Mask / SDF**

→ Drives shallow-water color, transparency, and shoreline foam.

### Ship Interaction

**Local RenderTexture / VFX / local masks**

→ Drives wakes, bow waves, and contact foam.

### Distance

**Camera Distance + Fog**

→ Reduces detail and unifies horizon color.

---

## 3. Phase Breakdown

## Phase 0 — Unity Water LookDev Scene Setup

**Goal:** Establish a reliable scale and environment bridge between Blender and Unity.

### Tasks

1. Create a dedicated scene:

```text
Assets/Game/Scenes/Test/Water_LookDev.unity
```

2. Recommended folder structure:

```text
Assets/Game/Environment/Ocean/
├─ Models/
│  ├─ Production/
│  └─ Reference/
├─ Materials/
├─ Textures/
├─ Shaders/
└─ Prefabs/
```

3. Import:

- `Environment_Land_v01.fbx`
- `Ocean_ReferenceMesh_v01.fbx` into `Reference`
- one current Combat-Ready test ship for scale and readability checks

4. Rebuild the scene-side environment in Unity:

- Directional Light
- Camera
- Global Volume
- simple sky / Skybox
- preserve the project scale standard: **1 Unity Unit = 1 meter**

### Acceptance Gate

- Land scale, orientation, and normals are correct.
- Ship scale is correct.
- Reference Ocean visually matches the Blender reference closely enough for comparison.
- Do not begin shader development until coordinate systems, scale, and import workflow are verified.

---

## Phase 1 — Ocean Shader v0.1: Capture the Core Look

**Goal:** Implement only the four elements that define the visual identity most strongly.

### Implement Only

1. Base Color
2. Crest Color
3. Fresnel Highlight
4. Simple Vertex Waves

### Do Not Implement Yet

- transparency
- shoreline foam
- refraction
- real-time reflections
- wakes
- projectile splash effects
- Scene Depth
- FFT
- SSR
- underwater effects

### A. Base Color

Use one main water color as the initial base.

Expose:

```text
_BaseColor
```

### B. Crest Color

Use `World Position Y` or wave-function height to generate a crest mask.

Expose:

```text
_CrestColor
_CrestThreshold
_CrestSoftness
```

### C. Fresnel

Use a Fresnel Effect to create grazing-angle highlights.

Expose:

```text
_FresnelColor
_FresnelPower
_FresnelStrength
```

### D. Vertex Waves

The first version only needs **two Gerstner waves**.

Expose:

```text
_Wave1_Direction
_Wave1_Length
_Wave1_Amplitude
_Wave1_Speed

_Wave2_Direction
_Wave2_Length
_Wave2_Amplitude
_Wave2_Speed
```

Use the **visual result** of the Blender Ocean reference as the target instead of trying to achieve mathematically identical simulation.

### Acceptance Gate

- A still frame clearly reads as a stylized ocean instead of a flat blue plane.
- Wave scale is close to the current Blender reference.
- Wave height does not visually obscure RTS ships.
- Fresnel does not create large blown-out white areas.
- Ships remain clearly readable from elevated RTS camera angles.

---

## Phase 2 — Ocean Shader v0.2: Stylized Highlights + Crest Foam

**Goal:** Reproduce the most recognizable cartoon/stylized language of the Blender water.

### Add

#### 1. Stylized Lighting

- Use `Normal · Light Direction`.
- Use `Smoothstep`, `Step`, or a ramp to control light/dark regions.
- Do **not** try to copy Blender `Shader to RGB` directly.

#### 2. Wave Foam

- Use wave height or steepness as the base mask.
- Add one low-cost noise layer.
- Use `Smoothstep` to control the whitecap boundary.

Expose:

```text
_HighlightColor
_HighlightThreshold
_HighlightSoftness

_FoamColor
_FoamThreshold
_FoamNoiseScale
_FoamNoiseStrength
```

### RTS Rules

- White wave-detail density should be roughly **30–50% lower** than the current Blender version.
- Foam strength should decrease at higher camera distances.
- Dense fine whitecaps should not remain visible in the far distance.

### Acceptance Gate

- Close range: clearly stylized crests are visible.
- Mid range: the ocean still feels alive.
- High RTS view: the surface does not turn into a wall of blue-white visual noise.
- White sails, cannon smoke, and ocean foam remain visually distinct.

---

## Phase 3 — Ocean Shader v0.3: Shallow Water, Transparency, and Shoreline

**Goal:** Turn the current small-island setup into a usable environment test scene.

### Preferred Approach

For static islands, use a **Shore Mask / distance field** rather than performing runtime geometry proximity calculations every frame.

### Implement

1. Deep Color / Shallow Color
2. Shoreline transparency
3. Shoreline foam
4. Simplified Scene Depth
5. Seabed / beach base color

### Guidelines

- Open ocean should remain visually close to opaque whenever possible.
- Transparency should primarily serve near-shore regions.
- Do not pursue physically correct refraction.
- Shoreline foam should be a controlled stylized band, not a complex fluid simulation.

Expose:

```text
_DeepColor
_ShallowColor
_ShoreWidth
_ShoreFoamWidth
_ShoreFoamStrength
_WaterAlphaNearShore
```

### Acceptance Gate

- Island coastlines are clear but not excessively bright.
- Shallow water is readable without becoming neon green.
- Transparency sorting remains manageable.
- Open ocean does not pay unnecessary performance cost for transparency.

---

## Phase 4 — Water / Ship Interaction

**Goal:** Start serving actual naval combat rather than continuing to build a water showcase.

### Priority

**P0**
- ship wake
- bow wave

**P1**
- hull contact foam
- projectile water splash

**P2**
- local disturbance around sinking debris
- local splash effects around ports / coastal defenses

### Principles

- Do not solve the entire ocean as a physical fluid.
- Each ship should generate only local visual information.
- Interaction effects should remain decoupled from the core ocean shader.
- Prefer Particle / VFX / local RenderTexture approaches for wakes and bow waves.

### Acceptance Gate

- 10–30 ships can exist simultaneously while the scene remains readable.
- Wakes help communicate heading rather than becoming visual clutter.
- Bow waves do not obscure the ship’s gun deck.
- Projectile splashes remain more visually prominent than background whitecaps.

---

## Phase 5 — Environment Atmosphere

**Goal:** Upgrade the “water shader” into a complete naval-combat environment.

### Add

1. Skybox / procedural sky
2. sea-to-sky fog
3. horizon color unification
4. Directional Light baseline
5. cloud layer
6. coordinated island and ocean color palette

### Key Principles

- Treat **ocean + sky + fog + sun** as one visual system.
- The horizon should not appear as a hard blue line.
- Establish a clear-weather baseline first; storms come later.
- Do not implement a day/night cycle at this stage.

### Acceptance Gate

- Sea-to-sky relationship looks natural from elevated camera angles.
- Ships remain clearly separated from the background.
- Cannon smoke stays readable against both sky and water.
- The environment can suggest historical naval-painting atmosphere without sacrificing gameplay readability.

---

## Phase 6 — Productionization and Performance

Only begin after the previous five phases pass their gates.

Consider:

- camera-relative ocean
- chunked / ring mesh
- distance LOD
- foam distance attenuation
- normal-detail distance attenuation
- shader keyword reduction
- limiting transparent regions
- GPU Profiler validation
- large-fleet stress testing
- multiple sea-state parameter presets

---

## 4. Recommended First Shader Graph Structure

`SG_Ocean_Stylized_v01`

### Vertex

```text
Object Position
→ Gerstner Wave 1
→ Gerstner Wave 2
→ Vertex Position
```

### Fragment

```text
BaseColor
↓
CrestMask
↓
Lerp(BaseColor, CrestColor)
```

```text
Normal / Wave Normal
↓
N·L
↓
Stylized Highlight Mask
```

```text
View Direction
↓
Fresnel
↓
Fresnel Highlight
```

```text
Wave Height / Steepness
+
Noise
↓
Foam Mask
```

### Final Composition

```text
Base / Crest
+ Highlight
+ Fresnel
+ Foam
→ Base Color / Emission
```

`v0.1` should use **Opaque** rendering first.

At `v0.3`, decide whether to switch to Transparent or adopt a split approach such as:

```text
Open Ocean = Opaque
Near Shore = Transparent
```

---

## 5. Recommended Material Parameter Panel

To preserve the working style of the current Blender setup, the final Unity material should expose the following controls.

### Color

- Base Color
- Crest Color
- Fresnel Color
- Shallow Color
- Foam Color

### Waves

- Primary Wave Direction
- Primary Wavelength
- Primary Amplitude
- Primary Speed
- Secondary Wave Direction
- Secondary Wavelength
- Secondary Amplitude
- Secondary Speed

### Highlights

- Highlight Strength
- Highlight Threshold
- Fresnel Strength
- Fresnel Power

### Foam

- Foam Amount
- Foam Noise Scale
- Foam Noise Speed
- Foam Strength

### Shoreline

- Shore Width
- Shore Foam Width
- Shore Alpha

### Distance

- Distance Fade Start
- Distance Fade End
- Far Ocean Color

---

## 6. Explicitly Out of Scope for the Current Phase

Do **not** implement the following merely because they may become useful later:

- FFT Ocean
- Navier–Stokes fluid simulation
- global water simulation involving every ship
- full-ocean real-time reflection
- expensive refraction
- underwater camera system
- complete weather system
- storm-scale giant waves
- day/night cycle
- seabed caustics
- complex physical buoyancy

The ship movement system already owns vessel movement logic. For now, **water visuals and physical buoyancy remain decoupled**.

---

## 7. Project Evaluation Criteria

Every water iteration should be evaluated using four camera contexts.

### A. High RTS Camera

Check:

- ship silhouette
- conflict between white sails and whitecaps
- whether texture detail becomes too fragmented
- whether the ocean steals too much attention

### B. Mid-Range Combat Camera

Check:

- wave volume
- cannon-smoke readability
- room for wake effects
- brightness contrast between hull and ocean

### C. Close Presentation Camera

Check:

- whether the water looks too plastic
- foam layering
- whether crests feel natural
- whether highlights clip or blow out

### D. Far Horizon

Check:

- sea/sky blending
- fog
- distant shimmer
- whether ocean detail should attenuate further

---

## 8. Immediate Execution Order

Proceed strictly in this order. Do not build multiple feature groups in parallel.

### Step 1

Enter Unity and create:

```text
Water_LookDev.unity
```

### Step 2

Import:

```text
Environment_Land_v01.fbx
Ocean_ReferenceMesh_v01.fbx
```

### Step 3

Add one production test ship and verify:

```text
1 Unity Unit = 1 meter
```

and confirm the overall scale relationship.

### Step 4

Create a simple, regular, vertex-displaceable **Ocean Test Mesh** in Unity.

### Step 5

Create:

```text
SG_Ocean_Stylized_v01
```

### Step 6

Implement **Base Color only**.

### Step 7

Add **two simple Gerstner waves**.

### Step 8

Add **Crest Color**.

### Step 9

Add **Fresnel**.

### Step 10

Compare side by side against the Blender reference and complete the **Ocean Shader v0.1 Gate**.

Only after the v0.1 gate passes should foam and shoreline work begin.

---

## 9. Ocean Shader v0.1 Definition of Done

Stop polishing and move to the next phase once all of the following are true:

- The Unity water can immediately be recognized as belonging to the same visual direction as the current Blender water.
- Wave scale suits Age-of-Sail ships in the roughly **30–50 m** class.
- Ship readability remains strong from an RTS camera.
- The ocean has no large-area shimmer or high-frequency white noise.
- Shader structure remains simple and parameters remain adjustable.
- No Blender runtime dependency exists.
- No prerendered ocean animation is required.
- On the PC target platform, the runtime cost is clearly lower than a full Ocean / FFT simulation.

---

## 10. Document Maintenance Rules

This document records only the **Unity ocean/environment execution roadmap and acceptance gates**.

Detailed Shader Graph nodes, parameters, and implementation steps should be appended later by version:

- `SG_Ocean_Stylized_v01`
- `SG_Ocean_Stylized_v02`
- `SG_Ocean_Stylized_v03`

The Blender source-node analysis remains in:

**Age of Sail Naval RTS — Art & Music Style, Technical Stack, and Production Workflow**

Section 15:

**Water Material Node Breakdown Archive**

From this point onward, this document serves as the **primary Unity ocean implementation document**.
