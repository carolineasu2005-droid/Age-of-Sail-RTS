using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class BroadsideShotSamplerTests
{
    private const string CombatPrefabPath =
        "Assets/Assets/Game/Ship/Proxy/"
        + "PF_Ship_Gelderland_Combat_v01.prefab";
    private const float Tolerance = 0.0001f;
    private const BindingFlags PrivateInstance =
        BindingFlags.Instance | BindingFlags.NonPublic;

    private readonly List<GameObject> createdShips =
        new List<GameObject>();


    [TearDown]
    public void TearDown()
    {
        for (int index = createdShips.Count - 1; index >= 0; index--)
        {
            if (createdShips[index] != null)
            {
                UnityEngine.Object.DestroyImmediate(createdShips[index]);
            }
        }

        createdShips.Clear();
    }


    [TestCase(CombatSide.Port)]
    [TestCase(CombatSide.Starboard)]
    public void FormalGelderland_BroadsideProducesThirteenRoundShotSamples(
        CombatSide side
    )
    {
        GameObject sourceRoot = CreateOperationalCombatShip();
        FireAimBasis basis = BuildBlindFireBasis(sourceRoot, side, 100f);

        BroadsideShotSamplingResult result = Sample(
            sourceRoot,
            basis,
            12345u
        );

        Assert.That(result.Count, Is.EqualTo(13));
        Assert.That(result.Side, Is.EqualTo(side));
        Assert.That(result.BroadsideSeed, Is.EqualTo(12345u));
        Assert.That(
            result.ShotSamples.All(sample =>
                sample.AmmunitionType
                    == FoundationAmmunitionType.RoundShot),
            Is.True
        );
    }


    [TestCase(CombatSide.Port)]
    [TestCase(CombatSide.Starboard)]
    public void CanonicalMuzzles_MapOneToOneInBowToSternOrder(
        CombatSide side
    )
    {
        GameObject sourceRoot = CreateOperationalCombatShip();
        ShipMuzzleSockets sockets =
            sourceRoot.GetComponent<ShipMuzzleSockets>();
        IReadOnlyList<Transform> selected = side == CombatSide.Port
            ? sockets.PortMuzzles
            : sockets.StarboardMuzzles;
        IReadOnlyList<Transform> opposite = side == CombatSide.Port
            ? sockets.StarboardMuzzles
            : sockets.PortMuzzles;
        BroadsideShotSamplingResult result = Sample(
            sourceRoot,
            BuildBlindFireBasis(sourceRoot, side, 100f),
            77u
        );

        Assert.That(result.Count, Is.EqualTo(selected.Count));

        for (int index = 0; index < selected.Count; index++)
        {
            ShotSample sample = result.ShotSamples[index];
            Assert.That(sample.MuzzleSequenceIndex, Is.EqualTo(index));
            Assert.That(sample.Side, Is.EqualTo(side));
            Assert.That(sample.OriginWorld, Is.EqualTo(selected[index].position));
            Assert.That(
                opposite.Any(muzzle =>
                    muzzle.position == sample.OriginWorld),
                Is.False
            );

            if (index > 0)
            {
                float previousZ = sourceRoot.transform
                    .InverseTransformPoint(selected[index - 1].position).z;
                float currentZ = sourceRoot.transform
                    .InverseTransformPoint(selected[index].position).z;
                Assert.That(previousZ, Is.GreaterThan(currentZ));
            }
        }
    }


    [Test]
    public void SameSeedAndState_ReproducesEverySampleExactly()
    {
        GameObject sourceRoot = CreateOperationalCombatShip();
        FireAimBasis basis = BuildBlindFireBasis(
            sourceRoot,
            CombatSide.Starboard,
            250f
        );

        BroadsideShotSamplingResult first = Sample(
            sourceRoot,
            basis,
            0u
        );
        BroadsideShotSamplingResult second = Sample(
            sourceRoot,
            basis,
            0u
        );

        Assert.That(second.Count, Is.EqualTo(first.Count));

        for (int index = 0; index < first.Count; index++)
        {
            AssertSampleEqual(
                first.ShotSamples[index],
                second.ShotSamples[index]
            );
        }
    }


    [Test]
    public void DifferentSeed_ChangesAtLeastOneMappedSamplePoint()
    {
        GameObject sourceRoot = CreateOperationalCombatShip();
        FireAimBasis basis = BuildBlindFireBasis(
            sourceRoot,
            CombatSide.Port,
            250f
        );
        BroadsideShotSamplingResult first = Sample(
            sourceRoot,
            basis,
            100u
        );
        BroadsideShotSamplingResult second = Sample(
            sourceRoot,
            basis,
            101u
        );

        Assert.That(
            Enumerable.Range(0, first.Count).Any(index =>
                first.ShotSamples[index].AimPlaneSamplePointWorld
                    != second.ShotSamples[index].AimPlaneSamplePointWorld),
            Is.True
        );
    }


    [Test]
    public void EverySample_UsesCommonBasisAndLiesInsideCommonEllipse()
    {
        GameObject sourceRoot = CreateOperationalCombatShip();
        FireAimBasis basis = BuildBlindFireBasis(
            sourceRoot,
            CombatSide.Starboard,
            300f
        );
        BroadsideShotSamplingResult result = Sample(
            sourceRoot,
            basis,
            9001u
        );
        DispersionProfile profile = sourceRoot
            .GetComponent<ShipDispersionConfiguration>()
            .DispersionProfile;

        Assert.That(
            DispersionGeometry.TryCalculate(
                basis,
                profile,
                out _,
                out DispersionEllipse ellipse
            ),
            Is.True
        );
        AssertBasisEqual(result.AimBasis, basis);

        foreach (ShotSample sample in result.ShotSamples)
        {
            Vector3 offset =
                sample.AimPlaneSamplePointWorld - ellipse.Plane.CenterWorld;
            float horizontal = Vector3.Dot(
                offset,
                ellipse.Plane.HorizontalAxisWorld
            ) / ellipse.HorizontalSemiAxisMeters;
            float vertical = Vector3.Dot(
                offset,
                ellipse.Plane.VerticalAxisWorld
            ) / ellipse.VerticalSemiAxisMeters;

            Assert.That(
                horizontal * horizontal + vertical * vertical,
                Is.LessThanOrEqualTo(1f + Tolerance)
            );
        }
    }


    [Test]
    public void BallisticSnapshots_UseFrozenFlightTimeVelocityAndGravity()
    {
        GameObject sourceRoot = CreateOperationalCombatShip();
        ProjectileFlightProfile profile = sourceRoot
            .GetComponent<ShipProjectileFlightConfiguration>()
            .ProjectileFlightProfile;
        BroadsideShotSamplingResult result = Sample(
            sourceRoot,
            BuildBlindFireBasis(sourceRoot, CombatSide.Port, 300f),
            42u
        );

        foreach (ShotSample sample in result.ShotSamples)
        {
            Vector3 delta =
                sample.AimPlaneSamplePointWorld - sample.OriginWorld;
            float horizontalDistance =
                new Vector3(delta.x, 0f, delta.z).magnitude;
            float expectedTime = horizontalDistance
                / profile.NominalHorizontalSpeedMetersPerSecond;
            Vector3 reconstructed = sample.OriginWorld
                + sample.InitialVelocityWorld
                    * sample.NominalFlightTimeSeconds
                + 0.5f
                    * sample.GravityWorld
                    * sample.NominalFlightTimeSeconds
                    * sample.NominalFlightTimeSeconds;

            Assert.That(
                sample.NominalFlightTimeSeconds,
                Is.EqualTo(expectedTime).Within(Tolerance)
            );
            AssertVector(
                sample.GravityWorld,
                new Vector3(
                    0f,
                    -profile.GravityMagnitudeMetersPerSecondSquared,
                    0f
                )
            );
            AssertVector(reconstructed, sample.AimPlaneSamplePointWorld);
            Assert.That(sample.MaxLifetimeSeconds, Is.EqualTo(10f));
            Assert.That(
                sample.MaxLifetimeSeconds,
                Is.GreaterThan(sample.NominalFlightTimeSeconds)
            );
        }
    }


    [Test]
    public void SamplesRemainFrozenAfterRootMuzzleAndWaterlineMove()
    {
        GameObject instance = CreateOperationalCombatShip();
        ProjectileFlightProfile mutableFlightProfile =
            UnityEngine.Object.Instantiate(
                instance
                    .GetComponent<ShipProjectileFlightConfiguration>()
                    .ProjectileFlightProfile
            );

        try
        {
            SetPrivateField(
                instance.GetComponent<ShipProjectileFlightConfiguration>(),
                "projectileFlightProfile",
                mutableFlightProfile
            );
            ShipMuzzleSockets sockets =
                instance.GetComponent<ShipMuzzleSockets>();
            ShipArtDefinition artDefinition =
                instance.GetComponent<ShipArtDefinition>();
            BroadsideShotSamplingResult result = Sample(
                instance,
                BuildBlindFireBasis(instance, CombatSide.Port, 150f),
                81u
            );
            ShotSample snapshot = result.ShotSamples[0];
            Vector3 origin = snapshot.OriginWorld;
            Vector3 samplePoint = snapshot.AimPlaneSamplePointWorld;
            Vector3 initialVelocity = snapshot.InitialVelocityWorld;
            Vector3 gravity = snapshot.GravityWorld;
            float waterLevel = snapshot.WaterLevelWorldY;
            float maxLifetime = snapshot.MaxLifetimeSeconds;

            Assert.That(
                waterLevel,
                Is.EqualTo(
                    artDefinition.WaterlineReference.position.y
                )
            );

            instance.transform.SetPositionAndRotation(
                new Vector3(400f, 25f, -120f),
                Quaternion.Euler(0f, 110f, 0f)
            );
            sockets.PortMuzzles[0].position += new Vector3(7f, 3f, -5f);
            artDefinition.WaterlineReference.position += Vector3.up * 20f;
            SetPrivateField(
                mutableFlightProfile,
                "gravityMagnitudeMetersPerSecondSquared",
                2f
            );
            SetPrivateField(
                mutableFlightProfile,
                "maxLifetimeSeconds",
                99f
            );

            Assert.That(snapshot.OriginWorld, Is.EqualTo(origin));
            Assert.That(
                snapshot.AimPlaneSamplePointWorld,
                Is.EqualTo(samplePoint)
            );
            Assert.That(
                snapshot.InitialVelocityWorld,
                Is.EqualTo(initialVelocity)
            );
            Assert.That(snapshot.GravityWorld, Is.EqualTo(gravity));
            Assert.That(snapshot.WaterLevelWorldY, Is.EqualTo(waterLevel));
            Assert.That(
                snapshot.MaxLifetimeSeconds,
                Is.EqualTo(maxLifetime)
            );
            Assert.That(
                snapshot.SourceShipRootIdentity,
                Is.SameAs(instance)
            );
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(mutableFlightProfile);
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }


    [Test]
    public void ShotAndBroadsideValues_AreImmutableAndCollectionIsReadOnly()
    {
        GameObject sourceRoot = CreateOperationalCombatShip();
        BroadsideShotSamplingResult result = Sample(
            sourceRoot,
            BuildBlindFireBasis(sourceRoot, CombatSide.Port, 100f),
            5u
        );

        AssertImmutableValue<ShotSample>();
        AssertImmutableValue<BroadsideShotSamplingResult>();
        Assert.That(
            typeof(ShotSample).GetFields(
                PrivateInstance | BindingFlags.Public
            ).Any(field =>
                typeof(Transform).IsAssignableFrom(field.FieldType)
                || field.FieldType == typeof(ExposureRect)
                || field.FieldType == typeof(ShipCombatGeometry)),
            Is.False
        );

        IList<ShotSample> listView =
            result.ShotSamples as IList<ShotSample>;
        Assert.That(listView, Is.Not.Null);
        Assert.That(listView.IsReadOnly, Is.True);
        Assert.Throws<NotSupportedException>(() =>
            listView.Add(default)
        );
        Assert.That(result.Count, Is.EqualTo(13));
    }


    [Test]
    public void Sampling_DoesNotMutateCombatStateOrRootPose()
    {
        GameObject instance = CreateOperationalCombatShip();

        try
        {
            ShipCombatState state = instance.GetComponent<ShipCombatState>();
            state.SetAutoFireEnabled(true);
            Vector3 position = instance.transform.position;
            Quaternion rotation = instance.transform.rotation;
            BroadsideReloadState port = state.PortBroadsideState;
            BroadsideReloadState starboard = state.StarboardBroadsideState;

            Sample(
                instance,
                BuildBlindFireBasis(instance, CombatSide.Port, 100f),
                111u
            );

            Assert.That(state.AutoFireEnabled, Is.True);
            Assert.That(state.PortBroadsideState, Is.EqualTo(port));
            Assert.That(state.StarboardBroadsideState, Is.EqualTo(starboard));
            Assert.That(instance.transform.position, Is.EqualTo(position));
            Assert.That(instance.transform.rotation, Is.EqualTo(rotation));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }


    [Test]
    public void MissingSelectedMuzzleCollection_FailsWithoutPartialResult()
    {
        GameObject instance = CreateOperationalCombatShip();

        try
        {
            FireAimBasis basis = BuildBlindFireBasis(
                instance,
                CombatSide.Port,
                100f
            );
            SetPrivateField(
                instance.GetComponent<ShipMuzzleSockets>(),
                "portMuzzlesContainer",
                null
            );

            bool sampled = ShipBroadsideShotSampler.TrySample(
                instance,
                basis,
                12u,
                out BroadsideShotSamplingResult result,
                out BroadsideShotSamplingFailure failure
            );

            Assert.That(sampled, Is.False);
            Assert.That(result.Count, Is.Zero);
            Assert.That(
                failure,
                Is.EqualTo(
                    BroadsideShotSamplingFailure.InvalidMuzzleCollection
                )
            );
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }


    [Test]
    public void NullEntryInSelectedCollection_FailsWithoutPartialResult()
    {
        GameObject instance = CreateOperationalCombatShip();

        try
        {
            FireAimBasis basis = BuildBlindFireBasis(
                instance,
                CombatSide.Port,
                100f
            );
            ShipMuzzleSockets sockets =
                instance.GetComponent<ShipMuzzleSockets>();
            IReadOnlyList<Transform> cachedMuzzles = sockets.PortMuzzles;
            UnityEngine.Object.DestroyImmediate(
                cachedMuzzles[0].gameObject
            );

            bool sampled = ShipBroadsideShotSampler.TrySample(
                instance,
                basis,
                13u,
                out BroadsideShotSamplingResult result,
                out BroadsideShotSamplingFailure failure
            );

            Assert.That(sampled, Is.False);
            Assert.That(result.Count, Is.Zero);
            Assert.That(
                failure,
                Is.EqualTo(
                    BroadsideShotSamplingFailure.InvalidMuzzleSocket
                )
            );
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }


    [Test]
    public void SamplingSource_HasNoExecutionOrDeferredSystemDependency()
    {
        string combatRoot = Path.Combine(
            Application.dataPath,
            "Assets/Game/Scripts/Combat"
        );
        string source = File.ReadAllText(
            Path.Combine(combatRoot, "ShipBroadsideShotSampler.cs")
        );
        string ammunitionSource = File.ReadAllText(
            Path.Combine(combatRoot, "ShotSample.cs")
        );

        Assert.That(source, Does.Not.Contain("UnityEngine.Random"));
        Assert.That(source, Does.Not.Contain("TryCommitBroadsideFire"));
        Assert.That(source, Does.Not.Contain("SetAutoFireEnabled"));
        Assert.That(source, Does.Not.Contain("Instantiate("));
        Assert.That(source, Does.Not.Contain("Rigidbody"));
        Assert.That(source, Does.Not.Contain("ShipCombatGeometry"));
        Assert.That(source, Does.Not.Contain("CombatVFX"));
        Assert.That(source, Does.Not.Contain("Damage"));
        Assert.That(source, Does.Not.Contain("ShipSailingSpeed"));
        Assert.That(source, Does.Not.Contain("ShipTurning"));
        Assert.That(source, Does.Not.Contain("transform.position ="));
        Assert.That(source, Does.Not.Contain("transform.rotation ="));
        Assert.That(ammunitionSource, Does.Not.Contain("Chain"));
        Assert.That(ammunitionSource, Does.Not.Contain("Grape"));
        Assert.That(
            File.Exists(Path.Combine(combatRoot, "Projectile.cs")),
            Is.False
        );
    }


    private static BroadsideShotSamplingResult Sample(
        GameObject sourceRoot,
        FireAimBasis basis,
        uint seed
    )
    {
        bool sampled = ShipBroadsideShotSampler.TrySample(
            sourceRoot,
            basis,
            seed,
            out BroadsideShotSamplingResult result,
            out BroadsideShotSamplingFailure failure
        );

        Assert.That(sampled, Is.True, failure.ToString());
        Assert.That(
            failure,
            Is.EqualTo(BroadsideShotSamplingFailure.None)
        );
        return result;
    }


    private static FireAimBasis BuildBlindFireBasis(
        GameObject sourceRoot,
        CombatSide side,
        float distanceMeters
    )
    {
        Vector3 sideDirection = side == CombatSide.Port
            ? -sourceRoot.transform.right
            : sourceRoot.transform.right;
        Vector3 aimPoint = sourceRoot.transform.position
            + sideDirection * distanceMeters;
        ShipFireEligibility eligibility =
            sourceRoot.GetComponent<ShipFireEligibility>();

        Assert.That(
            eligibility.TryEvaluateBlindFireAtPoint(
                aimPoint,
                out BlindFireEligibilityResult eligibilityResult
            ),
            Is.True
        );
        Assert.That(eligibilityResult.CanBlindFire, Is.True);
        Assert.That(
            ShipFireAimBasisBuilder.TryBuildBlindFirePoint(
                eligibilityResult,
                out FireAimBasis basis,
                out FireAimBasisFailure failure
            ),
            Is.True
        );
        Assert.That(failure, Is.EqualTo(FireAimBasisFailure.None));
        return basis;
    }


    private GameObject CreateOperationalCombatShip()
    {
        GameObject instance = UnityEngine.Object.Instantiate(
            LoadCombatPrefab()
        );
        CombatLifecycleTestUtility.EnsureOperational(instance);
        createdShips.Add(instance);
        return instance;
    }


    private static GameObject LoadCombatPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            CombatPrefabPath
        );
        Assert.That(prefab, Is.Not.Null);
        return prefab;
    }


    private static void AssertSampleEqual(
        ShotSample expected,
        ShotSample actual
    )
    {
        Assert.That(
            actual.SourceShipRootIdentity,
            Is.SameAs(expected.SourceShipRootIdentity)
        );
        Assert.That(actual.Side, Is.EqualTo(expected.Side));
        Assert.That(
            actual.MuzzleSequenceIndex,
            Is.EqualTo(expected.MuzzleSequenceIndex)
        );
        Assert.That(actual.BroadsideSeed, Is.EqualTo(expected.BroadsideSeed));
        Assert.That(actual.OriginWorld, Is.EqualTo(expected.OriginWorld));
        Assert.That(
            actual.AimPlaneSamplePointWorld,
            Is.EqualTo(expected.AimPlaneSamplePointWorld)
        );
        Assert.That(
            actual.InitialVelocityWorld,
            Is.EqualTo(expected.InitialVelocityWorld)
        );
        Assert.That(actual.GravityWorld, Is.EqualTo(expected.GravityWorld));
        Assert.That(
            actual.NominalFlightTimeSeconds,
            Is.EqualTo(expected.NominalFlightTimeSeconds)
        );
        Assert.That(
            actual.WaterLevelWorldY,
            Is.EqualTo(expected.WaterLevelWorldY)
        );
        Assert.That(
            actual.MaxLifetimeSeconds,
            Is.EqualTo(expected.MaxLifetimeSeconds)
        );
        Assert.That(actual.AmmunitionType, Is.EqualTo(expected.AmmunitionType));
    }


    private static void AssertBasisEqual(
        FireAimBasis actual,
        FireAimBasis expected
    )
    {
        Assert.That(actual.Side, Is.EqualTo(expected.Side));
        Assert.That(actual.SourceKind, Is.EqualTo(expected.SourceKind));
        Assert.That(
            actual.AimPlaneCenterWorld,
            Is.EqualTo(expected.AimPlaneCenterWorld)
        );
        Assert.That(
            actual.AimDirectionWorld,
            Is.EqualTo(expected.AimDirectionWorld)
        );
        Assert.That(
            actual.AimDistanceMeters,
            Is.EqualTo(expected.AimDistanceMeters)
        );
    }


    private static void AssertImmutableValue<T>()
    {
        Type type = typeof(T);
        Assert.That(type.IsValueType, Is.True);
        Assert.That(
            type.GetFields(
                BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic
                    | BindingFlags.DeclaredOnly
            ).All(field => field.IsInitOnly),
            Is.True
        );
        Assert.That(
            type.GetProperties(
                BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.DeclaredOnly
            ).All(property => property.SetMethod == null),
            Is.True
        );
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


    private static void AssertVector(Vector3 actual, Vector3 expected)
    {
        Assert.That(actual.x, Is.EqualTo(expected.x).Within(Tolerance));
        Assert.That(actual.y, Is.EqualTo(expected.y).Within(Tolerance));
        Assert.That(actual.z, Is.EqualTo(expected.z).Within(Tolerance));
    }
}
