using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("Age of Sail/Combat/Ship Broadside Fire Executor")]
[DisallowMultipleComponent]
public sealed class ShipBroadsideFireExecutor : MonoBehaviour
{
    private const uint InitialPortSeedState = 0xA341316Cu;
    private const uint InitialStarboardSeedState = 0xC8013EA4u;
    private const float MinimumDirectionSqrMagnitude = 0.000001f;

    [Header("Physical Fire")]

    [SerializeField]
    private CombatProjectile projectilePrefab;

    [SerializeField]
    private MonoBehaviour combatVFXReceiver;

    private uint portSeedState = InitialPortSeedState;
    private uint starboardSeedState = InitialStarboardSeedState;
    private uint executionSequence;
    private int lastHullHitCount;
    private int lastWaterMissCount;
    private int lastExpiredCount;


    public CombatProjectile ProjectilePrefab => projectilePrefab;

    public ICombatVFXEventReceiver CombatVFXReceiver =>
        combatVFXReceiver as ICombatVFXEventReceiver;

    public int LastHullHitCount => lastHullHitCount;

    public int LastWaterMissCount => lastWaterMissCount;

    public int LastExpiredCount => lastExpiredCount;


    public bool TryExecute(
        FireAimBasis aimBasis,
        uint broadsideSeed,
        out BroadsideFireExecutionResult result
    )
    {
        result = default;

        if (!IsValidAimBasis(aimBasis))
        {
            result = CreateRejected(
                aimBasis,
                broadsideSeed,
                BroadsideShotSamplingFailure.InvalidAimBasis,
                BroadsideFireExecutionFailure.InvalidAimBasis
            );
            return false;
        }

        ShipCombatState combatState = GetComponent<ShipCombatState>();
        ShipMuzzleSockets muzzleSockets = GetComponent<ShipMuzzleSockets>();

        if (combatState == null || muzzleSockets == null)
        {
            result = CreateRejected(
                aimBasis,
                broadsideSeed,
                BroadsideShotSamplingFailure.MissingConfiguration,
                BroadsideFireExecutionFailure.MissingDependencies
            );
            return false;
        }

        if (!ShipBroadsideShotSampler.TrySample(
            gameObject,
            aimBasis,
            broadsideSeed,
            out BroadsideShotSamplingResult samplingResult,
            out BroadsideShotSamplingFailure samplingFailure
        ))
        {
            result = CreateRejected(
                aimBasis,
                broadsideSeed,
                samplingFailure,
                BroadsideFireExecutionFailure.ShotSamplingFailed
            );
            return false;
        }

        if (!IsValidProjectilePrefab(projectilePrefab))
        {
            result = new BroadsideFireExecutionResult(
                false,
                aimBasis.Side,
                broadsideSeed,
                aimBasis,
                samplingResult,
                0,
                BroadsideShotSamplingFailure.None,
                BroadsideFireExecutionFailure.InvalidProjectilePrefab
            );
            return false;
        }

        if (!combatState.TryCommitBroadsideFire(aimBasis.Side))
        {
            result = new BroadsideFireExecutionResult(
                false,
                aimBasis.Side,
                broadsideSeed,
                aimBasis,
                samplingResult,
                0,
                BroadsideShotSamplingFailure.None,
                BroadsideFireExecutionFailure.BroadsideCommitFailed
            );
            return false;
        }

        uint currentSequence = ++executionSequence;
        lastHullHitCount = 0;
        lastWaterMissCount = 0;
        lastExpiredCount = 0;
        ICombatVFXEventReceiver receiver = CombatVFXReceiver;
        IReadOnlyList<Transform> muzzles = aimBasis.Side == CombatSide.Port
            ? muzzleSockets.PortMuzzles
            : muzzleSockets.StarboardMuzzles;
        int spawnedCount = 0;

        for (int index = 0; index < samplingResult.Count; index++)
        {
            ShotSample sample = samplingResult.ShotSamples[index];

            if (!CombatProjectile.TrySpawn(
                projectilePrefab,
                sample,
                out CombatProjectile projectile
            ))
            {
                result = new BroadsideFireExecutionResult(
                    false,
                    aimBasis.Side,
                    broadsideSeed,
                    aimBasis,
                    samplingResult,
                    spawnedCount,
                    BroadsideShotSamplingFailure.None,
                    BroadsideFireExecutionFailure.ProjectileSpawnFailed
                );
                return false;
            }

            spawnedCount++;
            projectile.Terminated += contact => HandleTerminalContact(
                contact,
                currentSequence,
                receiver
            );

            if (receiver != null)
            {
                CombatOutcomeVFXBridge.TryEmitMuzzleFire(
                    sample,
                    muzzles[index].name,
                    receiver
                );
            }
        }

        result = new BroadsideFireExecutionResult(
            true,
            aimBasis.Side,
            broadsideSeed,
            aimBasis,
            samplingResult,
            spawnedCount,
            BroadsideShotSamplingFailure.None,
            BroadsideFireExecutionFailure.None
        );
        return true;
    }


    public bool TryExecuteWithRuntimeSeed(
        FireAimBasis aimBasis,
        out BroadsideFireExecutionResult result
    )
    {
        if (!IsValidAimBasis(aimBasis))
        {
            return TryExecute(aimBasis, 0u, out result);
        }

        return TryExecute(
            aimBasis,
            NextRuntimeSeed(aimBasis.Side),
            out result
        );
    }


    private void HandleTerminalContact(
        ProjectileTerminalContact contact,
        uint contactExecutionSequence,
        ICombatVFXEventReceiver receiver
    )
    {
        if (!FoundationShotOutcomeResolver.TryResolve(
            contact,
            out FoundationShotOutcome outcome,
            out _
        ))
        {
            return;
        }

        if (contactExecutionSequence == executionSequence)
        {
            switch (outcome.Kind)
            {
                case FoundationShotOutcomeKind.HullHit:
                    lastHullHitCount++;
                    break;
                case FoundationShotOutcomeKind.WaterMiss:
                    lastWaterMissCount++;
                    break;
                case FoundationShotOutcomeKind.ExpiredNonHit:
                    lastExpiredCount++;
                    break;
            }
        }

        CombatOutcomeVFXBridge.TryEmitResolvedOutcome(outcome, receiver);
    }


    private uint NextRuntimeSeed(CombatSide side)
    {
        uint state = side == CombatSide.Port
            ? portSeedState
            : starboardSeedState;
        DeterministicRandom32 random = new DeterministicRandom32(state);
        uint next = random.NextUInt();

        if (side == CombatSide.Port)
        {
            portSeedState = next;
        }
        else
        {
            starboardSeedState = next;
        }

        return next;
    }


    private static BroadsideFireExecutionResult CreateRejected(
        FireAimBasis aimBasis,
        uint broadsideSeed,
        BroadsideShotSamplingFailure samplingFailure,
        BroadsideFireExecutionFailure executionFailure
    )
    {
        return new BroadsideFireExecutionResult(
            false,
            null,
            broadsideSeed,
            aimBasis,
            default,
            0,
            samplingFailure,
            executionFailure
        );
    }


    private static bool IsValidProjectilePrefab(
        CombatProjectile candidate
    )
    {
        return candidate != null
            && !candidate.IsInitialized
            && !candidate.IsTerminated
            && candidate.GetComponentsInChildren<CombatProjectile>(true)
                .Length == 1
            && candidate.GetComponentInChildren<Rigidbody>(true) == null
            && candidate.GetComponentInChildren<Collider>(true) == null;
    }


    private static bool IsValidAimBasis(FireAimBasis basis)
    {
        return IsFinite(basis.AimPlaneCenterWorld)
            && IsFinite(basis.AimDirectionWorld)
            && basis.AimDirectionWorld.sqrMagnitude
                > MinimumDirectionSqrMagnitude
            && IsFinite(basis.AimDistanceMeters)
            && basis.AimDistanceMeters > 0f;
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
