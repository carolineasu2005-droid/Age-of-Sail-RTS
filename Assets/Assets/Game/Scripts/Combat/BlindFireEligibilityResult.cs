using System;

[Flags]
public enum BlindFireEligibilityFailure
{
    None = 0,
    InvalidAim = 1 << 0,
    NoBroadsideArc = 1 << 1,
    BeyondMaximumRange = 1 << 2,
    BroadsideReloading = 1 << 3,
    LifecycleDisallowsFire = 1 << 4
}

public readonly struct BlindFireEligibilityResult
{
    internal BlindFireEligibilityResult(
        BlindFireAim aim,
        CombatSide? side,
        bool inBroadsideArc,
        float aimLocalBearingDegrees,
        bool? withinMaximumRange,
        bool reloadReady,
        bool lifecycleAllowsFire,
        BlindFireEligibilityFailure failureReasons
    )
    {
        Aim = aim;
        Side = side;
        InBroadsideArc = inBroadsideArc;
        AimLocalBearingDegrees = aimLocalBearingDegrees;
        WithinMaximumRange = withinMaximumRange;
        ReloadReady = reloadReady;
        LifecycleAllowsFire = lifecycleAllowsFire;
        FailureReasons = failureReasons;
    }


    public BlindFireAim Aim { get; }

    public bool ValidAim => Aim.IsValid;

    public CombatSide? Side { get; }

    public bool InBroadsideArc { get; }

    public float AimLocalBearingDegrees { get; }

    public bool RangeApplicable => Aim.HasWorldAimPoint;

    public bool? WithinMaximumRange { get; }

    public bool ReloadReady { get; }

    public bool LifecycleAllowsFire { get; }

    public BlindFireEligibilityFailure FailureReasons { get; }

    public bool CanBlindFire =>
        ValidAim
        && Side.HasValue
        && InBroadsideArc
        && (!RangeApplicable || WithinMaximumRange == true)
        && ReloadReady
        && LifecycleAllowsFire;
}
