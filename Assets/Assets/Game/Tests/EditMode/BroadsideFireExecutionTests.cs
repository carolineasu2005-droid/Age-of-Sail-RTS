using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class BroadsideFireExecutionTests
{
    private const float MeaningfulSampleDeltaMeters = 0.00001f;
    private const string CombatPrefabPath =
        "Assets/Assets/Game/Ship/Proxy/"
        + "PF_Ship_Gelderland_Combat_v01.prefab";

    private readonly List<GameObject> createdShips =
        new List<GameObject>();


    [TearDown]
    public void TearDown()
    {
        foreach (CombatProjectile projectile in UnityEngine.Object
            .FindObjectsByType<CombatProjectile>(
                FindObjectsInactive.Include
            ))
        {
            UnityEngine.Object.DestroyImmediate(projectile.gameObject);
        }

        for (int index = createdShips.Count - 1; index >= 0; index--)
        {
            UnityEngine.Object.DestroyImmediate(createdShips[index]);
        }

        createdShips.Clear();
    }


    [TestCase(CombatSide.Port, -1f)]
    [TestCase(CombatSide.Starboard, 1f)]
    public void LegalTargetedFire_UsesCommonExecutorAndSpawnsFormalCount(
        CombatSide expectedSide,
        float sideSign
    )
    {
        GameObject shooter = CreateShip(Vector3.zero);
        GameObject target = CreateShip(Vector3.right * sideSign * 100f);
        ShipCombatState state = shooter.GetComponent<ShipCombatState>();
        Vector3 sourcePosition = shooter.transform.position;
        Quaternion sourceRotation = shooter.transform.rotation;

        TargetedFireExecutionResult result = ExecuteTargeted(
            shooter,
            target,
            12345u
        );

        Assert.That(result.Accepted, Is.True);
        Assert.That(result.BroadsideExecution.Side,
            Is.EqualTo(expectedSide));
        Assert.That(result.BroadsideExecution.ShotCount, Is.EqualTo(13));
        Assert.That(
            result.BroadsideExecution.SpawnedProjectileCount,
            Is.EqualTo(13)
        );
        Assert.That(
            UnityEngine.Object.FindObjectsByType<CombatProjectile>(
                FindObjectsInactive.Include
            ).Length,
            Is.EqualTo(13)
        );
        Assert.That(
            expectedSide == CombatSide.Port
                ? state.PortBroadsideState
                : state.StarboardBroadsideState,
            Is.EqualTo(BroadsideReloadState.Reloading)
        );
        Assert.That(
            expectedSide == CombatSide.Port
                ? state.StarboardBroadsideState
                : state.PortBroadsideState,
            Is.EqualTo(BroadsideReloadState.Ready)
        );
        Assert.That(shooter.transform.position, Is.EqualTo(sourcePosition));
        Assert.That(shooter.transform.rotation, Is.EqualTo(sourceRotation));
        Assert.That(
            result.BroadsideExecution.ShotSamples.All(
                sample => sample.AmmunitionType
                    == FoundationAmmunitionType.RoundShot
            ),
            Is.True
        );
    }


    [Test]
    public void TargetedAimCenter_ComesFromTargetExposureCenter()
    {
        GameObject shooter = CreateShip(Vector3.zero);
        GameObject target = CreateShip(Vector3.left * 100f);
        ShipExposureReference exposure =
            target.GetComponent<ShipExposureReference>();
        Assert.That(exposure.TryCalculateExposure(
            shooter.transform.position,
            out ExposureRect expectedExposure
        ), Is.True);

        TargetedFireExecutionResult result = ExecuteTargeted(
            shooter,
            target,
            51u
        );

        Assert.That(
            result.BroadsideExecution.AimBasis.AimPlaneCenterWorld,
            Is.EqualTo(expectedExposure.CenterWorld)
        );
        Assert.That(
            result.BroadsideExecution.AimBasis.SourceKind,
            Is.EqualTo(FireAimSourceKind.Targeted)
        );
    }


    [Test]
    public void SameExplicitSeed_ReproducesSamplesAndDifferentSeedChangesThem()
    {
        ExecutionSnapshot first = ExecuteAndSnapshot(12345u);
        ResetExecutionScene();
        ExecutionSnapshot repeated = ExecuteAndSnapshot(12345u);
        ResetExecutionScene();
        ExecutionSnapshot changed = ExecuteAndSnapshot(54321u);

        AssertDeterministicGeometryEqual(first, repeated);
        Assert.That(repeated.BroadsideSeed, Is.EqualTo(first.BroadsideSeed));
        Assert.That(repeated.SampleOffsets, Is.EqualTo(first.SampleOffsets));

        AssertDeterministicGeometryEqual(first, changed);
        Assert.That(changed.BroadsideSeed, Is.Not.EqualTo(
            first.BroadsideSeed
        ));
        Assert.That(
            changed.SampleOffsets.Zip(
                first.SampleOffsets,
                (actual, expected) => Vector3.Distance(actual, expected)
            ).Any(delta => delta > MeaningfulSampleDeltaMeters),
            Is.True
        );
    }


    [Test]
    public void RuntimeSeedStreams_AreIndependentPerBroadsideSide()
    {
        GameObject firstShooter = CreateShip(Vector3.zero);
        GameObject firstPortTarget = CreateShip(Vector3.left * 100f);
        GameObject firstStarboardTarget = CreateShip(Vector3.right * 100f);
        ShipTargetedFireCommand firstCommand =
            CreateTargetedCommand(firstShooter);
        Physics.SyncTransforms();

        Assert.That(firstCommand.TryExecuteWithRuntimeSeed(
            firstPortTarget,
            true,
            out TargetedFireExecutionResult firstPort
        ), Is.True);
        Assert.That(firstCommand.TryExecuteWithRuntimeSeed(
            firstStarboardTarget,
            true,
            out TargetedFireExecutionResult firstStarboard
        ), Is.True);

        GameObject secondShooter = CreateShip(
            new Vector3(0f, 0f, 1000f)
        );
        GameObject secondStarboardTarget = CreateShip(
            new Vector3(100f, 0f, 1000f)
        );
        Physics.SyncTransforms();
        Assert.That(CreateTargetedCommand(secondShooter)
            .TryExecuteWithRuntimeSeed(
                secondStarboardTarget,
                true,
                out TargetedFireExecutionResult isolatedStarboard
            ), Is.True);

        GameObject thirdShooter = CreateShip(
            new Vector3(0f, 0f, 2000f)
        );
        GameObject thirdPortTarget = CreateShip(
            new Vector3(-100f, 0f, 2000f)
        );
        Physics.SyncTransforms();
        Assert.That(CreateTargetedCommand(thirdShooter)
            .TryExecuteWithRuntimeSeed(
                thirdPortTarget,
                true,
                out TargetedFireExecutionResult isolatedPort
            ), Is.True);

        Assert.That(
            firstPort.BroadsideExecution.BroadsideSeed,
            Is.EqualTo(isolatedPort.BroadsideExecution.BroadsideSeed)
        );
        Assert.That(
            firstStarboard.BroadsideExecution.BroadsideSeed,
            Is.EqualTo(isolatedStarboard.BroadsideExecution.BroadsideSeed)
        );
    }


    [Test]
    public void TargetedCommand_ReevaluatesReloadAndRejectsWithoutProjectile()
    {
        GameObject shooter = CreateShip(Vector3.zero);
        GameObject target = CreateShip(Vector3.right * 100f);
        ShipCombatState state = shooter.GetComponent<ShipCombatState>();
        Assert.That(
            state.TryCommitBroadsideFire(CombatSide.Starboard),
            Is.True
        );
        float reloadRemaining = state.StarboardReloadRemainingSeconds;

        ShipTargetedFireCommand command = CreateTargetedCommand(shooter);
        bool accepted = command.TryExecute(
            target,
            true,
            99u,
            out TargetedFireExecutionResult result
        );

        Assert.That(accepted, Is.False);
        Assert.That(result.FailureReasons.HasFlag(
            TargetedFireExecutionFailure.EligibilityRejected
        ), Is.True);
        Assert.That(result.Eligibility.ReloadReady, Is.False);
        Assert.That(
            state.StarboardReloadRemainingSeconds,
            Is.EqualTo(reloadRemaining)
        );
        Assert.That(
            UnityEngine.Object.FindObjectsByType<CombatProjectile>(
                FindObjectsInactive.Include
            ),
            Is.Empty
        );
    }


    [Test]
    public void CommonExecutor_IsSoleSamplingSpawnAndReloadCoordinator()
    {
        string executorSource = ReadCombatSource(
            "ShipBroadsideFireExecutor.cs"
        );
        string targetedSource = ReadCombatSource(
            "ShipTargetedFireCommand.cs"
        );
        string blindSource = ReadCombatSource("ShipBlindFireCommand.cs");

        Assert.That(executorSource, Does.Contain(
            "ShipBroadsideShotSampler.TrySample"
        ));
        Assert.That(executorSource, Does.Contain(
            "CombatProjectile.TrySpawn"
        ));
        Assert.That(
            CountOccurrences(executorSource, "TryCommitBroadsideFire("),
            Is.EqualTo(1)
        );
        Assert.That(targetedSource, Does.Contain("broadsideExecutor"));
        Assert.That(blindSource, Does.Contain("broadsideExecutor"));
        Assert.That(targetedSource, Does.Not.Contain(
            "ShipBroadsideShotSampler"
        ));
        Assert.That(blindSource, Does.Not.Contain(
            "ShipBroadsideShotSampler"
        ));
        Assert.That(targetedSource, Does.Not.Contain(
            "CombatProjectile.TrySpawn"
        ));
        Assert.That(blindSource, Does.Not.Contain(
            "CombatProjectile.TrySpawn"
        ));
    }


    [Test]
    public void Executor_HasNoMovementDamagePerGunReloadOrHitChanceAuthority()
    {
        string source = ReadCombatSource("ShipBroadsideFireExecutor.cs");

        Assert.That(source, Does.Not.Contain("ShipDestinationController"));
        Assert.That(source, Does.Not.Contain("ShipTurning"));
        Assert.That(source, Does.Not.Contain("Damage"));
        Assert.That(source, Does.Not.Contain("Integrity"));
        Assert.That(source, Does.Not.Contain("HitChance"));
        Assert.That(source, Does.Not.Contain("UnityEngine.Random"));
        Assert.That(source, Does.Not.Contain("transform.rotation ="));
        Assert.That(source, Does.Not.Contain("transform.position ="));
    }


    [Test]
    public void FormalPrefab_WiresExecutorAndGenericMovementProxyDoesNot()
    {
        GameObject combatPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            CombatPrefabPath
        );
        Assert.That(combatPrefab, Is.Not.Null);
        ShipBroadsideFireExecutor executor =
            combatPrefab.GetComponent<ShipBroadsideFireExecutor>();

        Assert.That(executor, Is.Not.Null);
        Assert.That(executor.ProjectilePrefab, Is.Not.Null);
        Assert.That(executor.CombatVFXReceiver, Is.Not.Null);

        string[] movementProxyPaths =
        {
            "Assets/Assets/Game/Ship/Proxy/PF_Proxy_Light_v01.prefab",
            "Assets/Assets/Game/Ship/Proxy/PF_Proxy_Medium_v01.prefab",
            "Assets/Assets/Game/Ship/Proxy/PF_Proxy_Heavy_v01.prefab"
        };

        Assert.That(movementProxyPaths, Is.Not.Empty);
        foreach (string path in movementProxyPaths)
        {
            GameObject proxy = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(
                proxy.GetComponent<ShipBroadsideFireExecutor>(),
                Is.Null,
                path
            );
        }
    }


    [Test]
    public void ExecutionResults_AreImmutableReadOnlyValues()
    {
        AssertImmutableValue(typeof(BroadsideFireExecutionResult));
        AssertImmutableValue(typeof(TargetedFireExecutionResult));
    }


    private TargetedFireExecutionResult ExecuteTargeted(
        GameObject shooter,
        GameObject target,
        uint seed
    )
    {
        Physics.SyncTransforms();
        ShipTargetedFireCommand command = CreateTargetedCommand(shooter);
        bool accepted = command.TryExecute(
            target,
            true,
            seed,
            out TargetedFireExecutionResult result
        );
        Assert.That(accepted, Is.True, result.FailureReasons.ToString());
        return result;
    }


    private ExecutionSnapshot ExecuteAndSnapshot(uint seed)
    {
        GameObject shooter = CreateShip(Vector3.zero);
        GameObject target = CreateShip(Vector3.left * 100f);
        ShipMuzzleSockets sockets = shooter.GetComponent<ShipMuzzleSockets>();
        TargetedFireExecutionResult result = ExecuteTargeted(
            shooter,
            target,
            seed
        );
        BroadsideFireExecutionResult execution =
            result.BroadsideExecution;
        Vector3 center = execution.AimBasis.AimPlaneCenterWorld;

        return new ExecutionSnapshot(
            shooter.transform.position,
            shooter.transform.rotation,
            sockets.PortMuzzles.Select(muzzle => muzzle.position).ToArray(),
            execution.AimBasis,
            execution.SamplingResult.DispersionEllipse,
            execution.BroadsideSeed,
            execution.ShotSamples
                .Select(sample =>
                    sample.AimPlaneSamplePointWorld - center
                )
                .ToArray()
        );
    }


    private GameObject CreateShip(Vector3 position)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            CombatPrefabPath
        );
        Assert.That(prefab, Is.Not.Null);
        GameObject instance = UnityEngine.Object.Instantiate(prefab);
        instance.transform.position = position;
        instance.GetComponent<CombatVFXPlaceholderReceiver>()
            .VisualSpawningEnabled = false;
        createdShips.Add(instance);
        return instance;
    }


    private static ShipTargetedFireCommand CreateTargetedCommand(
        GameObject shooter
    )
    {
        return new ShipTargetedFireCommand(
            shooter.GetComponent<ShipFireEligibility>(),
            shooter.GetComponent<ShipBroadsideFireExecutor>()
        );
    }


    private static void DestroyProjectiles()
    {
        foreach (CombatProjectile projectile in UnityEngine.Object
            .FindObjectsByType<CombatProjectile>(
                FindObjectsInactive.Include
            ))
        {
            UnityEngine.Object.DestroyImmediate(projectile.gameObject);
        }
    }


    private void ResetExecutionScene()
    {
        DestroyProjectiles();

        for (int index = createdShips.Count - 1; index >= 0; index--)
        {
            UnityEngine.Object.DestroyImmediate(createdShips[index]);
        }

        createdShips.Clear();
        Physics.SyncTransforms();
    }


    private static void AssertDeterministicGeometryEqual(
        ExecutionSnapshot expected,
        ExecutionSnapshot actual
    )
    {
        Assert.That(actual.ShooterPosition, Is.EqualTo(
            expected.ShooterPosition
        ));
        Assert.That(actual.ShooterRotation, Is.EqualTo(
            expected.ShooterRotation
        ));
        Assert.That(actual.MuzzlePositions, Is.EqualTo(
            expected.MuzzlePositions
        ));
        Assert.That(
            actual.AimBasis.AimPlaneCenterWorld,
            Is.EqualTo(expected.AimBasis.AimPlaneCenterWorld)
        );
        Assert.That(
            actual.AimBasis.AimDirectionWorld,
            Is.EqualTo(expected.AimBasis.AimDirectionWorld)
        );
        Assert.That(
            actual.AimBasis.AimDistanceMeters,
            Is.EqualTo(expected.AimBasis.AimDistanceMeters)
        );
        Assert.That(
            actual.DispersionEllipse.HorizontalSemiAxisMeters,
            Is.EqualTo(
                expected.DispersionEllipse.HorizontalSemiAxisMeters
            )
        );
        Assert.That(
            actual.DispersionEllipse.VerticalSemiAxisMeters,
            Is.EqualTo(
                expected.DispersionEllipse.VerticalSemiAxisMeters
            )
        );
    }


    private static string ReadCombatSource(string fileName)
    {
        return File.ReadAllText(Path.Combine(
            Application.dataPath,
            "Assets/Game/Scripts/Combat",
            fileName
        ));
    }


    private static int CountOccurrences(string text, string value)
    {
        int count = 0;
        int index = 0;

        while ((index = text.IndexOf(
            value,
            index,
            StringComparison.Ordinal
        )) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }


    private static void AssertImmutableValue(Type type)
    {
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


    private sealed class ExecutionSnapshot
    {
        public ExecutionSnapshot(
            Vector3 shooterPosition,
            Quaternion shooterRotation,
            Vector3[] muzzlePositions,
            FireAimBasis aimBasis,
            DispersionEllipse dispersionEllipse,
            uint broadsideSeed,
            Vector3[] sampleOffsets
        )
        {
            ShooterPosition = shooterPosition;
            ShooterRotation = shooterRotation;
            MuzzlePositions = muzzlePositions;
            AimBasis = aimBasis;
            DispersionEllipse = dispersionEllipse;
            BroadsideSeed = broadsideSeed;
            SampleOffsets = sampleOffsets;
        }


        public Vector3 ShooterPosition { get; }

        public Quaternion ShooterRotation { get; }

        public Vector3[] MuzzlePositions { get; }

        public FireAimBasis AimBasis { get; }

        public DispersionEllipse DispersionEllipse { get; }

        public uint BroadsideSeed { get; }

        public Vector3[] SampleOffsets { get; }
    }
}
