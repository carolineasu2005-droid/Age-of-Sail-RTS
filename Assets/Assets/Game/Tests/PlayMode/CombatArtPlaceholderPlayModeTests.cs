using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class CombatArtPlaceholderPlayModeTests
{
    private const string CombatPrefabPath =
        "Assets/Assets/Game/Ship/Proxy/PF_Ship_Gelderland_Combat_v01.prefab";

    private GameObject instance;


    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (instance != null)
        {
            Object.Destroy(instance);
        }

        yield return null;
    }


    [UnityTest]
    public IEnumerator DimensionsRemainStableWhenRootPositionAndHeadingChange()
    {
#if UNITY_EDITOR
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            CombatPrefabPath
        );
        Assert.That(prefab, Is.Not.Null);
        instance = Object.Instantiate(prefab);
        ShipArtDefinition definition = instance.GetComponent<ShipArtDefinition>();

        Assert.That(definition, Is.Not.Null);
        Assert.That(definition.TryGetLength(out float initialLength), Is.True);
        Assert.That(definition.TryGetBeam(out float initialBeam), Is.True);
        Vector3 bowRootLocal = instance.transform.InverseTransformPoint(
            definition.BowReference.position
        );

        instance.transform.SetPositionAndRotation(
            new Vector3(137f, 0f, -82f),
            Quaternion.Euler(0f, 123f, 0f)
        );
        yield return null;

        Assert.That(definition.TryGetLength(out float movedLength), Is.True);
        Assert.That(definition.TryGetBeam(out float movedBeam), Is.True);
        Assert.That(movedLength, Is.EqualTo(initialLength).Within(0.0001f));
        Assert.That(movedBeam, Is.EqualTo(initialBeam).Within(0.0001f));
        Assert.That(
            Vector3.Distance(
                definition.BowReference.position,
                instance.transform.TransformPoint(bowRootLocal)
            ),
            Is.LessThan(0.0001f)
        );
#else
        Assert.Ignore("Prefab asset loading requires the Unity Editor.");
        yield break;
#endif
    }
}
