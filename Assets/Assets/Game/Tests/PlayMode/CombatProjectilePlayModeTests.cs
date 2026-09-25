using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class CombatProjectilePlayModeTests
{
    private const string ProjectilePrefabPath =
        "Assets/Assets/Game/Combat/Prefabs/"
        + "PF_CombatProjectile_Foundation_v01.prefab";
    private const int CombatGeometryLayer = 8;
    private const float Tolerance = 0.0001f;

    private readonly List<GameObject> createdObjects =
        new List<GameObject>();

    private GameObject sourceRoot;
    private bool originalQueriesHitTriggers;


    [UnitySetUp]
    public IEnumerator SetUp()
    {
        originalQueriesHitTriggers = Physics.queriesHitTriggers;
        sourceRoot = CreateObject("Source Root", Vector3.zero);
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
    public IEnumerator SweptQuery_DetectsTriggerCrossedBetweenEndpoints()
    {
        Physics.queriesHitTriggers = false;
        Collider hull = CreateCollider(
            "Crossed Combat Geometry",
            null,
            new Vector3(5f, 5f, 0f),
            Vector3.one,
            CombatGeometryLayer,
            true
        );
        ShotSample sample = CreateSample(
            new Vector3(0f, 5f, 0f),
            Vector3.right * 10f,
            Vector3.zero,
            -100f,
            5f
        );
        Physics.SyncTransforms();

        bool contacted = CombatProjectileContactQuery.TryFindEarliestContact(
            sample,
            new Vector3(0f, 5f, 0f),
            new Vector3(10f, 5f, 0f),
            0f,
            1f,
            out ProjectileTerminalContact contact
        );

        Assert.That(contacted, Is.True);
        Assert.That(
            contact.Kind,
            Is.EqualTo(
                ProjectileTerminalContactKind.CombatGeometryContact
            )
        );
        Assert.That(contact.ContactedCollider, Is.SameAs(hull));
        Assert.That(contact.SegmentFraction, Is.InRange(0f, 1f));
        yield return null;
    }


    [UnityTest]
    public IEnumerator Query_IgnoresDefaultLayerAndEntireSourceHierarchy()
    {
        CreateCollider(
            "Generic ShipCollider",
            null,
            new Vector3(1f, 5f, 0f),
            Vector3.one,
            0,
            false
        );
        CreateCollider(
            "Source Combat Geometry",
            sourceRoot.transform,
            new Vector3(2f, 5f, 0f),
            Vector3.one,
            CombatGeometryLayer,
            true
        );
        Collider thirdParty = CreateCollider(
            "Third Party Combat Geometry",
            null,
            new Vector3(6f, 5f, 0f),
            Vector3.one,
            CombatGeometryLayer,
            true
        );
        ShotSample sample = CreateSample(
            new Vector3(0f, 5f, 0f),
            Vector3.right * 10f,
            Vector3.zero,
            -100f,
            5f
        );
        Physics.SyncTransforms();

        bool contacted = CombatProjectileContactQuery.TryFindEarliestContact(
            sample,
            new Vector3(0f, 5f, 0f),
            new Vector3(10f, 5f, 0f),
            0f,
            1f,
            out ProjectileTerminalContact contact
        );

        Assert.That(contacted, Is.True);
        Assert.That(contact.ContactedCollider, Is.SameAs(thirdParty));
        yield return null;
    }


    [UnityTest]
    public IEnumerator Query_SelectsNearestNonSourceCombatGeometry()
    {
        Collider nearest = CreateCollider(
            "Nearest Hull",
            null,
            new Vector3(3f, 5f, 0f),
            Vector3.one,
            CombatGeometryLayer,
            true
        );
        CreateCollider(
            "Far Hull",
            null,
            new Vector3(7f, 5f, 0f),
            Vector3.one,
            CombatGeometryLayer,
            true
        );
        ShotSample sample = CreateSample(
            new Vector3(0f, 5f, 0f),
            Vector3.right * 10f,
            Vector3.zero,
            -100f,
            5f
        );
        Physics.SyncTransforms();

        CombatProjectileContactQuery.TryFindEarliestContact(
            sample,
            new Vector3(0f, 5f, 0f),
            new Vector3(10f, 5f, 0f),
            0f,
            1f,
            out ProjectileTerminalContact contact
        );

        Assert.That(contact.ContactedCollider, Is.SameAs(nearest));
        yield return null;
    }


    [UnityTest]
    public IEnumerator WaterCrossing_UsesFrozenPlaneFormula()
    {
        ShotSample sample = CreateSample(
            new Vector3(0f, 10f, 0f),
            new Vector3(10f, -20f, 0f),
            Vector3.zero,
            0f,
            5f
        );

        bool contacted = CombatProjectileContactQuery.TryFindEarliestContact(
            sample,
            new Vector3(0f, 10f, 0f),
            new Vector3(10f, -10f, 0f),
            2f,
            3f,
            out ProjectileTerminalContact contact
        );

        Assert.That(contacted, Is.True);
        Assert.That(
            contact.Kind,
            Is.EqualTo(ProjectileTerminalContactKind.WaterContact)
        );
        Assert.That(contact.SegmentFraction, Is.EqualTo(0.5f));
        AssertVector(contact.PointWorld, new Vector3(5f, 0f, 0f));
        Assert.That(contact.NormalWorld, Is.EqualTo(Vector3.up));
        Assert.That(contact.ContactedCollider, Is.Null);
        Assert.That(contact.ElapsedFlightTimeSeconds, Is.EqualTo(2.5f));
        yield return null;
    }


    [UnityTest]
    public IEnumerator SegmentEntirelyAboveWater_HasNoContact()
    {
        ShotSample sample = CreateSample(
            new Vector3(0f, 10f, 0f),
            Vector3.right * 10f,
            Vector3.zero,
            0f,
            5f
        );

        bool contacted = CombatProjectileContactQuery.TryFindEarliestContact(
            sample,
            new Vector3(0f, 10f, 0f),
            new Vector3(10f, 2f, 0f),
            0f,
            1f,
            out _
        );

        Assert.That(contacted, Is.False);
        yield return null;
    }


    [UnityTest]
    public IEnumerator EarliestContact_HullBeforeWaterChoosesHull()
    {
        Collider hull = CreateOrderingHull(2.5f);
        ProjectileTerminalContact contact = QueryOrderingSegment();

        Assert.That(contact.ContactedCollider, Is.SameAs(hull));
        Assert.That(
            contact.Kind,
            Is.EqualTo(
                ProjectileTerminalContactKind.CombatGeometryContact
            )
        );
        yield return null;
    }


    [UnityTest]
    public IEnumerator EarliestContact_WaterBeforeHullChoosesWater()
    {
        CreateOrderingHull(8.5f);
        ProjectileTerminalContact contact = QueryOrderingSegment();

        Assert.That(
            contact.Kind,
            Is.EqualTo(ProjectileTerminalContactKind.WaterContact)
        );
        yield return null;
    }


    [UnityTest]
    public IEnumerator EarliestContact_NumericalTieChoosesHull()
    {
        Collider hull = CreateOrderingHull(5.5f);
        ProjectileTerminalContact contact = QueryOrderingSegment();

        Assert.That(
            contact.Kind,
            Is.EqualTo(
                ProjectileTerminalContactKind.CombatGeometryContact
            )
        );
        Assert.That(contact.ContactedCollider, Is.SameAs(hull));
        Assert.That(
            contact.SegmentFraction,
            Is.EqualTo(0.5f).Within(Tolerance)
        );
        yield return null;
    }


    [UnityTest]
    public IEnumerator RuntimeProjectile_TerminatesOnceAndDoesNotMutateShip()
    {
        ShipCombatState sourceState =
            sourceRoot.AddComponent<ShipCombatState>();
        GameObject manualTarget = CreateObject(
            "Manual Target Root",
            Vector3.right * 50f
        );
        manualTarget.AddComponent<ShipCombatState>();
        Assert.That(sourceState.AssignManualTarget(manualTarget), Is.True);
        Vector3 sourcePosition = sourceRoot.transform.position;
        Quaternion sourceRotation = sourceRoot.transform.rotation;
        GameObject projectileObject = CreateObject(
            "Runtime Projectile",
            Vector3.zero
        );
        CombatProjectile projectile =
            projectileObject.AddComponent<CombatProjectile>();
        projectile.enabled = false;
        ShotSample sample = CreateSample(
            new Vector3(0f, 1f, 0f),
            new Vector3(2f, -2f, 0f),
            Vector3.zero,
            0f,
            5f
        );
        int terminalCount = 0;
        projectile.Terminated += _ => terminalCount++;

        Assert.That(projectile.TryInitialize(sample), Is.True);
        Assert.That(projectile.TryInitialize(sample), Is.False);
        Assert.That(projectile.SimulateStep(1f), Is.True);
        Vector3 terminalPosition = projectile.transform.position;

        Assert.That(projectile.IsTerminated, Is.True);
        Assert.That(terminalCount, Is.EqualTo(1));
        Assert.That(projectile.SimulateStep(1f), Is.False);
        Assert.That(terminalCount, Is.EqualTo(1));
        Assert.That(projectile.transform.position, Is.EqualTo(terminalPosition));
        Assert.That(sourceState.ManualTarget, Is.SameAs(manualTarget));
        Assert.That(sourceState.AutoFireEnabled, Is.False);
        Assert.That(
            sourceState.PortBroadsideState,
            Is.EqualTo(BroadsideReloadState.Ready)
        );
        Assert.That(
            sourceState.StarboardBroadsideState,
            Is.EqualTo(BroadsideReloadState.Ready)
        );
        Assert.That(sourceRoot.transform.position, Is.EqualTo(sourcePosition));
        Assert.That(sourceRoot.transform.rotation, Is.EqualTo(sourceRotation));
        yield return null;
    }


    [UnityTest]
    public IEnumerator RuntimeProjectile_MaxLifetimeProducesExplicitFallback()
    {
        GameObject projectileObject = CreateObject(
            "Expiring Projectile",
            Vector3.zero
        );
        CombatProjectile projectile =
            projectileObject.AddComponent<CombatProjectile>();
        projectile.enabled = false;
        ShotSample sample = CreateSample(
            new Vector3(0f, 10f, 0f),
            Vector3.right * 10f,
            Vector3.zero,
            -100f,
            0.5f
        );
        ProjectileTerminalContact observed = default;
        projectile.Terminated += contact => observed = contact;

        Assert.That(projectile.TryInitialize(sample), Is.True);
        Assert.That(projectile.SimulateStep(1f), Is.True);

        Assert.That(projectile.IsTerminated, Is.True);
        Assert.That(
            observed.Kind,
            Is.EqualTo(
                ProjectileTerminalContactKind.ExpiredSafetyFallback
            )
        );
        Assert.That(observed.ElapsedFlightTimeSeconds, Is.EqualTo(0.5f));
        AssertVector(
            observed.PointWorld,
            CombatProjectileTrajectory.EvaluatePosition(sample, 0.5f)
        );
        yield return null;
    }


    [UnityTest]
    public IEnumerator FoundationPrefab_SpawnsVisibleNonPhysicsFlight()
    {
#if UNITY_EDITOR
        GameObject prefabObject = AssetDatabase.LoadAssetAtPath<GameObject>(
            ProjectilePrefabPath
        );
        Assert.That(prefabObject, Is.Not.Null);
        CombatProjectile prefab = prefabObject.GetComponent<CombatProjectile>();
        ShotSample sample = CreateSample(
            new Vector3(0f, 10f, 0f),
            Vector3.right * 10f,
            Vector3.zero,
            -100f,
            5f
        );

        Assert.That(
            CombatProjectile.TrySpawn(
                prefab,
                sample,
                out CombatProjectile projectile
            ),
            Is.True
        );
        createdObjects.Add(projectile.gameObject);
        projectile.enabled = false;
        Vector3 origin = projectile.transform.position;

        Assert.That(
            projectile.GetComponentsInChildren<Collider>(true),
            Is.Empty
        );
        Assert.That(
            projectile.GetComponentsInChildren<Rigidbody>(true),
            Is.Empty
        );
        Assert.That(
            projectile.GetComponentsInChildren<MeshRenderer>(true)
                .Any(renderer => renderer.enabled),
            Is.True
        );
        Assert.That(projectile.SimulateStep(0.25f), Is.True);
        Assert.That(projectile.transform.position, Is.Not.EqualTo(origin));
#else
        Assert.Ignore("Prefab asset loading requires the Unity Editor.");
#endif
        yield return null;
    }


    private ProjectileTerminalContact QueryOrderingSegment()
    {
        ShotSample sample = CreateSample(
            new Vector3(0f, 10f, 0f),
            new Vector3(10f, -20f, 0f),
            Vector3.zero,
            0f,
            5f
        );
        Physics.SyncTransforms();
        bool contacted = CombatProjectileContactQuery.TryFindEarliestContact(
            sample,
            new Vector3(0f, 10f, 0f),
            new Vector3(10f, -10f, 0f),
            0f,
            1f,
            out ProjectileTerminalContact contact
        );
        Assert.That(contacted, Is.True);
        return contact;
    }


    private Collider CreateOrderingHull(float centerX)
    {
        return CreateCollider(
            "Ordering Hull",
            null,
            new Vector3(centerX, 0f, 0f),
            new Vector3(1f, 100f, 1f),
            CombatGeometryLayer,
            true
        );
    }


    private ShotSample CreateSample(
        Vector3 origin,
        Vector3 velocity,
        Vector3 gravity,
        float waterLevel,
        float maxLifetime
    )
    {
        float nominalFlightTime = 1f;
        Vector3 samplePoint = origin
            + velocity * nominalFlightTime
            + 0.5f
                * gravity
                * nominalFlightTime
                * nominalFlightTime;
        ConstructorInfo constructor = typeof(ShotSample).GetConstructors(
            BindingFlags.Instance | BindingFlags.NonPublic
        ).Single();
        return (ShotSample)constructor.Invoke(new object[]
        {
            sourceRoot,
            CombatSide.Port,
            0,
            17u,
            origin,
            samplePoint,
            velocity,
            gravity,
            nominalFlightTime,
            waterLevel,
            maxLifetime,
            FoundationAmmunitionType.RoundShot
        });
    }


    private Collider CreateCollider(
        string name,
        Transform parent,
        Vector3 position,
        Vector3 size,
        int layer,
        bool isTrigger
    )
    {
        GameObject colliderObject = CreateObject(name, Vector3.zero);
        colliderObject.layer = layer;

        if (parent != null)
        {
            colliderObject.transform.SetParent(parent, false);
            colliderObject.transform.localPosition = position;
        }
        else
        {
            colliderObject.transform.position = position;
        }

        BoxCollider collider = colliderObject.AddComponent<BoxCollider>();
        collider.size = size;
        collider.isTrigger = isTrigger;
        return collider;
    }


    private GameObject CreateObject(string name, Vector3 position)
    {
        GameObject created = new GameObject(name);
        created.transform.position = position;
        createdObjects.Add(created);
        return created;
    }


    private static void AssertVector(Vector3 actual, Vector3 expected)
    {
        Assert.That(actual.x, Is.EqualTo(expected.x).Within(Tolerance));
        Assert.That(actual.y, Is.EqualTo(expected.y).Within(Tolerance));
        Assert.That(actual.z, Is.EqualTo(expected.z).Within(Tolerance));
    }
}
