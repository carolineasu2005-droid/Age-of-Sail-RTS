using System;
using UnityEngine;

[AddComponentMenu("Age of Sail/Combat/Combat Projectile")]
[DisallowMultipleComponent]
public sealed class CombatProjectile : MonoBehaviour
{
    private const float MinimumLifetimeSeconds = 0.0001f;

    private ShotSample shotSample;
    private float elapsedTimeSeconds;
    private bool initialized;
    private bool terminated;
    private ProjectileTerminalContact terminalContact;


    public event Action<ProjectileTerminalContact> Terminated;


    public bool IsInitialized => initialized;

    public bool IsTerminated => terminated;

    public float ElapsedTimeSeconds => elapsedTimeSeconds;

    public ShotSample ShotSample => shotSample;

    public ProjectileTerminalContact TerminalContact => terminalContact;


    public static bool TrySpawn(
        CombatProjectile projectilePrefab,
        ShotSample sample,
        out CombatProjectile projectile
    )
    {
        projectile = null;

        if (projectilePrefab == null)
        {
            return false;
        }

        CombatProjectile instance = Instantiate(projectilePrefab);

        if (!instance.TryInitialize(sample))
        {
            Destroy(instance.gameObject);
            return false;
        }

        projectile = instance;
        return true;
    }


    public bool TryInitialize(ShotSample sample)
    {
        if (initialized
            || terminated
            || !IsValidSample(sample)
            || GetComponentInChildren<Rigidbody>(true) != null
            || GetComponentInChildren<Collider>(true) != null)
        {
            return false;
        }

        shotSample = sample;
        elapsedTimeSeconds = 0f;
        transform.position = sample.OriginWorld;
        initialized = true;
        return true;
    }


    public bool SimulateStep(float deltaTimeSeconds)
    {
        if (!initialized
            || terminated
            || !IsFinite(deltaTimeSeconds)
            || deltaTimeSeconds <= 0f)
        {
            return false;
        }

        float previousTimeSeconds = elapsedTimeSeconds;
        float currentTimeSeconds = Mathf.Min(
            previousTimeSeconds + deltaTimeSeconds,
            shotSample.MaxLifetimeSeconds
        );
        Vector3 previousPositionWorld =
            CombatProjectileTrajectory.EvaluatePosition(
                shotSample,
                previousTimeSeconds
            );
        Vector3 nextPositionWorld =
            CombatProjectileTrajectory.EvaluatePosition(
                shotSample,
                currentTimeSeconds
            );

        if (CombatProjectileContactQuery.TryFindEarliestContact(
            shotSample,
            previousPositionWorld,
            nextPositionWorld,
            previousTimeSeconds,
            currentTimeSeconds,
            out ProjectileTerminalContact contact
        ))
        {
            Complete(contact);
            return true;
        }

        elapsedTimeSeconds = currentTimeSeconds;
        transform.position = nextPositionWorld;

        if (currentTimeSeconds >= shotSample.MaxLifetimeSeconds)
        {
            Complete(new ProjectileTerminalContact(
                ProjectileTerminalContactKind.ExpiredSafetyFallback,
                shotSample,
                nextPositionWorld,
                Vector3.zero,
                null,
                1f,
                currentTimeSeconds
            ));
        }

        return true;
    }


    private void Update()
    {
        SimulateStep(Time.deltaTime);
    }


    private void Complete(ProjectileTerminalContact contact)
    {
        if (terminated)
        {
            return;
        }

        terminalContact = contact;
        elapsedTimeSeconds = contact.ElapsedFlightTimeSeconds;
        transform.position = contact.PointWorld;
        terminated = true;
        enabled = false;
        Terminated?.Invoke(contact);
        Destroy(gameObject);
    }


    private static bool IsValidSample(ShotSample sample)
    {
        return sample.SourceShipRootIdentity != null
            && sample.AmmunitionType
                == FoundationAmmunitionType.RoundShot
            && IsFinite(sample.OriginWorld)
            && IsFinite(sample.AimPlaneSamplePointWorld)
            && IsFinite(sample.InitialVelocityWorld)
            && IsFinite(sample.GravityWorld)
            && IsFinite(sample.NominalFlightTimeSeconds)
            && sample.NominalFlightTimeSeconds > 0f
            && IsFinite(sample.WaterLevelWorldY)
            && IsFinite(sample.MaxLifetimeSeconds)
            && sample.MaxLifetimeSeconds > MinimumLifetimeSeconds;
    }


    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x)
            && IsFinite(value.y)
            && IsFinite(value.z);
    }


    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
