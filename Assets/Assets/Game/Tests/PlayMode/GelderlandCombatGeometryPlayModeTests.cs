using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class GelderlandCombatGeometryPlayModeTests
{
    private const string CombatPrefabPath =
        "Assets/Assets/Game/Ship/Proxy/PF_Ship_Gelderland_Combat_v01.prefab";
    private const int CombatGeometryLayer = 8;
    private const float QueryDistance = 40f;

    private static readonly Type[] MovementBehaviourTypes =
    {
        typeof(ShipMovementProfileController),
        typeof(ShipSailingSpeed),
        typeof(ShipTurning),
        typeof(ShipHeadingController),
        typeof(ShipTacking),
        typeof(ShipWearing),
        typeof(ShipManeuverPlanner),
        typeof(ShipDestinationController)
    };

    private GameObject instance;
    private ShipCombatGeometry geometry;


    [UnitySetUp]
    public IEnumerator SetUp()
    {
#if UNITY_EDITOR
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            CombatPrefabPath
        );
        Assert.That(prefab, Is.Not.Null);
        instance = UnityEngine.Object.Instantiate(prefab);
        geometry = instance.GetComponent<ShipCombatGeometry>();
        Assert.That(geometry, Is.Not.Null);
        DisableMovementBehaviours();
        Physics.SyncTransforms();
        yield return null;
#else
        Assert.Ignore("Prefab asset loading requires the Unity Editor.");
        yield break;
#endif
    }


    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (instance != null)
        {
            UnityEngine.Object.Destroy(instance);
        }

        yield return null;
    }


    [UnityTest]
    public IEnumerator BowQuery_ResolvesBowRegion()
    {
        AssertCombatQuery(geometry.BowRegion, CombatHullRegion.Bow);
        yield return null;
    }


    [UnityTest]
    public IEnumerator MidshipQuery_ResolvesMidshipRegion()
    {
        AssertCombatQuery(geometry.MidshipRegion, CombatHullRegion.Midship);
        yield return null;
    }


    [UnityTest]
    public IEnumerator SternQuery_ResolvesSternRegion()
    {
        AssertCombatQuery(geometry.SternRegion, CombatHullRegion.Stern);
        yield return null;
    }


    [UnityTest]
    public IEnumerator QueryOutsideHull_MissesCombatGeometry()
    {
        Vector3 origin = instance.transform.TransformPoint(
            new Vector3(-20f, 0f, 30f)
        );

        bool hit = Physics.Raycast(
            origin,
            instance.transform.right,
            out RaycastHit _,
            QueryDistance,
            CombatMask,
            QueryTriggerInteraction.Collide
        );

        Assert.That(hit, Is.False);
        yield return null;
    }


    [UnityTest]
    public IEnumerator DisabledVisualRoot_DoesNotChangeCombatQuery()
    {
        AssertCombatQuery(geometry.MidshipRegion, CombatHullRegion.Midship);

        Transform visualRoot = instance.transform.Find("VisualRoot");
        Assert.That(visualRoot, Is.Not.Null);
        visualRoot.gameObject.SetActive(false);
        Physics.SyncTransforms();

        AssertCombatQuery(geometry.MidshipRegion, CombatHullRegion.Midship);
        yield return null;
    }


    [UnityTest]
    public IEnumerator DisabledGenericShipCollider_DoesNotChangeCombatQuery()
    {
        Collider shipCollider = GetGenericShipCollider();
        shipCollider.enabled = false;
        Physics.SyncTransforms();

        AssertCombatQuery(geometry.BowRegion, CombatHullRegion.Bow);
        yield return null;
    }


    [UnityTest]
    public IEnumerator ResizedGenericShipCollider_IsExcludedByCombatMask()
    {
        BoxCollider shipCollider = GetGenericShipCollider() as BoxCollider;
        Assert.That(shipCollider, Is.Not.Null);
        shipCollider.size = new Vector3(1000f, 1000f, 1000f);
        Physics.SyncTransforms();

        RaycastHit hit = CastIntoRegion(geometry.SternRegion);

        Assert.That(hit.collider, Is.Not.SameAs(shipCollider));
        Assert.That(hit.collider.gameObject.layer, Is.EqualTo(CombatGeometryLayer));
        Assert.That(
            geometry.TryResolveRegion(hit.collider, out CombatHitRegion resolved),
            Is.True
        );
        Assert.That(resolved, Is.SameAs(geometry.SternRegion));
        yield return null;
    }


    [UnityTest]
    public IEnumerator RootTranslation_MovesCombatQueryGeometry()
    {
        instance.transform.position = new Vector3(137f, 4f, -82f);
        Physics.SyncTransforms();

        AssertCombatQuery(geometry.MidshipRegion, CombatHullRegion.Midship);
        yield return null;
    }


    [UnityTest]
    public IEnumerator RootRotation_MovesCombatQueryGeometry()
    {
        instance.transform.rotation = Quaternion.Euler(0f, 127f, 0f);
        Physics.SyncTransforms();

        AssertCombatQuery(geometry.BowRegion, CombatHullRegion.Bow);
        AssertCombatQuery(geometry.SternRegion, CombatHullRegion.Stern);
        yield return null;
    }


    [UnityTest]
    public IEnumerator TriggerQuery_RequiresExplicitCollidePolicy()
    {
        CombatHitRegion region = geometry.MidshipRegion;
        Vector3 origin = QueryOrigin(region);

        bool ignored = Physics.Raycast(
            origin,
            instance.transform.right,
            out RaycastHit _,
            QueryDistance,
            CombatMask,
            QueryTriggerInteraction.Ignore
        );
        bool collided = Physics.Raycast(
            origin,
            instance.transform.right,
            out RaycastHit hit,
            QueryDistance,
            CombatMask,
            QueryTriggerInteraction.Collide
        );

        Assert.That(ignored, Is.False);
        Assert.That(collided, Is.True);
        Assert.That(hit.collider, Is.SameAs(region.QueryCollider));
        yield return null;
    }


    private int CombatMask => 1 << CombatGeometryLayer;


    private void AssertCombatQuery(
        CombatHitRegion expected,
        CombatHullRegion expectedType
    )
    {
        RaycastHit hit = CastIntoRegion(expected);

        Assert.That(hit.collider, Is.SameAs(expected.QueryCollider));
        Assert.That(
            geometry.TryResolveRegion(hit.collider, out CombatHitRegion resolved),
            Is.True
        );
        Assert.That(resolved, Is.SameAs(expected));
        Assert.That(resolved.Owner, Is.SameAs(geometry));
        Assert.That(resolved.Region, Is.EqualTo(expectedType));
    }


    private RaycastHit CastIntoRegion(CombatHitRegion region)
    {
        bool hit = Physics.Raycast(
            QueryOrigin(region),
            instance.transform.right,
            out RaycastHit result,
            QueryDistance,
            CombatMask,
            QueryTriggerInteraction.Collide
        );

        Assert.That(hit, Is.True, $"Combat query missed {region.Region}.");
        return result;
    }


    private Vector3 QueryOrigin(CombatHitRegion region)
    {
        Physics.SyncTransforms();
        return region.QueryCollider.bounds.center - instance.transform.right * 20f;
    }


    private Collider GetGenericShipCollider()
    {
        Transform colliderTransform = instance.transform.Find(
            "CollisionRoot/ShipCollider"
        );
        Assert.That(colliderTransform, Is.Not.Null);
        Collider shipCollider = colliderTransform.GetComponent<Collider>();
        Assert.That(shipCollider, Is.Not.Null);
        Assert.That(shipCollider.gameObject.layer, Is.Not.EqualTo(CombatGeometryLayer));
        return shipCollider;
    }


    private void DisableMovementBehaviours()
    {
        foreach (Type componentType in MovementBehaviourTypes)
        {
            Behaviour behaviour = instance.GetComponent(componentType) as Behaviour;

            if (behaviour != null)
            {
                behaviour.enabled = false;
            }
        }
    }
}
