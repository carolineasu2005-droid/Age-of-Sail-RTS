using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class CombatAIFoundationTests
{
    private const string ProfilePath =
        "Assets/Assets/Game/Data/SO_CombatAI_Foundation.asset";
    private const string IntegrityProfilePath =
        "Assets/Assets/Game/Data/SO_ShipIntegrity_Foundation.asset";
    private readonly List<GameObject> created = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        for (int i = created.Count - 1; i >= 0; i--)
        {
            if (created[i] != null)
            {
                Object.DestroyImmediate(created[i]);
            }
        }
        created.Clear();
    }

    [Test]
    public void FoundationProfile_HasSeparateSearchRadiusAndQuarterSecondCadence()
    {
        CombatAIProfile profile = AssetDatabase.LoadAssetAtPath<CombatAIProfile>(ProfilePath);
        Assert.That(profile, Is.Not.Null);
        Assert.That(profile.IsValid, Is.True);
        Assert.That(profile.ThinkIntervalSeconds, Is.EqualTo(0.25f));
        Assert.That(profile.TargetAcquisitionRadiusMeters, Is.EqualTo(1000f));
    }

    [Test]
    public void RelationshipAndLifecycleGate_AppliesToAcquisition()
    {
        GameObject source = CreateShip(0, Vector3.zero, false);
        GameObject friendly = CreateShip(0, Vector3.right * 30f, true);
        // Directly ahead is a broadside dead zone, but remains an AI target.
        GameObject hostile = CreateShip(1, Vector3.forward * 40f, true);
        GameObject unknown = CreateShip(-1, Vector3.right * 20f, true);

        Assert.That(CombatAITargetAcquisition.IsLegal(source, source, 1000f), Is.False);
        Assert.That(CombatAITargetAcquisition.IsLegal(source, friendly, 1000f), Is.False);
        Assert.That(CombatAITargetAcquisition.IsLegal(source, unknown, 1000f), Is.False);
        Assert.That(CombatAITargetAcquisition.IsLegal(source, hostile, 1000f), Is.True);
        Assert.That(CombatAITargetAcquisition.Select(source, null, 1000f), Is.SameAs(hostile));

        SetLifecycle(hostile, ShipCombatLifecycleState.CombatDisabled);
        Assert.That(CombatAITargetAcquisition.IsLegal(source, hostile, 1000f), Is.True);
        SetLifecycle(hostile, ShipCombatLifecycleState.Sinking);
        Assert.That(CombatAITargetAcquisition.IsLegal(source, hostile, 1000f), Is.False);
        Assert.That(CombatAITargetAcquisition.Select(source, hostile, 1000f), Is.Null);
    }

    [Test]
    public void ThreeRegions_DeduplicateToNearestAuthoritativeRoot()
    {
        GameObject source = CreateShip(0, Vector3.zero, true);
        GameObject near = CreateShip(1, Vector3.right * 100f, true);
        GameObject far = CreateShip(2, Vector3.right * 200f, true);

        Assert.That(near.GetComponent<ShipCombatGeometry>().BowRegion, Is.Not.Null);
        Assert.That(near.GetComponent<ShipCombatGeometry>().MidshipRegion, Is.Not.Null);
        Assert.That(near.GetComponent<ShipCombatGeometry>().SternRegion, Is.Not.Null);
        Assert.That(CombatAITargetAcquisition.Select(source, null, 1000f), Is.SameAs(near));

        near.transform.position = Vector3.right * 250f;
        Physics.SyncTransforms();
        Assert.That(CombatAITargetAcquisition.Select(source, null, 1000f), Is.SameAs(far));
    }

    [Test]
    public void EqualDistance_UsesRootEntityIdInsteadOfPhysicsOrder()
    {
        GameObject source = CreateShip(0, Vector3.zero, false);
        GameObject a = CreateShip(1, Vector3.right * 100f, true);
        GameObject b = CreateShip(2, Vector3.left * 100f, true);
        GameObject expected = a.GetEntityId().CompareTo(b.GetEntityId()) < 0
            ? a : b;

        Assert.That(CombatAITargetAcquisition.Select(source, null, 1000f), Is.SameAs(expected));
    }

    [Test]
    public void Controller_SticksUntilInvalidThenReacquiresAndCanReturnToNoTarget()
    {
        GameObject source = CreateShip(0, Vector3.zero, false);
        GameObject first = CreateShip(1, Vector3.right * 100f, true);
        GameObject second = CreateShip(2, Vector3.right * 110f, true);
        ShipCombatAIController controller = AddController(source);

        Assert.That(controller.Think(0f), Is.True);
        Assert.That(controller.State, Is.EqualTo(CombatAIState.Maneuvering));
        Assert.That(controller.CurrentTargetShipRoot, Is.SameAs(first));

        second.transform.position = Vector3.right * 90f;
        Physics.SyncTransforms();
        Assert.That(controller.Think(0.25f), Is.True);
        Assert.That(controller.CurrentTargetShipRoot, Is.SameAs(first));

        SetLifecycle(first, ShipCombatLifecycleState.Sinking);
        Assert.That(controller.Think(0.5f), Is.True);
        Assert.That(controller.CurrentTargetShipRoot, Is.SameAs(second));

        SetTeam(second, 0);
        Assert.That(controller.Think(0.75f), Is.True);
        Assert.That(controller.CurrentTargetShipRoot, Is.Null);
        Assert.That(controller.State, Is.EqualTo(CombatAIState.NoTarget));
    }

    [Test]
    public void Controller_RespectsCadenceAndCannotOperateWhenShooterDisabled()
    {
        GameObject source = CreateShip(0, Vector3.zero, false);
        GameObject target = CreateShip(1, Vector3.right * 100f, true);
        ShipCombatAIController controller = AddController(source);
        Vector3 originalPosition = source.transform.position;
        Quaternion originalRotation = source.transform.rotation;
        int projectileCount = Object.FindObjectsByType<CombatProjectile>(
            FindObjectsInactive.Include
        ).Length;

        Assert.That(controller.Think(0f), Is.True);
        Assert.That(controller.CurrentTargetShipRoot, Is.SameAs(target));
        SetLifecycle(target, ShipCombatLifecycleState.Sinking);
        Assert.That(controller.Think(0.1f), Is.False);
        Assert.That(controller.CurrentTargetShipRoot, Is.SameAs(target));
        Assert.That(controller.Think(0.25f), Is.True);
        Assert.That(controller.State, Is.EqualTo(CombatAIState.NoTarget));

        SetLifecycle(source, ShipCombatLifecycleState.CombatDisabled);
        Assert.That(controller.Think(0.5f), Is.True);
        Assert.That(controller.State, Is.EqualTo(CombatAIState.CombatIncapable));
        Assert.That(controller.CurrentTargetShipRoot, Is.Null);
        Assert.That(source.transform.position, Is.EqualTo(originalPosition));
        Assert.That(source.transform.rotation, Is.EqualTo(originalRotation));
        Assert.That(Object.FindObjectsByType<CombatProjectile>(
            FindObjectsInactive.Include
        ).Length, Is.EqualTo(projectileCount));
    }

    [Test]
    public void TargetLeavingSearchRadius_IsReleased()
    {
        GameObject source = CreateShip(0, Vector3.zero, false);
        GameObject target = CreateShip(1, Vector3.right * 100f, true);
        ShipCombatAIController controller = AddController(source);
        Assert.That(controller.Think(0f), Is.True);
        Assert.That(controller.CurrentTargetShipRoot, Is.SameAs(target));

        target.transform.position = Vector3.right * 1001f;
        Physics.SyncTransforms();
        Assert.That(controller.Think(0.25f), Is.True);
        Assert.That(controller.CurrentTargetShipRoot, Is.Null);
        Assert.That(controller.State, Is.EqualTo(CombatAIState.NoTarget));
    }

    [Test]
    public void DestroyedTarget_IsReleasedAndNextHostileAcquired()
    {
        GameObject source = CreateShip(0, Vector3.zero, false);
        GameObject first = CreateShip(1, Vector3.right * 100f, true);
        GameObject second = CreateShip(2, Vector3.right * 200f, true);
        ShipCombatAIController controller = AddController(source);
        Assert.That(controller.Think(0f), Is.True);
        Assert.That(controller.CurrentTargetShipRoot, Is.SameAs(first));

        Object.DestroyImmediate(first);
        Physics.SyncTransforms();
        Assert.That(controller.Think(0.25f), Is.True);
        Assert.That(controller.CurrentTargetShipRoot, Is.SameAs(second));
    }

    [Test]
    public void SinkingShooter_BecomesCombatIncapable()
    {
        GameObject source = CreateShip(0, Vector3.zero, false);
        CreateShip(1, Vector3.right * 100f, true);
        ShipCombatAIController controller = AddController(source);
        SetLifecycle(source, ShipCombatLifecycleState.Sinking);

        Assert.That(controller.Think(0f), Is.True);
        Assert.That(controller.State, Is.EqualTo(CombatAIState.CombatIncapable));
        Assert.That(controller.CurrentTargetShipRoot, Is.Null);
    }

    [Test]
    public void GenericMovementProxies_HaveNoCombatAI()
    {
        foreach (string name in new[] { "Light", "Medium", "Heavy" })
        {
            string path = "Assets/Assets/Game/Ship/Proxy/PF_Proxy_" + name + "_v01.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);
            Assert.That(prefab.GetComponentsInChildren<ShipCombatAIController>(true), Is.Empty, path);
        }
    }

    private GameObject CreateShip(int team, Vector3 position, bool withRegions)
    {
        GameObject root = new GameObject("Combat Ship");
        created.Add(root);
        root.transform.position = position;
        ShipCombatGeometry geometry = root.AddComponent<ShipCombatGeometry>();
        ShipCombatAffiliation affiliation = root.AddComponent<ShipCombatAffiliation>();
        SetTeam(root, team);
        ShipIntegrity integrity = root.AddComponent<ShipIntegrity>();
        SetObjectReference(integrity, "integrityProfile",
            AssetDatabase.LoadAssetAtPath<ShipIntegrityProfile>(IntegrityProfilePath));
        Assert.That(integrity.TryInitialize(), Is.True);

        if (withRegions)
        {
            CombatHitRegion[] regions = new CombatHitRegion[3];
            for (int i = 0; i < regions.Length; i++)
            {
                GameObject child = new GameObject("Region");
                child.transform.SetParent(root.transform, false);
                child.transform.localPosition = Vector3.forward * ((i - 1) * 10f);
                child.layer = 8;
                BoxCollider collider = child.AddComponent<BoxCollider>();
                collider.isTrigger = true;
                CombatHitRegion region = child.AddComponent<CombatHitRegion>();
                SerializedObject serialized = new SerializedObject(region);
                serialized.FindProperty("region").enumValueIndex = i;
                serialized.FindProperty("owner").objectReferenceValue = geometry;
                serialized.FindProperty("queryCollider").objectReferenceValue = collider;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                regions[i] = region;
            }
            SerializedObject geometryData = new SerializedObject(geometry);
            geometryData.FindProperty("bowRegion").objectReferenceValue = regions[0];
            geometryData.FindProperty("midshipRegion").objectReferenceValue = regions[1];
            geometryData.FindProperty("sternRegion").objectReferenceValue = regions[2];
            geometryData.ApplyModifiedPropertiesWithoutUndo();
        }
        Physics.SyncTransforms();
        return root;
    }

    private static ShipCombatAIController AddController(GameObject root)
    {
        ShipCombatAIController controller = root.AddComponent<ShipCombatAIController>();
        SetObjectReference(controller, "profile",
            AssetDatabase.LoadAssetAtPath<CombatAIProfile>(ProfilePath));
        return controller;
    }

    private static void SetTeam(GameObject root, int team)
    {
        SerializedObject serialized = new SerializedObject(root.GetComponent<ShipCombatAffiliation>());
        serialized.FindProperty("teamId").intValue = team;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetLifecycle(GameObject root, ShipCombatLifecycleState lifecycle)
    {
        if (lifecycle == ShipCombatLifecycleState.Operational) return;
        ShipIntegrity integrity = root.GetComponent<ShipIntegrity>();
        float loss = lifecycle == ShipCombatLifecycleState.CombatDisabled
            ? integrity.MaximumIntegrity * 0.8f
            : integrity.MaximumIntegrity;
        Assert.That(integrity.TryApplyIntegrityLoss(loss, out _), Is.True);
        Assert.That(integrity.LifecycleState, Is.EqualTo(lifecycle));
    }

    private static void SetObjectReference(Object owner, string field, Object value)
    {
        Assert.That(value, Is.Not.Null);
        SerializedObject serialized = new SerializedObject(owner);
        serialized.FindProperty(field).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
