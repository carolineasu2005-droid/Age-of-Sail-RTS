using System;
using UnityEngine;

[AddComponentMenu("Age of Sail/Combat/Ship Fire Eligibility")]
[DisallowMultipleComponent]
public sealed class ShipFireEligibility : MonoBehaviour
{
    private const int CombatGeometryLayer = 8;
    private const int CombatGeometryMask = 1 << CombatGeometryLayer;

    [Header("Broadside Arc")]

    [SerializeField]
    [Range(0f, ShipBroadsideGeometry.MaximumSideArcLimitDegrees)]
    private float forwardArcLimitDegrees = 80f;

    [SerializeField]
    [Range(0f, ShipBroadsideGeometry.MaximumSideArcLimitDegrees)]
    private float aftArcLimitDegrees = 70f;

    [Header("Range")]

    [SerializeField]
    [Min(0f)]
    private float effectiveRangeMeters = 300f;

    [SerializeField]
    [Min(0f)]
    private float maximumRangeMeters = 500f;


    public float ForwardArcLimitDegrees => forwardArcLimitDegrees;

    public float AftArcLimitDegrees => aftArcLimitDegrees;

    public float EffectiveRangeMeters => effectiveRangeMeters;

    public float MaximumRangeMeters => maximumRangeMeters;


    public bool TryEvaluate(
        GameObject targetShipRoot,
        bool targetRelationshipAllowsFire,
        out FireEligibilityResult result
    )
    {
        result = default;

        if (!HasValidConfiguration()
            || !TryGetShooterDependencies(
                out ShipCombatState combatState,
                out Vector3 obstructionOriginWorld
            ))
        {
            return false;
        }

        bool lifecycleAllowsFire = EvaluateLifecycleAllowsFire();

        if (!TryGetTargetSpatialIdentity(
            targetShipRoot,
            out Vector3 obstructionDestinationWorld
        ))
        {
            result = CreateInvalidTargetResult(
                lifecycleAllowsFire,
                obstructionOriginWorld
            );
            return true;
        }

        if (!TryEvaluateGeometry(
            targetShipRoot.transform.position,
            out FireEligibilityResult geometryResult
        ))
        {
            return false;
        }

        bool targetLegal = targetRelationshipAllowsFire;
        bool reloadReady = geometryResult.Side.HasValue
            && IsBroadsideReady(combatState, geometryResult.Side.Value);
        bool blocked = TryFindBlockingCollider(
            obstructionOriginWorld,
            obstructionDestinationWorld,
            transform,
            targetShipRoot.transform,
            out Collider blockingCollider
        );
        FireEligibilityFailure failureReasons = GetFailureReasons(
            targetLegal,
            geometryResult,
            reloadReady,
            lifecycleAllowsFire,
            blocked
        );

        result = new FireEligibilityResult(
            targetLegal,
            geometryResult.Side,
            geometryResult.InBroadsideArc,
            geometryResult.TargetLocalBearingDegrees,
            geometryResult.DistanceMeters,
            geometryResult.WithinMaximumRange,
            geometryResult.WithinEffectiveRange,
            reloadReady,
            lifecycleAllowsFire,
            true,
            obstructionOriginWorld,
            obstructionDestinationWorld,
            blocked,
            blockingCollider,
            failureReasons
        );
        return true;
    }


    public bool TryEvaluateBlindFireAtPoint(
        Vector3 worldAimPoint,
        out BlindFireEligibilityResult result
    )
    {
        result = default;

        if (!HasValidConfiguration()
            || !TryGetBlindFireDependencies(out ShipCombatState combatState))
        {
            return false;
        }

        bool lifecycleAllowsFire = EvaluateLifecycleAllowsFire();

        if (!IsFinite(worldAimPoint))
        {
            result = CreateInvalidBlindFireResult(
                default,
                null,
                lifecycleAllowsFire
            );
            return true;
        }

        Vector3 offsetWorld = worldAimPoint - transform.position;
        float distanceMeters = offsetWorld.magnitude;

        if (!IsFinite(distanceMeters))
        {
            result = CreateInvalidBlindFireResult(
                default,
                null,
                lifecycleAllowsFire
            );
            return true;
        }

        bool withinMaximumRange = distanceMeters <= maximumRangeMeters;

        if (!TryEvaluateBroadsideGeometry(
            offsetWorld,
            out BroadsideGeometryResult geometry
        ))
        {
            BlindFireAim invalidAim = new BlindFireAim(
                false,
                Vector3.zero,
                worldAimPoint,
                distanceMeters
            );
            result = CreateInvalidBlindFireResult(
                invalidAim,
                withinMaximumRange,
                lifecycleAllowsFire
            );
            return true;
        }

        BlindFireAim aim = new BlindFireAim(
            true,
            geometry.WorldDirection,
            worldAimPoint,
            distanceMeters
        );
        result = CreateBlindFireResult(
            aim,
            geometry,
            withinMaximumRange,
            combatState,
            lifecycleAllowsFire
        );
        return true;
    }


    public bool TryEvaluateBlindFireDirection(
        Vector3 worldAimDirection,
        out BlindFireEligibilityResult result
    )
    {
        result = default;

        if (!HasValidConfiguration()
            || !TryGetBlindFireDependencies(out ShipCombatState combatState))
        {
            return false;
        }

        bool lifecycleAllowsFire = EvaluateLifecycleAllowsFire();

        if (!TryEvaluateBroadsideGeometry(
            worldAimDirection,
            out BroadsideGeometryResult geometry
        ))
        {
            result = CreateInvalidBlindFireResult(
                default,
                null,
                lifecycleAllowsFire
            );
            return true;
        }

        BlindFireAim aim = new BlindFireAim(
            true,
            geometry.WorldDirection,
            null,
            null
        );
        result = CreateBlindFireResult(
            aim,
            geometry,
            null,
            combatState,
            lifecycleAllowsFire
        );
        return true;
    }


    private bool TryEvaluateGeometry(
        Vector3 targetWorldPosition,
        out FireEligibilityResult result
    )
    {
        result = default;

        if (!HasValidConfiguration()
            || !IsFinite(targetWorldPosition)
            || !IsFinite(transform.position))
        {
            return false;
        }

        Vector3 targetOffsetWorld =
            targetWorldPosition - transform.position;
        float distanceMeters = targetOffsetWorld.magnitude;

        if (!IsFinite(distanceMeters))
        {
            return false;
        }

        if (!TryEvaluateBroadsideGeometry(
            targetOffsetWorld,
            out BroadsideGeometryResult geometry
        ))
        {
            return false;
        }

        result = new FireEligibilityResult(
            false,
            geometry.Side,
            geometry.InBroadsideArc,
            geometry.LocalBearingDegrees,
            distanceMeters,
            distanceMeters <= maximumRangeMeters,
            distanceMeters <= effectiveRangeMeters,
            false,
            true,
            false,
            Vector3.zero,
            Vector3.zero,
            false,
            null,
            FireEligibilityFailure.None
        );
        return true;
    }


    private bool TryEvaluateBroadsideGeometry(
        Vector3 worldDirection,
        out BroadsideGeometryResult result
    )
    {
        return ShipBroadsideGeometry.TryEvaluate(
            transform,
            worldDirection,
            forwardArcLimitDegrees,
            aftArcLimitDegrees,
            out result
        );
    }


    private bool TryGetBlindFireDependencies(
        out ShipCombatState combatState
    )
    {
        combatState = GetComponent<ShipCombatState>();

        return combatState != null
            && IsFinite(transform.position)
            && IsFinite(transform.forward)
            && IsFinite(transform.right);
    }


    private bool TryGetShooterDependencies(
        out ShipCombatState combatState,
        out Vector3 obstructionOriginWorld
    )
    {
        combatState = GetComponent<ShipCombatState>();
        obstructionOriginWorld = default;
        ShipArtDefinition artDefinition = GetComponent<ShipArtDefinition>();

        if (combatState == null
            || artDefinition == null
            || artDefinition.CenterReference == null
            || !IsFinite(transform.position)
            || !IsFinite(artDefinition.CenterReference.position))
        {
            return false;
        }

        obstructionOriginWorld = artDefinition.CenterReference.position;
        return true;
    }


    private bool TryGetTargetSpatialIdentity(
        GameObject targetShipRoot,
        out Vector3 obstructionDestinationWorld
    )
    {
        obstructionDestinationWorld = default;

        if (targetShipRoot == null || targetShipRoot == gameObject)
        {
            return false;
        }

        ShipCombatState targetCombatState =
            targetShipRoot.GetComponent<ShipCombatState>();
        ShipArtDefinition targetArtDefinition =
            targetShipRoot.GetComponent<ShipArtDefinition>();
        ShipCombatGeometry targetCombatGeometry =
            targetShipRoot.GetComponent<ShipCombatGeometry>();

        if (targetCombatState == null
            || targetArtDefinition == null
            || targetArtDefinition.CenterReference == null
            || targetCombatGeometry == null
            || !IsFinite(targetShipRoot.transform.position)
            || !IsFinite(targetArtDefinition.CenterReference.position))
        {
            return false;
        }

        obstructionDestinationWorld =
            targetArtDefinition.CenterReference.position;
        return true;
    }


    private static bool IsBroadsideReady(
        ShipCombatState combatState,
        CombatSide side
    )
    {
        return side == CombatSide.Port
            ? combatState.PortBroadsideState == BroadsideReloadState.Ready
            : combatState.StarboardBroadsideState
                == BroadsideReloadState.Ready;
    }


    private static bool EvaluateLifecycleAllowsFire()
    {
        // Phase 7 will replace this bridge with the registered lifecycle
        // Source of Truth. Foundation ships currently have no lifecycle state.
        return true;
    }


    private static BlindFireEligibilityResult CreateBlindFireResult(
        BlindFireAim aim,
        BroadsideGeometryResult geometry,
        bool? withinMaximumRange,
        ShipCombatState combatState,
        bool lifecycleAllowsFire
    )
    {
        bool reloadReady = geometry.Side.HasValue
            && IsBroadsideReady(combatState, geometry.Side.Value);
        BlindFireEligibilityFailure failureReasons =
            BlindFireEligibilityFailure.None;

        if (!geometry.Side.HasValue || !geometry.InBroadsideArc)
        {
            failureReasons |=
                BlindFireEligibilityFailure.NoBroadsideArc;
        }

        if (withinMaximumRange == false)
        {
            failureReasons |=
                BlindFireEligibilityFailure.BeyondMaximumRange;
        }

        if (geometry.Side.HasValue && !reloadReady)
        {
            failureReasons |=
                BlindFireEligibilityFailure.BroadsideReloading;
        }

        if (!lifecycleAllowsFire)
        {
            failureReasons |=
                BlindFireEligibilityFailure.LifecycleDisallowsFire;
        }

        return new BlindFireEligibilityResult(
            aim,
            geometry.Side,
            geometry.InBroadsideArc,
            geometry.LocalBearingDegrees,
            withinMaximumRange,
            reloadReady,
            lifecycleAllowsFire,
            failureReasons
        );
    }


    private static BlindFireEligibilityResult CreateInvalidBlindFireResult(
        BlindFireAim aim,
        bool? withinMaximumRange,
        bool lifecycleAllowsFire
    )
    {
        BlindFireEligibilityFailure failureReasons =
            BlindFireEligibilityFailure.InvalidAim;

        if (withinMaximumRange == false)
        {
            failureReasons |=
                BlindFireEligibilityFailure.BeyondMaximumRange;
        }

        if (!lifecycleAllowsFire)
        {
            failureReasons |=
                BlindFireEligibilityFailure.LifecycleDisallowsFire;
        }

        return new BlindFireEligibilityResult(
            aim,
            null,
            false,
            0f,
            withinMaximumRange,
            false,
            lifecycleAllowsFire,
            failureReasons
        );
    }


    private static FireEligibilityResult CreateInvalidTargetResult(
        bool lifecycleAllowsFire,
        Vector3 obstructionOriginWorld
    )
    {
        return new FireEligibilityResult(
            false,
            null,
            false,
            0f,
            0f,
            false,
            false,
            false,
            lifecycleAllowsFire,
            false,
            obstructionOriginWorld,
            Vector3.zero,
            false,
            null,
            FireEligibilityFailure.TargetIllegal
        );
    }


    private static FireEligibilityFailure GetFailureReasons(
        bool targetLegal,
        FireEligibilityResult geometryResult,
        bool reloadReady,
        bool lifecycleAllowsFire,
        bool blocked
    )
    {
        FireEligibilityFailure reasons = FireEligibilityFailure.None;

        if (!targetLegal)
        {
            reasons |= FireEligibilityFailure.TargetIllegal;
        }

        if (!geometryResult.Side.HasValue
            || !geometryResult.InBroadsideArc)
        {
            reasons |= FireEligibilityFailure.NoBroadsideArc;
        }

        if (!geometryResult.WithinMaximumRange)
        {
            reasons |= FireEligibilityFailure.BeyondMaximumRange;
        }

        if (geometryResult.Side.HasValue && !reloadReady)
        {
            reasons |= FireEligibilityFailure.BroadsideReloading;
        }

        if (!lifecycleAllowsFire)
        {
            reasons |= FireEligibilityFailure.LifecycleDisallowsFire;
        }

        if (blocked)
        {
            reasons |= FireEligibilityFailure.Obstructed;
        }

        return reasons;
    }


    private static bool TryFindBlockingCollider(
        Vector3 originWorld,
        Vector3 destinationWorld,
        Transform shooterRoot,
        Transform targetRoot,
        out Collider blockingCollider
    )
    {
        blockingCollider = null;
        Vector3 offset = destinationWorld - originWorld;
        float distance = offset.magnitude;

        if (!IsFinite(distance)
            || distance <= Mathf.Epsilon)
        {
            return false;
        }

        RaycastHit[] hits = Physics.RaycastAll(
            originWorld,
            offset / distance,
            distance,
            CombatGeometryMask,
            QueryTriggerInteraction.Collide
        );
        Array.Sort(hits, CompareRaycastHits);

        foreach (RaycastHit hit in hits)
        {
            Collider candidate = hit.collider;

            if (candidate == null
                || candidate.transform.IsChildOf(shooterRoot)
                || candidate.transform.IsChildOf(targetRoot))
            {
                continue;
            }

            blockingCollider = candidate;
            return true;
        }

        return false;
    }


    private static int CompareRaycastHits(RaycastHit left, RaycastHit right)
    {
        int distanceComparison = left.distance.CompareTo(right.distance);

        if (distanceComparison != 0)
        {
            return distanceComparison;
        }

        if (left.collider == null)
        {
            return right.collider == null ? 0 : -1;
        }

        if (right.collider == null)
        {
            return 1;
        }

        return left.collider.GetEntityId().CompareTo(
            right.collider.GetEntityId()
        );
    }


    private bool HasValidConfiguration()
    {
        if (!IsFinite(effectiveRangeMeters)
            || !IsFinite(maximumRangeMeters))
        {
            return false;
        }

        bool rangesValid = effectiveRangeMeters >= 0f
            && maximumRangeMeters >= 0f
            && effectiveRangeMeters <= maximumRangeMeters;

        return ShipBroadsideGeometry.HasValidArcConfiguration(
            forwardArcLimitDegrees,
            aftArcLimitDegrees
        ) && rangesValid;
    }


    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }


    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x)
            && IsFinite(value.y)
            && IsFinite(value.z);
    }
}
