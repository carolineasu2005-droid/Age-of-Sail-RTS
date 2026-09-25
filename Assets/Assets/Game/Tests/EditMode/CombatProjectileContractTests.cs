using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class CombatProjectileContractTests
{
    private const string ProjectilePrefabPath =
        "Assets/Assets/Game/Combat/Prefabs/"
        + "PF_CombatProjectile_Foundation_v01.prefab";
    private const float Tolerance = 0.0001f;

    private GameObject sourceRoot;


    [SetUp]
    public void SetUp()
    {
        sourceRoot = new GameObject("Projectile Source Root");
    }


    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(sourceRoot);
    }


    [Test]
    public void AnalyticTrajectory_AtZero_EqualsFrozenOrigin()
    {
        ShotSample sample = CreateSample();

        Vector3 position = CombatProjectileTrajectory.EvaluatePosition(
            sample,
            0f
        );

        Assert.That(position, Is.EqualTo(sample.OriginWorld));
    }


    [Test]
    public void AnalyticTrajectory_AtNominalTime_ReconstructsSamplePoint()
    {
        ShotSample sample = CreateSample();

        Vector3 position = CombatProjectileTrajectory.EvaluatePosition(
            sample,
            sample.NominalFlightTimeSeconds
        );

        AssertVector(position, sample.AimPlaneSamplePointWorld);
    }


    [Test]
    public void AnalyticTrajectory_IsDeterministicAndIgnoresLaterPoseChanges()
    {
        ShotSample sample = CreateSample();
        GameObject unrelatedTarget = new GameObject("Moving Target");

        try
        {
            Vector3 expected = CombatProjectileTrajectory.EvaluatePosition(
                sample,
                0.75f
            );
            sourceRoot.transform.SetPositionAndRotation(
                new Vector3(400f, 20f, -90f),
                Quaternion.Euler(0f, 130f, 0f)
            );
            unrelatedTarget.transform.SetPositionAndRotation(
                new Vector3(-200f, 50f, 300f),
                Quaternion.Euler(30f, 70f, 10f)
            );

            Vector3 actual = CombatProjectileTrajectory.EvaluatePosition(
                sample,
                0.75f
            );

            Assert.That(actual, Is.EqualTo(expected));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(unrelatedTarget);
        }
    }


    [Test]
    public void ProjectileContract_HasNoTargetOrSemanticResolutionState()
    {
        FieldInfo[] fields = typeof(CombatProjectile).GetFields(
            BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic
        );

        Assert.That(
            fields.Any(field =>
                typeof(Transform).IsAssignableFrom(field.FieldType)),
            Is.False
        );
        Assert.That(
            fields.Any(field => field.Name.Contains("target")),
            Is.False
        );
        Assert.That(
            fields.Any(field =>
                field.FieldType == typeof(CombatHitRegion)
                || field.FieldType == typeof(ShipCombatGeometry)),
            Is.False
        );
        Assert.That(
            typeof(ProjectileTerminalContact).GetFields(
                BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic
                    | BindingFlags.DeclaredOnly
            ).All(field => field.IsInitOnly),
            Is.True
        );
    }


    [Test]
    public void FoundationProjectilePrefab_IsVisibleAndHasNoPhysicsBody()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            ProjectilePrefabPath
        );

        Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.GetComponent<CombatProjectile>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<MeshFilter>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<MeshRenderer>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<MeshRenderer>().enabled, Is.True);
        Assert.That(
            prefab.GetComponentsInChildren<Rigidbody>(true),
            Is.Empty
        );
        Assert.That(
            prefab.GetComponentsInChildren<Collider>(true),
            Is.Empty
        );
    }


    [Test]
    public void ProjectileSources_ContainNoResolutionOrHomingImplementation()
    {
        string combatRoot = Path.Combine(
            Application.dataPath,
            "Assets/Game/Scripts/Combat"
        );
        string runtimeSource = File.ReadAllText(
            Path.Combine(combatRoot, "CombatProjectile.cs")
        );
        string querySource = File.ReadAllText(
            Path.Combine(combatRoot, "CombatProjectileContactQuery.cs")
        );
        string combined = runtimeSource + querySource;

        Assert.That(combined, Does.Not.Contain("HitChance"));
        Assert.That(combined, Does.Not.Contain("TryResolveRegion"));
        Assert.That(combined, Does.Not.Contain("CombatHullRegion"));
        Assert.That(combined, Does.Not.Contain("Damage"));
        Assert.That(combined, Does.Not.Contain("Integrity"));
        Assert.That(combined, Does.Not.Contain("CombatVFX"));
        Assert.That(combined, Does.Not.Contain("AddComponent<Rigidbody>"));
        Assert.That(combined, Does.Not.Contain("AddComponent<Collider>"));
        Assert.That(combined, Does.Not.Contain("Renderer.bounds"));
        Assert.That(combined, Does.Not.Contain("Mesh.bounds"));
        Assert.That(combined, Does.Not.Contain("ShipCollider"));
        Assert.That(combined, Does.Not.Contain("ShipSailingSpeed"));
        Assert.That(combined, Does.Not.Contain("ShipTurning"));
        Assert.That(combined, Does.Not.Contain("ShipDestinationController"));
        Assert.That(
            combined,
            Does.Not.Contain("SourceShipRootIdentity.transform.position")
        );
        Assert.That(combined, Does.Not.Contain("transform.forward"));
    }


    private ShotSample CreateSample()
    {
        Vector3 origin = new Vector3(2f, 4f, -3f);
        Vector3 gravity = new Vector3(0f, -10f, 0f);
        float flightTime = 2f;
        Vector3 velocity = new Vector3(12f, 10f, 1f);
        Vector3 samplePoint = origin
            + velocity * flightTime
            + 0.5f * gravity * flightTime * flightTime;

        return CreateShotSample(
            sourceRoot,
            origin,
            samplePoint,
            velocity,
            gravity,
            flightTime,
            -2f,
            5f
        );
    }


    private static ShotSample CreateShotSample(
        GameObject source,
        Vector3 origin,
        Vector3 samplePoint,
        Vector3 velocity,
        Vector3 gravity,
        float flightTime,
        float waterLevel,
        float maxLifetime
    )
    {
        ConstructorInfo constructor = typeof(ShotSample).GetConstructors(
            BindingFlags.Instance | BindingFlags.NonPublic
        ).Single();
        return (ShotSample)constructor.Invoke(new object[]
        {
            source,
            CombatSide.Port,
            0,
            91u,
            origin,
            samplePoint,
            velocity,
            gravity,
            flightTime,
            waterLevel,
            maxLifetime,
            FoundationAmmunitionType.RoundShot
        });
    }


    private static void AssertVector(Vector3 actual, Vector3 expected)
    {
        Assert.That(actual.x, Is.EqualTo(expected.x).Within(Tolerance));
        Assert.That(actual.y, Is.EqualTo(expected.y).Within(Tolerance));
        Assert.That(actual.z, Is.EqualTo(expected.z).Within(Tolerance));
    }
}
