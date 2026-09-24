# Ocean Phase A Static Parity

This implementation targets the Blender 5.2.2 Eevee look-development state at frame 44. It is a visual/functional URP translation on the frozen `Ocean_ReferenceMesh_v02` mesh, not a node-for-node material conversion and not a runtime ocean simulation.

## 1. What Was Implemented

- A readable, single-pass URP HLSL shader and material for static ocean look parity.
- Object-space crest coloring, Schlick Fresnel shaping, graphic light bands, a cyan transmission-like response, exported vertex-color foam, deterministic foam breakup, and a pale far-water fade.
- Dynamic use of the active URP main directional light through `GetMainLight`; no light direction is hard-coded in the shader.
- Seven material debug views: final, base, crest, Fresnel, highlight, transmission, foam, and distance.
- A dedicated physical reference camera and converted reference sun in `Water_LookDev`.
- An editor-only, repeatable build/validation utility at `Tools > Ocean` that configures imports, creates the material, configures the scene, checks the exact source mesh and hashes, and can render comparison diagnostics in batch mode.
- Opaque rendering with no displacement, runtime waves, real-time reflections, shoreline transparency, or water shadow-caster pass.

The source FBXs and their mesh data are not edited. The static material is assigned as a scene-level prefab material override.

## 2. Files Created

- `Assets/Assets/Game/Environment/Ocean/Shaders/OceanParityURP.shader` — Phase A URP shader.
- `Assets/Assets/Game/Environment/Ocean/Materials/M_Ocean_Parity_Static.mat` — calibrated static parity material.
- `Assets/Assets/Game/Environment/Ocean/Textures/Ramps/WaterRamp_01_ContactFoamShape.png`
- `Assets/Assets/Game/Environment/Ocean/Textures/Ramps/WaterRamp_02_CrestHeight.png`
- `Assets/Assets/Game/Environment/Ocean/Textures/Ramps/WaterRamp_03_BroadHighlight.png`
- `Assets/Assets/Game/Environment/Ocean/Textures/Ramps/WaterRamp_04_ShadowTone.png`
- `Assets/Assets/Game/Environment/Ocean/Textures/Ramps/WaterRamp_05_FresnelShadow.png`
- `Assets/Assets/Game/Environment/Ocean/Textures/Ramps/WaterRamp_06_FresnelHighlight.png`
- `Assets/Assets/Game/Environment/Ocean/Textures/Ramps/WaterRamp_07_Transmission.png`
- `Assets/Assets/Game/Environment/Ocean/Textures/Ramps/WaterRamp_08_SharpHighlight.png`
- `Assets/Assets/Game/Environment/Ocean/Textures/Ramps/WaterRamp_09_WaveFoamThreshold.png`
- `Assets/Assets/Game/Environment/Ocean/Textures/Ramps/WaterRamp_10_WaveFoamShape.png`
- `Assets/Assets/Game/Environment/Ocean/Textures/Ramps/WaterRamp_11_TransmissionGloss.png`
- `Assets/Assets/Game/Editor/Ocean/OceanParityPhaseASetup.cs` — focused setup, validation, and preview utility; it is editor-only and adds no runtime component.
- `Assets/Assets/Game/Environment/Ocean/OceanParity_README.md` — this record.

Unity also generated the corresponding `.meta` files and folder metadata.

## 3. Files Modified

- `Assets/Scenes/Water_LookDev.unity`
  - Applies `M_Ocean_Parity_Static` to the renderers under `Ocean_ReferenceMesh_v02` as a scene override.
  - Disables water shadow casting/receiving and disables the overlapping `Ocean_TestMesh_v01` comparison surface.
  - Adds `ReferenceCamera_Blender44`, untagged and at depth 1. The existing `Main Camera`, its tag, transform, enabled state, and `AudioListener` are preserved.
  - Reuses the scene's simple directional light as `ReferenceSun_Blender44`, converts its direction, and assigns it as `RenderSettings.sun`.

No render-pipeline asset, renderer asset, unrelated scene, prefab, gameplay script, or global project architecture was modified.

The following authoritative assets remain byte-for-byte unchanged:

- `Ocean_ReferenceMesh_v02.fbx`
- `Environment_Land_v02.fbx`
- `Ocean_TestMesh_v01.fbx`
- All source PNGs in `Docs/OceanReference/Water_Ramp_LUTs/`

The LUTs under `Assets` are copies with project-friendly English filenames; their PNG contents were not altered.

## 4. Blender Feature to Unity Feature Mapping

| Blender feature | Unity Phase A equivalent |
|---|---|
| Base water color | Literal `_BaseColor` in a custom opaque URP forward pass |
| Object-space Z crest lookup | Imported local Y (converted Blender Z), shaped by `_Ramp02` |
| Fresnel, IOR 1.5 | Schlick dielectric Fresnel (`F0 = 0.04`) shaped by `_Ramp05` and `_Ramp06` |
| Glossy BSDF to Shader to RGB | Explicit `N dot H` signals shaped by `_Ramp03`, `_Ramp04`, and `_Ramp08` |
| Named blurred normal `模糊法向` | Interpolated mesh normal flattened 50% toward world up for the transmission branch |
| Blurred-normal dot Sun | Signed dot with `GetMainLight().direction`, remapped from -1..1 to 0..1, then `_Ramp07` |
| Transmission glossy gate | Flattened-normal `N dot H` shaped by `_Ramp11` |
| Ocean foam layer `浪花泡沫` | Real imported vertex `COLOR.r`, shaped by `_Ramp09` and `_Ramp10` |
| Procedural foam noise | Stable low-cost value noise at 0.12 cells per world meter |
| Contact foam `接触浮沫` | Property and `_Ramp01` retained, behavior postponed |
| Shore region `岸边区域` | Postponed; no usable exported channel was found |
| Distance/View Z branch | Unity positive view depth, 0..500 m, blended toward `_FarOceanColor` |
| Reflection factor 0 | No SSR, planar reflection, or extra reflection system |
| Light Path water-shadow bypass | No `ShadowCaster` pass plus renderer shadow casting disabled |
| Blender emission-style color outputs | Custom-lit opaque color; Fresnel/transmission target colors remain literal and light direction controls their masks |

The reference FBX contains 51,076 vertices and a real grayscale vertex-color layer named `浪花泡沫` in the FBX. Its direct values span 0 to about 0.447. The shader therefore uses the source foam data rather than a height/slope fallback.

## 5. Blender Property to Unity Property Mapping

The material-instance values below override divergent group defaults found in the dump.

| Blender value | Unity property | Final value |
|---|---|---|
| Base Color | `_BaseColor` | `(0, 0.296138316, 1, 1)` |
| Crest Color | `_CrestColor` | `(0.0630086586, 0.623961091, 0.806952596, 1)` |
| Fresnel Highlight Color | `_FresnelColor` | `(0.270494998, 0.973446071, 1, 1)` |
| Transmission Color | `_TransmissionColor` | `(0, 0.879623294, 1, 1)` |
| Highlight Strength | `_HighlightStrength` | `0.800000012` |
| Wave Foam Brightness | `_WaveFoamBrightness` | `1` |
| Contact Foam Range | `_ContactFoamRange` | `1` (reserved) |
| Contact Foam Evolution Speed | `_ContactFoamEvolutionSpeed` | `0.700000048` (reserved) |
| Contact Foam Strength | `_ContactFoamStrength` | `0.100000024` (reserved) |
| Contact Foam Color | `_ContactFoamColor` | `(0.622619152, 0.622619152, 0.622619152, 1)` (reserved) |
| Water Transparency Range | `_WaterTransparencyRange` | `3.39999986` (reserved) |
| Water Reflection Factor | `_WaterReflectionFactor` | `0` |
| Distance Depth | `_BlenderDistanceDepth` | `-0.799999237` (reference only; see section 11) |
| Far emission color | `_FarOceanColor` | `(0.590620279, 0.830767214, 0.930094957, 1)` |
| Fresnel IOR | `_FresnelIOR` | `1.5` |

Additional Unity visual-parity controls:

| Property | Final value | Purpose |
|---|---:|---|
| `_CrestHeightScale`, `_CrestHeightOffset`, `_CrestStrength` | `1`, `0`, `1` | Apply Ramp 02 directly to imported object height |
| `_ShadowStrength` | `0.5` | Strength of broad stylized tonal shaping |
| `_BroadHighlightPower` | `4` | Narrows the broad glossy proxy |
| `_HighlightSignalPower` | `12` | Keeps Ramp 08 as a narrow graphic highlight rather than a white wall |
| `_TransmissionNormalFlattening` | `0.5` | Explicit proxy for the missing blurred-normal attribute |
| `_MainLightShadowInfluence` | `0.15` | Keeps graphic shading readable in URP shadows |
| `_FoamAttributeScale` | `0.6` | Reduces exported foam coverage before the sharp ramp |
| `_WaveFoamStrength` | `0.4` | Reduces final bright-foam dominance |
| `_FoamNoiseScale`, `_FoamNoiseStrength` | `0.12`, `1` | Low-cost stable breakup, corresponding to Blender scale 6 over about 50 m |
| `_FoamDistanceFadeStart`, `_FoamDistanceFadeEnd` | `35`, `100` m | Suppresses distant bright detail |
| `_DistanceFadeStart`, `_DistanceFadeEnd`, `_DistanceFadeStrength` | `0`, `500`, `1` | Required far-water softening |

The project uses Linear color space. The authoritative material numbers are stored as supplied and passed through Unity material color properties without hand-authored gamma compensation; Blender Standard and Unity Linear output still need visual rather than framebuffer-identical comparison.

## 6. Color Ramp to LUT Mapping

| Shader property | Project LUT filename | Use | Authoritative stops |
|---|---|---|---|
| `_Ramp01` | `WaterRamp_01_ContactFoamShape.png` | Contact foam, bound but not sampled in Phase A | Linear: black at `0.0458015203`, white at `0.179390043` |
| `_Ramp02` | `WaterRamp_02_CrestHeight.png` | Crest/height mix | Linear: white at `0`, black at `0.751908243` |
| `_Ramp03` | `WaterRamp_03_BroadHighlight.png` | Broad glossy/light split | Linear: black at `0.667938769`, white at `1` |
| `_Ramp04` | `WaterRamp_04_ShadowTone.png` | Shadow tonal remap | Linear: gray `0.272830486` at `0`, white at `0.858778656` |
| `_Ramp05` | `WaterRamp_05_FresnelShadow.png` | Broad Fresnel/shadow input | Linear: black at `0.324427426`, white at `0.507633507` |
| `_Ramp06` | `WaterRamp_06_FresnelHighlight.png` | Main Fresnel threshold | Linear: black at `0.595419586`, white at `0.6183213` |
| `_Ramp07` | `WaterRamp_07_Transmission.png` | Transmission facing response | Ease: white at `0.282442689`, black at `0.653053045` |
| `_Ramp08` | `WaterRamp_08_SharpHighlight.png` | Sharp graphic highlight | Linear: black at `0.97272706`, white at `1` |
| `_Ramp09` | `WaterRamp_09_WaveFoamThreshold.png` | Exported foam pre-threshold | Linear: black at `0.0381679386`, white at `0.0801528767` |
| `_Ramp10` | `WaterRamp_10_WaveFoamShape.png` | Final foam edge | Ease: black at `0`, white at `0.0839695409` |
| `_Ramp11` | `WaterRamp_11_TransmissionGloss.png` | Transmission glossy gate | Linear: black at `0.580153048`, white at `1` |

Every LUT is imported as a 512x1 Texture2D with non-color/linear sampling, Clamp wrap mode, bilinear filtering, anisotropic filtering off, NPOT scaling off, mipmaps and streaming mipmaps off, compression and Crunch off, maximum size 512, and all common platform overrides cleared. Validation also rejects an actually compressed runtime graphics format. The supplied PNG files themselves remain unchanged.

## 7. Reference Camera Conversion

Blender source:

- Position: `(2.63429046, -3.04802895, 7.38444996)`
- Euler radians: `(1.17912614, -0.0156398658, 2.95672894)`
- Lens/sensor: 25 mm, 36x24 mm
- Clip range: 0.1 to 1000 m
- Reference aspect: 1920x1080

The imported asset convention was verified and the conversion `(xB, yB, zB) -> (xB, zB, yB)` was applied to basis vectors rather than copying Euler components. Unity result:

- Position: `(2.63429046, 7.38444996, -3.04802895)`
- Forward: `(-0.175761238, -0.381685972, -0.907427013)`
- Up: `(-0.0559586436, 0.924159765, -0.377885431)`
- Equivalent quaternion XYZW: `(0.010322135, -0.976528014, 0.194457146, 0.092048394)`
- Horizontal FOV: about `71.5078` degrees
- Effective 16:9 vertical FOV with horizontal gate fit: about `44.0959` degrees

`ReferenceCamera_Blender44` uses Unity physical-camera mode with the exact 25 mm lens, 36x24 mm sensor, horizontal gate fit, and 0.1/1000 m clip planes. It is additive, untagged, and does not rewrite the existing `Main Camera`.

## 8. Sun and Light Conversion

Blender source:

- Euler radians: `(1.31781781, 0.440343082, -0.579557538)`
- Color: white
- Energy: 10
- Angular size: `0.00918043219` radians = `0.526000019` degrees
- Shadows: enabled

Converted Unity orientation:

- Directional Light forward/emitted-ray direction: `(0.440958291, -0.226412609, 0.868500471)`
- Surface-to-light direction expected from URP `GetMainLight`: `(-0.440958291, 0.226412609, -0.868500471)`
- Equivalent quaternion XYZW: `(0.056067982, 0.250212177, -0.242795966, 0.935574883)`

The final Unity directional-light intensity is `1.0`, white, with soft shadows and a `0.526` degree shadow angle. Blender Energy 10 was deliberately not copied numerically. The light direction is configured only on the LookDev light; the shader queries the current URP main light dynamically.

## 9. Approximated Blender Features

- Eevee `Shader to RGB` and Glossy BSDF are replaced by explicit, inspectable `N dot H` signals and the captured ramps. URP cannot reproduce Eevee's internal result exactly.
- The FBX has no second vector channel for `模糊法向`. A 50% blend from the mesh normal toward world up provides a stable broad normal for both transmission signals.
- Exact Blender procedural noise is replaced by deterministic bilinear value noise. Scale is visually tied to the 50 m reference patch.
- Fresnel uses the standard Schlick dielectric approximation at IOR 1.5, then the exact captured LUT thresholds.
- Foam uses the genuine exported `浪花泡沫` color layer, but Blender's exact noise and Map Range composition is approximated. Coverage and strength are intentionally reduced for RTS readability.
- Main-light color affects the explicit white highlight, while the emission-like Fresnel and transmission target colors remain literal.
- The visible distance fade is a requirement-driven interpretation, not the literal captured negative factor; see section 11.
- Foam fading from 35 to 100 m is a readability extension derived from the roadmap, not a literal Phase A Blender graph value.

## 10. Intentionally Postponed

- Gerstner or FFT runtime displacement and runtime normal reconstruction
- Ship wakes, bow waves, and ship/contact foam
- Shore SDF generation, Scene Depth shoreline, and `岸边区域` behavior
- Full near-shore transparency, refraction, and underwater rendering
- Planar reflections, SSR, and added reflection-probe systems
- Weather, storms, day/night, and caustics
- Buoyancy, ship movement, navigation, and combat integration

The captured contact-foam, transparency, and reflection values remain visible as reserved material properties so later phases can retain the source terminology without pretending those systems exist now.

## 11. Known Parity Gaps and Conflicts

- The task and actual project assets use v02 reference names, while parts of the earlier roadmap still refer to v01. The existing v02 assets are authoritative here.
- The roadmap describes dynamic-wave work that is explicitly superseded by this task's static Phase A boundary.
- `Ocean_ReferenceMesh_v02` contains usable `浪花泡沫` vertex color, normals, and UVs, but no usable exported `接触浮沫`, `岸边区域`, or `模糊法向` channels were found.
- The active Blender material-output node is not identified in the text dump, and the environment dump omits some world-ramp/Float Curve control points.
- No canonical Blender framebuffer/reference image was supplied, so calibration is based on the authoritative parameters, mesh data, LUTs, and the required visual behavior rather than pixel comparison.
- Blender Standard and Unity Linear rendering/color management do not produce identical framebuffers.
- The static 50 m mesh is finite. Its edge/horizon behavior is a reference limitation, not a production large-ocean solution.
- The shader has no `DepthOnly` or `DepthNormals` pass in Phase A. If depth priming, SSAO integration, or shoreline depth sampling becomes required, add those deliberately in a later phase.
- The production land material and geometry were left untouched; its appearance is not part of ocean parity.

The captured distance branch is internally inconsistent with the requested visible far fade:

```text
MapRange(ViewZ 0..500, -1..DistanceDepth)
Captured DistanceDepth = -0.799999237
Result = -1..-0.799999237
```

That result is nonpositive and would normally clamp the far-emission contribution off. A divergent group default of about `15.3999987` would activate it. `_BlenderDistanceDepth` preserves the captured value for traceability, while the active Unity implementation uses explicit 0..500 m positive view-depth fading because the task explicitly requires the far ocean to become lighter and softer. This is a documented requirement-driven extension.

## 12. Performance Implications

- The material is opaque: no transparent sorting, water transparency overdraw, refraction copy, planar-reflection camera, or SSR cost.
- One custom forward pass is used, with no water shadow-caster pass and no displacement or tessellation.
- The final path samples 10 ramp LUTs. `_Ramp01` is bound for later contact foam but not sampled.
- Foam evaluates one low-cost 2D value-noise sample (four hash corners plus interpolation).
- Other work is the main-light lookup, dot products, Schlick Fresnel, a few powers, fog, and mask blends.
- Eleven uncompressed 512x1 RGBA LUTs total roughly 22 KiB of raw texel data before platform/runtime overhead; disabling mipmaps and compression preserves their narrow thresholds.
- Debug views use one material integer and dynamic fragment branches; they do not create a large shader-keyword variant set.
- Phase A proves material identity only. It does not establish production ocean tiling, LOD, culling, or large-world cost.

## 13. Recommended Phase B

Add two initial Gerstner waves while preserving this static setup for direct A/B comparison. Visually target the Blender Ocean Modifier rather than trying to reproduce a full Phillips spectrum immediately:

- Spatial reference: 50 m
- Wave scale: 0.4 m
- Smallest wave: 0.1 m
- Choppiness: 1
- Wind velocity: 3
- Damping: 0.5
- Frame-44 source time: 1.4666667 s (`frame / 30`)

Recompute displaced normals consistently, feed runtime height/steepness into the existing crest and foam masks, and validate at both the reference camera and elevated RTS cameras around 30-50 m ships. Keep visual waves decoupled from ship movement and buoyancy. Only after dynamic-wave parity passes should wakes and shoreline systems begin.
