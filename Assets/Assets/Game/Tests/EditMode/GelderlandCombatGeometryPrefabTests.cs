using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class GelderlandCombatGeometryPrefabTests
{
    private const string CombatPrefabPath =
        "Assets/Assets/Game/Ship/Proxy/PF_Ship_Gelderland_Combat_v01.prefab";
    private const int CombatGeometryLayer = 8;

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
    public void CombatGeometryLayer_UsesRegisteredNameAndIndex()
    {
        Assert.That(
            LayerMask.NameToLayer("CombatGeometry"),
            Is.EqualTo(CombatGeometryLayer)
        );
        Assert.That(
            LayerMask.LayerToName(CombatGeometryLayer),
            Is.EqualTo("CombatGeometry")
        );
    }


    [Test]
    public void GelderlandCombatGeometry_UsesSchemeBOnAuthoritativeRoot()
    {
        GameObject prefab = LoadCombatPrefab();
        ShipCombatGeometry geometry = prefab.GetComponent<ShipCombatGeometry>();
        Transform combatGeometry = prefab.transform.Find("CombatGeometry");
        Transform mainHull = prefab.transform.Find("CombatGeometry/MainHull");

        Assert.That(geometry, Is.Not.Null);
        Assert.That(geometry.transform, Is.SameAs(prefab.transform));
        Assert.That(combatGeometry, Is.Not.Null);
        Assert.That(mainHull, Is.Not.Null);
        Assert.That(geometry.MainHullRoot, Is.SameAs(mainHull));
        Assert.That(mainHull.GetComponents<Collider>(), Is.Empty);
        Assert.That(mainHull.childCount, Is.EqualTo(3));
        AssertRegion(geometry, geometry.BowRegion, "Bow", CombatHullRegion.Bow);
        AssertRegion(
            geometry,
            geometry.MidshipRegion,
            "Midship",
            CombatHullRegion.Midship
        );
        AssertRegion(
            geometry,
            geometry.SternRegion,
            "Stern",
            CombatHullRegion.Stern
        );
    }


    [Test]
    public void GelderlandCombatGeometry_UsesDocumentedProvisionalVolumes()
    {
        ShipCombatGeometry geometry = LoadCombatPrefab()
            .GetComponent<ShipCombatGeometry>();

        AssertBox(
            geometry.BowRegion,
            new Vector3(0f, 0f, 15f),
            new Vector3(9f, 4f, 15f)
        );
        AssertBox(
            geometry.MidshipRegion,
            Vector3.zero,
            new Vector3(11f, 4f, 15f)
        );
        AssertBox(
            geometry.SternRegion,
            new Vector3(0f, 0f, -15f),
            new Vector3(9f, 4f, 15f)
        );
    }


    [Test]
    public void CombatGeometry_RemainsIndependentFromGenericShipCollider()
    {
        GameObject prefab = LoadCombatPrefab();
        ShipCombatGeometry geometry = prefab.GetComponent<ShipCombatGeometry>();
        Transform collisionRoot = prefab.transform.Find("CollisionRoot");
        Transform shipColliderObject = prefab.transform.Find(
            "CollisionRoot/ShipCollider"
        );
        Collider shipCollider = shipColliderObject != null
            ? shipColliderObject.GetComponent<Collider>()
            : null;

        Assert.That(collisionRoot, Is.Not.Null);
        Assert.That(shipColliderObject, Is.Not.Null);
        Assert.That(shipCollider, Is.Not.Null);
        Assert.That(shipCollider.isTrigger, Is.False);
        Assert.That(shipCollider.gameObject.layer, Is.Not.EqualTo(CombatGeometryLayer));
        Assert.That(
            geometry.TryResolveRegion(shipCollider, out CombatHitRegion region),
            Is.False
        );
        Assert.That(region, Is.Null);
    }


    [Test]
    public void CombatGeometry_PreservesArtSocketsAndMovementWithoutCombatLeakage()
    {
        GameObject prefab = LoadCombatPrefab();

        Assert.That(prefab.GetComponent<ShipArtDefinition>(), Is.Not.Null);
        Assert.That(prefab.transform.Find("ArtReferences"), Is.Not.Null);
        Assert.That(prefab.GetComponent<ShipMuzzleSockets>(), Is.Not.Null);
        Assert.That(prefab.transform.Find("CombatSockets/Muzzles/Port"), Is.Not.Null);
        Assert.That(
            prefab.transform.Find("CombatSockets/Muzzles/Starboard"),
            Is.Not.Null
        );

        foreach (Type componentType in RequiredMovementComponents)
        {
            Assert.That(
                prefab.GetComponent(componentType),
                Is.Not.Null,
                $"Movement Root is missing {componentType.Name}."
            );
        }

        foreach (Component component in prefab.GetComponentsInChildren<Component>(true))
        {
            string typeName = component.GetType().Name;
            Assert.That(typeName, Does.Not.Contain("Projectile"));
            Assert.That(typeName, Does.Not.Contain("Damage"));
        }
    }


    [Test]
    public void GenericMovementProxies_DoNotContainCombatGeometry()
    {
        string[] paths =
        {
            "Assets/Assets/Game/Ship/Proxy/PF_Proxy_Light_v01.prefab",
            "Assets/Assets/Game/Ship/Proxy/PF_Proxy_Medium_v01.prefab",
            "Assets/Assets/Game/Ship/Proxy/PF_Proxy_Heavy_v01.prefab"
        };

        foreach (string path in paths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, $"Missing generic proxy {path}.");
            Assert.That(prefab.GetComponent<ShipCombatGeometry>(), Is.Null);
            Assert.That(prefab.transform.Find("CombatGeometry"), Is.Null);
            Assert.That(
                prefab.GetComponentInChildren<CombatHitRegion>(true),
                Is.Null
            );
        }
    }


    private static GameObject LoadCombatPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            CombatPrefabPath
        );
        Assert.That(prefab, Is.Not.Null, "Combat placeholder prefab was not found.");
        return prefab;
    }


    private static void AssertRegion(
        ShipCombatGeometry geometry,
        CombatHitRegion region,
        string expectedName,
        CombatHullRegion expectedType
    )
    {
        Assert.That(region, Is.Not.Null, $"Missing {expectedName} region.");
        Assert.That(region.name, Is.EqualTo(expectedName));
        Assert.That(region.transform.parent, Is.SameAs(geometry.MainHullRoot));
        Assert.That(region.Region, Is.EqualTo(expectedType));
        Assert.That(region.Owner, Is.SameAs(geometry));
        Assert.That(region.gameObject.layer, Is.EqualTo(CombatGeometryLayer));
        Assert.That(region.GetComponents<CombatHitRegion>(), Has.Length.EqualTo(1));
        Assert.That(region.GetComponents<Collider>(), Has.Length.EqualTo(1));
        Assert.That(region.QueryCollider, Is.SameAs(region.GetComponent<Collider>()));
        Assert.That(region.QueryCollider, Is.TypeOf<BoxCollider>());
        Assert.That(region.QueryCollider.isTrigger, Is.True);
        Assert.That(region.GetComponent<MeshCollider>(), Is.Null);
        Assert.That(region.GetComponent<Renderer>(), Is.Null);
        Assert.That(region.GetComponent<MeshFilter>(), Is.Null);
        Assert.That(
            geometry.TryResolveRegion(region.QueryCollider, out CombatHitRegion resolved),
            Is.True
        );
        Assert.That(resolved, Is.SameAs(region));
    }


    private static void AssertBox(
        CombatHitRegion region,
        Vector3 expectedLocalPosition,
        Vector3 expectedSize
    )
    {
        BoxCollider box = region.QueryCollider as BoxCollider;

        Assert.That(box, Is.Not.Null);
        Assert.That(region.transform.localPosition, Is.EqualTo(expectedLocalPosition));
        Assert.That(region.transform.localRotation, Is.EqualTo(Quaternion.identity));
        Assert.That(region.transform.localScale, Is.EqualTo(Vector3.one));
        Assert.That(box.center, Is.EqualTo(Vector3.zero));
        Assert.That(box.size, Is.EqualTo(expectedSize));
    }
}
