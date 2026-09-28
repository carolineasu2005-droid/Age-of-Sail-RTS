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

public class BroadsideFireExecutionPlayModeTests
{
    private const string CombatPrefabPath =
        "Assets/Assets/Game/Ship/Proxy/"
        + "PF_Ship_Gelderland_Combat_v01.prefab";
    private const float PositionTolerance = 0.001f;
    private const string IslandPrefabPath =
        "Assets/Assets/Game/Combat/Prefabs/"
        + "PF_Debug_CombatIsland_v01.prefab";

    private readonly List<GameObject> createdShips =
        new List<GameObject>();


    [UnityTearDown]
    public IEnumerator TearDown()
    {
        foreach (CombatProjectile projectile in Object
            .FindObjectsByType<CombatProjectile>(
                FindObjectsInactive.Include
            ))
        {
            Object.Destroy(projectile.gameObject);
        }

        foreach (GameObject placeholder in Object
            .FindObjectsByType<GameObject>(FindObjectsInactive.Include)
            .Where(candidate => candidate.name.StartsWith(
                "Combat VFX Placeholder -"
            )))
        {
            Object.Destroy(placeholder);
        }

        foreach (GameObject ship in createdShips)
        {
            if (ship != null)
            {
                Object.Destroy(ship);
            }
        }

        createdShips.Clear();
        yield return null;
    }


    [UnityTest]
    public IEnumerator TargetMovementAfterLaunch_DoesNotHomeProjectile()
    {
        GameObject shooter = CreateShip(Vector3.zero, false);
        GameObject target = CreateShip(Vector3.left * 300f, false);
        TargetedFireExecutionResult result = ExecuteTargeted(
            shooter,
            target,
            12345u
        );
        CombatProjectile[] projectiles = FindCurrentProjectiles();
        Assert.That(projectiles, Has.Length.EqualTo(13));
        foreach (CombatProjectile projectile in projectiles)
        {
            projectile.enabled = false;
        }

        CombatProjectile observed = projectiles[0];
        ShotSample launchSnapshot = observed.ShotSample;
        Vector3 launchPosition = observed.transform.position;
        target.transform.position += Vector3.forward * 200f;
        Physics.SyncTransforms();

        Assert.That(observed.SimulateStep(0.1f), Is.True);
        Vector3 expected = CombatProjectileTrajectory.EvaluatePosition(
            launchSnapshot,
            0.1f
        );

        Assert.That(result.BroadsideExecution.ShotCount, Is.EqualTo(13));
        Assert.That(observed.IsTerminated, Is.False);
        Assert.That(
            Vector3.Distance(observed.transform.position, launchPosition),
            Is.GreaterThan(PositionTolerance)
        );
        Assert.That(
            Vector3.Distance(observed.transform.position, expected),
            Is.LessThan(PositionTolerance)
        );
        yield return null;
    }


    [UnityTest]
    public IEnumerator AcceptedBroadside_EmitsOneLingeringSmokePerShot()
    {
        GameObject shooter = CreateShip(Vector3.zero, true);
        GameObject target = CreateShip(Vector3.right * 100f, false);

        TargetedFireExecutionResult result = ExecuteTargeted(
            shooter,
            target,
            4321u
        );
        ParticleSystem[] muzzleSmoke = Object
            .FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include)
            .Where(candidate => candidate.gameObject.name
                == "VFX_Cannon_LingeringSmoke_v01(Clone)")
            .ToArray();
        CombatProjectile[] projectiles = FindCurrentProjectiles();
        ShipCombatState state = shooter.GetComponent<ShipCombatState>();

        Assert.That(result.BroadsideExecution.ShotCount, Is.EqualTo(13));
        Assert.That(
            result.BroadsideExecution.SpawnedProjectileCount,
            Is.EqualTo(13)
        );
        Assert.That(projectiles, Has.Length.EqualTo(13));
        Assert.That(
            state.StarboardBroadsideState,
            Is.EqualTo(BroadsideReloadState.Reloading)
        );
        Assert.That(muzzleSmoke, Has.Length.EqualTo(13));
        Assert.That(
            muzzleSmoke.All(candidate =>
                candidate.isPlaying
                && candidate.transform.parent == null),
            Is.True
        );
        yield return null;
    }


    [UnityTest]
    public IEnumerator IntegratedHullContact_ResolvesDamageAndVFX()
    {
        GameObject shooter = CreateShip(Vector3.zero, true);
        GameObject target = CreateShip(Vector3.right * 100f, false);
        ShipCombatGeometry geometry =
            target.GetComponent<ShipCombatGeometry>();
        BoxCollider midshipCollider =
            geometry.MidshipRegion.QueryCollider as BoxCollider;
        Assert.That(midshipCollider, Is.Not.Null);
        midshipCollider.size = new Vector3(60f, 60f, 60f);
        Physics.SyncTransforms();
        Vector3 shooterPosition = shooter.transform.position;
        Quaternion shooterRotation = shooter.transform.rotation;
        ShipIntegrity targetIntegrity = target.GetComponent<ShipIntegrity>();
        float previousIntegrity = targetIntegrity.CurrentIntegrity;
        CombatDamageProfile damageProfile = shooter
            .GetComponent<ShipBroadsideFireExecutor>()
            .CombatDamageProfile;

        ExecuteTargeted(shooter, target, 2468u);
        CombatProjectile[] projectiles = FindCurrentProjectiles();
        foreach (CombatProjectile projectile in projectiles)
        {
            projectile.enabled = false;
        }

        CombatProjectile observed = projectiles[0];
        SimulateUntilTerminal(observed);
        ProjectileTerminalContact contact = observed.TerminalContact;
        Assert.That(
            FoundationShotOutcomeResolver.TryResolve(
                contact,
                out FoundationShotOutcome outcome,
                out FoundationShotOutcomeFailure failure
            ),
            Is.True,
            failure.ToString()
        );

        Assert.That(
            outcome.Kind,
            Is.EqualTo(FoundationShotOutcomeKind.HullHit)
        );
        Assert.That(outcome.HasHitContext, Is.True);
        Assert.That(outcome.HitContext.TargetShipRoot, Is.SameAs(target));
        Assert.That(
            outcome.HitContext.Region,
            Is.EqualTo(CombatHullRegion.Midship)
        );
        Assert.That(
            shooter.GetComponent<ShipBroadsideFireExecutor>()
                .LastHullHitCount,
            Is.EqualTo(1)
        );
        ShipBroadsideFireExecutor executor =
            shooter.GetComponent<ShipBroadsideFireExecutor>();
        Assert.That(executor.HasLastTerminalResolution, Is.True);
        Assert.That(
            executor.LastTerminalResolution.Outcome.HitContext.Region,
            Is.EqualTo(CombatHullRegion.Midship)
        );
        Assert.That(
            executor.LastTerminalResolution.DamageResult.HitRegion,
            Is.EqualTo(CombatHullRegion.Midship)
        );
        Assert.That(
            targetIntegrity.CurrentIntegrity,
            Is.EqualTo(
                previousIntegrity - damageProfile.RoundShotBaseDamage
            )
        );
        Assert.That(
            Object.FindObjectsByType<GameObject>(
                FindObjectsInactive.Include
            ).Count(candidate => candidate.name
                == "Combat VFX Placeholder - Hull Impact"),
            Is.EqualTo(1)
        );
        Assert.That(shooter.transform.position, Is.EqualTo(shooterPosition));
        Assert.That(shooter.transform.rotation, Is.EqualTo(shooterRotation));

        float integrityAfterTerminal = targetIntegrity.CurrentIntegrity;
        Assert.That(observed.SimulateStep(0.02f), Is.False);
        Assert.That(
            targetIntegrity.CurrentIntegrity,
            Is.EqualTo(integrityAfterTerminal)
        );
        Assert.That(executor.LastHullHitCount, Is.EqualTo(1));
        yield return null;
    }


    [UnityTest]
    public IEnumerator FriendlyShipMovedIntoFlightPath_BlocksWithoutDamage()
    {
        GameObject shooter = CreateShip(Vector3.zero, false);
        GameObject intendedTarget = CreateShip(Vector3.right * 100f, false);
        GameObject interceptor = CreateShip(
            Vector3.right * 50f + Vector3.forward * 200f,
            false
        );
        SetTeamId(interceptor, 1);
        PrepareInterceptor(interceptor, Quaternion.identity);
        ShipIntegrity interceptorIntegrity =
            interceptor.GetComponent<ShipIntegrity>();
        ShipIntegrity targetIntegrity =
            intendedTarget.GetComponent<ShipIntegrity>();
        float interceptorBefore = interceptorIntegrity.CurrentIntegrity;
        float targetBefore = targetIntegrity.CurrentIntegrity;
        CombatProjectile projectile = LaunchAndFreezeOne(
            shooter,
            intendedTarget,
            9101u
        );
        ShipCombatState state = shooter.GetComponent<ShipCombatState>();
        float reloadRemaining = state.StarboardReloadRemainingSeconds;
        Vector3 shooterPosition = shooter.transform.position;
        Quaternion shooterRotation = shooter.transform.rotation;
        interceptor.transform.position = Vector3.right * 50f;
        Vector3 interceptorPosition = interceptor.transform.position;
        Quaternion interceptorRotation = interceptor.transform.rotation;
        Physics.SyncTransforms();

        SimulateUntilTerminal(projectile);

        CombatTerminalResolutionDiagnostics diagnostics = shooter
            .GetComponent<ShipBroadsideFireExecutor>()
            .LastTerminalResolution;
        Assert.That(
            diagnostics.Outcome.Kind,
            Is.EqualTo(FoundationShotOutcomeKind.FriendlyShipBlocked)
        );
        Assert.That(
            diagnostics.Outcome.HitContext.TargetShipRoot,
            Is.SameAs(interceptor)
        );
        Assert.That(diagnostics.DamageResolutionSucceeded, Is.True);
        Assert.That(diagnostics.DamageResult.Applied, Is.False);
        Assert.That(diagnostics.DamageResult.RequestedDamage, Is.Zero);
        Assert.That(diagnostics.DamageResult.AppliedDamage, Is.Zero);
        Assert.That(diagnostics.DamageResult.HasRakingResult, Is.False);
        Assert.That(
            interceptorIntegrity.CurrentIntegrity,
            Is.EqualTo(interceptorBefore)
        );
        Assert.That(targetIntegrity.CurrentIntegrity, Is.EqualTo(targetBefore));
        Assert.That(interceptorIntegrity.LifecycleState,
            Is.EqualTo(ShipCombatLifecycleState.Operational));
        Assert.That(state.StarboardBroadsideState,
            Is.EqualTo(BroadsideReloadState.Reloading));
        Assert.That(state.StarboardReloadRemainingSeconds,
            Is.EqualTo(reloadRemaining));
        Assert.That(shooter.transform.position, Is.EqualTo(shooterPosition));
        Assert.That(shooter.transform.rotation, Is.EqualTo(shooterRotation));
        Assert.That(interceptor.transform.position,
            Is.EqualTo(interceptorPosition));
        Assert.That(interceptor.transform.rotation,
            Is.EqualTo(interceptorRotation));
        Assert.That(projectile.SimulateStep(0.02f), Is.False);
        yield return null;
    }


    [UnityTest]
    public IEnumerator HostileShipMovedIntoFlightPath_TakesActualHitDamage()
    {
        GameObject shooter = CreateShip(Vector3.zero, false);
        GameObject intendedTarget = CreateShip(Vector3.right * 100f, false);
        GameObject interceptor = CreateShip(
            Vector3.right * 50f + Vector3.forward * 200f,
            false
        );
        SetTeamId(interceptor, 3);
        PrepareInterceptor(
            interceptor,
            Quaternion.LookRotation(Vector3.right, Vector3.up)
        );
        ShipIntegrity interceptorIntegrity =
            interceptor.GetComponent<ShipIntegrity>();
        ShipIntegrity targetIntegrity =
            intendedTarget.GetComponent<ShipIntegrity>();
        float interceptorBefore = interceptorIntegrity.CurrentIntegrity;
        float targetBefore = targetIntegrity.CurrentIntegrity;
        CombatProjectile projectile = LaunchAndFreezeOne(
            shooter,
            intendedTarget,
            9102u
        );
        interceptor.transform.position = Vector3.right * 50f;
        Physics.SyncTransforms();

        SimulateUntilTerminal(projectile);

        CombatTerminalResolutionDiagnostics diagnostics = shooter
            .GetComponent<ShipBroadsideFireExecutor>()
            .LastTerminalResolution;
        Assert.That(
            diagnostics.Outcome.Kind,
            Is.EqualTo(FoundationShotOutcomeKind.HullHit)
        );
        Assert.That(
            diagnostics.Outcome.HitContext.TargetShipRoot,
            Is.SameAs(interceptor)
        );
        Assert.That(
            diagnostics.DamageResult.TargetShipRoot,
            Is.SameAs(interceptor)
        );
        Assert.That(diagnostics.DamageResult.AppliedDamage,
            Is.GreaterThan(0f));
        Assert.That(diagnostics.DamageResult.HasRakingResult, Is.True);
        Assert.That(diagnostics.DamageResult.RakingType,
            Is.EqualTo(CombatRakingType.Stern));
        Assert.That(interceptorIntegrity.CurrentIntegrity,
            Is.LessThan(interceptorBefore));
        Assert.That(targetIntegrity.CurrentIntegrity, Is.EqualTo(targetBefore));
        yield return null;
    }


    [UnityTest]
    public IEnumerator UnknownShipMovedIntoFlightPath_BlocksWithoutDamage()
    {
        GameObject shooter = CreateShip(Vector3.zero, false);
        GameObject intendedTarget = CreateShip(Vector3.right * 100f, false);
        GameObject interceptor = CreateShip(
            Vector3.right * 50f + Vector3.forward * 200f,
            false
        );
        PrepareInterceptor(interceptor, Quaternion.identity);
        ShipIntegrity interceptorIntegrity =
            interceptor.GetComponent<ShipIntegrity>();
        ShipIntegrity targetIntegrity =
            intendedTarget.GetComponent<ShipIntegrity>();
        float interceptorBefore = interceptorIntegrity.CurrentIntegrity;
        float targetBefore = targetIntegrity.CurrentIntegrity;
        CombatProjectile projectile = LaunchAndFreezeOne(
            shooter,
            intendedTarget,
            9103u
        );
        interceptor.transform.position = Vector3.right * 50f;
        Physics.SyncTransforms();

        SimulateUntilTerminal(projectile);

        CombatTerminalResolutionDiagnostics diagnostics = shooter
            .GetComponent<ShipBroadsideFireExecutor>()
            .LastTerminalResolution;
        Assert.That(
            diagnostics.Outcome.Kind,
            Is.EqualTo(FoundationShotOutcomeKind.UnknownShipBlocked)
        );
        Assert.That(diagnostics.DamageResult.Applied, Is.False);
        Assert.That(diagnostics.DamageResult.RequestedDamage, Is.Zero);
        Assert.That(diagnostics.DamageResult.AppliedDamage, Is.Zero);
        Assert.That(diagnostics.DamageResult.HasRakingResult, Is.False);
        Assert.That(interceptorIntegrity.CurrentIntegrity,
            Is.EqualTo(interceptorBefore));
        Assert.That(targetIntegrity.CurrentIntegrity, Is.EqualTo(targetBefore));
        yield return null;
    }


    [UnityTest]
    public IEnumerator WorldObstructionMovedIntoFlightPath_TerminatesRound()
    {
        GameObject shooter = CreateShip(Vector3.zero, false);
        GameObject intendedTarget = CreateShip(Vector3.right * 100f, false);
        GameObject islandPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            IslandPrefabPath
        );
        Assert.That(islandPrefab, Is.Not.Null);
        GameObject island = Object.Instantiate(islandPrefab);
        island.transform.position =
            Vector3.right * 50f + Vector3.forward * 200f;
        createdShips.Add(island);
        ShipIntegrity targetIntegrity =
            intendedTarget.GetComponent<ShipIntegrity>();
        float targetBefore = targetIntegrity.CurrentIntegrity;
        CombatProjectile projectile = LaunchAndFreezeOne(
            shooter,
            intendedTarget,
            9104u
        );
        island.transform.position = Vector3.right * 50f;
        Physics.SyncTransforms();

        SimulateUntilTerminal(projectile);

        CombatTerminalResolutionDiagnostics diagnostics = shooter
            .GetComponent<ShipBroadsideFireExecutor>()
            .LastTerminalResolution;
        Assert.That(
            projectile.TerminalContact.Kind,
            Is.EqualTo(
                ProjectileTerminalContactKind.WorldObstructionContact
            )
        );
        Assert.That(
            diagnostics.Outcome.Kind,
            Is.EqualTo(FoundationShotOutcomeKind.WorldObstructionBlocked)
        );
        Assert.That(diagnostics.Outcome.HasHitContext, Is.False);
        Assert.That(diagnostics.DamageResolutionSucceeded, Is.True);
        Assert.That(diagnostics.DamageResult.TargetShipRoot, Is.Null);
        Assert.That(diagnostics.DamageResult.AppliedDamage, Is.Zero);
        Assert.That(diagnostics.DamageResult.HasRakingResult, Is.False);
        Assert.That(targetIntegrity.CurrentIntegrity, Is.EqualTo(targetBefore));
        Assert.That(projectile.SimulateStep(0.02f), Is.False);
        yield return null;
    }


    [UnityTest]
    public IEnumerator RepeatedPhysicalHits_ReachDisabledThenSinkingPresentation()
    {
        GameObject shooter = CreateShip(Vector3.zero, false);
        GameObject target = CreateShip(Vector3.right * 100f, false);
        ShipCombatGeometry geometry =
            target.GetComponent<ShipCombatGeometry>();
        BoxCollider midshipCollider =
            geometry.MidshipRegion.QueryCollider as BoxCollider;
        Assert.That(midshipCollider, Is.Not.Null);
        midshipCollider.size = new Vector3(60f, 60f, 60f);
        ShipIntegrity integrity = target.GetComponent<ShipIntegrity>();
        ShipSinkingPresentation presentation =
            target.GetComponent<ShipSinkingPresentation>();
        Transform visualRoot = target.GetComponent<ShipArtDefinition>()
            .VisualRoot;
        ShipCombatState shooterState =
            shooter.GetComponent<ShipCombatState>();
        ShipBroadsideFireExecutor executor =
            shooter.GetComponent<ShipBroadsideFireExecutor>();
        CombatDamageProfile damageProfile = executor.CombatDamageProfile;
        Physics.SyncTransforms();

        const int sinkingBroadsideIndex = 8;

        for (int broadsideIndex = 1;
            broadsideIndex <= sinkingBroadsideIndex;
            broadsideIndex++)
        {
            TargetedFireExecutionResult execution = ExecuteTargeted(
                shooter,
                target,
                (uint)(7000 + broadsideIndex)
            );
            Assert.That(
                execution.BroadsideExecution.ShotCount,
                Is.EqualTo(13)
            );
            Assert.That(
                execution.BroadsideExecution.SpawnedProjectileCount,
                Is.EqualTo(13)
            );

            CombatProjectile[] projectiles = FindCurrentProjectiles();
            Assert.That(projectiles, Has.Length.EqualTo(13));
            foreach (CombatProjectile projectile in projectiles)
            {
                projectile.enabled = false;
            }

            float integrityBeforeBroadside = integrity.CurrentIntegrity;

            foreach (CombatProjectile projectile in projectiles)
            {
                SimulateUntilTerminal(projectile);
            }

            Assert.That(
                integrity.CurrentIntegrity,
                Is.EqualTo(Mathf.Max(
                    0f,
                    integrityBeforeBroadside
                        - projectiles.Length
                        * damageProfile.RoundShotBaseDamage
                ))
            );
            Assert.That(executor.LastHullHitCount, Is.EqualTo(13));
            Assert.That(executor.LastWaterMissCount, Is.Zero);

            yield return null;

            if (broadsideIndex <= 5)
            {
                Assert.That(
                    integrity.LifecycleState,
                    Is.EqualTo(ShipCombatLifecycleState.Operational)
                );
            }
            else if (broadsideIndex == 6 || broadsideIndex == 7)
            {
                Assert.That(
                    integrity.LifecycleState,
                    Is.EqualTo(ShipCombatLifecycleState.CombatDisabled)
                );
                Assert.That(presentation.HasStarted, Is.False);
            }

            if (broadsideIndex < sinkingBroadsideIndex)
            {
                AdvanceReloadsToReady(shooterState);
            }
        }

        Assert.That(
            integrity.LifecycleState,
            Is.EqualTo(ShipCombatLifecycleState.Sinking)
        );
        Assert.That(presentation.HasStarted, Is.True);
        Assert.That(target.activeSelf, Is.True);
        Vector3 rootPosition = target.transform.position;
        Quaternion rootRotation = target.transform.rotation;
        Vector3 initialVisualPosition = visualRoot.localPosition;

        AdvanceSinkingPresentation(presentation, 1f);

        Assert.That(
            visualRoot.localPosition.y,
            Is.LessThan(initialVisualPosition.y)
        );
        Assert.That(target.transform.position, Is.EqualTo(rootPosition));
        Assert.That(target.transform.rotation, Is.EqualTo(rootRotation));
        Assert.That(target.activeSelf, Is.True);
    }


    [UnityTest]
    public IEnumerator IntegratedBlindFireMiss_ResolvesWaterAndVFXOnly()
    {
        GameObject shooter = CreateShip(Vector3.zero, true);
        GameObject unaffectedTarget = CreateShip(
            Vector3.right * 100f,
            false
        );
        ShipIntegrity unaffectedIntegrity =
            unaffectedTarget.GetComponent<ShipIntegrity>();
        float integrityBeforeMiss = unaffectedIntegrity.CurrentIntegrity;
        ShipBlindFireCommand command = new ShipBlindFireCommand(
            shooter.GetComponent<ShipFireEligibility>(),
            shooter.GetComponent<ShipCombatState>(),
            shooter.GetComponent<ShipBroadsideFireExecutor>()
        );
        ShipFireEligibility eligibility =
            shooter.GetComponent<ShipFireEligibility>();
        Assert.That(eligibility.TryEvaluateBlindFireAtPoint(
            Vector3.left * 100f,
            out BlindFireEligibilityResult query
        ), Is.True);
        Assert.That(command.TryExecuteBlindFire(
            query.Aim,
            9876u,
            out BlindFireExecutionResult execution
        ), Is.True);
        CombatProjectile[] projectiles = FindCurrentProjectiles();
        foreach (CombatProjectile projectile in projectiles)
        {
            projectile.enabled = false;
        }

        CombatProjectile observed = projectiles[0];
        SimulateUntilTerminal(observed);

        Assert.That(execution.BroadsideExecution.ShotCount, Is.EqualTo(13));
        Assert.That(
            observed.TerminalContact.Kind,
            Is.EqualTo(ProjectileTerminalContactKind.WaterContact)
        );
        Assert.That(
            shooter.GetComponent<ShipBroadsideFireExecutor>()
                .LastWaterMissCount,
            Is.EqualTo(1)
        );
        ShipBroadsideFireExecutor executor =
            shooter.GetComponent<ShipBroadsideFireExecutor>();
        Assert.That(executor.HasLastTerminalResolution, Is.True);
        Assert.That(
            executor.LastTerminalResolution.Outcome.Kind,
            Is.EqualTo(FoundationShotOutcomeKind.WaterMiss)
        );
        Assert.That(
            executor.LastTerminalResolution.DamageResult.AppliedDamage,
            Is.Zero
        );
        Assert.That(
            unaffectedIntegrity.CurrentIntegrity,
            Is.EqualTo(integrityBeforeMiss)
        );
        Assert.That(
            Object.FindObjectsByType<GameObject>(
                FindObjectsInactive.Include
            ).Count(candidate => candidate.name
                == "Combat VFX Placeholder - Water Impact"),
            Is.EqualTo(1)
        );
        yield return null;
    }


    private GameObject CreateShip(Vector3 position, bool enableVFX)
    {
#if UNITY_EDITOR
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            CombatPrefabPath
        );
        Assert.That(prefab, Is.Not.Null);
        GameObject ship = Object.Instantiate(prefab);
        ship.transform.position = position;
        ship.GetComponent<CombatVFXPlaceholderReceiver>()
            .VisualSpawningEnabled = enableVFX;
        createdShips.Add(ship);
        return ship;
#else
        Assert.Ignore("Prefab loading requires the Unity Editor.");
        return null;
#endif
    }


    private static TargetedFireExecutionResult ExecuteTargeted(
        GameObject shooter,
        GameObject target,
        uint seed
    )
    {
        SetTeamId(shooter, 1);
        SetTeamId(target, 2);
        Physics.SyncTransforms();
        ShipTargetedFireCommand command = new ShipTargetedFireCommand(
            shooter.GetComponent<ShipFireEligibility>(),
            shooter.GetComponent<ShipBroadsideFireExecutor>()
        );
        Assert.That(command.TryExecute(
            target,
            true,
            seed,
            out TargetedFireExecutionResult result
        ), Is.True, result.FailureReasons.ToString());
        return result;
    }


    private static void SetTeamId(GameObject shipRoot, int teamId)
    {
        ShipCombatAffiliation affiliation =
            shipRoot.GetComponent<ShipCombatAffiliation>();

        if (affiliation == null)
        {
            affiliation = shipRoot.AddComponent<ShipCombatAffiliation>();
        }

        FieldInfo field = typeof(ShipCombatAffiliation).GetField(
            "teamId",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        Assert.That(field, Is.Not.Null);
        field.SetValue(affiliation, teamId);
    }


    private static CombatProjectile[] FindCurrentProjectiles()
    {
        return Object.FindObjectsByType<CombatProjectile>(
            FindObjectsInactive.Include
        );
    }


    private static CombatProjectile LaunchAndFreezeOne(
        GameObject shooter,
        GameObject intendedTarget,
        uint seed
    )
    {
        TargetedFireExecutionResult execution = ExecuteTargeted(
            shooter,
            intendedTarget,
            seed
        );
        Assert.That(execution.Accepted, Is.True);
        CombatProjectile[] projectiles = FindCurrentProjectiles();
        Assert.That(projectiles, Has.Length.EqualTo(13));

        foreach (CombatProjectile projectile in projectiles)
        {
            projectile.enabled = false;
        }

        return projectiles[0];
    }


    private static void PrepareInterceptor(
        GameObject interceptor,
        Quaternion rotation
    )
    {
        interceptor.transform.rotation = rotation;
        ShipCombatGeometry geometry =
            interceptor.GetComponent<ShipCombatGeometry>();
        geometry.BowRegion.QueryCollider.enabled = false;
        geometry.SternRegion.QueryCollider.enabled = false;
        BoxCollider midship = geometry.MidshipRegion.QueryCollider
            as BoxCollider;
        Assert.That(midship, Is.Not.Null);
        midship.size = Vector3.one * 40f;
    }


    private static void SimulateUntilTerminal(CombatProjectile projectile)
    {
        const float stepSeconds = 0.02f;
        const int maximumSteps = 1000;

        for (int step = 0;
            step < maximumSteps && !projectile.IsTerminated;
            step++)
        {
            Assert.That(projectile.SimulateStep(stepSeconds), Is.True);
        }

        Assert.That(projectile.IsTerminated, Is.True);
    }


    private static void AdvanceReloadsToReady(ShipCombatState state)
    {
        MethodInfo method = typeof(ShipCombatState).GetMethod(
            "AdvanceReloads",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        Assert.That(method, Is.Not.Null);
        method.Invoke(
            state,
            new object[] { state.BroadsideReloadDurationSeconds }
        );
        Assert.That(
            state.PortBroadsideState,
            Is.EqualTo(BroadsideReloadState.Ready)
        );
        Assert.That(
            state.StarboardBroadsideState,
            Is.EqualTo(BroadsideReloadState.Ready)
        );
    }


    private static void AdvanceSinkingPresentation(
        ShipSinkingPresentation presentation,
        float deltaTimeSeconds
    )
    {
        MethodInfo method = typeof(ShipSinkingPresentation).GetMethod(
            "AdvancePresentation",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        Assert.That(method, Is.Not.Null);
        method.Invoke(presentation, new object[] { deltaTimeSeconds });
    }
}
