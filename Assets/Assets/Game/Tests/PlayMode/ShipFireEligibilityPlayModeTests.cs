using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class ShipFireEligibilityPlayModeTests
{
    private const int CombatGeometryLayer = 8;
    private const BindingFlags PrivateInstance =
        BindingFlags.Instance | BindingFlags.NonPublic;

    private readonly List<GameObject> createdObjects =
        new List<GameObject>();

    private GameObject shooterRoot;
    private GameObject targetRoot;
    private ShipFireEligibility eligibility;
    private bool originalQueriesHitTriggers;


    [UnitySetUp]
    public IEnumerator SetUp()
    {
        originalQueriesHitTriggers = Physics.queriesHitTriggers;
        shooterRoot = CreateShip("Shooter Root", Vector3.zero);
        targetRoot = CreateShip(
            "Target Root",
            Vector3.right * 50f
        );
        CreateCombatGeometryCollider(
            "Target Combat Geometry",
            targetRoot.transform,
            Vector3.zero
        );
        eligibility = shooterRoot.AddComponent<ShipFireEligibility>();
        SetPrivateField(eligibility, "effectiveRangeMeters", 30f);
        SetPrivateField(eligibility, "maximumRangeMeters", 100f);
        Physics.SyncTransforms();
        yield return null;
    }


    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Physics.queriesHitTriggers = originalQueriesHitTriggers;

        foreach (GameObject createdObject in createdObjects)
        {
            if (createdObject != null)
            {
                Object.Destroy(createdObject);
            }
        }

        createdObjects.Clear();
        yield return null;
    }


    [UnityTest]
    public IEnumerator ClearCombatGeometryLine_IsNotBlocked()
    {
        FireEligibilityResult result = Evaluate();

        Assert.That(result.Blocked, Is.False);
        Assert.That(result.BlockingCollider, Is.Null);
        Assert.That(result.CanFire, Is.True);
        yield return null;
    }


    [UnityTest]
    public IEnumerator CombatGeometryBlockerBetweenShips_BlocksFire()
    {
        Collider blocker = CreateCombatGeometryCollider(
            "Between Blocker",
            null,
            Vector3.right * 25f
        );

        FireEligibilityResult result = Evaluate();

        Assert.That(result.Blocked, Is.True);
        Assert.That(result.BlockingCollider, Is.SameAs(blocker));
        Assert.That(result.BlockingObject, Is.SameAs(blocker.gameObject));
        Assert.That(result.CanFire, Is.False);
        Assert.That(
            result.FailureReasons.HasFlag(
                FireEligibilityFailure.Obstructed
            ),
            Is.True
        );
        yield return null;
    }


    [UnityTest]
    public IEnumerator CombatGeometryBlockerBehindTarget_DoesNotBlock()
    {
        CreateCombatGeometryCollider(
            "Behind Blocker",
            null,
            Vector3.right * 75f
        );

        FireEligibilityResult result = Evaluate();

        Assert.That(result.Blocked, Is.False);
        Assert.That(result.CanFire, Is.True);
        yield return null;
    }


    [UnityTest]
    public IEnumerator DefaultLayerColliderBetweenShips_IsExcludedByFilter()
    {
        GameObject unrelated = CreateRoot(
            "Unrelated Default Collider",
            Vector3.right * 25f
        );
        BoxCollider unrelatedCollider =
            unrelated.AddComponent<BoxCollider>();
        unrelatedCollider.isTrigger = false;

        FireEligibilityResult result = Evaluate();

        Assert.That(unrelated.layer, Is.EqualTo(0));
        Assert.That(result.Blocked, Is.False);
        Assert.That(result.CanFire, Is.True);
        yield return null;
    }


    [UnityTest]
    public IEnumerator TriggerBlocker_IsDetectedWhenGlobalTriggerQueriesAreOff()
    {
        Physics.queriesHitTriggers = false;
        Collider blocker = CreateCombatGeometryCollider(
            "Trigger Blocker",
            null,
            Vector3.right * 25f
        );

        FireEligibilityResult result = Evaluate();

        Assert.That(blocker.isTrigger, Is.True);
        Assert.That(result.Blocked, Is.True);
        Assert.That(result.BlockingCollider, Is.SameAs(blocker));
        yield return null;
    }


    [UnityTest]
    public IEnumerator DisabledRenderer_DoesNotChangeEligibilityGeometry()
    {
        MeshRenderer renderer = targetRoot.AddComponent<MeshRenderer>();
        FireEligibilityResult before = Evaluate();
        renderer.enabled = false;

        FireEligibilityResult after = Evaluate();

        Assert.That(after.Side, Is.EqualTo(before.Side));
        Assert.That(
            after.DistanceMeters,
            Is.EqualTo(before.DistanceMeters)
        );
        Assert.That(after.Blocked, Is.EqualTo(before.Blocked));
        Assert.That(after.CanFire, Is.EqualTo(before.CanFire));
        yield return null;
    }


    [UnityTest]
    public IEnumerator GenericShipColliderResize_DoesNotRedefineCombatQuery()
    {
        GameObject collisionRoot = new GameObject("CollisionRoot");
        collisionRoot.transform.SetParent(targetRoot.transform, false);
        GameObject shipColliderObject = new GameObject("ShipCollider");
        shipColliderObject.transform.SetParent(
            collisionRoot.transform,
            false
        );
        BoxCollider shipCollider =
            shipColliderObject.AddComponent<BoxCollider>();
        shipCollider.size = Vector3.one;
        FireEligibilityResult before = Evaluate();

        shipCollider.size = Vector3.one * 1000f;
        FireEligibilityResult after = Evaluate();

        Assert.That(shipColliderObject.layer, Is.EqualTo(0));
        Assert.That(after.Blocked, Is.EqualTo(before.Blocked));
        Assert.That(after.CanFire, Is.EqualTo(before.CanFire));
        Assert.That(after.DistanceMeters, Is.EqualTo(before.DistanceMeters));
        yield return null;
    }


    private FireEligibilityResult Evaluate()
    {
        Physics.SyncTransforms();
        bool evaluated = eligibility.TryEvaluate(
            targetRoot,
            true,
            out FireEligibilityResult result
        );

        Assert.That(evaluated, Is.True);
        return result;
    }


    private GameObject CreateShip(string name, Vector3 position)
    {
        GameObject root = CreateRoot(name, position);
        root.AddComponent<ShipCombatState>();
        root.AddComponent<ShipCombatGeometry>();
        ShipArtDefinition artDefinition =
            root.AddComponent<ShipArtDefinition>();
        GameObject center = new GameObject("Center Reference");
        center.transform.SetParent(root.transform, false);
        SetPrivateField(
            artDefinition,
            "centerReference",
            center.transform
        );
        return root;
    }


    private Collider CreateCombatGeometryCollider(
        string name,
        Transform parent,
        Vector3 position
    )
    {
        GameObject geometry = new GameObject(name);
        geometry.layer = CombatGeometryLayer;

        if (parent != null)
        {
            geometry.transform.SetParent(parent, false);
            geometry.transform.localPosition = position;
        }
        else
        {
            geometry.transform.position = position;
            createdObjects.Add(geometry);
        }

        BoxCollider collider = geometry.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        Physics.SyncTransforms();
        return collider;
    }


    private GameObject CreateRoot(string name, Vector3 position)
    {
        GameObject root = new GameObject(name);
        root.transform.position = position;
        createdObjects.Add(root);
        return root;
    }


    private static void SetPrivateField(
        object target,
        string fieldName,
        object value
    )
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            PrivateInstance
        );
        Assert.That(field, Is.Not.Null, $"Missing field {fieldName}.");
        field.SetValue(target, value);
    }
}
