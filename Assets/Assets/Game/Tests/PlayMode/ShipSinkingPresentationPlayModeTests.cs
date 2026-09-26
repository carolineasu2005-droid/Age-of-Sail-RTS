using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class ShipSinkingPresentationPlayModeTests
{
    private const string CombatPrefabPath =
        "Assets/Assets/Game/Ship/Proxy/"
        + "PF_Ship_Gelderland_Combat_v01.prefab";

    private GameObject root;
    private ShipIntegrityProfile integrityProfile;
    private ShipSinkingPresentationProfile presentationProfile;


    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (root != null)
        {
            Object.Destroy(root);
        }

        if (integrityProfile != null)
        {
            Object.Destroy(integrityProfile);
        }

        if (presentationProfile != null)
        {
            Object.Destroy(presentationProfile);
        }

        yield return null;
    }


    [UnityTest]
    public IEnumerator FormalCombatPrefab_InitializesIntegrityOperational()
    {
#if UNITY_EDITOR
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            CombatPrefabPath
        );
        Assert.That(prefab, Is.Not.Null);
        root = Object.Instantiate(prefab);

        yield return null;

        ShipIntegrity integrity = root.GetComponent<ShipIntegrity>();
        Assert.That(integrity, Is.Not.Null);
        Assert.That(integrity.IsInitialized, Is.True);
        Assert.That(
            integrity.CurrentIntegrity,
            Is.EqualTo(integrity.MaximumIntegrity)
        );
        Assert.That(
            integrity.LifecycleState,
            Is.EqualTo(ShipCombatLifecycleState.Operational)
        );
#else
        Assert.Ignore("Prefab loading requires the Unity Editor.");
        yield break;
#endif
    }


    [UnityTest]
    public IEnumerator SinkingLifecycle_AnimatesVisualRootOnlyAndRemainsComplete()
    {
        root = new GameObject("Runtime Sinking Presentation Ship");
        root.SetActive(false);
        root.transform.SetPositionAndRotation(
            new Vector3(10f, 2f, -4f),
            Quaternion.Euler(0f, 25f, 0f)
        );

        GameObject visual = new GameObject("VisualRoot");
        visual.transform.SetParent(root.transform, false);
        visual.transform.localPosition = Vector3.up;
        visual.transform.localRotation = Quaternion.Euler(0f, 3f, 0f);
        GameObject geometry = new GameObject("CombatGeometry");
        geometry.transform.SetParent(root.transform, false);
        geometry.transform.localPosition = Vector3.forward * 2f;

        integrityProfile =
            ScriptableObject.CreateInstance<ShipIntegrityProfile>();
        SetPrivateField(integrityProfile, "maximumIntegrity", 1000f);
        SetPrivateField(
            integrityProfile,
            "combatDisabledThresholdNormalized",
            0.25f
        );
        SetPrivateField(
            integrityProfile,
            "sinkingThresholdNormalized",
            0.05f
        );
        presentationProfile = ScriptableObject.CreateInstance<
            ShipSinkingPresentationProfile
        >();
        SetPrivateField(
            presentationProfile,
            "sinkDurationSeconds",
            0.04f
        );
        SetPrivateField(
            presentationProfile,
            "sinkDepthMeters",
            2f
        );
        SetPrivateField(
            presentationProfile,
            "optionalRollDegrees",
            5f
        );

        ShipIntegrity integrity = root.AddComponent<ShipIntegrity>();
        SetPrivateField(integrity, "integrityProfile", integrityProfile);
        Assert.That(integrity.TryInitialize(), Is.True);
        ShipArtDefinition art = root.AddComponent<ShipArtDefinition>();
        SetPrivateField(art, "visualRoot", visual.transform);
        ShipSinkingPresentation presentation =
            root.AddComponent<ShipSinkingPresentation>();
        SetPrivateField(
            presentation,
            "presentationProfile",
            presentationProfile
        );
        root.SetActive(true);

        Vector3 rootPosition = root.transform.position;
        Quaternion rootRotation = root.transform.rotation;
        Vector3 geometryPosition = geometry.transform.localPosition;
        Vector3 initialVisualPosition = visual.transform.localPosition;
        Quaternion initialVisualRotation = visual.transform.localRotation;
        Assert.That(
            integrity.TryApplyIntegrityLoss(950f, out _),
            Is.True
        );
        float sinkingIntegrity = integrity.CurrentIntegrity;

        yield return null;

        Assert.That(presentation.HasStarted, Is.True);
        Assert.That(presentation.Progress, Is.Zero);
        Assert.That(
            visual.transform.localPosition,
            Is.EqualTo(initialVisualPosition)
        );

        float completionTimeoutSeconds = Mathf.Max(
            0.5f,
            presentationProfile.SinkDurationSeconds * 5f
        );
        float completionDeadline = Time.realtimeSinceStartup
            + completionTimeoutSeconds;

        while (!presentation.IsComplete
            && Time.realtimeSinceStartup < completionDeadline)
        {
            yield return null;
        }

        Assert.That(
            presentation.IsComplete,
            Is.True,
            $"Sinking presentation did not complete within "
                + $"{completionTimeoutSeconds:F2}s; progress was "
                + $"{presentation.Progress:F3}."
        );
        Assert.That(presentation.Progress, Is.EqualTo(1f));
        Assert.That(
            visual.transform.localPosition,
            Is.EqualTo(initialVisualPosition + Vector3.down * 2f)
        );
        Assert.That(
            Quaternion.Angle(
                visual.transform.localRotation,
                initialVisualRotation
                    * Quaternion.AngleAxis(5f, Vector3.forward)
            ),
            Is.LessThan(0.001f)
        );
        Assert.That(root.transform.position, Is.EqualTo(rootPosition));
        Assert.That(
            Quaternion.Angle(root.transform.rotation, rootRotation),
            Is.LessThan(0.001f)
        );
        Assert.That(
            geometry.transform.localPosition,
            Is.EqualTo(geometryPosition)
        );
        Assert.That(integrity.CurrentIntegrity, Is.EqualTo(sinkingIntegrity));
        Assert.That(
            integrity.LifecycleState,
            Is.EqualTo(ShipCombatLifecycleState.Sinking)
        );
        Assert.That(root.activeSelf, Is.True);

        Vector3 completedPosition = visual.transform.localPosition;
        yield return null;
        yield return null;
        Assert.That(
            visual.transform.localPosition,
            Is.EqualTo(completedPosition)
        );
        Assert.That(root, Is.Not.Null);
    }


    private static void SetPrivateField(
        object target,
        string fieldName,
        object value
    )
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        Assert.That(field, Is.Not.Null, fieldName);
        field.SetValue(target, value);
    }
}
