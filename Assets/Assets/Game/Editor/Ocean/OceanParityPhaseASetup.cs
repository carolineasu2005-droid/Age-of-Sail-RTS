using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace AgeOfSailRTS.Editor.Ocean
{
    /// <summary>
    /// Reproducible editor-only setup and validation for the frozen Frame 44 ocean parity scene.
    /// This utility intentionally does not touch runtime wave, ship, combat, or shoreline systems.
    /// </summary>
    public static class OceanParityPhaseASetup
    {
        private const string ScenePath = "Assets/Scenes/Water_LookDev.unity";
        private const string ShaderPath =
            "Assets/Assets/Game/Environment/Ocean/Shaders/OceanParityURP.shader";
        private const string MaterialPath =
            "Assets/Assets/Game/Environment/Ocean/Materials/M_Ocean_Parity_Static.mat";
        private const string ReferenceOceanAssetPath =
            "Assets/Assets/Game/Environment/Ocean/Models/Reference/Ocean_ReferenceMesh_v02.fbx";
        private const string RampFolder =
            "Assets/Assets/Game/Environment/Ocean/Textures/Ramps";
        private const string PreviewPath = "Logs/OceanParity_PhaseA_Preview.png";

        private const string ReferenceOceanName = "Ocean_ReferenceMesh_v02";
        private const string TestOceanName = "Ocean_TestMesh_v01";
        private const string ReferenceCameraName = "ReferenceCamera_Blender44";
        private const string ReferenceSunName = "ReferenceSun_Blender44";

        // Blender (X,Y,Z) -> Unity (X,Z,Y). These were converted from basis vectors,
        // not by copying Blender Euler angles into Unity.
        private static readonly Vector3 CameraPosition =
            new Vector3(2.63429046f, 7.38444996f, -3.04802895f);
        private static readonly Vector3 CameraForward =
            new Vector3(-0.175761238f, -0.381685972f, -0.907427013f).normalized;
        private static readonly Vector3 CameraUp =
            new Vector3(-0.0559586436f, 0.924159765f, -0.377885431f).normalized;

        // Unity directional-light forward is the emitted ray direction. URP's
        // GetMainLight().direction is the opposite, surface-to-light direction.
        private static readonly Vector3 SunPosition =
            new Vector3(0f, 18.8003654f, -43.9675369f);
        private static readonly Vector3 SunRayDirection =
            new Vector3(0.440958291f, -0.226412609f, 0.868500471f).normalized;
        private static readonly Vector3 SunUp =
            new Vector3(0.4823654f, 0.875813007f, -0.0165894274f).normalized;

        private static readonly IReadOnlyList<RampBinding> RampBindings =
            new[]
            {
                new RampBinding("_Ramp01", "WaterRamp_01_ContactFoamShape.png"),
                new RampBinding("_Ramp02", "WaterRamp_02_CrestHeight.png"),
                new RampBinding("_Ramp03", "WaterRamp_03_BroadHighlight.png"),
                new RampBinding("_Ramp04", "WaterRamp_04_ShadowTone.png"),
                new RampBinding("_Ramp05", "WaterRamp_05_FresnelShadow.png"),
                new RampBinding("_Ramp06", "WaterRamp_06_FresnelHighlight.png"),
                new RampBinding("_Ramp07", "WaterRamp_07_Transmission.png"),
                new RampBinding("_Ramp08", "WaterRamp_08_SharpHighlight.png"),
                new RampBinding("_Ramp09", "WaterRamp_09_WaveFoamThreshold.png"),
                new RampBinding("_Ramp10", "WaterRamp_10_WaveFoamShape.png"),
                new RampBinding("_Ramp11", "WaterRamp_11_TransmissionGloss.png"),
            };

        private static readonly IReadOnlyList<string> RequiredMaterialProperties =
            new[]
            {
                "_BaseColor", "_CrestColor", "_FresnelColor", "_TransmissionColor",
                "_FarOceanColor", "_CrestHeightScale", "_CrestHeightOffset", "_CrestStrength",
                "_FresnelIOR", "_FresnelStrength", "_ShadowStrength", "_BroadHighlightPower",
                "_HighlightStrength", "_HighlightSignalPower", "_TransmissionStrength",
                "_TransmissionGlossPower", "_TransmissionNormalFlattening",
                "_MainLightShadowInfluence", "_WaveFoamBrightness", "_FoamAttributeScale",
                "_WaveFoamStrength", "_FoamNoiseScale", "_FoamNoiseStrength",
                "_FoamNoiseOffset", "_FoamDistanceFadeStart", "_FoamDistanceFadeEnd",
                "_DistanceFadeStart", "_DistanceFadeEnd", "_DistanceFadeStrength",
                "_BlenderDistanceDepth", "_ContactFoamRange", "_ContactFoamEvolutionSpeed",
                "_ContactFoamStrength", "_ContactFoamColor", "_WaterTransparencyRange",
                "_WaterReflectionFactor", "_DebugMode",
            };

        private static readonly IReadOnlyList<string> TextureOverridePlatforms =
            new[] { "Standalone", "Android", "iPhone", "WebGL", "Windows Store Apps" };

        private static readonly IReadOnlyDictionary<string, string> ReferenceHashes =
            new Dictionary<string, string>
            {
                {
                    "Assets/Assets/Game/Environment/Ocean/Models/Production/Environment_Land_v02.fbx",
                    "A6D03038E2280F240A4F03695A7950C1F45AE81F0900BBB93434564E3F73B288"
                },
                {
                    "Assets/Assets/Game/Environment/Ocean/Models/Reference/Ocean_ReferenceMesh_v02.fbx",
                    "D052B248A84F46D87975F256038B9131B495DDC21893002EF4BF04FA89BECB87"
                },
                {
                    "Assets/Assets/Game/Environment/Ocean/Models/Production/Ocean_TestMesh_v01.fbx",
                    "9D0CA2820389A198311395359BA6BF7C93673A4D4C37E921AD993E8646C55644"
                },
            };

        [MenuItem("Tools/Ocean/Build Phase A Static Parity")]
        public static void BuildFromMenu()
        {
            RunMenuAction(
                () =>
                {
                    Build();
                    Validate();
                },
                "Ocean Phase A static parity setup and validation completed.");
        }

        [MenuItem("Tools/Ocean/Validate Phase A Static Parity")]
        public static void ValidateFromMenu()
        {
            RunMenuAction(
                Validate,
                "Ocean Phase A static parity validation passed.");
        }

        private static void RunMenuAction(Action action, string successMessage)
        {
            // These commands load the dedicated look-dev scene in Single mode. Ask Unity
            // to save any unrelated scene edits, then put the user's scene workspace back.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                action();
                Debug.Log(successMessage);
            }
            finally
            {
                EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
            }
        }

        /// <summary>Command-line entry point used by the repository validation run.</summary>
        public static void RunBatch()
        {
            try
            {
                Require(
                    SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null,
                    "Ocean parity preview requires a graphics device; do not run this entry point with -nographics.");
                Build();
                Validate();
                RenderPreview();
                Debug.Log("OCEAN_PARITY_PHASE_A_SUCCESS");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("OCEAN_PARITY_PHASE_A_FAILED");
                EditorApplication.Exit(1);
            }
        }

        private static void Build()
        {
            // Reject missing or changed source references before any importer, material, or
            // scene mutation can leave a partial setup behind.
            ValidateReferenceAssetHashes();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureRampImports();

            AssetDatabase.ImportAsset(
                ShaderPath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            Require(shader != null, $"Ocean shader was not imported at {ShaderPath}.");

            Material material = CreateOrUpdateMaterial(shader);
            ConfigureLookDevScene(material);
            AssetDatabase.SaveAssets();
        }

        private static void ConfigureRampImports()
        {
            foreach (RampBinding binding in RampBindings)
            {
                string path = binding.AssetPath;
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Require(importer != null, $"Missing ramp texture importer: {path}");

                importer.textureType = TextureImporterType.Default;
                importer.textureShape = TextureImporterShape.Texture2D;
                importer.sRGBTexture = false;
                importer.mipmapEnabled = false;
                importer.streamingMipmaps = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.anisoLevel = 0;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.crunchedCompression = false;
                importer.maxTextureSize = 512;

                foreach (string platformName in TextureOverridePlatforms)
                {
                    TextureImporterPlatformSettings settings =
                        importer.GetPlatformTextureSettings(platformName);
                    if (!settings.overridden)
                        continue;

                    settings.overridden = false;
                    importer.SetPlatformTextureSettings(settings);
                }

                importer.SaveAndReimport();
            }
        }

        private static Material CreateOrUpdateMaterial(Shader shader)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "M_Ocean_Parity_Static" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            ValidateMaterialSchema(material);

            material.enableInstancing = true;
            material.renderQueue = -1;

            // Actual material-instance values from Water_Full_Dump.txt.
            material.SetColor("_BaseColor", new Color(0f, 0.296138316f, 1f, 1f));
            material.SetColor(
                "_CrestColor",
                new Color(0.0630086586f, 0.623961091f, 0.806952596f, 1f));
            material.SetColor(
                "_FresnelColor",
                new Color(0.270494998f, 0.973446071f, 1f, 1f));
            material.SetColor(
                "_TransmissionColor",
                new Color(0f, 0.879623294f, 1f, 1f));
            material.SetColor(
                "_FarOceanColor",
                new Color(0.590620279f, 0.830767214f, 0.930094957f, 1f));
            material.SetFloat("_HighlightStrength", 0.800000012f);
            material.SetFloat("_WaveFoamBrightness", 1f);
            material.SetFloat("_ContactFoamRange", 1f);
            material.SetFloat("_ContactFoamEvolutionSpeed", 0.700000048f);
            material.SetFloat("_ContactFoamStrength", 0.100000024f);
            material.SetColor(
                "_ContactFoamColor",
                new Color(0.622619152f, 0.622619152f, 0.622619152f, 1f));
            material.SetFloat("_WaterTransparencyRange", 3.39999986f);
            material.SetFloat("_WaterReflectionFactor", 0f);
            material.SetFloat("_BlenderDistanceDepth", -0.799999237f);

            // Unity parity controls. Object-space Y is converted Blender object-space Z.
            material.SetFloat("_CrestHeightScale", 1f);
            material.SetFloat("_CrestHeightOffset", 0f);
            material.SetFloat("_CrestStrength", 1f);
            material.SetFloat("_FresnelIOR", 1.5f);
            material.SetFloat("_FresnelStrength", 1f);
            material.SetFloat("_ShadowStrength", 0.5f);
            material.SetFloat("_BroadHighlightPower", 4f);
            material.SetFloat("_HighlightSignalPower", 12f);
            material.SetFloat("_TransmissionStrength", 1f);
            material.SetFloat("_TransmissionGlossPower", 1f);
            material.SetFloat("_TransmissionNormalFlattening", 0.5f);
            material.SetFloat("_MainLightShadowInfluence", 0.15f);
            material.SetFloat("_FoamAttributeScale", 0.6f);
            material.SetFloat("_WaveFoamStrength", 0.4f);
            material.SetFloat("_FoamNoiseScale", 0.12f);
            material.SetFloat("_FoamNoiseStrength", 1f);
            material.SetVector("_FoamNoiseOffset", Vector4.zero);
            material.SetFloat("_FoamDistanceFadeStart", 35f);
            material.SetFloat("_FoamDistanceFadeEnd", 100f);
            material.SetFloat("_DistanceFadeStart", 0f);
            material.SetFloat("_DistanceFadeEnd", 500f);
            material.SetFloat("_DistanceFadeStrength", 1f);
            material.SetFloat("_DebugMode", 0f);

            foreach (RampBinding binding in RampBindings)
            {
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(binding.AssetPath);
                Require(texture != null, $"Ramp texture did not load: {binding.AssetPath}");
                material.SetTexture(binding.ShaderProperty, texture);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigureLookDevScene(Material material)
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Require(scene.IsValid() && scene.isLoaded, $"Unable to open scene: {ScenePath}");

            GameObject referenceOcean = FindRoot(scene, ReferenceOceanName);
            Require(referenceOcean != null, $"Missing scene root: {ReferenceOceanName}");
            referenceOcean.SetActive(true);
            PrefabUtility.RecordPrefabInstancePropertyModifications(referenceOcean);
            ApplyOceanMaterial(referenceOcean, material);

            GameObject testOcean = FindRoot(scene, TestOceanName);
            if (testOcean != null)
            {
                testOcean.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(testOcean);
            }

            ConfigureReferenceCamera(scene);
            ConfigureReferenceSun(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            Require(EditorSceneManager.SaveScene(scene), $"Unable to save scene: {ScenePath}");
        }

        private static void ApplyOceanMaterial(GameObject referenceOcean, Material material)
        {
            Renderer[] renderers = referenceOcean.GetComponentsInChildren<Renderer>(true);
            Require(renderers.Length > 0, "Reference ocean has no Renderer component.");

            foreach (Renderer renderer in renderers)
            {
                int materialCount = Math.Max(1, renderer.sharedMaterials.Length);
                renderer.sharedMaterials = Enumerable.Repeat(material, materialCount).ToArray();
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
        }

        private static void ConfigureReferenceCamera(Scene scene)
        {
            GameObject cameraObject = FindRoot(scene, ReferenceCameraName);
            if (cameraObject == null)
            {
                cameraObject = new GameObject(ReferenceCameraName);
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
            }

            cameraObject.SetActive(true);

            Camera camera = cameraObject.GetComponent<Camera>();
            if (camera == null)
                camera = cameraObject.AddComponent<Camera>();

            // The look-dev camera is additive. Never rewrite gameplay camera tags,
            // enabled state, transforms, or AudioListeners.
            cameraObject.tag = "Untagged";
            cameraObject.transform.SetPositionAndRotation(
                CameraPosition,
                Quaternion.LookRotation(CameraForward, CameraUp));
            cameraObject.transform.localScale = Vector3.one;

            camera.enabled = true;
            camera.orthographic = false;
            camera.usePhysicalProperties = true;
            camera.focalLength = 25f;
            camera.sensorSize = new Vector2(36f, 24f);
            camera.gateFit = Camera.GateFitMode.Horizontal;
            camera.lensShift = Vector2.zero;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 1000f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.15628466f, 0.816111207f, 1f, 1f);
            camera.allowHDR = true;
            camera.allowMSAA = false;
            camera.depth = 1f;

            RecordPrefabOverrides(cameraObject, cameraObject.transform, camera);
        }

        private static void ConfigureReferenceSun(Scene scene)
        {
            GameObject sunObject = FindRoot(scene, ReferenceSunName)
                ?? FindRoot(scene, "Directional Light");
            if (sunObject == null)
            {
                sunObject = new GameObject(ReferenceSunName);
                SceneManager.MoveGameObjectToScene(sunObject, scene);
            }

            sunObject.SetActive(true);
            sunObject.name = ReferenceSunName;
            Light sun = sunObject.GetComponent<Light>();
            if (sun == null)
                sun = sunObject.AddComponent<Light>();

            sunObject.transform.SetPositionAndRotation(
                SunPosition,
                Quaternion.LookRotation(SunRayDirection, SunUp));
            sunObject.transform.localScale = Vector3.one;

            sun.type = LightType.Directional;
            sun.color = Color.white;
            sun.intensity = 1f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 1f;
            sun.shadowBias = 0.05f;
            sun.shadowNormalBias = 0.4f;
            sun.shadowNearPlane = 0.2f;
            sun.shadowAngle = 0.526000019f;
            sun.bounceIntensity = 1f;
            sun.enabled = true;
            RenderSettings.sun = sun;

            RecordPrefabOverrides(sunObject, sunObject.transform, sun);
        }

        private static void ValidateMaterialSchema(Material material)
        {
            foreach (string propertyName in RequiredMaterialProperties)
            {
                Require(
                    material.HasProperty(propertyName),
                    $"Ocean shader is missing required material property {propertyName}.");
            }

            foreach (RampBinding binding in RampBindings)
            {
                Require(
                    material.HasProperty(binding.ShaderProperty),
                    $"Ocean shader is missing required LUT property {binding.ShaderProperty}.");
            }
        }

        private static void Validate()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            Require(shader != null, "Ocean shader asset is missing.");
            Require(shader.isSupported, "Ocean shader is unsupported on the current editor platform.");

            var shaderErrors = ShaderUtil.GetShaderMessages(shader)
                .Where(message => string.Equals(
                    message.severity.ToString(),
                    "Error",
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();
            Require(
                shaderErrors.Length == 0,
                "Ocean shader compiler errors:\n" + string.Join(
                    "\n",
                    shaderErrors.Select(message => message.message)));

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            Require(material != null, "Static ocean parity material is missing.");
            Require(material.shader == shader, "Static ocean material uses the wrong shader.");
            ValidateMaterialSchema(material);
            RequireApproximately(material.GetFloat("_FresnelIOR"), 1.5f, "Fresnel IOR");
            RequireApproximately(material.GetFloat("_HighlightStrength"), 0.800000012f, "highlight strength");
            RequireApproximately(material.GetFloat("_HighlightSignalPower"), 12f, "highlight signal power");
            RequireApproximately(material.GetFloat("_FoamAttributeScale"), 0.6f, "foam coverage scale");
            RequireApproximately(material.GetFloat("_WaveFoamStrength"), 0.4f, "wave foam strength");
            RequireApproximately(material.GetFloat("_DebugMode"), 0f, "debug mode");

            foreach (RampBinding binding in RampBindings)
            {
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(binding.AssetPath);
                Require(texture != null, $"Missing imported LUT: {binding.AssetPath}");
                Require(
                    texture.width == 512 && texture.height == 1,
                    $"LUT must be 512x1: {binding.AssetPath}");
                Require(
                    material.GetTexture(binding.ShaderProperty) == texture,
                    $"Material LUT reference is missing: {binding.ShaderProperty}");

                TextureImporter importer = AssetImporter.GetAtPath(binding.AssetPath) as TextureImporter;
                Require(importer != null, $"Missing LUT importer: {binding.AssetPath}");
                Require(!importer.sRGBTexture, $"LUT must use non-color sampling: {binding.AssetPath}");
                Require(!importer.mipmapEnabled, $"LUT mipmaps must be disabled: {binding.AssetPath}");
                Require(!importer.streamingMipmaps, $"LUT streaming must be disabled: {binding.AssetPath}");
                Require(importer.wrapMode == TextureWrapMode.Clamp, $"LUT must clamp: {binding.AssetPath}");
                Require(importer.filterMode == FilterMode.Bilinear, $"LUT must use bilinear filtering: {binding.AssetPath}");
                Require(importer.anisoLevel == 0, $"LUT anisotropic filtering must be disabled: {binding.AssetPath}");
                Require(importer.npotScale == TextureImporterNPOTScale.None, $"LUT NPOT scaling must be disabled: {binding.AssetPath}");
                Require(importer.maxTextureSize == 512, $"LUT maximum size must be 512: {binding.AssetPath}");
                Require(importer.textureShape == TextureImporterShape.Texture2D, $"LUT must import as 2D: {binding.AssetPath}");
                Require(
                    importer.textureCompression == TextureImporterCompression.Uncompressed,
                    $"LUT must be uncompressed: {binding.AssetPath}");
                Require(
                    !GraphicsFormatUtility.IsCompressedFormat(texture.graphicsFormat),
                    $"Imported LUT graphics format must be uncompressed: {binding.AssetPath}");

                foreach (string platformName in TextureOverridePlatforms)
                {
                    Require(
                        !importer.GetPlatformTextureSettings(platformName).overridden,
                        $"LUT has an unexpected {platformName} override: {binding.AssetPath}");
                }
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject referenceOcean = FindRoot(scene, ReferenceOceanName);
            Require(
                referenceOcean != null && referenceOcean.activeInHierarchy,
                "Reference ocean is missing or disabled.");

            Renderer[] renderers = referenceOcean.GetComponentsInChildren<Renderer>(true);
            Require(renderers.Length > 0, "Reference ocean has no renderer.");
            Require(renderers.All(renderer => renderer.enabled), "Reference ocean has a disabled renderer.");
            Require(
                renderers.All(renderer => renderer.sharedMaterials.Length > 0),
                "Reference ocean has an empty material slot array.");
            Require(
                renderers.All(renderer => renderer.sharedMaterials.All(candidate => candidate == material)),
                "Reference ocean does not use the static parity material on every slot.");
            Require(
                renderers.All(renderer => renderer.shadowCastingMode == ShadowCastingMode.Off),
                "Reference ocean shadow casting must be disabled.");
            Require(
                renderers.All(renderer => !renderer.receiveShadows),
                "Reference ocean shadow receiving must be disabled.");

            MeshFilter meshFilter = referenceOcean.GetComponentInChildren<MeshFilter>(true);
            Require(meshFilter != null && meshFilter.sharedMesh != null, "Reference ocean mesh is missing.");
            Mesh mesh = meshFilter.sharedMesh;
            Require(mesh.vertexCount >= 51076, "Reference ocean mesh vertex count is unexpectedly low.");
            Require(
                string.Equals(
                    AssetDatabase.GetAssetPath(mesh),
                    ReferenceOceanAssetPath,
                    StringComparison.OrdinalIgnoreCase),
                "Reference ocean scene object is not using Ocean_ReferenceMesh_v02.fbx.");
            Require(
                mesh.HasVertexAttribute(VertexAttribute.Color),
                "Reference ocean lost its exported wave-foam COLOR attribute.");

            GameObject testOcean = FindRoot(scene, TestOceanName);
            Require(testOcean == null || !testOcean.activeSelf, "Overlapping ocean test mesh must be disabled.");

            GameObject cameraObject = FindRoot(scene, ReferenceCameraName);
            Camera camera = cameraObject != null ? cameraObject.GetComponent<Camera>() : null;
            Require(
                cameraObject != null && cameraObject.activeInHierarchy && camera != null && camera.enabled,
                "Reference camera is missing, inactive, or disabled.");
            Require(camera.usePhysicalProperties, "Reference camera must use physical properties.");
            Require(Mathf.Abs(camera.focalLength - 25f) < 0.001f, "Reference camera focal length is wrong.");
            Require(camera.sensorSize == new Vector2(36f, 24f), "Reference camera sensor is wrong.");
            Require(camera.gateFit == Camera.GateFitMode.Horizontal, "Reference camera gate fit is wrong.");
            Require(Vector3.Distance(camera.transform.position, CameraPosition) < 0.0001f, "Camera position mismatch.");
            Require(Vector3.Angle(camera.transform.forward, CameraForward) < 0.01f, "Camera direction mismatch.");
            Require(Vector3.Angle(camera.transform.up, CameraUp) < 0.01f, "Camera roll/up-vector mismatch.");

            GameObject sunObject = FindRoot(scene, ReferenceSunName);
            Light sun = sunObject != null ? sunObject.GetComponent<Light>() : null;
            Require(
                sunObject != null && sunObject.activeInHierarchy && sun != null && sun.enabled
                    && sun.type == LightType.Directional,
                "Reference sun is missing, inactive, disabled, or not directional.");
            Require(RenderSettings.sun == sun, "Reference sun is not assigned as the scene sun.");
            Require(Vector3.Angle(sun.transform.forward, SunRayDirection) < 0.01f, "Sun direction mismatch.");
            Require(Vector3.Angle(sun.transform.up, SunUp) < 0.01f, "Sun roll/up-vector mismatch.");
            Require(Mathf.Abs(sun.intensity - 1f) < 0.001f, "Reference sun intensity mismatch.");
            Require(sun.color == Color.white, "Reference sun color mismatch.");
            Require(sun.shadows == LightShadows.Soft, "Reference sun must use soft shadows.");
            Require(Mathf.Abs(sun.shadowAngle - 0.526000019f) < 0.001f, "Reference sun angle mismatch.");

            ValidateReferenceAssetHashes();

            Debug.Log(
                $"Ocean parity validation: shader OK, material OK, {RampBindings.Count} LUTs OK, "
                + $"mesh={mesh.name} vertices={mesh.vertexCount} COLOR=yes, camera OK, sun OK.");
        }

        private static void ValidateReferenceAssetHashes()
        {
            foreach (KeyValuePair<string, string> pair in ReferenceHashes)
            {
                string fullPath = ToProjectAbsolutePath(pair.Key);
                Require(File.Exists(fullPath), $"Reference asset is missing: {pair.Key}");
                Require(
                    string.Equals(ComputeSha256(fullPath), pair.Value, StringComparison.OrdinalIgnoreCase),
                    $"Reference asset changed unexpectedly: {pair.Key}");
            }
        }

        private static string ComputeSha256(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
            }
        }

        private static void RenderPreview()
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject cameraObject = FindRoot(scene, ReferenceCameraName);
            Camera camera = cameraObject != null ? cameraObject.GetComponent<Camera>() : null;
            Require(camera != null, "Cannot render preview without the reference camera.");

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            Require(material != null, "Cannot render preview without the parity material.");

            float previousDebugMode = material.GetFloat("_DebugMode");
            try
            {
                RenderPreviewImage(camera, material, 0f, PreviewPath);
                RenderPreviewImage(camera, material, 2f, "Logs/OceanParity_Debug_Crest.png");
                RenderPreviewImage(camera, material, 3f, "Logs/OceanParity_Debug_Fresnel.png");
                RenderPreviewImage(camera, material, 4f, "Logs/OceanParity_Debug_Highlight.png");
                RenderPreviewImage(camera, material, 5f, "Logs/OceanParity_Debug_Transmission.png");
                RenderPreviewImage(camera, material, 6f, "Logs/OceanParity_Debug_Foam.png");
                RenderPreviewImage(camera, material, 7f, "Logs/OceanParity_Debug_Distance.png");
            }
            finally
            {
                material.SetFloat("_DebugMode", previousDebugMode);
            }
        }

        private static void RenderPreviewImage(
            Camera camera,
            Material material,
            float debugMode,
            string outputPath)
        {
            material.SetFloat("_DebugMode", debugMode);

            const int width = 960;
            const int height = 540;
            RenderTexture target = RenderTexture.GetTemporary(
                width,
                height,
                24,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Default);
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            Texture2D image = null;

            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image = new Texture2D(width, height, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply(false, false);

                string fullPath = ToProjectAbsolutePath(outputPath);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                File.WriteAllBytes(fullPath, image.EncodeToPNG());
                Debug.Log($"Ocean parity preview mode {debugMode:0} written to {fullPath}");
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(target);
                if (image != null)
                    UnityEngine.Object.DestroyImmediate(image);
            }
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            return scene.GetRootGameObjects().FirstOrDefault(root => root.name == name);
        }

        private static string ToProjectAbsolutePath(string projectRelativePath)
        {
            DirectoryInfo projectRoot = Directory.GetParent(Application.dataPath);
            Require(projectRoot != null, "Unable to resolve the Unity project root.");
            return Path.GetFullPath(Path.Combine(
                projectRoot.FullName,
                projectRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static void RecordPrefabOverrides(params UnityEngine.Object[] objects)
        {
            foreach (UnityEngine.Object candidate in objects)
            {
                if (candidate != null && PrefabUtility.IsPartOfPrefabInstance(candidate))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(candidate);
            }
        }

        private static void RequireApproximately(float actual, float expected, string label)
        {
            Require(
                Mathf.Abs(actual - expected) < 0.0001f,
                $"Ocean material {label} mismatch: expected {expected}, got {actual}.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private readonly struct RampBinding
        {
            public RampBinding(string shaderProperty, string fileName)
            {
                ShaderProperty = shaderProperty;
                FileName = fileName;
            }

            public string ShaderProperty { get; }
            public string FileName { get; }
            public string AssetPath => $"{RampFolder}/{FileName}";
        }
    }
}
