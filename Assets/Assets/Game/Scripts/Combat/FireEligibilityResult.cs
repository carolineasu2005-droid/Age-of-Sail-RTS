using System;
using UnityEngine;

[Flags]
public enum FireEligibilityFailure
{
    None = 0,
    TargetIllegal = 1 << 0,
    NoBroadsideArc = 1 << 1,
    BeyondMaximumRange = 1 << 2,
    BroadsideReloading = 1 << 3,
    LifecycleDisallowsFire = 1 << 4,
    Obstructed = 1 << 5
}

public readonly struct FireEligibilityResult
{
    internal FireEligibilityResult(
        bool targetLegal,
        CombatSide? side,
        bool inBroadsideArc,
        float targetLocalBearingDegrees,
        float distanceMeters,
        bool withinMaximumRange,
        bool withinEffectiveRange,
        bool reloadReady,
        bool lifecycleAllowsFire,
        bool hasObstructionPath,
        Vector3 obstructionOriginWorld,
        Vector3 obstructionDestinationWorld,
        bool blocked,
        Collider blockingCollider,
        FireEligibilityFailure failureReasons
    )
    {
        TargetLegal = targetLegal;
        Side = side;
        InBroadsideArc = inBroadsideArc;
        TargetLocalBearingDegrees = targetLocalBearingDegrees;
        DistanceMeters = distanceMeters;
        WithinMaximumRange = withinMaximumRange;
        WithinEffectiveRange = withinEffectiveRange;
        ReloadReady = reloadReady;
        LifecycleAllowsFire = lifecycleAllowsFire;
        HasObstructionPath = hasObstructionPath;
        ObstructionOriginWorld = obstructionOriginWorld;
        ObstructionDestinationWorld = obstructionDestinationWorld;
        Blocked = blocked;
        BlockingCollider = blockingCollider;
        FailureReasons = failureReasons;
    }


    public bool TargetLegal { get; }

    public CombatSide? Side { get; }

    public bool InBroadsideArc { get; }

    public float TargetLocalBearingDegrees { get; }

    public float DistanceMeters { get; }

    public bool WithinMaximumRange { get; }

    public bool WithinEffectiveRange { get; }

    public bool ReloadReady { get; }

    public bool LifecycleAllowsFire { get; }

    public bool HasObstructionPath { get; }

    public Vector3 ObstructionOriginWorld { get; }

    public Vector3 ObstructionDestinationWorld { get; }

    public bool Blocked { get; }

    public Collider BlockingCollider { get; }

    public GameObject BlockingObject => BlockingCollider != null
        ? BlockingCollider.gameObject
        : null;

    public FireEligibilityFailure FailureReasons { get; }

    public bool CanFireGeometry =>
        InBroadsideArc && WithinMaximumRange;

    public bool CanFire =>
        TargetLegal
        && Side.HasValue
        && InBroadsideArc
        && WithinMaximumRange
        && ReloadReady
        && LifecycleAllowsFire
        && !Blocked;
}
