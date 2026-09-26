using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class ShipSinkingPresentationTests
{
    private const BindingFlags PrivateInstance =
        BindingFlags.Instance | BindingFlags.NonPublic;
    private const string CombatPrefabPath =
        "Assets/Assets/Game/Ship/Proxy/"
        + "PF_Ship_Gelderland_Combat_v01.prefab";
    private static readonly string[] GenericProxyPaths =
    {
        "Assets/Assets/Game/Ship/Proxy/PF_Proxy_Light_v01.prefab",
        "Assets/Assets/Game/Ship/Proxy/PF_Proxy_Medium_v01.prefab",
        "Assets/Assets/Game/Ship/Proxy/PF_Proxy_Heavy_v01.prefab"
    };

    private PresentationFixture fixture;


    [TearDown]
    public void TearDown()
    {
        fixture?.Dispose();
        fixture = null;
    }


    [TestCase(ShipCombatLifecycleState.Operational)]
    [TestCase(ShipCombatLifecycleState.CombatDisabled)]
    public void NonSinkingLifecycle_DoesNotStartOrChangePresentation(
        ShipCombatLifecycleState lifecycle
    )
    {
        fixture = PresentationFixture.Create();
        fixture.SetLifecycle(lifecycle);
        Vector3 initialPosition = fixture.VisualRoot.localPosition;
        Quaternion initialRotation = fixture.VisualRoot.localRotation;

        Advance(fixture.Presentation, 20f);
        Advance(fixture.Presentation, 20f);

        Assert.That(fixture.Presentation.HasStarted, Is.False);
        Assert.That(fixture.Presentation.IsComplete, Is.False);
        Assert.That(fixture.Presentation.Progress, Is.Zero);
        Assert.That(
            fixture.VisualRoot.localPosition,
            Is.EqualTo(initialPosition)
        );
        AssertQuaternion(fixture.VisualRoot.localRotation, initialRotation);
    }


    [Test]
    public void FirstSinkingRead_StartsAtInitialPoseAndDoesNotRestart()
    {
        fixture = PresentationFixture.Create();
        fixture.SetLifecycle(ShipCombatLifecycleState.Sinking);
        Vector3 initialPosition = fixture.VisualRoot.localPosition;
        Quaternion initialRotation = fixture.VisualRoot.localRotation;

        Advance(fixture.Presentation, 5f);

        Assert.That(fixture.Presentation.HasStarted, Is.True);
        Assert.That(fixture.Presentation.Progress, Is.Zero);
        Assert.That(
            fixture.VisualRoot.localPosition,
            Is.EqualTo(initialPosition)
        );
        AssertQuaternion(fixture.VisualRoot.localRotation, initialRotation);

        Advance(fixture.Presentation, 5f);
        Assert.That(fixture.Presentation.Progress, Is.EqualTo(0.5f));

        Advance(fixture.Presentation, 1f);
        Assert.That(fixture.Presentation.Progress, Is.EqualTo(0.6f));
        Assert.That(
            fixture.VisualRoot.localPosition.y,
            Is.EqualTo(initialPosition.y - 3.6f).Within(0.0001f)
        );
    }


    [Test]
    public void SinkingProgress_AppliesHalfAndFullPoseThenClamps()
    {
        fixture = PresentationFixture.Create();
        fixture.SetLifecycle(ShipCombatLifecycleState.Sinking);
        Vector3 initialPosition = fixture.VisualRoot.localPosition;
        Quaternion initialRotation = fixture.VisualRoot.localRotation;
        Advance(fixture.Presentation, 0f);

        Advance(fixture.Presentation, 5f);

        Assert.That(fixture.Presentation.Progress, Is.EqualTo(0.5f));
        Assert.That(
            fixture.VisualRoot.localPosition,
            Is.EqualTo(initialPosition + Vector3.down * 3f)
        );
        AssertQuaternion(
            fixture.VisualRoot.localRotation,
            initialRotation * Quaternion.AngleAxis(6f, Vector3.forward)
        );

        Advance(fixture.Presentation, 100f);
        Vector3 completedPosition = fixture.VisualRoot.localPosition;
        Quaternion completedRotation = fixture.VisualRoot.localRotation;

        Assert.That(fixture.Presentation.Progress, Is.EqualTo(1f));
        Assert.That(fixture.Presentation.IsComplete, Is.True);
        Assert.That(
            completedPosition,
            Is.EqualTo(initialPosition + Vector3.down * 6f)
        );
        AssertQuaternion(
            completedRotation,
            initialRotation * Quaternion.AngleAxis(12f, Vector3.forward)
        );

        Advance(fixture.Presentation, 100f);
        Assert.That(fixture.Presentation.Progress, Is.EqualTo(1f));
        Assert.That(
            fixture.VisualRoot.localPosition,
            Is.EqualTo(completedPosition)
        );
        AssertQuaternion(
            fixture.VisualRoot.localRotation,
            completedRotation
        );
    }


    [Test]
    public void SinkingPresentation_ChangesOnlyVisualLocalPose()
    {
        fixture = PresentationFixture.Create();
        fixture.SetLifecycle(ShipCombatLifecycleState.Sinking);
        Vector3 rootPosition = fixture.Root.transform.position;
        Quaternion rootRotation = fixture.Root.transform.rotation;
        Vector3 geometryLocalPosition =
            fixture.CombatGeometryRoot.localPosition;
        Quaternion geometryLocalRotation =
            fixture.CombatGeometryRoot.localRotation;
        float integrityAfterTransition = fixture.Integrity.CurrentIntegrity;
        ShipCombatLifecycleState lifecycleAfterTransition =
            fixture.Integrity.LifecycleState;

        Advance(fixture.Presentation, 0f);
        Advance(fixture.Presentation, 10f);

        Assert.That(fixture.Root.transform.position, Is.EqualTo(rootPosition));
        AssertQuaternion(fixture.Root.transform.rotation, rootRotation);
        Assert.That(
            fixture.CombatGeometryRoot.localPosition,
            Is.EqualTo(geometryLocalPosition)
        );
        AssertQuaternion(
            fixture.CombatGeometryRoot.localRotation,
            geometryLocalRotation
        );
        Assert.That(
            fixture.Integrity.CurrentIntegrity,
            Is.EqualTo(integrityAfterTransition)
        );
        Assert.That(
            fixture.Integrity.LifecycleState,
            Is.EqualTo(lifecycleAfterTransition)
        );
        Assert.That(fixture.Root.activeSelf, Is.True);
        Assert.That(fixture.Integrity.enabled, Is.True);
        Assert.That(fixture.Presentation.enabled, Is.True);
    }


    [Test]
    public void FormalPrefab_HasConfiguredPresentationAndSeparatedGeometry()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            CombatPrefabPath
        );

        Assert.That(prefab, Is.Not.Null);
        ShipSinkingPresentation[] presentations =
            prefab.GetComponents<ShipSinkingPresentation>();
        Assert.That(presentations, Has.Length.EqualTo(1));
        Assert.That(presentations[0].PresentationProfile, Is.Not.Null);
        Assert.That(
            presentations[0].PresentationProfile.SinkDurationSeconds,
            Is.GreaterThan(0f)
        );
        Assert.That(
            presentations[0].PresentationProfile.SinkDepthMeters,
            Is.GreaterThan(0f)
        );

        ShipArtDefinition art = prefab.GetComponent<ShipArtDefinition>();
        ShipCombatGeometry geometry =
            prefab.GetComponent<ShipCombatGeometry>();
        Assert.That(art.VisualRoot, Is.Not.Null);
        Assert.That(geometry.MainHullRoot, Is.Not.Null);
        Assert.That(art.VisualRoot, Is.Not.SameAs(prefab.transform));
        Assert.That(
            geometry.MainHullRoot.IsChildOf(art.VisualRoot),
            Is.False
        );

        foreach (string path in GenericProxyPaths)
        {
            GameObject generic = AssetDatabase.LoadAssetAtPath<GameObject>(
                path
            );
            Assert.That(generic, Is.Not.Null, path);
            Assert.That(
                generic.GetComponent<ShipSinkingPresentation>(),
                Is.Null,
                path
            );
        }
    }


    [Test]
    public void PresentationSource_HasNoGameplayOrPhysicsOwnership()
    {
        string source = File.ReadAllText(Path.Combine(
            Application.dataPath,
            "Assets/Game/Scripts/Combat/ShipSinkingPresentation.cs"
        ));

        Assert.That(source, Does.Contain("ShipIntegrity"));
        Assert.That(source, Does.Contain("VisualRoot"));
        Assert.That(source, Does.Contain("localPosition"));
        Assert.That(source, Does.Contain("localRotation"));
        foreach (string forbidden in new[]
        {
            "transform.position =",
            "transform.rotation =",
            "transform.localPosition =",
            "transform.localRotation =",
            "TryApplyIntegrityLoss",
            "ShipFireEligibility",
            "ShipAutoTargetScorer",
            "ShipExposureReference",
            "ShipDispersionConfiguration",
            "CombatProjectile",
            "ShipCombatGeometry",
            "ShipSailingSpeed",
            "ShipTurning",
            "ShipDestinationController",
            "Rigidbody",
            "Destroy("
        })
        {
            Assert.That(source, Does.Not.Contain(forbidden), forbidden);
        }
    }


    private static void Advance(
        ShipSinkingPresentation presentation,
        float deltaTimeSeconds
    )
    {
        MethodInfo method = typeof(ShipSinkingPresentation).GetMethod(
            "AdvancePresentation",
            PrivateInstance
        );
        Assert.That(method, Is.Not.Null);
        method.Invoke(presentation, new object[] { deltaTimeSeconds });
    }


    private static void AssertQuaternion(
        Quaternion actual,
        Quaternion expected
    )
    {
        Assert.That(Quaternion.Angle(actual, expected), Is.LessThan(0.001f));
    }


    private sealed class PresentationFixture
    {
        private ShipIntegrityProfile integrityProfile;
        private ShipSinkingPresentationProfile presentationProfile;

        public GameObject Root { get; private set; }

        public Transform VisualRoot { get; private set; }

        public Transform CombatGeometryRoot { get; private set; }

        public ShipIntegrity Integrity { get; private set; }

        public ShipSinkingPresentation Presentation { get; private set; }


        public static PresentationFixture Create()
        {
            PresentationFixture result = new PresentationFixture();
            result.Root = new GameObject("Sinking Presentation Test Ship");
            result.Root.SetActive(false);
            result.Root.transform.SetPositionAndRotation(
                new Vector3(12f, 3f, -8f),
                Quaternion.Euler(0f, 37f, 0f)
            );

            GameObject visual = new GameObject("VisualRoot");
            visual.transform.SetParent(result.Root.transform, false);
            visual.transform.localPosition = new Vector3(1f, 2f, 3f);
            visual.transform.localRotation = Quaternion.Euler(2f, 4f, 6f);
            result.VisualRoot = visual.transform;

            GameObject geometry = new GameObject("CombatGeometry");
            geometry.transform.SetParent(result.Root.transform, false);
            geometry.transform.localPosition = new Vector3(4f, 5f, 6f);
            geometry.transform.localRotation = Quaternion.Euler(1f, 3f, 5f);
            result.CombatGeometryRoot = geometry.transform;

            result.integrityProfile =
                ScriptableObject.CreateInstance<ShipIntegrityProfile>();
            SetSerialized(result.integrityProfile, "maximumIntegrity", 1000f);
            SetSerialized(
                result.integrityProfile,
                "combatDisabledThresholdNormalized",
                0.25f
            );
            SetSerialized(
                result.integrityProfile,
                "sinkingThresholdNormalized",
                0.05f
            );

            result.presentationProfile = ScriptableObject.CreateInstance<
                ShipSinkingPresentationProfile
            >();
            SetSerialized(
                result.presentationProfile,
                "sinkDurationSeconds",
                10f
            );
            SetSerialized(
                result.presentationProfile,
                "sinkDepthMeters",
                6f
            );
            SetSerialized(
                result.presentationProfile,
                "optionalRollDegrees",
                12f
            );

            result.Integrity = result.Root.AddComponent<ShipIntegrity>();
            SetSerialized(
                result.Integrity,
                "integrityProfile",
                result.integrityProfile
            );
            Assert.That(result.Integrity.TryInitialize(), Is.True);
            ShipArtDefinition art =
                result.Root.AddComponent<ShipArtDefinition>();
            SetSerialized(art, "visualRoot", result.VisualRoot);
            result.Presentation =
                result.Root.AddComponent<ShipSinkingPresentation>();
            SetSerialized(
                result.Presentation,
                "presentationProfile",
                result.presentationProfile
            );
            result.Root.SetActive(true);
            return result;
        }


        public void SetLifecycle(ShipCombatLifecycleState lifecycle)
        {
            Integrity = CombatLifecycleTestUtility
                .SetLifecycleThroughIntegrityLoss(Root, lifecycle);
            Assert.That(Integrity.LifecycleState, Is.EqualTo(lifecycle));
        }


        public void Dispose()
        {
            Object.DestroyImmediate(Root);
            Object.DestroyImmediate(integrityProfile);
            Object.DestroyImmediate(presentationProfile);
        }


        private static void SetSerialized(
            Object target,
            string propertyName,
            Object value
        )
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }


        private static void SetSerialized(
            Object target,
            string propertyName,
            float value
        )
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
