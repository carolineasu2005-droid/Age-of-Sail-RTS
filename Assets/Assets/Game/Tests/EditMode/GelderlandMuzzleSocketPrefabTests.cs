using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class GelderlandMuzzleSocketPrefabTests
{
    private const string CombatPrefabPath =
        "Assets/Assets/Game/Ship/Proxy/PF_Ship_Gelderland_Combat_v01.prefab";

    private static readonly Type[] RequiredMovementComponents =
    {
        typeof(ShipMovementProfileController),
        typeof(ShipSailingSpeed),
        typeof(ShipTurning),
        typeof(ShipHeadingController),
        typeof(ShipTacking),
        typeof(ShipWearing),
        typeof(ShipLeeway),
        typeof(ShipManeuverPlanner),
        typeof(ShipDestinationController)
    };


    [Test]
    public void GelderlandMuzzleSockets_AreIntegratedOnAuthoritativeRoot()
    {
        GameObject prefab = LoadCombatPrefab();
        ShipMuzzleSockets muzzleSockets = prefab.GetComponent<ShipMuzzleSockets>();

        Assert.That(muzzleSockets, Is.Not.Null);
        Assert.That(muzzleSockets.transform, Is.SameAs(prefab.transform));
        Assert.That(prefab.transform.Find("CombatSockets/Muzzles/Port"), Is.Not.Null);
        Assert.That(
            prefab.transform.Find("CombatSockets/Muzzles/Starboard"),
            Is.Not.Null
        );
    }


    [Test]
    public void GelderlandMainBattery_HasThirteenSocketsPerSideAndTwentySixTotal()
    {
        ShipMuzzleSockets muzzleSockets = LoadMuzzleSockets();

        Assert.That(muzzleSockets.PortMuzzles, Has.Count.EqualTo(13));
        Assert.That(muzzleSockets.StarboardMuzzles, Has.Count.EqualTo(13));
        Assert.That(
            muzzleSockets.PortMuzzles.Count + muzzleSockets.StarboardMuzzles.Count,
            Is.EqualTo(26)
        );
    }


    [Test]
    public void GelderlandMainBattery_UsesCanonicalNamesAndBowToSternOrder()
    {
        GameObject prefab = LoadCombatPrefab();
        ShipMuzzleSockets muzzleSockets = prefab.GetComponent<ShipMuzzleSockets>();

        AssertCanonicalOrder(prefab.transform, muzzleSockets.PortMuzzles, "P");
        AssertCanonicalOrder(
            prefab.transform,
            muzzleSockets.StarboardMuzzles,
            "S"
        );

        ShipArtDefinition artDefinition = prefab.GetComponent<ShipArtDefinition>();
        float bowZ = RootLocalPosition(prefab.transform, artDefinition.BowReference).z;
        float sternZ = RootLocalPosition(prefab.transform, artDefinition.SternReference).z;

        Assert.That(
            RootLocalPosition(prefab.transform, muzzleSockets.PortMuzzles[0]).z,
            Is.LessThan(bowZ)
        );
        Assert.That(
            RootLocalPosition(prefab.transform, muzzleSockets.PortMuzzles[12]).z,
            Is.GreaterThan(sternZ)
        );
        Assert.That(
            RootLocalPosition(prefab.transform, muzzleSockets.StarboardMuzzles[0]).z,
            Is.LessThan(bowZ)
        );
        Assert.That(
            RootLocalPosition(prefab.transform, muzzleSockets.StarboardMuzzles[12]).z,
            Is.GreaterThan(sternZ)
        );
    }


    [Test]
    public void GelderlandMainBattery_IsMirroredAndPointsOutward()
    {
        GameObject prefab = LoadCombatPrefab();
        ShipMuzzleSockets muzzleSockets = prefab.GetComponent<ShipMuzzleSockets>();

        for (int index = 0; index < 13; index++)
        {
            Transform port = muzzleSockets.PortMuzzles[index];
            Transform starboard = muzzleSockets.StarboardMuzzles[index];
            Vector3 portPosition = RootLocalPosition(prefab.transform, port);
            Vector3 starboardPosition = RootLocalPosition(
                prefab.transform,
                starboard
            );
            Vector3 portDirection = prefab.transform.InverseTransformDirection(
                port.forward
            );
            Vector3 starboardDirection = prefab.transform.InverseTransformDirection(
                starboard.forward
            );

            Assert.That(portPosition.x, Is.LessThan(0f));
            Assert.That(starboardPosition.x, Is.GreaterThan(0f));
            Assert.That(portPosition.x, Is.EqualTo(-starboardPosition.x).Within(0.001f));
            Assert.That(portPosition.y, Is.EqualTo(starboardPosition.y).Within(0.001f));
            Assert.That(portPosition.z, Is.EqualTo(starboardPosition.z).Within(0.001f));
            Assert.That(Vector3.Dot(portDirection, Vector3.left), Is.GreaterThan(0.99f));
            Assert.That(
                Vector3.Dot(starboardDirection, Vector3.right),
                Is.GreaterThan(0.99f)
            );
        }
    }


    [Test]
    public void GelderlandMuzzleIntegration_PreservesPhaseOneAndMovementBoundaries()
    {
        GameObject prefab = LoadCombatPrefab();
        ShipArtDefinition artDefinition = prefab.GetComponent<ShipArtDefinition>();
        Transform artReferences = prefab.transform.Find("ArtReferences");
        Transform debugRoot = prefab.transform.Find("DebugRoot");

        Assert.That(artDefinition, Is.Not.Null);
        Assert.That(artReferences, Is.Not.Null);
        Assert.That(debugRoot, Is.Not.Null);
        Assert.That(artReferences, Is.Not.SameAs(debugRoot));
        Assert.That(artDefinition.BowReference.parent, Is.SameAs(artReferences));
        Assert.That(artDefinition.SternReference.parent, Is.SameAs(artReferences));
        Assert.That(artDefinition.PortReference.parent, Is.SameAs(artReferences));
        Assert.That(artDefinition.StarboardReference.parent, Is.SameAs(artReferences));

        foreach (Type componentType in RequiredMovementComponents)
        {
            Assert.That(
                prefab.GetComponent(componentType),
                Is.Not.Null,
                $"Movement Root is missing {componentType.Name}."
            );
        }

        Assert.That(prefab.transform.Find("CombatGeometry"), Is.Null);
        Assert.That(prefab.transform.Find("Exposure"), Is.Null);
    }


    private static GameObject LoadCombatPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            CombatPrefabPath
        );
        Assert.That(prefab, Is.Not.Null, "Combat placeholder prefab was not found.");
        return prefab;
    }


    private static ShipMuzzleSockets LoadMuzzleSockets()
    {
        ShipMuzzleSockets muzzleSockets = LoadCombatPrefab()
            .GetComponent<ShipMuzzleSockets>();
        Assert.That(muzzleSockets, Is.Not.Null);
        return muzzleSockets;
    }


    private static void AssertCanonicalOrder(
        Transform root,
        System.Collections.Generic.IReadOnlyList<Transform> sockets,
        string prefix
    )
    {
        Assert.That(sockets, Has.Count.EqualTo(13));

        for (int index = 0; index < sockets.Count; index++)
        {
            Assert.That(sockets[index].name, Is.EqualTo(prefix + (index + 1).ToString("00")));
            Assert.That(
                sockets[index].childCount,
                Is.Zero,
                $"{sockets[index].name} must be the authored muzzle origin, not a cannon-tip container."
            );

            if (index == 0)
            {
                continue;
            }

            float previousZ = RootLocalPosition(root, sockets[index - 1]).z;
            float currentZ = RootLocalPosition(root, sockets[index]).z;
            Assert.That(previousZ, Is.GreaterThan(currentZ));
        }
    }


    private static Vector3 RootLocalPosition(Transform root, Transform item)
    {
        return root.InverseTransformPoint(item.position);
    }
}
