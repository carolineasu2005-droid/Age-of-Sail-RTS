using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    public IEnumerator AcceptedBroadside_EmitsOneMuzzlePlaceholderPerShot()
    {
        GameObject shooter = CreateShip(Vector3.zero, true);
        GameObject target = CreateShip(Vector3.right * 100f, false);

        TargetedFireExecutionResult result = ExecuteTargeted(
            shooter,
            target,
            4321u
        );
        GameObject[] muzzlePlaceholders = Object
            .FindObjectsByType<GameObject>(FindObjectsInactive.Include)
            .Where(candidate => candidate.name
                == "Combat VFX Placeholder - Muzzle Fire")
            .ToArray();

        Assert.That(result.BroadsideExecution.ShotCount, Is.EqualTo(13));
        Assert.That(muzzlePlaceholders, Has.Length.EqualTo(13));
        yield return null;
    }


    [UnityTest]
    public IEnumerator IntegratedHullContact_ResolvesSemanticHitAndVFXOnly()
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
        Assert.That(
            Object.FindObjectsByType<GameObject>(
                FindObjectsInactive.Include
            ).Count(candidate => candidate.name
                == "Combat VFX Placeholder - Hull Impact"),
            Is.EqualTo(1)
        );
        Assert.That(shooter.transform.position, Is.EqualTo(shooterPosition));
        Assert.That(shooter.transform.rotation, Is.EqualTo(shooterRotation));
        yield return null;
    }


    [UnityTest]
    public IEnumerator IntegratedBlindFireMiss_ResolvesWaterAndVFXOnly()
    {
        GameObject shooter = CreateShip(Vector3.zero, true);
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


    private static CombatProjectile[] FindCurrentProjectiles()
    {
        return Object.FindObjectsByType<CombatProjectile>(
            FindObjectsInactive.Include
        );
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
}
