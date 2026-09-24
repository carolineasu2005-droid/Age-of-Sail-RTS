# Codex Task Brief — Blender Stylized Ocean → Unity 6.5 URP Parity Prototype

## 0. Mission

Reproduce the current Blender stylized ocean in Unity 6.5 URP as closely as practical in **visual appearance and functional behavior**, while keeping the implementation suitable for a real-time naval RTS.

This is a **visual/function parity task**, not a literal node-by-node translation.

The target is:

> Under a matched reference camera and approximately matched lighting, the Unity ocean should immediately read as the same stylized water system as the Blender reference.

Do **not** pursue pixel-perfect or algorithm-identical reproduction when Blender-specific systems do not map directly to Unity.

---

## 1. Project Context

Project: **Age of Sail Naval RTS**

Rendering target:
- Unity 6.5
- URP
- PC
- 1 Unity Unit = 1 meter

Game requirements:
- RTS camera readability has priority over physically correct water.
- Large-scale waves should define the water shape.
- Medium/high-frequency detail should only support material response and foam.
- Whitecaps, reflections, and specular detail must not overpower ships, sails, cannon smoke, or combat VFX.
- The runtime ocean must remain cheaper and simpler than a full FFT ocean solution.

Do not modify unrelated gameplay systems.

---

## 2. Source-of-Truth Files

Read these files completely before implementing anything:

```text
OceanReference/
├─ Ocean_Environment_Unity6_URP_Implementation_Roadmap_v0.1.md
├─ Water_Full_Dump.txt
├─ Water_Ramp_Summary.txt
├─ Blender_LookDev_Environment_Dump.txt
├─ Water_Ramp_LUTs/
│  ├─ 01__...png
│  ├─ 02__...png
│  ├─ ...
│  └─ 11__...png
└─ Reference/
   ├─ Environment_Land_v02.fbx
   ├─ Ocean_ReferenceMesh_v02.fbx
   └─ Ocean_TestMesh_v01.fbx
```

Treat the Blender dump files as the authoritative record of the reference water setup.

Do not guess missing Blender values.

If a required value is not present in the source files, record it as an explicit parity gap instead of inventing it.

---

## 3. Reference Blender State

The reference water was captured from:

- Blender 5.2.2 LTS
- Eevee
- Frame 44
- 24 FPS
- Metric units
- Unit Scale = 1.0
- Length Unit = meters

Reference frame synchronization matters because both water animation and material animation are frame-driven.

### Reference Camera

Use the reference camera data from `Blender_LookDev_Environment_Dump.txt`.

Key captured values:

```text
Projection: Perspective
Position:
  X = 2.63429046
  Y = -3.04802895
  Z = 7.38444996

Rotation Euler:
  X = 1.17912614
  Y = -0.0156398658
  Z = 2.95672894

Focal Length = 25 mm
Sensor Width = 36 mm
Sensor Height = 24 mm
Clip Start = 0.1
Clip End = 1000
```

Rebuild a Unity reference camera that matches the Blender view as closely as practical.

Document any coordinate-system conversion required.

### Reference Sun

Use the reference Sun direction from the environment dump.

Captured Blender values:

```text
Rotation Euler:
  X = 1.31781781
  Y = 0.440343082
  Z = -0.579557538

Color = white
Energy = 10
Angle = 0.00918043219
Shadows = enabled
```

Important:

Do **not** assume Blender `Energy = 10` should become Unity `Intensity = 10`.

Match the light direction exactly, then tune Unity light intensity visually.

### Color Management

Reference Blender color management:

```text
Display Device = sRGB
View Transform = Standard
Look = None
Exposure = 0
Gamma = 1
```

Keep this in mind when comparing screenshots. Do not assume Unity and Blender tone mapping are equivalent.

---

## 4. Blender Ocean Geometry Reference

The source dynamic ocean object is `实例洋面`.

Its Ocean Modifier values are captured in `Water_Full_Dump.txt`.

Key values:

```text
Geometry Mode      = GENERATE
Resolution         = 15
Viewport Resolution= 15
Spatial Size       = 50 m
Wave Scale         = 0.4
Smallest Wave      = 0.1 m
Choppiness         = 1
Wind Velocity      = 3
Wave Alignment     = 0
Wave Direction     = 0
Damping            = 0.5
Depth              = 200 m
Foam               = enabled
Foam Layer Name    = 浪花泡沫
Spectrum           = PHILLIPS
Repeat X           = 1
Repeat Y           = 1
Random Seed        = 0
```

The Ocean Modifier animation driver is:

```text
Ocean.time = frame / 30
```

At reference frame 44:

```text
Ocean.time = 1.4666667
```

Do not try to reproduce Blender's Ocean Modifier mathematically unless necessary.

For the production runtime ocean, use the roadmap's intended solution:

- 2–4 Gerstner or Gerstner-like waves
- adjustable wavelength
- amplitude
- direction
- speed
- derived wave normal / slope
- crest and steepness masks

The visual wave scale should be calibrated against `Ocean_ReferenceMesh_v02.fbx`.

---

## 5. Blender Water Material Reference

The Blender water material exposes these current top-level values:

```text
Base Color:
[0, 0.296138316, 1, 1]

Crest Color:
[0.0630086586, 0.623961091, 0.806952596, 1]

Fresnel Highlight:
[0.270494998, 0.973446071, 1, 1]

Transmission Color:
[0, 0.879623294, 1, 1]

Highlight Strength:
0.800000012

Wave Foam Brightness:
1

Contact Foam Range:
1

Contact Foam Evolution Speed:
0.700000048

Contact Foam Strength:
0.100000024

Contact Foam Color:
[0.622619152, 0.622619152, 0.622619152, 1]

Water Transparency Range:
3.39999986

Water Reflection Factor:
0

Distance Depth:
-0.799999237
```

Preserve equivalent Unity material properties where practical.

Use clear English property names in Unity, but document the original Blender property name in comments or README.

---

## 6. Exact Color Ramp Data

There are **11 captured Color Ramps**.

Their exact interpolation mode, control-point positions, and RGBA values are recorded in:

```text
Water_Ramp_Summary.txt
```

Equivalent 512×1 LUT PNGs are available in:

```text
Water_Ramp_LUTs/
```

Prefer one of the following:

1. Sample the supplied LUTs directly for parity-critical masks, or
2. Reconstruct the ramps mathematically when doing so is guaranteed to preserve the captured behavior.

Do not approximate narrow ramps casually.

Several ramps are intentionally very sharp and strongly affect the stylized look.

Examples:

```text
Color Ramp.006
LINEAR
0.595419586 -> black
0.618321300 -> white
```

```text
Color Ramp.008
LINEAR
0.972727060 -> black
1.000000000 -> white
```

```text
Color Ramp.009
LINEAR
0.0381679386 -> black
0.0801528767 -> white
```

Two ramps use EASE interpolation:

```text
Color Ramp.007
Color Ramp.010
```

Use `Water_Ramp_Summary.txt` as the final authority.

---

## 7. Blender Material Animation / Drivers

The Blender water material contains time- and light-driven logic.

Important captured driver:

```text
Value.001 = frame / 1.5
```

The Blender material also reads the Sun rotation components through drivers.

Therefore:

- Unity procedural animation should use time as an explicit input.
- Unity lighting should use the current URP main directional light direction rather than a hard-coded light vector.

Do not bake the Blender Sun Euler values directly into the shader if the Unity light can provide the equivalent direction dynamically.

---

## 8. Required Functional Modules

The Unity implementation must eventually reproduce these modules:

### 8.1 Base Water Color

Equivalent to Blender base-color logic.

### 8.2 Crest / Wave Height Color

Use wave displacement height and/or wave steepness.

Goal:
- broad readable crest color regions
- not noisy micro-detail

### 8.3 Fresnel Highlight

Use view direction and surface normal.

Match the captured Blender ramp behavior using the supplied ramp data/LUTs.

### 8.4 Stylized Specular / Highlight

Blender uses Glossy BSDF → Shader to RGB → Color Ramp.

URP cannot reproduce this literally.

Implement a visual equivalent using:
- N·L
- half-vector / specular term if useful
- Smoothstep / Ramp / LUT
- artist-controlled thresholds

The result should preserve the graphic stylized highlight shapes.

### 8.5 Transmission-Like Lighting

The Blender material uses:
- blurred surface normal
- light direction
- dot product
- remapping
- a color ramp
- Transmission Color

Implement a visually equivalent subsurface/transmission-like tint.

This is an art effect, not physically correct refraction.

### 8.6 Wave Foam

Reference logic uses:
- Blender Ocean foam layer `浪花泡沫`
- Color Ramp.009
- procedural Noise
- Map Range
- Color Ramp.010

Unity production equivalent should use:
- wave height and/or steepness mask
- procedural or texture noise
- parity LUT/ramp
- adjustable foam amount and strength

### 8.7 Contact Foam

Blender named attribute:

```text
接触浮沫
```

Blender generates this from Geometry Proximity.

Do not perform expensive full-scene geometry proximity in Unity.

Design the Unity input so it can later be driven by:
- local interaction mask
- RenderTexture
- localized VFX
- ship contact/wake system

For the parity prototype, a temporary test mask is acceptable.

### 8.8 Shore Region / Shore Transparency

Blender named attribute:

```text
岸边区域
```

Blender generates this from geometry proximity to land.

Production Unity solution should use one of:
- Shore Mask
- distance field / SDF
- Scene Depth
- equivalent low-cost static-island solution

Do not create a per-frame whole-ocean geometry-distance solver.

### 8.9 Blurred Normal

Blender named attribute:

```text
模糊法向
```

The Geometry Nodes setup blurs the normal with 12 iterations.

Unity equivalent should use:
- analytic wave normal
- filtered wave normal
- lower-frequency normal
- or an equivalent smoothed normal representation

It does not need to reproduce Blender's Geometry Nodes blur algorithm exactly.

### 8.10 Distance Color Unification

The Blender material uses camera depth / distance to fade the ocean toward a pale far-ocean color.

Implement a Unity equivalent based on camera distance or view depth.

The goal is:
- reduced far-distance contrast
- softer horizon
- less high-frequency detail at distance

---

## 9. Blender-Specific Features That Must Be Translated, Not Copied

### Blender `Shader to RGB`

No direct URP equivalent.

Translate to explicit lighting math plus ramp/LUT shaping.

### Blender `Glossy BSDF`

Do not try to reproduce the full Blender BSDF implementation.

Reproduce the visible stylized result.

### Blender `Light Path`

The water material disables shadow contribution through Blender ray-type logic.

Unity equivalent:
- configure the ocean material / renderer to avoid undesirable water shadow casting
- do not attempt to emulate Blender ray categories literally

### Blender Procedural Noise

Exact numerical parity across engines is not required.

If procedural differences are visually significant:
- use baked/reference LUT/noise textures
- or implement a stable Unity approximation and document the difference

### Blender Ocean Spectrum

Do not implement FFT solely for mathematical parity.

The production roadmap explicitly prefers Gerstner-like runtime waves.

---

## 10. Implementation Strategy

Prefer:

- URP ShaderLab + HLSL
- small focused C# helper components where needed
- explicit shader properties
- text-based source files that are easy to inspect and version-control

Avoid generating a giant opaque `.shadergraph` asset unless there is a strong practical reason.

The parity implementation should be easy to debug and compare against the Blender dump.

---

## 11. Required Development Phases

### Phase A — Static Material Parity

Target:
`Ocean_ReferenceMesh_v02`

Goal:
Reproduce the Blender surface appearance on the frozen reference geometry.

Implement first:

1. Base Color
2. Crest Color
3. Fresnel
4. Stylized highlight
5. transmission-like tint
6. distance color
7. wave-foam appearance where possible

Do **not** implement dynamic Gerstner displacement yet.

This phase isolates material parity from wave-shape parity.

Create:

```text
M_Ocean_Parity_Static
```

and an appropriate shader, for example:

```text
OceanParityURP.shader
```

### Phase B — Runtime Wave Geometry

Target:
`Ocean_TestMesh_v01`

Add:
- Gerstner / Gerstner-like displacement
- analytic or derived wave normal
- wave height
- steepness
- wave time
- crest masks

Calibrate the runtime waves visually against:

```text
Ocean_ReferenceMesh_v02
```

Do not overwrite the reference mesh.

### Phase C — Foam / Shore / Interaction Inputs

Add:
- wave foam
- shore mask input
- contact foam input
- near-shore transparency behavior

Keep ship interaction decoupled from the core ocean shader.

### Phase D — RTS Readability Pass

Evaluate:
- high RTS camera
- mid-range combat camera
- close presentation camera
- far horizon

Reduce:
- excessive whitecaps
- high-frequency sparkle
- overbright Fresnel
- excessive reflection
- far-distance noise

---

## 12. Test Scene Requirements

Use:

```text
Water_LookDev
```

The scene should contain:

- `Environment_Land_v02`
- `Ocean_ReferenceMesh_v02`
- `Ocean_TestMesh_v01`
- a matched or clearly documented reference camera
- a Unity Directional Light derived from the Blender Sun direction

Keep the reference ocean toggleable for A/B comparison.

If useful, create a simple comparison controller that switches:

```text
Reference Ocean
Production Ocean
```

without altering their transforms.

---

## 13. Material Controls

Expose a practical material inspector with controls corresponding to the roadmap.

At minimum:

### Colors

```text
Base Color
Crest Color
Fresnel Color
Transmission Color
Foam Color
Far Ocean Color
```

### Waves

```text
Primary Wave Direction
Primary Wavelength
Primary Amplitude
Primary Speed

Secondary Wave Direction
Secondary Wavelength
Secondary Amplitude
Secondary Speed
```

Additional wave layers are allowed only if needed to visually match the reference.

### Crest

```text
Crest Threshold
Crest Softness
```

### Highlight

```text
Highlight Strength
Highlight Threshold
Highlight Softness
```

### Fresnel

```text
Fresnel Strength
Fresnel Power
```

### Foam

```text
Foam Amount
Foam Threshold
Foam Noise Scale
Foam Noise Speed
Foam Strength
```

### Shore / Interaction

```text
Shore Width
Shore Foam Width
Shore Alpha
Contact Foam Strength
```

### Distance

```text
Distance Fade Start
Distance Fade End
Far Ocean Color
```

---

## 14. Runtime Constraints

Do not introduce:

- FFT Ocean
- Navier–Stokes fluid simulation
- global ship/ocean fluid interaction
- full-ocean real-time planar reflection
- expensive refraction
- underwater rendering system
- full weather system
- storm system
- day/night cycle
- caustics
- complex physical buoyancy

Do not modify the ship movement system.

Water visuals and gameplay buoyancy remain decoupled.

---

## 15. Forbidden Changes

Do not:

- modify ship movement code
- modify combat code
- modify unrelated scenes
- delete or overwrite Blender reference assets
- replace `Ocean_ReferenceMesh_v02`
- import a third-party water package
- introduce FFT merely to match Blender
- silently invent missing Blender parameters
- refactor unrelated project architecture
- add unnecessary dependencies

Keep all changes scoped to the ocean/environment implementation.

---

## 16. Required Deliverables

Produce:

### Code / Assets

- one or more URP HLSL shader files
- required C# helper scripts
- parity material
- runtime ocean material
- any LUT/noise texture import configuration required
- updated `Water_LookDev` test setup

### Documentation

Create:

```text
OceanParity_README.md
```

It must include:

1. Blender → Unity feature mapping
2. files created
3. files modified
4. shader property mapping
5. Color Ramp / LUT mapping
6. coordinate-system conversions
7. reference camera conversion
8. light-direction conversion
9. known parity gaps
10. performance notes
11. recommended next steps

### Validation

Before finishing:

- Unity project must compile
- no C# compile errors
- no shader compile errors
- no broken material references
- reference assets remain unchanged

---

## 17. Acceptance Criteria

The task is successful when:

- Unity water is immediately recognizable as belonging to the same visual family as the Blender reference.
- Base color, crest color, Fresnel, stylized highlights, transmission tint, foam character, and distance fade are visibly similar.
- Static material parity can be evaluated independently on `Ocean_ReferenceMesh_v02`.
- Runtime wave geometry on `Ocean_TestMesh_v01` has a similar overall wave scale and silhouette.
- The runtime ocean does not depend on Blender.
- The runtime ocean does not depend on prerendered ocean animation.
- The runtime implementation remains substantially simpler than a full FFT ocean.
- RTS ship readability remains intact.
- Foam and highlights do not create a dense blue-white noise field at high camera altitude.

Pixel-perfect matching is **not** required.

Visual parity under controlled reference conditions is required.

---

## 18. Working Method

Before editing:

1. Inspect the Unity project structure.
2. Read all source-of-truth files.
3. Identify the current URP renderer setup.
4. Identify the current `Water_LookDev` scene and ocean asset paths.
5. Write a short implementation plan.

Then implement **Phase A first**.

Do not jump directly to the full production solution.

After Phase A:
- verify compile state
- compare against the frozen reference mesh
- document parity gaps

Then continue to Phase B.

If a Blender feature cannot be translated directly:
- explain why
- implement the lowest-cost visual equivalent
- document the substitution

Do not conceal approximations.

---

## 19. Final Report Format

At completion, report:

```text
Implemented:
- ...

Created:
- ...

Modified:
- ...

Reference parity:
- Base color: ...
- Crest: ...
- Fresnel: ...
- Stylized highlight: ...
- Transmission: ...
- Wave foam: ...
- Shore: ...
- Distance fade: ...
- Wave geometry: ...

Known differences from Blender:
- ...

Compile status:
- ...

Recommended next step:
- ...
```

The final implementation should prioritize **visual identity, RTS readability, maintainability, and runtime cost**, in that order.
