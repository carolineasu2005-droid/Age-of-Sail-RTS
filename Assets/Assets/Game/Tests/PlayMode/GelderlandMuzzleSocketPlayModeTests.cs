using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class GelderlandMuzzleSocketPlayModeTests
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
    public IEnumerator MuzzleDirectionsAndCanonicalOrderFollowRootTransform()
    {
#if UNITY_EDITOR
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            CombatPrefabPath
        );
        Assert.That(prefab, Is.Not.Null);
        instance = Object.Instantiate(prefab);
        ShipMuzzleSockets muzzleSockets = instance.GetComponent<ShipMuzzleSockets>();

        Assert.That(muzzleSockets, Is.Not.Null);
        Assert.That(muzzleSockets.PortMuzzles, Has.Count.EqualTo(13));
        Assert.That(muzzleSockets.StarboardMuzzles, Has.Count.EqualTo(13));

        Transform[] originalPortOrder = CopyOrder(muzzleSockets.PortMuzzles);
        Transform[] originalStarboardOrder = CopyOrder(
            muzzleSockets.StarboardMuzzles
        );
        Vector3 portRootLocalDirection = instance.transform.InverseTransformDirection(
            originalPortOrder[0].forward
        );
        Vector3 starboardRootLocalDirection =
            instance.transform.InverseTransformDirection(
                originalStarboardOrder[0].forward
            );

        instance.transform.SetPositionAndRotation(
            new Vector3(-94f, 0f, 163f),
            Quaternion.Euler(0f, 137f, 0f)
        );
        yield return null;

        Assert.That(muzzleSockets.PortMuzzles, Has.Count.EqualTo(13));
        Assert.That(muzzleSockets.StarboardMuzzles, Has.Count.EqualTo(13));
        AssertOrderUnchanged(originalPortOrder, muzzleSockets.PortMuzzles);
        AssertOrderUnchanged(
            originalStarboardOrder,
            muzzleSockets.StarboardMuzzles
        );
        Assert.That(
            Vector3.Distance(
                originalPortOrder[0].forward,
                instance.transform.TransformDirection(portRootLocalDirection)
            ),
            Is.LessThan(0.0001f)
        );
        Assert.That(
            Vector3.Distance(
                originalStarboardOrder[0].forward,
                instance.transform.TransformDirection(starboardRootLocalDirection)
            ),
            Is.LessThan(0.0001f)
        );
#else
        Assert.Ignore("Prefab asset loading requires the Unity Editor.");
        yield break;
#endif
    }


    private static Transform[] CopyOrder(
        System.Collections.Generic.IReadOnlyList<Transform> sockets
    )
    {
        Transform[] copy = new Transform[sockets.Count];

        for (int index = 0; index < sockets.Count; index++)
        {
            copy[index] = sockets[index];
        }

        return copy;
    }


    private static void AssertOrderUnchanged(
        Transform[] expected,
        System.Collections.Generic.IReadOnlyList<Transform> actual
    )
    {
        Assert.That(actual, Has.Count.EqualTo(expected.Length));

        for (int index = 0; index < expected.Length; index++)
        {
            Assert.That(actual[index], Is.SameAs(expected[index]));
        }
    }
}
